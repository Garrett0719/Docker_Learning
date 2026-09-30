# 第33天学习笔记：Dapr Pub/Sub 与异步通信

> 根据第33天的10道验收题、原始作答、批改及官方资料整理。“容易混淆”注明实际答题薄弱点；其他提醒为必要知识补充。资料核对日期：2026-09-30。

## 1. 核心知识点

### 1.1 同步还是异步：先问是否必须立即拿到结果

| 业务要求 | 更适合 | 原因 |
|---|---|---|
| 下单前必须立即确认库存 | HTTP / Dapr Service Invocation | 后续操作依赖当前响应 |
| 订单成功后发送邮件、写分析数据 | Pub/Sub | 允许稍后完成，不必阻塞下单 |
| Shipping 只要求收到订单事件 | Pub/Sub | 没有要求下单接口等待物流处理结果 |

同步调用等待被调用服务响应；异步消息让发布者交出消息后继续工作。异步能解耦和缓冲积压，但要承担延迟一致、重复消息、重试和监控的成本。

**容易混淆（第10题）：** 你把 Shipping 选成同步。判断依据不是“物流重要不重要”，而是“当前步骤是否必须等它返回结果”。若业务另有即时确认要求，再重新选择。

**补充：** C# 的 `await` 不决定业务通信属于同步还是异步。`await PublishEventAsync(...)` 等待的是发布完成，不是下游业务全部完成。

### 1.2 Component、Topic、app-id 各指什么

```csharp
await client.PublishEventAsync("orderpubsub", "orders", order);
```

| 名称 | 含义 |
|---|---|
| `orderpubsub` | Dapr Pub/Sub 组件的逻辑名称 |
| `orders` | 发布消息的主题 Topic |
| `order` | 消息内容 |
| `app-id` | 发布或订阅应用的身份，不是这里的第一个参数 |
| Broker | 实际传递、保存消息的系统，例如 Redis Streams、RabbitMQ |

发布者面向 Topic 发消息，不需要指定每个订阅者的地址。Dapr 是对消息系统的抽象，**不是替代 Broker 的内存队列**。

