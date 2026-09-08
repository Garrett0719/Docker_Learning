# 第十二天：健康检查、优雅退出与 PID 1

## 目标

让编排平台能分清三件事：应用**启动好了没有**、**是否该接新请求**、**是否已坏到要重启**；停止时则给正在处理的请求留出收尾时间。

---

## 1. 三种检查：不要混成一个“健康”

| 检查 | 要回答的问题 | 失败后的合理动作 | 典型内容 |
| --- | --- | --- | --- |
| 启动（startup） | 应用是否完成启动？ | Kubernetes 继续等待；超过失败阈值则终止容器 | 慢初始化、配置下载、预热 |
| 存活（liveness） | 进程是否卡死或无法工作？ | Kubernetes 终止容器；是否重启取决于 `restartPolicy` | 只判断 API 自己还活着 |
| 就绪（readiness） | 现在适合接新流量吗？ | 从 Service 的可用后端中暂时摘除，不应仅因此重启 | 数据库/关键依赖可用、初始化完成 |

一句话记忆：

```text
startup   = 能不能启动完成？
liveness  = 坏到需要重启了吗？
readiness = 现在能接新请求吗？
```

### 一个常见流程

```text
应用初始化 40 秒
  └─ startupProbe 未通过：先不运行 liveness/readiness

初始化完成
  └─ startup / live / ready 均通过：开始接流量

数据库短暂不可用
  └─ ready 失败：不再接新的 Service 流量
  └─ live 通常仍通过：API 本身没有卡死，不要重启
```

`readiness` 失败不是“立刻断开所有连接”，而是停止分配**新的正常流量**；已经进入应用的请求仍可在停止窗口内完成。

---

## 2. ASP.NET Core：把 live 和 ready 分开

最小做法：`/health/live` 不检查外部依赖；`/health/ready` 只检查标记为 `ready` 的项目。

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    // 这里替换为实际数据库健康检查；给它加上 ready 标签
    .AddCheck("database", () => HealthCheckResult.Healthy(), tags: ["ready"]);

var app = builder.Build();

// Predicate 返回 false：不执行任何已注册检查，只确认应用能响应 HTTP。
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// 仅检查影响接流量的依赖。
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
```

### 检查依赖的原则

- `live` 不要查询数据库、第三方 API 或消息队列。依赖暂时故障不等于进程必须重启。
- `ready` 只检查真正决定“能否服务”的依赖。
- 查询必须轻：能连通通常就够了；若必须查询，使用类似 `SELECT 1` 的轻量语句。
- 健康端点不应输出密码、连接串、堆栈等敏感信息。

> 不能因为“想检查得更彻底”就在每次探针里跑复杂 SQL。探针会周期性执行，反而可能加重故障中的数据库。

---

## 3. Docker `HEALTHCHECK`：容器级状态，不等于 Kubernetes Probe

Docker 的健康检查会在容器内执行命令，并让容器显示三种状态：`starting`、`healthy`、`unhealthy`。

```dockerfile
# 运行时镜像中必须真的有 wget；不要假定 .NET 镜像自带 curl。
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD wget -q -O /dev/null http://127.0.0.1:8080/health/live || exit 1
```

| 参数 | 含义 |
| --- | --- |
| `interval` | 两次检查的大致间隔 |
| `timeout` | 单次检查的最长时间 |
| `retries` | 连续失败多少次后标为 `unhealthy` |
| `start-period` | 容器刚启动时的宽限期；期间失败不计入失败次数 |

注意：`HEALTHCHECK` 只负责给 Docker 标状态；**它本身不会按 Kubernetes liveness 的语义自动重启容器**。Dockerfile 中只能生效最后一条 `HEALTHCHECK`。

### `start-period` 与 `startupProbe` 的区别（本次易错点）

| 项目 | Docker `start-period` | Kubernetes `startupProbe` |
| --- | --- | --- |
| 本质 | 健康检查的失败宽限期 | 独立的启动检查 |
| 失败结果 | 宽限期内不计入 `retries` | 达到阈值后 kubelet 终止容器 |
| 对 live / ready 的影响 | 不会替代它们 | 成功前，liveness 和 readiness 都不执行 |

启动最长可能 40 秒时，不是简单写“40 秒”就结束，而是保证：

```text
periodSeconds × failureThreshold ≥ 最坏启动时间
```

再留一点余量。例如每 5 秒检查一次、容忍 12 次失败，最多约 60 秒。

---

## 4. Kubernetes Probe 配置示例

```yaml
startupProbe:
  httpGet:
    path: /health/live
    port: 8080
  periodSeconds: 5
  failureThreshold: 12 # 约给 60 秒启动窗口

livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  periodSeconds: 10
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  periodSeconds: 5
  failureThreshold: 1
```

这三项共享 HTTP 端点也可以，但语义不能混：`/health/live` 应很轻，`/health/ready` 可以包含关键依赖。

---

## 5. 停止容器：SIGTERM → 排空请求 → 超时后 SIGKILL

以 `docker stop -t 30 my-api` 为例：

```text
现在立刻发送 SIGTERM 给 PID 1
        ↓
最多等待 30 秒，让应用停止接新请求并收尾
        ↓
仍未退出
        ↓
SIGKILL 强制结束
```

`-t 30` 不是“30 秒后才发 SIGTERM”，而是给优雅退出的等待窗口。

在 Kubernetes 中也一样：Pod 终止时通常先向主进程发送 `SIGTERM`，等待 `terminationGracePeriodSeconds`；仍未退出才发送 `SIGKILL`。

### ASP.NET Core 应用应做什么

- 不要自行捕获 `SIGTERM` 后立刻强制退出。
- 把后台任务写成能响应 `CancellationToken` 的形式。
- 停止新工作、关闭消费者、释放连接；让正在执行的请求尽量在超时内结束。
- 最坏情况下，停止超时要小于平台的宽限期；否则会被 `SIGKILL` 中断。

`ApplicationStopping` 只适合触发很短的停止通知，不要在里面做长时间阻塞工作。

---

## 6. PID 1 与 exec-form 入口

容器里最先启动的进程是 PID 1。停止信号默认先发给它，因此应用最好直接成为 PID 1。

```dockerfile
# 推荐：exec form，dotnet 直接是 PID 1，能收到 SIGTERM
ENTRYPOINT ["dotnet", "MyApi.dll"]

# 不推荐：shell form，实际会变为 /bin/sh -c ...
# shell 可能不转发信号，dotnet 通常不是 PID 1
ENTRYPOINT dotnet MyApi.dll
```

如果确实需要启动脚本，脚本最后使用 `exec` 替换自己：

```sh
#!/bin/sh
# 这里可做很短的准备工作
exec dotnet MyApi.dll
```

---

## 7. 本次错题与易混点

### 错题 1：把 Docker `start-period` 当成 Kubernetes `startupProbe`

不对。前者只是 Docker 健康检查的启动宽限期；后者是 Kubernetes 的独立探针，未成功前会阻止 live/ready 检查，持续失败会导致容器被终止。

### 错题 2：认为 liveness 失败“一定重启”

更严谨的说法是：liveness 连续失败达到阈值后，kubelet 会先终止容器；之后是否重新启动要看 Pod 的 `restartPolicy`。readiness 失败则通常只会把实例从 Service 可用后端中摘除。

### 易混点 3：数据库不可用时，live 也应该失败

通常不应该。数据库短暂不可用更适合让 `ready` 失败，暂停分流；如果 API 自身仍能正常运行，`live` 保持通过，避免无意义的重启循环。

### 易混点 4：`unhealthy` 就代表 Docker 会重启容器

不对。`HEALTHCHECK` 只改变健康状态。是否重启由你运行容器的策略或更上层编排平台决定。

---

## 8. 实践与验收清单

### 应用

- [ ] `AddHealthChecks()` 已注册。
- [ ] `/health/live` 只确认应用可响应，不查询依赖。
- [ ] `/health/ready` 只包含关键依赖，查询轻且有超时。

### 镜像与运行

- [ ] Dockerfile 有一条可执行的 `HEALTHCHECK`；检查工具确实存在于运行时镜像。
- [ ] `ENTRYPOINT ["dotnet", "你的应用.dll"]` 为 exec form。
- [ ] 使用 `docker inspect <容器名>` 查看健康状态与失败输出。

### 优雅退出

- [ ] 发起一个较慢请求后执行 `docker stop -t 30 <容器名>`。
- [ ] 日志能看到应用进入停止状态；没有新请求继续进入。
- [ ] 已进入的请求在停止超时内完成；超时后才会被强制结束。

---

## 参考资料

- [ASP.NET Core Health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Dockerfile HEALTHCHECK / ENTRYPOINT / STOPSIGNAL](https://docs.docker.com/reference/dockerfile/#healthcheck)
- [Kubernetes Container Probes 与终止流程](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/#container-probes)
