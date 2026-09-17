# 第25天学习笔记：Ingress、内部服务发现与多服务通信

> 根据第25天的学习资料及16道验收题整理。已合并重复知识点，忽略临时操作问题；“容易混淆”重点记录答题中暴露的理解偏差。

## 1. 核心知识点

### 1.1 Ingress：给应用提供入站入口

| 配置 | 含义 | 什么时候用 |
| --- | --- | --- |
| external | 允许从 Environment 外部边界访问 | 对外的 Gateway API |
| internal | 应用自身入口仅供同一 Environment 内访问 | Catalog 等内部服务 |
| disabled | 不提供 ACA Ingress 入口 | 不需要接收入站请求的后台服务 |

**容易混淆：**

- `internal` 是“有入口，但限制访问范围”，不是关闭入口。
- 你曾把 disabled 理解成“不接收任何网络”。准确说是**关闭 Ingress，不是断网**；应用仍可主动访问数据库、队列等服务。
- internal 不是“只允许 Gateway”：同环境其他应用也可能访问，业务权限仍需鉴权。
- external 不一定是公网入口。Environment 本身只有内网入口时，external 也不会把应用发布到公网。

本日采用普通应用入口，不额外配置环境级 HTTP 路由。若以后使用环境级路由，不要把 internal 服务另行映射到外部入口。[Ingress 官方说明](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)

### 1.2 targetPort：Ingress 往容器里面转发的端口

必须记住：

```text
targetPort = 容器中应用实际监听的端口
```

例如：

```text
浏览器 HTTPS :443
→ ACA Ingress
→ targetPort 5000
→ 应用实际监听 :5000
```

**容易混淆：**

你多次把 targetPort 当成外部端口，或看到实际监听 `5000`，仍回答应该配置 `8080`。

正确理解：

- `8080` 不是固定答案，以应用实际监听值为准。
- targetPort 不会让程序自动改为监听该端口。
- 普通 HTTP ingress 下，浏览器访问 `https://应用FQDN`，不加容器端口。
- Gateway 调用 `http://Catalog应用名称/products`，也不需要加 Catalog 的内部监听端口。

如果 Catalog 从监听 `5000` 改为 `7000`，应同步修改 targetPort；调用方的应用名地址不变。手动配置过探针端口时，也要一起核对。

