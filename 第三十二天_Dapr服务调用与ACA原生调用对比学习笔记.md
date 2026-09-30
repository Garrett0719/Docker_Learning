# 第32天学习笔记：Dapr 服务调用

> 根据第32天问答、验收与官方资料整理。只保留可复用知识；“容易混淆”中注明答题暴露的问题，其余为必要提醒。平台支持范围核对于 2026-09-30。

## 1. 核心知识点

### 1.1 Dapr 是可选的中间层，不是 ACA 的必选项

Dapr 把服务发现、安全通信等通用能力放到应用旁边的 **sidecar（伴随进程/容器）** 中。业务程序通过 HTTP、gRPC 或 SDK 调用它，不必把这些能力全写进业务代码。

- sidecar 是独立运行的程序，不是安装一个 NuGet 包就自动存在。
- SDK 只是方便调用 Dapr API；不用 SDK，也可以通过 HTTP 调用。
- 本次普通 Dapr 服务间调用链路中，调用方和被调用方都应启用 Dapr。
- 只有简单的 ACA 内部 HTTP 调用时，直接使用应用名通常就够了。

**容易混淆（第2题表述）：** 减少平台相关代码，来自“让业务使用 Dapr 抽象、把部分通用能力交给 Dapr”，不是“自己重新实现这些能力”。仍然会依赖 Dapr API，并不是完全没有依赖。

