# 第十八天：配置、Secret 与声明式资源

> 根据官方资料和本地 `study_security` 项目整理。共享会话中的第十八天题目与批改未能展开，因此暂不列“已确认错题”；下文“易混点”不代表你答错过。只收录本日内容，文中的部署命令未替你执行。

## 1. 镜像不变，运行时换配置

| 放在哪里 | 放什么 | 例子 |
| --- | --- | --- |
| 镜像 | 程序、依赖、非敏感通用默认值 | API 程序 |
| ConfigMap | 非敏感、随环境变化的配置 | 提示文字、数据库地址 |
| Secret | 密码、令牌、密钥 | 数据库密码、API Key |

测试和生产可以用同一个镜像，分别注入配置。只改配置时通常不需要重新构建镜像；程序原本不支持读取这些配置时，才需要先改代码、发布新镜像。

**ConfigMap 和 Secret 不会自动进入容器，必须在 Pod 配置中引用。**常规环境变量和卷引用要求它们与 Pod 位于同一 namespace。[ConfigMap](https://kubernetes.io/docs/concepts/configuration/configmap/)、[Secret](https://kubernetes.io/docs/concepts/configuration/secret/)

## 2. 两种注入方式

### 2.1 环境变量：你现在项目采用的方式

你的 `configmap.yaml` 核心内容：

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: study-security-config
data:
  App__Message: "Hello k8s v2"
  ASPNETCORE_ENVIRONMENT: "Production"
  STARTUP_DELAY_SECONDS: "20"
```

`data` 中的值是字符串，数字和布尔值也应加引号。

Deployment 中，以下片段放在 `spec.template.spec.containers` 的对应容器下：

```yaml
envFrom:
  - configMapRef:
      name: study-security-config
  - secretRef:
      name: study-security-secret
```

- `envFrom`：把这个资源中的键值批量注入。
- `env[].valueFrom`：只选一个键，还可以换环境变量名。例如：

```yaml
env:
  - name: Secrets__ApiKey
    valueFrom:
      secretKeyRef:
        name: study-security-secret
        key: Secrets__ApiKey
```

不要用两种写法重复注入同一项；显式 `env` 优先于 `envFrom`，重复来源容易让排查变复杂。普通配置可以批量注入，敏感值优先按需选取。

你的 C# 读取方式：

```csharp
string? message = configuration["App:Message"];
string? apiKey = configuration["Secrets:ApiKey"];
```

**`App__Message` → `App:Message` 是 .NET 配置提供程序的转换，不是 Kubernetes 的转换。**默认配置下，环境变量可以覆盖 `appsettings.json` 中的同名配置。[ASP.NET Core 配置](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)

### 2.2 文件：把每个键变成一个文件

以下是 Pod 配置片段，不是完整 Deployment；若用于 Deployment，应放进 `spec.template.spec`：

```yaml
containers:
  - name: study-security
    image: study-security:v3
    volumeMounts:
      - name: api-secret
        mountPath: /run/secrets/api
        readOnly: true
volumes:
  - name: api-secret
    secret:
      secretName: study-security-secret
```

Secret 中的 `Secrets__ApiKey` 会成为 `/run/secrets/api/Secrets__ApiKey` 文件，内容是原始密钥，应用不需要自行解码 base64。

ConfigMap 同理：卷来源改成 `configMap: { name: study-security-config }`。一个键也可以装整份配置文件，例如键名 `appsettings.json`、值为 JSON 内容。

**挂载文件不等于应用会读它。**例如 C# 可以显式读取：

```csharp
string apiKey = File.ReadAllText("/run/secrets/api/Secrets__ApiKey");
```

也可以配置 `.NET` 的 `AddKeyPerFile` 或 `AddJsonFile`；你当前项目仅使用环境变量，不会自动读取上述目录。[.NET 文件配置](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/#key-per-file-configuration-provider)

## 3. 修改后，什么时候生效？

| 注入方式 | 修改 ConfigMap / Secret 后 | 应用侧要做什么 |
| --- | --- | --- |
| 环境变量 | 已运行容器里的变量不变 | 本日用滚动重启创建新 Pod |
| 普通卷挂载 | 挂载内容会延迟更新，不保证立即生效 | 重新读取文件或支持配置重载 |
| `subPath` 挂载单个文件 | 不自动更新 | 重建 Pod |

文件更新了，但程序启动时只读过一次，程序仍可能使用旧值。**“文件变了”和“应用用上新值了”是两回事。**[配置更新实验](https://kubernetes.io/docs/tutorials/configuration/updating-configuration-via-a-configmap/)、[Secret 卷更新](https://kubernetes.io/docs/concepts/configuration/secret/#using-secrets-as-files-from-a-pod)

本日记住这条顺序即可：

```text
修改配置文件 → apply 更新集群资源 → rollout restart → 等待发布完成 → 验证 API
```

只更新同名 ConfigMap / Secret，通常不会自动触发 Deployment 滚动发布。`rollout restart` 也不会替你提交尚未 apply 的本地配置。[滚动重启命令](https://kubernetes.io/docs/reference/kubectl/generated/kubectl_rollout/kubectl_rollout_restart/)

## 4. Secret 的安全边界

- `data` 填 base64 编码；`stringData` 可以填明文。两种写法都不能让含真实凭据的 YAML 安全地进入 Git。
- **base64 不是加密**，谁拿到内容，谁就能解码。
- Secret 默认并不保证在 etcd 中加密存储；生产需要配置静态加密、限制读取权限，并只提供给必要的容器。
- 能读取 Secret，或能在对应 namespace 创建任意 Pod 的人，可能取得密钥。Secret 不是“管理员也看不到”的保险箱。
- 不在日志、截图或验收接口中输出真实值；环境变量注入也不意味着无法泄露。[Secret 安全说明](https://kubernetes.io/docs/concepts/configuration/secret/)、[安全实践](https://kubernetes.io/docs/concepts/security/secrets-good-practices/)

你现有的 `/config-check` 只返回 `SecretLoaded`，没有回显密钥，这适合学习验收。不过 `true` 只说明读到了非空值，**不证明密钥正确或外部认证成功**。

### Git 与镜像构建要分别防护

真实值可以先保存在本地 `secret.local.env`，仓库只留带占位符的 `secret.example.yaml`。分别在仓库的 `.gitignore` 和实际构建上下文生效的 `.dockerignore` 中加入：

```gitignore
**/secret.local.env
**/secret.local.yaml
```

- `.gitignore`：避免未跟踪文件被普通 Git 添加操作收录；对已跟踪文件不生效。
- `.dockerignore`：避免文件进入 Docker 构建上下文，与 Git 忽略规则不是一回事。
- 你当前项目的 `.dockerignore` 只有 `bin/`、`obj/`、`.vs/`、`.git/`，还没有上述密钥文件排除规则。
- 若真实密钥已提交或打进镜像，先撤销/轮换密钥，再清理泄漏内容；只删除当前文件或加忽略规则不够。[Git 忽略规则](https://git-scm.com/docs/gitignore)、[Docker 构建上下文](https://docs.docker.com/build/concepts/context/#dockerignore-files)

## 5. 声明式与命令式：以什么为准？

| 方式 | 例子 | 适合什么时候 |
| --- | --- | --- |
| 声明式：写下想要的状态 | 改 YAML，再 `kubectl apply -f` | 正式配置、评审、重复部署 |
| 命令式：直接执行一个操作 | `kubectl edit`、`kubectl scale` | 临时实验、排障 |

- YAML 是本地文件；`apply` 才把它提交到集群，Kubernetes 不会一直监视你的电脑文件。
- 集群控制器维护的是已经提交的期望状态。例如 Deployment 要求两个副本，就设法维持两个。
- 直接修改集群不会自动改回本地 YAML。若同一字段仍由 YAML 管理，下次 apply 可能覆盖临时修改。
- 普通 `apply -f` 不会因为你删了一个本地 YAML 文件，就自动删除集群中对应资源。

本日默认：**配置以 YAML 为准，修改后 apply；临时改动若要保留，应同步回配置文件。**[声明式资源管理](https://kubernetes.io/docs/tasks/manage-kubernetes-objects/declarative-config/)

## 6. 实践与常用命令（PowerShell）

### ① 确认位置、集群与 namespace

```powershell
Set-Location 'D:\Docker_Learning\study_security\study_security'
kubectl config current-context
kubectl get deployment -A
$ns = 'default' # 如果资源在其他 namespace，改这里
```

以下沿用你的资源名。ConfigMap、Secret、Deployment 必须都在 `$ns` 中。

### ② 准备 Secret，不在命令里写真实值

先配置好忽略规则。用编辑器创建本地 `secret.local.env`，格式如下；将占位符换成本地实验值，不要提交：

```dotenv
Secrets__ApiKey=REPLACE_WITH_LOCAL_TEST_VALUE
```

```powershell
kubectl create secret generic study-security-secret -n $ns --from-env-file=secret.local.env --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -n $ns -f configmap.yaml
kubectl apply -n $ns -f deployment.yaml
```

`--dry-run=client` 只生成 YAML；`-o yaml` 选择输出格式；管道交给 apply，`-f -` 表示从管道读取。创建或修改同名 Secret 都可使用这条管道，不需要落地真实 Secret YAML。[用 kubectl 管理 Secret](https://kubernetes.io/docs/tasks/configmap-secret/managing-secret-using-kubectl/)

注意：管道避免的是把凭据写进命令文本或输出文件，不是加密。不要单独运行前半段并分享其输出；默认客户端 apply 的记录注解也可能包含编码后的 Secret。

### ③ 修改配置并滚动重启

先把 `configmap.yaml` 中 `App__Message` 改成 `"Hello k8s v3"`，保留其他需要的键，再执行：

```powershell
kubectl diff -n $ns -f configmap.yaml
kubectl apply -n $ns -f configmap.yaml
kubectl rollout restart -n $ns deployment/study-security
kubectl rollout status -n $ns deployment/study-security --timeout=180s
kubectl get pods -n $ns -l app=study-security
```

`diff` 用来预览差异，有差异时返回码为 1，并不代表操作失败。不要对含真实 Secret 的文件随意输出 diff。

你的配置包含 20 秒启动延迟，等待就绪时不要误以为“配置没生效”。更新 Secret 后采用环境变量注入的应用，同样需要滚动重启。

### ④ 验证应用，而不只是看资源存在

在终端 A 启动转发；已有旧转发时先按 `Ctrl+C` 结束，再重新启动：

```powershell
kubectl port-forward -n $ns service/study-security 18080:80
```

在终端 B 验证：

```powershell
Invoke-RestMethod 'http://localhost:18080/config-check'
```

预期：`message` 为 `Hello k8s v3`，`secretLoaded` 为 `true`。此检查只证明被访问的那个 Pod；如需逐个验证，将转发对象换成每个 `pod/实际名称`，端口映射用 `18080:8080`。

### ⑤ 排查与提交前检查

```powershell
kubectl get configmap,secret -n $ns
kubectl describe deployment study-security -n $ns
kubectl get pods -n $ns -l app=study-security
kubectl describe pod POD_NAME -n $ns
git check-ignore -v -- secret.local.env
git ls-files -- secret.local.env
git diff --cached --name-only
```

把 `POD_NAME` 换成实际名称。资源引用失败时，先看 Pod Events，再核对 namespace、资源名、键名。`git check-ignore` 应显示匹配规则，`git ls-files` 不应列出真实密钥文件；这些检查不是完整的凭据扫描，也不能证明历史提交没有泄漏。

## 7. 易混点与验收清单

以下是本日需要会解释的点，不是已确认的个人错题：

- **“改完 ConfigMap，旧 Pod 为什么还是旧值？”** 环境变量不会自动刷新；apply 后还要滚动重启。
- **“Secret 用了 base64，能提交吗？”** 不能；编码可逆，真实凭据仍然泄漏。
- **“文件挂载好了，C# 为什么没读到？”** 应用必须读取该文件或注册对应配置来源。
- **“apply 成功就算完成了吗？”** 不算；还要检查发布状态和应用实际读取的值。

完成标准：

- [ ] 非敏感环境配置放入 ConfigMap，敏感值放入 Secret。
- [ ] 两个 API 副本均能读取配置；修改后通过滚动重启用上新值。
- [ ] 修改配置没有重新构建镜像，验收接口和日志没有输出密钥。
- [ ] YAML 仓库仅保留无真实凭据的模板，本地密钥文件同时被 Git 和构建上下文排除。
- [ ] 能说明环境变量、普通挂载、`subPath` 三种方式的更新差别。

个人错题部分待共享会话中的第十八天题目和批改可读取后补充，不混入探针、资源限制等其他天的错误。