应用应监听容器可访问的地址，例如 `0.0.0.0` 或 `[::]`，不能只监听 localhost。[目标端口说明](https://learn.microsoft.com/en-us/azure/container-apps/troubleshoot-target-port-settings)

### 1.3 应用名发现：找到 App，而不是固定 Replica

同一 Environment 内，可以使用：

```text
http://被调用的ContainerApp名称/接口路径
```

前提是目标应用已启用相应 ingress。

**容易混淆：**

你曾把地址中的名字称为 `servername`，或直接写 C# 项目名 `Catalog.Api`。

这里必须使用**实际部署的 Container App 名称**，不是项目名、镜像名、Revision 名或 Replica 名。

调用方不需要维护副本 IP。DNS 和平台代理共同完成发现与路由：

```text
应用名 → ACA 代理 → 目标 Revision → 可用 Replica
```

同环境使用应用名或完整 FQDN 调用，流量都不会绕到公网再回来。[服务发现说明](https://learn.microsoft.com/en-us/azure/container-apps/connect-apps)

### 1.4 应用名、FQDN 与访问权限是三回事

- **应用名**：同环境内部调用的简短地址。
- **FQDN**：完整域名；有完整域名不代表一定能访问。
- **访问权限**：由入口范围、网络和鉴权等共同决定。

**容易混淆：**

你曾认为应用名“更安全”。它主要是**简单、便于维护**，不是安全机制。

跨 Environment 时：

- 不能继续依赖目标服务的短应用名。
- 要使用可访问的 FQDN 或另外设计的入口。
- 仅打通网络，不会让目标 App 的 internal 入口自动接受其他 Environment 的请求。

同一 VNet 也不等于同一 ACA Environment。[访问范围说明](https://learn.microsoft.com/en-us/azure/container-apps/connect-apps#external-and-internal-fqdns)

### 1.5 HTTPS、HTTP 与 TCP

本日两个 C# API 使用 **HTTP ingress**：

- 外部通常访问 `https://应用FQDN`，使用 HTTPS 443。
- ACA 在入口处理 TLS，应用不必因此改为监听 HTTPS 443。
- HTTP 80 默认重定向到 HTTPS；`allowInsecure=true` 才允许不重定向的 HTTP。
- 同环境的应用名调用可按官方方式使用 `http://应用名称`，但不要据此推断所有通信都实现了端到端 TLS。

**容易混淆：**

外部使用 HTTPS，不只是因为需要完整域名，还因为它保护客户端到入口之间的传输。

TCP ingress 用于非 HTTP 协议。TCP 的 `exposedPort` 是入口端口，`targetPort` 是后端监听端口；不要把 HTTP 的 80/443 规则套到 TCP 上。[协议说明](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview#protocol-types)

### 1.6 地址稳定，不代表实例固定

| 发生变化 | 调用方的应用名地址是否需要改 |
| --- | --- |
| Replica 扩缩、替换、IP 变化 | 不需要 |
| 同一 App 发布新 Revision | 不需要 |
| 后端监听端口改变，并同步修正 targetPort | 不需要 |
| 换成另一个名称的 Container App | 需要更新目标地址 |

**容易混淆：**

你知道扩容后地址不变，但最初不清楚原因：**调用方找的是 App，平台负责选择后端实例。**

不能把创建一个新名字的 App 理解为“自动覆盖旧名字的映射”。

另外：

- 按已有规则自动增减 Replica，不创建新 Revision。
- 修改镜像、环境变量等模板配置，会创建新 Revision。
- 修改 ingress 属于应用级变更，影响所有 Revision，不创建新 Revision。[配置范围](https://learn.microsoft.com/en-us/azure/container-apps/ingress-how-to)

### 1.7 按错误发生的层次判断，而不是背状态码

| 现象 | 优先检查 |
| --- | --- |
| `No such host`／名称解析失败 | App 名称、目标是否存在、是否同环境、目标 ingress |
| `Connection refused` | 实际连接地址和端口、应用是否启动并监听 |
| `404` | 接口路径、路由、入口访问范围 |
| `502 / 503` | 后端监听端口、targetPort、可用副本和健康状态 |
| Gateway 返回 `500` | 先看 Gateway 日志，再根据具体异常定位 |

**容易混淆：**

- 你曾把 targetPort 不匹配判断成 `404`。它更常表现为后端连接失败，不是接口路径不存在。
- 你曾看到 `500` 就直接怀疑环境变量。应先看日志，不能只凭状态码确定原因。
- `Connection refused` 通常说明名称已解析，但不证明连接到了正确的入口和端口。
- Gateway `/` 返回 200，只证明这条请求成功，不能证明 Gateway → Catalog 也正常。
- 外部直接访问 internal FQDN 被拒绝，可能正是预期隔离行为。

这些只是排查方向，不是一一对应的诊断结论。[端口问题说明](https://learn.microsoft.com/en-us/azure/container-apps/troubleshoot-target-port-settings)

\*\*补正原验收批改：\*\*Replica 为 0 不一定是故障。配置了 HTTP 缩放时，应用可以正常缩零，并由请求触发启动；需要判断是否能成功唤醒，而不是直接认定“没有副本，所以无法调用”。[扩缩容说明](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)

## 2. 核心机制与关系

### 2.1 本日目标架构

```text
外部客户端
    │ HTTPS
    ▼
Gateway 的 external ingress
    │ Gateway targetPort
    ▼
Gateway.Api
    │ http://Catalog应用名称/products
    ▼
Catalog 的 internal ingress
    │ Catalog targetPort
    ▼
Catalog.Api
```

两个 App 位于同一 Environment；本日公网入口只留给 Gateway。

\*\*注意：\*\*Catalog 不直接对公网开放，不代表它返回的数据不会通过 Gateway 暴露。Gateway 仍需控制允许访问的接口和权限。

### 2.2 四个概念各管一件事

```text
应用名／FQDN → 找哪个应用
Ingress 范围 → 谁能通过入口访问
targetPort   → 平台往容器哪个端口转发
实际监听端口 → 程序真正在哪个端口接请求
```

最重要的对应关系：

```text
Catalog 实际监听 5000
→ Catalog targetPort 必须是 5000
→ Gateway 仍使用 http://Catalog应用名称
```

## 3. 必须掌握的命令

> 以下采用 PowerShell 单行写法。中文名称均为占位符，执行前替换。`-n` 指应用名称，`-g` 指资源组名称。

### 3.1 创建两个应用

前提：Environment 和镜像已准备好。私有 ACR 还需配置拉取身份与权限，本节不重复第23天内容。

```powershell
az containerapp create -n Catalog应用名称 -g 资源组名称 --environment 环境名称 --image Catalog镜像地址 --ingress internal --target-port Catalog实际监听端口

az containerapp create -n Gateway应用名称 -g 资源组名称 --environment 环境名称 --image Gateway镜像地址 --ingress external --target-port Gateway实际监听端口 --env-vars "CATALOG_BASE_URL=http://Catalog应用名称"
```

作用：Catalog 开内部入口，Gateway 开外部入口，并注入后端地址。

关键参数：

- `--environment`：两个应用填同一个环境。
- `--ingress`：控制入口范围。
- `--target-port`：各自应用的真实监听端口。
- `CATALOG_BASE_URL`：需要 Gateway 代码实际读取并用于 HTTP 调用；平台不会自动生成转发逻辑。

### 3.2 修改已有应用的入口

```powershell
az containerapp ingress enable -n Gateway应用名称 -g 资源组名称 --type external --target-port Gateway实际监听端口 --transport auto --allow-insecure false

az containerapp ingress enable -n Catalog应用名称 -g 资源组名称 --type internal --target-port Catalog实际监听端口 --transport auto
```

作用：启用或更新入口范围和目标端口。

关键参数：此命令使用 `--type`，创建应用时使用 `--ingress`；`auto` 用于普通 HTTP API。[Ingress CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/ingress)

### 3.3 关闭不需要的入口

```powershell
az containerapp ingress disable -n 容器应用名称 -g 资源组名称
```

作用：关闭 Ingress，不是停止应用或禁用出站网络。

关闭后不能继续按本日方式用应用名 HTTP 入口调用它。

### 3.4 查看入口、FQDN 和所属环境

```powershell
az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.configuration.ingress -o yaml

az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.configuration.ingress.fqdn -o tsv

az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.environmentId -o tsv
```

作用：核对 `external`、`targetPort`、FQDN，并比较两个 App 的环境 ID。

注意：入口对象存在且 `external=false` 表示 internal，不是 disabled。

### 3.5 更新 Gateway 的后端地址

```powershell
az containerapp update -n Gateway应用名称 -g 资源组名称 --set-env-vars "CATALOG_BASE_URL=http://Catalog应用名称"
```

作用：通过配置指定调用地址，不把环境地址写死在代码里。

关键参数：`--set-env-vars` 添加或更新指定环境变量；此变更属于模板配置，会创建新 Revision。

### 3.6 验证实际访问结果

```powershell
curl.exe -i "https://Gateway应用FQDN/Gateway实际接口路径"

curl.exe -i "https://Catalog应用FQDN/Catalog实际接口路径"
```

作用：从电脑分别验证 Gateway 的转发接口和 Catalog 的隔离。

关键参数：`-i` 同时显示响应状态与内容。

预期：通过 Gateway 能获得 Catalog 数据；电脑不能通过 Catalog 自身入口直接获得数据。不要只测试 Gateway 首页。

### 3.7 查看日志、Revision 和 Replica

```powershell
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type console --tail 50 --follow

az containerapp logs show -n 容器应用名称 -g 资源组名称 --type system --tail 50 --follow

az containerapp revision list -n 容器应用名称 -g 资源组名称 -o table

az containerapp replica list -n 容器应用名称 -g 资源组名称 --revision 修订版本名称 -o table
```

作用：

- console：查调用异常、实际监听端口。
- system：查平台启动、健康检查等事件。
- revision／replica：核对目标版本和运行实例。

关键参数：`--follow` 持续查看，按 `Ctrl+C` 退出。多容器、多副本时按需指定目标。

## 4. 我的薄弱知识点总结

按复习优先级排列：

1. **targetPort 与客户端访问端口不同**：以实际监听值为准，不固定填 8080。
2. **区分名称解析、连接失败、路由错误和应用异常**：先看日志，不靠状态码猜唯一原因。
3. **应用名代表 App，不代表 Replica**：平台代理负责后端选择和负载分配。
4. **使用实际 Container App 名称**：不能用 C# 项目名、镜像名替代。
5. **地址不是权限**：短名称不天然更安全，FQDN 也不能绕过 internal 限制。
6. **disabled 只关闭 Ingress**：不是应用完全没有网络能力。

## 5. 第25天最终复习清单

- [ ] 我能解释 external、internal、disabled 的区别。
- [ ] 我知道 internal 不等于“只允许 Gateway”，也不等于“整个 VNet 都能访问”。
- [ ] 我能根据应用日志填写正确的 targetPort。
- [ ] 我知道 HTTPS 443 与容器监听端口为什么可以不同。
- [ ] 我能用实际 Container App 名称配置同环境调用。
- [ ] 我能解释应用名 → 代理 → Revision → Replica 的关系。
- [ ] 我知道扩缩副本、发布新版后通常不需要修改调用地址。
- [ ] 我知道跨环境必须重新考虑地址和入口范围。
- [ ] 我能查看 ingress、FQDN、日志及运行副本。
- [ ] 我能验证“外部 → Gateway → Catalog 成功，外部不能直连 Catalog”。
- [ ] 我不会仅凭 404、500、502 或副本为 0 就认定故障原因。