参考：[Dapr 服务调用概览](https://docs.dapr.io/developing-applications/building-blocks/service-invocation/service-invocation-overview/)。

### 1.2 app-id：找服务，不是找某个副本

`app-id` 是 Dapr 识别目标服务的逻辑名称。调用方提供目标 app-id，Dapr 再找到可用实例。

- 它不是 IP、容器 ID、revision 名或 C# 项目名。
- 同一 ACA 应用的多个副本使用同一个 app-id。
- 同一 ACA 环境中的不同应用应使用不同 app-id。
- app-id 可以与 ACA 应用名相同，但两者不是同一个配置项。

**容易混淆：** `http://应用名` 使用 ACA 的服务发现；调用 Dapr app-id 使用 Dapr 的服务发现。字符串恰好相同，也不代表走同一条调用链。

### 1.3 三种端口：先看“谁连谁”

| 配置 | 谁连接它 | 用途 |
|---|---|---|
| Dapr HTTP port | 业务程序 → 自己的 sidecar | 调用 Dapr HTTP API；ACA 中为 3500 |
| Dapr app-port | 目标 sidecar → 目标业务程序 | 必须对应业务程序实际监听的端口，例如 8080 |
| ACA ingress target port | ACA 入口代理 → 业务程序 | 处理经 ACA ingress 进入的请求 |

Dapr 的 gRPC API 端口是另一回事，ACA 中为 50001。程序可读取平台提供的 `DAPR_HTTP_PORT`，不要把本地实验端口到处写死。

**容易混淆（第4题）：** Dapr HTTP port 不是笼统的“接收其他服务信号的端口”，首先要记住它是**本应用访问本地 sidecar 的入口**。`app-port` 才是 sidecar 找业务服务的端口。

**容易混淆（第10题A）：** 配置 app-port 不会让 C# 程序自动改为监听该端口。填错时，可能已经找到目标 app-id，却在“目标 sidecar → 业务程序”这一跳失败。不要把 3500 配成业务监听端口或公开到公网。

参考：[ACA Dapr 配置](https://learn.microsoft.com/en-us/azure/container-apps/enable-dapr)、[ACA Dapr 总览](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview)。

### 1.4 mTLS、托管身份、业务权限各管一件事

| 能力 | 解决的问题 | 不替代什么 |
|---|---|---|
| Dapr mTLS | sidecar 之间互相验证身份，并加密传输 | 用户登录、订单访问权限、数据库权限 |
| Azure 托管身份 + RBAC | 以应用身份取得访问 Azure 资源的权限 | 网络连通、Dapr 服务发现 |
| 业务认证与授权 | 判断请求者是谁、是否允许执行操作 | 网络加密 |

**容易混淆（第9题）：** 直接通过 ACA 应用名调用，并不必然要求先配置托管身份；是否需要令牌，取决于目标接口的认证要求。反过来，启用 Dapr mTLS 也不代表“所有安全问题都解决了”。

mTLS 的保护范围要看具体链路，不能直接推导为 App → sidecar、浏览器 → API 或数据库存储也都由它加密。ACA 托管 Dapr 提供自动 mTLS；本地 self-hosted Dapr 默认关闭 mTLS，跑通 quickstart 不等于验证了加密。

参考：[Dapr mTLS 配置与默认行为](https://docs.dapr.io/operations/security/mtls/)。

### 1.5 重试能提高成功率，但不能保证业务只执行一次

网络超时不一定代表服务没有执行。例如扣款已成功，但返回结果途中断网；再次请求可能重复扣款。

- 对暂时性失败设置有限次数的重试、间隔和超时；不要无限重试参数错误等永久失败。
- 明确由哪一层重试，避免 SDK、sidecar、业务代码叠加重试。
- 不要记成“启用 Dapr 后，任何 HTTP 错误都会按我想要的策略重试”。实际行为取决于版本、错误类型和可用策略。

**容易混淆（第7题）：** 你已识别重复扣款风险，但“先查数据库，没有再写”仍可能被并发请求同时通过。更可靠的组合是：

`同一业务操作使用稳定幂等键 → 数据库唯一约束拦住重复 → 事务保证本地业务写入与处理记录一致`

重试时沿用同一个幂等键，已处理的请求返回已有结果。涉及外部支付等跨系统操作，还需使用对方的幂等能力或补偿方案，不能只靠本地事务。

### 1.6 可移植不等于零改动；开源支持不等于 ACA 支持

Dapr 让调用代码主要依赖 app-id 和统一 API，迁移平台时可减少业务代码修改。但部署配置、身份权限、组件与版本支持仍要重新核对。

**容易混淆（第10题C）：** 当前 **ACA Jobs 不支持 Dapr**，不能找一个开关开启。不能因为 ACA App 能用，或开源 Dapr 有某项能力，就推断所有 ACA 资源都能用。

需要 Dapr 的常驻服务可使用 ACA App；有限任务应按 Job 的运行到完成模型设计，选择平台支持的直接调用或资源 SDK。不要为了套用示例而把有限任务改成无意义的常驻循环。

参考：[ACA Dapr 支持范围与限制](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview#limitations)。

## 2. 核心机制与关系

### 2.1 两条调用链

```text
ACA 原生调用：
调用方 → 目标 ACA 应用名 / FQDN → ACA ingress → 目标应用

Dapr 调用：
调用方 → 自己的 Dapr sidecar → 按目标 app-id 发现服务
      → 目标 Dapr sidecar → app-port → 目标应用
```

Dapr 服务调用仍是请求—响应，不等于把请求放进队列异步处理。C# 使用 `await`，也不会自动把业务改成消息队列模型。

### 2.2 如何选

| 比较项 | 直接 ACA 应用名调用 | Dapr 调用 |
|---|---|---|
| 服务发现 | ACA 名称解析与入口路由 | Dapr app-id |
| 配置成本 | 配置目标 ingress 和调用地址 | 额外配置两端 sidecar、app-id、app-port |
| 安全 | 按 ACA 网络及应用认证方案配置 | 增加 sidecar 间 mTLS；业务授权仍需设计 |
| 重试 | 明确配置客户端或平台策略 | 可利用 Dapr 能力，但仍需确认策略与幂等 |
| 可移植性 | 与 ACA 地址、网络约定有关 | 调用接口更统一，平台部署仍有差异 |
| 运行与诊断 | 链路较短 | 多一层资源开销，也需查看 sidecar 状态与日志 |

**选择结论：** 简单的 ACA 内部 API，优先原生调用；确实需要跨平台调用抽象、统一通信能力，且团队能承担额外运维复杂度时，再引入 Dapr。服务多不是唯一理由。

### 2.3 根据失败位置理解机制

| 现象 | 优先核对的层次 |
|---|---|
| 连本地 Dapr HTTP 端口都失败 | 调用方是否启用 Dapr，sidecar 是否就绪、端口是否正确 |
| Dapr 找不到 app-id | 目标 app-id、所在环境、目标 Dapr 是否启用及实例状态 |
| 找到目标但连接业务端口失败 | 目标 app-port、业务实际监听端口、HTTP/gRPC 协议 |
| 直接应用名解析或访问失败 | ACA 名称、环境、目标 ingress 和应用状态 |
| 业务接口返回错误 | 业务日志、请求参数、认证授权；不是一律归因于 Dapr |

这是调用链定位顺序，不是某次实验错误的复盘。最终结合两端日志确认，不能只看一个报错文字就下结论。

## 3. 必须掌握的命令

> 中文内容为待替换占位名称。命令采用单行，便于在 PowerShell 中执行。以下是复习用命令，不代表已经执行了云端修改。

### 3.1 初始化本地 Dapr

```powershell
dapr --version
dapr init
```

作用：查看 CLI/runtime 版本；准备本地运行环境。默认初始化需要 Docker 正常运行，会下载并启动相关依赖；不用于初始化 ACA 托管 Dapr。

### 3.2 获取并运行 C# quickstart

```powershell
git clone https://github.com/dapr/quickstarts.git
cd quickstarts/service_invocation/csharp/http
dapr run -f .
```

作用：按目录中的 `dapr.yaml` 一起启动示例应用与 sidecar。已有仓库无需重复克隆；先满足仓库声明的 .NET SDK 要求。

关键参数：`-f .` 使用当前目录的多应用运行配置。验收看两端日志能对应同一次订单调用，而不是只看进程启动成功。

### 3.3 单独启动一个被调用服务

```powershell
dapr run --app-id 服务标识 --app-port 应用监听端口 --app-protocol http --dapr-http-port 本地DaprHTTP端口 -- dotnet run --project 项目路径
```

作用：给指定 C# 程序配套启动 sidecar。

关键参数：`--app-id` 是服务身份；`--app-port` 是程序已经配置的监听端口；`--dapr-http-port` 是本地 Dapr API 端口；`--` 后面才是业务启动命令。同机多个 sidecar 的端口要避免冲突。

只发起调用、不接收调用的客户端通常无需配置 app-port。

### 3.4 查看和停止本地示例

```powershell
dapr list
dapr stop -f .
dapr stop --app-id 服务标识
```

作用：查看本地 Dapr 应用；按配置停止一组应用，或按 app-id 停止单个应用。停止时选择对应方式即可。

以上本地命令对应：[C# HTTP quickstart](https://github.com/dapr/quickstarts/tree/master/service_invocation/csharp/http)。

### 3.5 给 ACA 被调用应用启用 Dapr

```powershell
az containerapp dapr enable -n 被调用容器应用名称 -g 资源组名称 --dapr-app-id 目标服务标识 --dapr-app-port 应用监听端口 --dapr-app-protocol http
```

作用：启用 sidecar，并告诉它怎样连接业务服务。

关键参数：`--dapr-app-id` 供调用方寻址；`--dapr-app-port` 必须匹配业务端口；`--dapr-app-protocol` 描述 sidecar → 业务程序的协议。

仅向外调用、不提供入站业务接口的调用方：

```powershell
az containerapp dapr enable -n 调用方容器应用名称 -g 资源组名称 --dapr-app-id 调用方服务标识
```

参考：[Azure CLI Dapr 命令](https://learn.microsoft.com/en-us/cli/azure/containerapp/dapr?view=azure-cli-latest)。

### 3.6 查看 Dapr 设置与应用日志

```powershell
az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.configuration.dapr -o yaml
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type console --follow
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type system --follow
```

作用：确认启用状态、app-id、app-port；分别查看应用输出与平台系统事件。

关键参数：`--query` 筛选配置；`--follow` 持续查看。多 revision/replica 时用 `--revision`、`--replica` 定位，查看指定容器输出时用 `--container 容器名称`；同时结合两端与 Dapr 的日志判断。

### 3.7 用 HTTP 验证 Dapr 调用链

```powershell
curl.exe -i "http://localhost:本地DaprHTTP端口/v1.0/invoke/目标服务标识/method/接口路径"
```

作用：通过本地 sidecar 对目标服务发起 GET 请求；接口路径换成实际存在的 GET 路由。

关键点：这里填的是**调用方 sidecar 的端口**和**目标 app-id**，不是把目标业务端口填进 URL。ACA 上应从相应运行环境内部测试，电脑上的 localhost 不指向云端 sidecar。

## 4. 我的薄弱知识点总结

根据第32天答题记录，重点复习以下五项：

1. **端口方向不够明确**：能分别说清 App → sidecar、sidecar → App、ingress → App。（见 1.3）
2. **原生调用与托管身份混淆**：直接调用不必然需要托管身份；认证方式由接口要求决定。（见 1.4）
3. **重试后的幂等实现不够完整**：不只“先查后写”，还需稳定业务键、唯一约束与适当事务。（见 1.5）
4. **ACA 支持边界判断错误**：Jobs 当前不支持 Dapr，不能靠打开开关解决。（见 1.6）
5. **对比维度有遗漏**：除了发现、安全和移植，还要比较重试策略、额外配置及诊断成本。（见 2.2）

你已正确理解 app-id 的逻辑身份、基本 sidecar 链路、mTLS 的主要作用及 Dapr 的可选性；不把这些重复列为错题。

## 5. 最终复习清单

- [ ] 我能画出原生 ACA 调用与 Dapr 调用的两条链路。
- [ ] 我能解释 app-id、Dapr HTTP port、app-port 与 ingress target port。
- [ ] 我知道 mTLS、托管身份和业务授权不能互相替代。
- [ ] 我能说明超时为什么可能导致重复执行，以及怎样保证幂等。
- [ ] 我能启动 C# quickstart，并从两端日志验证同一次调用。
- [ ] 我能配置并查看 ACA 的 Dapr 设置，按失败所在链路定位问题。
- [ ] 我知道当前 ACA Jobs 不支持 Dapr，也不会照搬所有开源 Dapr 配置。
- [ ] 我能根据收益与成本决定是否引入 Dapr，而不是默认所有服务都加一层。

问答依据：[第32天共享会话](https://chatgpt.com/share/6aafa711-7484-83ee-8776-2d85e52f886e)。