参考：[Dapr Pub/Sub quickstart](https://docs.dapr.io/getting-started/quickstarts/pubsub-quickstart/)。

### 1.3 发布成功 ≠ 消费成功

发布成功表示消息已成功交给 Pub/Sub 系统，不表示订阅者已完成数据库写入或发邮件。

订阅者可能稍后才处理，也可能正在重试。业务需要知道最终结果时，应通过业务状态、结果事件等方式追踪。

**容易混淆（第3题表述需收紧）：** “上游不关心下游”是指不必同步等待，不是系统从此不用关心失败。积压、持续失败与死信仍需监控。

### 1.4 至少一次投递：为什么必须幂等

典型重复场景：

```text
发布一次 → 消费者写库成功 → 成功确认前崩溃
        → 消息系统没有收到确认 → 再次投递
```

**容易混淆（第4题漏答）：** At-least-once 保证的是投递语义，不是“业务恰好执行一次”。只要可能重复，消费者就必须让同一业务操作重复执行时不产生额外结果，即幂等。

同一消息可能再次落到其他副本；不能靠“发布者只发了一次”排除重复。至少一次也不代表非法业务一定能成功，仍受重试、过期与死信等策略影响。

参考：[消息投递与至少一次语义](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/#at-least-once-guarantee)。

### 1.5 幂等：数据库兜底，不只在代码里先查后写

**容易混淆（第5题）：** 两个副本可以同时查到“没有订单”，然后同时插入。“查询”和“写入”分成两步，就存在并发漏洞。

本次“重复订单 ID 不重复写入”的基本方案：

```text
稳定的 OrderId → 数据库 PRIMARY KEY / UNIQUE → 只允许一条订单记录
```

- 第一次插入成功后，再向 Dapr 返回成功。
- 遇到相同幂等键且确认已成功处理的重复请求，不重复执行业务，返回成功。
- 只把预期的重复键冲突当作重复处理；连接失败等异常不能一律吞掉并返回成功。
- 若还有库存、积分等数据库操作，把去重记录与本地业务更新放在同一事务中，避免“记录已处理，但业务没做完”。

**容易混淆（第6题回答不完整）：** `HashSet` 不只是占内存：不同副本不共享，进程重启会丢失，普通 HashSet 还需要考虑线程安全。它可以演示，不能作为跨副本、持久化去重的最后保障。

**幂等键按业务选：** 创建订单可用 OrderId；同一订单可能还有支付、取消等不同事件，不能全被同一个 OrderId 拦掉，可使用稳定 EventId 或“订单 ID + 操作类型”。重新发布同一业务时要保留去重依据，不能每次都生成全新的业务键。

**边界提醒：** 数据库唯一约束只能保证受它约束的数据。发送邮件、外部扣款不在本地数据库事务内；“发完邮件再写已发送”仍有崩溃窗口，需外部服务的幂等键或可恢复发送流程，不能宣称一个 UNIQUE 就保证外部动作只做一次。

### 1.6 不同订阅应用各一份，同一应用的副本分担处理

正常配置下，Email 与 Shipping 都订阅订单主题：

```text
订单事件
├─ Email（app-id=email）    → 它的3个副本竞争这一份
└─ Shipping（app-id=shipping）→ 它的2个副本竞争这一份
```

**容易混淆（第8题选错）：** 不是在 Email 和 Shipping 中随机选一个服务，也不是5个副本全部执行。不同订阅应用分别完成各自业务；同一应用的副本分担该应用的消息。

前提是组件支持这种消费模式，且没有覆盖默认消费组设置。**竞争消费不等于恰好一次**：失败重投仍可能让其他副本再次收到，所以仍要幂等。

参考：[Dapr 消费组与竞争消费](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/#consumer-groups-and-competing-consumers-pattern)。

### 1.7 Retry、毒消息、Dead Letter Topic

| 概念 | 作用 |
|---|---|
| Retry | 对暂时性失败再次尝试，例如数据库短暂不可用 |
| Poison Message（毒消息） | 持续导致处理失败的消息，例如非法字段触发同一错误 |
| Dead Letter Topic（死信主题） | 隔离无法正常处理的消息，供记录、告警、修复和重放 |

**容易混淆（第7题）：** 你把坏消息本身叫成了 Dead Letter Topic。应区分“有问题的消息”和“安置失败消息的地方”。死信主题不是负责判断哪些消息该重试的机制；重试与转移条件由策略决定。

配置要点：

- 订阅配置中的 `deadLetterTopic` 指定转移目的地，还应配置处理该主题的订阅者。
- 按需设置有限重试。Dapr 默认在设置死信主题却未配重试策略时，失败消息会直接转入死信主题。
- 消息进入死信不等于自动删除或自动修好：记录原因、告警、修复后再重放，确认无效才丢弃。
- Dapr 死信主题与 Broker 自带死信队列不是同一项配置；需分别确认行为。

参考：[Dapr Dead Letter Topics](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-deadletter/)。

Broker/组件本身也可能重投；Dapr 重试策略可能在其上叠加，不是覆盖它。不要多层无上限重试。[组件重试说明](https://docs.dapr.io/reference/components-reference/supported-pubsub/)

### 1.8 消费者何时确认成功

对于本次 HTTP 订阅方式，Dapr 把消息 POST 到业务路由，业务通过响应告诉 Dapr 处理结果：

| 响应 | 含义 |
|---|---|
| HTTP 2xx，空响应或没有 `status` 的普通 JSON | 默认视为成功 |
| HTTP 2xx + `{"status":"SUCCESS"}` | 明确成功 |
| HTTP 2xx + `{"status":"RETRY"}` | 请求重试 |
| HTTP 2xx + `{"status":"DROP"}` | 明确丢弃，不等于存入死信供以后修复 |

应在业务已持久完成后确认；已成功处理的重复消息也应确认。不能收到消息就先返回200，再指望进程里的后台任务一定做完。

提醒：普通失败响应通常触发重试，但404有丢弃语义，不能认为“所有非2xx都会重试”。具体重试与死信效果还需结合配置验证。

参考：[Dapr Pub/Sub 响应约定](https://docs.dapr.io/reference/api/pubsub_api/#expected-http-response)。

### 1.9 C# 订阅的最小知识

官方示例用以下几项连接消息与业务路由：

```csharp
app.UseCloudEvents();       // 处理 CloudEvents 消息封装
app.MapSubscribeHandler(); // 向 Dapr 提供 /dapr/subscribe 订阅信息

app.MapPost("/orders", [Topic("orderpubsub", "orders")] (Order order) =>
{
    // 在这里完成持久化、去重及业务处理，成功后才返回成功。
    return Results.Ok();
});
```

这是订阅接线示意，不是完整幂等实现。`Topic` 指定组件和主题，`/orders` 是本应用的接收路由；两者不必同名。订阅者 sidecar 的 app-port 必须对应业务实际监听端口。

Dapr 默认用 CloudEvents 封装消息，包含事件 ID 和业务 data。它统一消息格式，不会自动按 OrderId 帮你做业务去重。

参考：[官方 C# 订阅者代码](https://github.com/dapr/quickstarts/blob/master/pub_sub/csharp/sdk/order-processor/Program.cs)。

### 1.10 组件替换：降低迁移成本，不消除差异

应用依赖的是组件逻辑名和统一 API。保留组件名、主题与消息契约时，可把 `spec.type` 从 `pubsub.redis` 换成其他实现，并配套修改连接、身份等配置，尽量不改业务调用代码。

**容易混淆（第9题需收紧）：** 不是只改一个 type 就能“随意替换”。仍要验证顺序、消费组、重试、死信、消息大小和持久化等差异；已有积压不会因为换组件自动迁移到新 Broker。

本次先在本地完成验收。只有确实需要验证云端身份、网络、托管运维或扩缩容时，再部署到 ACA；这也是第10题遗漏的选型理由。

## 2. 核心机制与关系

### 2.1 消息链路

```text
Publisher → 本地 Dapr sidecar → Pub/Sub Component → Broker / Topic
                                                   ↓
Subscriber ← 它的 Dapr sidecar ← Pub/Sub Component ← 消息
     ↓
持久化业务 + 幂等 → 成功确认
     └─ 失败 → 按策略重试 → 仍失败 → Dead Letter Topic → 告警/修复/重放
```

记住三个不等式：

- 发布成功 ≠ 下游完成。
- 单次发布 ≠ 单次投递。
- 一次竞争消费选中一个副本 ≠ 永远只执行一次。

### 2.2 复习时应能复现的验收

1. **重复发布**：重复发送同一 OrderId，数据库订单记录仍只有一条。
2. **失败重投**：第一次写库成功后故意不给成功确认，观察再次收到消息，但不重复写入。
3. **重启与并发**：重启消费者、并发发送重复订单，数据库仍守住唯一性。
4. **持续失败**：非法消息经指定策略进入死信处理，而不是无限重试。

重复发布测试业务去重；“写库成功但确认失败”才是在验证消息重投的关键窗口。验收看数据库结果与确认行为，不只看日志出现几次。

## 3. 必须掌握的命令

> 中文名称是待替换参数；使用单行命令便于 PowerShell 执行。本日核心是本地实验，不要求额外创建 Azure 资源。

### 3.1 准备与运行官方 C# 示例

```powershell
dapr --version
dapr init
git clone https://github.com/dapr/quickstarts.git
cd quickstarts/pub_sub/csharp/sdk
dapr run -f .
```

作用：检查版本、初始化本地环境并启动官方发布者和订阅者。已有环境和仓库无需重复初始化、克隆；Docker 应已运行，.NET SDK 满足项目要求。

关键参数：`-f .` 使用当前目录的 `dapr.yaml`，其中 `resourcesPath` 指向组件资源目录，`appPort` 指向订阅者业务端口。

参考：[C# Pub/Sub quickstart](https://github.com/dapr/quickstarts/tree/master/pub_sub/csharp/sdk)。

### 3.2 单独运行带组件配置的订阅者

```powershell
dapr run --app-id 订阅应用标识 --app-port 应用监听端口 --dapr-http-port 本地DaprHTTP端口 --resources-path 资源目录 -- dotnet run --project 项目路径
```

作用：启动业务进程与 sidecar，并加载组件、订阅等资源。

关键参数：`--resources-path` 是 Dapr 资源目录；`--app-port` 必须匹配程序监听端口；`--` 后面是业务启动命令。本机启动多个 sidecar 要避免端口冲突。

### 3.3 发布测试消息

```powershell
dapr publish --publish-app-id 发布端应用标识 --pubsub 组件名称 --topic 主题名称 --data-file 消息JSON文件路径
```

作用：通过正在运行的本地 sidecar 发布消息。准备符合消息契约的 JSON 文件，例如内容为 `{"orderId":1001}`，重复执行即可验证同一订单的去重。

关键参数：`--publish-app-id` 是**借哪个本地应用的 sidecar 发布**，不是目标订阅者；`--pubsub` 是组件逻辑名；`--data-file` 读取消息文件，避免命令行 JSON 引号问题。

若原示例发布者已退出，其 sidecar 不再可用；先用 `dapr list` 确认可用实例。这是本地 CLI，不会直接寻找云端 ACA 实例。

参考：[dapr publish 命令](https://docs.dapr.io/reference/cli/dapr-publish/)。

### 3.4 通过 HTTP 发布（与上一种方式二选一）

```powershell
Invoke-RestMethod -Method Post -Uri 'http://localhost:本地DaprHTTP端口/v1.0/publish/组件名称/主题名称' -ContentType 'application/json' -InFile '消息JSON文件路径'
```

作用：直接调用 Dapr 发布 API。

关键参数：URL 端口是本地 sidecar 的 HTTP 端口，不是订阅者 app-port；`-InFile` 提供 JSON 消息。成功返回不表示订阅者业务已经完成。

### 3.5 查看订阅声明

```powershell
Invoke-RestMethod -Uri 'http://localhost:订阅者应用端口/dapr/subscribe'
```

作用：查看程序通过 `MapSubscribeHandler()` 声明的组件、主题和路由。

关键点：请求的是**业务端口**，不是 Dapr HTTP 端口；本命令针对程序式 HTTP 订阅，不是所有订阅方式的通用查询接口。

### 3.6 查看运行状态、Broker 日志与停止实验

```powershell
dapr list
docker ps
docker logs --tail 100 Broker容器名称
dapr stop -f .
```

作用：查看本地 sidecar 与容器状态、Broker 近期日志，最后停止当前多应用配置启动的进程。

关键参数：`--tail 100` 只看近期日志；`-f .` 对应当前示例配置。业务和 sidecar 输出在运行终端查看。停止示例不等于删除 Broker 容器或清空其数据。

## 4. 我的薄弱知识点总结

- **第4题漏解释**：至少一次 → 可能重复 → 必须幂等，不是只记术语。（1.4）
- **第5、6题不完整**：先查后写存在并发漏洞；HashSet 不共享、不持久，数据库唯一约束与事务才是关键。（1.5）
- **第7题概念错位**：毒消息是失败消息，死信主题是隔离目的地；重试策略、死信处理各有职责。（1.7）
- **第8题选错**：不同订阅应用分别收到，同一应用副本竞争消费；仍可能重投。（1.6）
- **第9题边界不够明确**：统一 API 不等于 Broker 行为相同，也不自动迁移消息。（1.10）
- **第10题选型与遗漏**：Shipping 是否同步看业务是否等待；没有新增学习收益，不必强行部署 ACA。（1.1、1.10）

已经答对的组件名/Topic 区分、发布成功不等于完成等内容保留作基础知识，不重复标成错题。

## 5. 最终复习清单

- [ ] 我能按“是否立即需要结果”选择同步调用或异步消息。
- [ ] 我能画出 Publisher、sidecar、Component、Broker、Subscriber 的关系。
- [ ] 我能解释一次发布为什么可能收到两次，以及多应用、多副本如何分配消息。
- [ ] 我能让重复订单在并发、重启、失败重投后仍不重复写入。
- [ ] 我知道何时返回 SUCCESS、RETRY，以及何时隔离到死信主题。
- [ ] 我能说明组件替换的收益与仍需验证的差异。
- [ ] 我能运行 C# 示例、发布重复消息、检查订阅与持久化结果。

问答依据：[第33天共享会话](https://chatgpt.com/share/6aafa711-7484-83ee-8776-2d85e52f886e)。生态了解：[Dapr 主项目](https://github.com/dapr/dapr)，本日无需阅读源码或记忆星标数量。
