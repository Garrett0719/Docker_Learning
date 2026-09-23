# 第30天学习笔记：事件驱动扩缩容与 KEDA

> 已核对[共享会话](https://chatgpt.com/share/6aafa711-7484-83ee-8776-2d85e52f886e)中第30天题目、1～58题答案及批改。错题与不完整答案已合并进相关知识；第59题未见作答。实践选择 Storage Queue 或 Service Bus 一条路线即可。

## 1. 核心知识点

### 1.1 为什么看队列积压，而不只看 CPU

队列里等待处理的任务，就是业务积压。Worker 可能一直在等数据库或外部 API，此时 CPU 不高，但任务已堆积很多。

- CPU 回答“当前实例计算忙不忙”。
- 队列积压回答“还有多少工作没处理”。
- 扩容增加消费者，但不能无限解决数据库限流、外部服务变慢等瓶颈。

**容易混淆：**消息多，不一定 CPU 高；CPU 低，也不代表业务容量足够。除了队列长度，还应观察最老消息等待时间、处理速度和失败率。

**第55题补正：**你说“既然异步，CPU一般就很小”，这个因果不成立。异步只描述执行方式；图像处理等异步任务也可能很耗 CPU。应根据实际工作负载选指标。

### 1.2 KEDA 管实例数量，Worker 管业务处理

KEDA scaler 读取事件源指标，平台据此调整 Replica 数量。C# Worker 自己接收消息、执行业务并确认处理结果。

**容易混淆：**配置 scaler 不会自动让 Worker 会消费消息，也不会替你实现重试、幂等或业务逻辑。ACA 已集成 KEDA，不需要自己安装；不能把独立 Kubernetes 的 ScaledObject YAML 原样当作 ACA 配置。

Dapr 是可选的通信层：它可帮助发布/订阅；KEDA 负责根据积压扩缩容。使用 Azure SDK 直接消费队列，也可以配置 KEDA，不要求启用 Dapr。[Dapr 与 KEDA 示例](https://learn.microsoft.com/en-us/azure/container-apps/dapr-keda-scaling)

### 1.3 队列目标、Worker 并发和最大副本不是同一个参数

| 参数 | 控制什么 |
| --- | --- |
| Storage Queue `queueLength` | 每副本对应的队列长度目标 |
| Service Bus `messageCount` | 每副本对应的消息数量目标 |
| Worker 每副本并发 | 一份实例最多同时处理多少条，由代码/SDK 配置 |
| `maxReplicas` | 当前 Revision 最多扩到多少份 |

简化理解：期望副本数约为 `向上取整(积压量 ÷ 每副本目标)`，再受上下限和扩缩容行为约束。

**你的反复错点（第9、10、51题）：**不能把小数当副本数。固定按“先除 → ceil向上取整 → 套min/max”计算。例如 `201÷20=10.05 → 11 → max=10时最多10`；`187÷25=7.48 → 8`。target=8 时，7条需1份、17条需3份、100条需13份，再受max限制。这里是期望值，不代表瞬间就有这些就绪实例。

例如积压 100 条、目标 20 条/副本，期望约 5 份；max=3 时只能扩到 3 份。如果每副本并发是 4，总同时处理能力约为 12 条，而不是 60 条。

**容易混淆：**把 queueLength 设为 20，不会自动把 Worker 并发设为 20，也不表示每次批量拉取 20 条。目标也不等于从零唤醒阈值；scaler 可能另有 activation 参数。

**第48、56题纠正：**`queueLength/messageCount` 不是事件源名称，也不是“队列最大允许积压值”，而是每副本目标积压量。target太小还可能增加成本、连接数和下游压力（第12题），不能只理解成“扩得早”。

依据：[Storage Queue scaler](https://keda.sh/docs/latest/scalers/azure-storage-queue/)、[Service Bus scaler](https://keda.sh/docs/latest/scalers/azure-service-bus/)。

### 1.4 从零唤醒与缩回零

```text
min=0，Worker 无运行副本
→ 平台 scaler 仍检查队列
→ 发现可触发的积压
→ 启动 Worker，再按需求扩到 N
→ 积压消退、相关规则不再活跃
→ 经过缩容/冷却后回到 0
```

自定义事件规则常见默认轮询为 30 秒、缩零冷却为 300 秒。实际恢复与缩零不是精确倒计时，还包含采样、启动和退出过程。

**容易混淆：**Worker 不需要 HTTP ingress 才能被队列唤醒；但 min=0 且没有有效事件规则，就不能指望停止的 Worker 自己轮询队列。

**你的薄弱点（第28、31、33题）：**顺序是 scaler 先读队列指标，再唤醒 Worker，最后 Worker 才接收消息。逐步扩容是扩容决策/步长机制，冷启动影响新副本何时就绪，两者不要混成同一原因；自定义规则默认30秒轮询、300秒缩零冷却也需记牢。

依据：[ACA custom scale rules](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#custom)。

### 1.5 触发器与 Worker 是两条认证链

| 访问者 | 需要做什么 | Storage Queue 权限示例 |
| --- | --- | --- |
| scaler | 读取队列指标 | `Storage Queue Data Reader` |
| Worker | 接收、更新可见性、删除已完成消息 | `Storage Queue Data Message Processor` |
| 生产者 | 发送消息 | `Storage Queue Data Message Sender` |

优先使用托管身份，把权限限定到目标队列。创建队列还需要管理队列的权限，不能把“能发消息”当成“能创建队列”。[Storage 数据角色](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/storage)

**容易混淆：**给 scaler 指定身份，并不等于 Worker 已正确选择该身份并有消费权限。两者可以复用同一身份，但必须分别配置并授予所需权限。ACR 的 `AcrPull` 也不包含队列权限。

**第20、23、54题补正：**`identity: system` 指系统分配托管身份，不是“系统权限”。Identity回答“我是谁”，RBAC回答“我能对哪个资源做什么”。所以可能 scaler 能读取积压并扩到5份，但5份 Worker 全部因缺少 Receive/Delete 权限而失败。

使用 Secret 时，也要分别建立引用：

- scaler 的 `auth`：将触发器参数（如 `connection`）映射到 ACA Secret 名。
- Worker：通过自己的环境变量/配置读取 Secret。

**第25题需要记住的映射：**Secret 保存敏感值；`triggerParameter` 是 scaler 要求的认证参数名；`secretRef` 是提供该值的 Secret 名。例如以下是 scale rule 的认证片段，不含真实密钥：

```yaml
auth:
  - triggerParameter: connection
    secretRef: queue-connection
```

意思是“从名为 queue-connection 的 ACA Secret 取值，交给 scaler 的 connection 参数”，不是把连接串写成 secretRef。

不是写了 `secretRef` 就自动授权。连接串不要进入 Git、普通日志或可共享的命令历史。网络、防火墙、私有端点也必须允许对应访问。[ACA scaler authentication](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#authentication)

### 1.6 消息被接收，不等于业务已完成

| 队列 | 常见处理流程 |
| --- | --- |
| Service Bus（PeekLock） | 锁定消息 → 处理 → 成功后 Complete；锁过期或未完成可能再次投递 |
| Storage Queue | 接收后暂时不可见 → 处理 → 成功后 Delete；可见性超时后可能再次被接收 |

处理耗时较长时，需要合理设置超时或续锁/延长不可见时间。提前确认后再执行业务，进程一旦崩溃可能丢失任务；业务已完成但确认失败，则可能重复执行。

**第45题纠正：**处理需要60秒、可见性超时只有20秒时，不是“进程被结束”。A仍可能在处理，但20秒后消息重新可见，B也可能领到，于是发生并发重复。应合理设置或延长可见性时间，同时仍保留幂等保护。

**容易混淆：**队列暂时看不到可见消息，不代表所有业务都完成。指标也可能是近似值，或包含不可见消息，不能只凭一个队列计数下结论。

依据：[Service Bus 消息锁与确认](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement)、[Storage Queue .NET](https://learn.microsoft.com/en-us/azure/storage/queues/storage-quickstart-queues-dotnet)。

### 1.7 幂等：同一业务任务重复收到，效果仍只发生一次

例子：同一个订单扣款任务被投递两次，不能扣两次款。建议消息携带稳定的业务任务 ID，而不只依赖每次发送生成的新消息 ID。

一种常用实现：用持久化数据库的唯一约束记录任务 ID，把“业务变更”和“完成记录”放进同一事务；重复请求检测到已完成时跳过业务操作，再确认消息。

**容易混淆：**先查“处理过没有”再执行，但没有唯一约束或事务，多副本仍可能同时通过检查。只把 ID 放进内存，也挡不住重启和跨副本重复。

**你的薄弱点（第39、41、42题）：**本地 HashSet 不仅会随重启/缩容丢失，各副本之间也不共享。最小可靠流程是：数据库事务内尝试写唯一业务任务ID，并完成本地业务变更 → 一起提交 → 再确认队列消息。重复键须确认对应事务已经成功完成，才能跳过业务；不能把“刚登记但没完成”当成成功。事务失败则回滚，消息保留重试机会。

外部扣款等无法与本地数据库共用事务时，应使用下游支持的幂等键或配套一致性设计。重试不等于幂等，KEDA 也不保证业务 exactly-once。

Service Bus 的发送去重功能不能代替消费者幂等；反复失败的消息应限制重试并隔离。普通 Storage Queue Worker 的毒消息处理需要自行设计，不能默认存在 Service Bus 式死信机制。

### 1.8 Revision 与队列消费的边界

修改 scale 规则会创建新 Revision，按现有规则增减 Replica 不会。按照 ACA 非 HTTP 事件规则的指导，本日 Worker 使用 Single 模式。

**容易混淆：**HTTP 流量权重为 0，不等于该 Revision 不消费队列。活跃 Worker 可以主动连接队列，新旧版本同时运行就可能竞争消息；不要用 HTTP 90/10 推断消息也按此比例分配。

依据：[ACA scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)。

### 1.9 竞争消费者：分摊消息，不是广播

同一个 Queue 的多个 Worker 竞争领取任务，通常A领消息1、B领消息2、C领消息3；不是每条消息天然复制给所有副本。Topic 的不同 Subscription 则可各自收到一份，一个 Subscription 内仍可由多个消费者竞争处理。

**你的薄弱点（第43、44题）：**不是“做了幂等，才能让多个副本分摊消息”。队列负责领取和锁定机制；幂等应对的是超时、确认失败等导致的再次投递，不能把二者混为一谈。[竞争消费者模式](https://learn.microsoft.com/en-us/azure/architecture/patterns/competing-consumers)

## 2. 核心机制与验收

### 2.1 两条链路一起成立才算成功

```text
控制链：队列积压 → scaler 读取指标 → ACA 调整 Replica
处理链：生产者 → 队列 → Worker → 业务结果落库 → 确认/删除消息
```

扩容成功但队列不下降，要检查消费链；消息处理成功但副本始终不变，要核对扩容规则、指标、上下限与负载持续时间。不能仅凭一个现象确定唯一原因。

**第53、54题补全：**已明确是队列积压高但副本为0时，先查规则是否存在、type、队列/账户/namespace及目标值，再查 scaler 身份或Secret、RBAC、网络和System日志，不要无依据地转去查HTTP ingress。已经扩容但消费被拒绝，则重点查Worker认证与消费权限。

### 2.2 0→N→0 验收步骤

1. 选择一种队列，确认 Worker 能消费单条消息并写出业务结果。
2. 配置 min=0、合理的 max、队列目标及身份；等待并确认缩零。
3. 投入足够的一批测试消息，记录时间、队列积压和对应 Revision 的副本数。
4. 观察从零唤醒及扩容。若消息被一份 Worker 很快处理完，未扩到多份不一定是错误。
5. 消息完成后，核对业务成功数、失败/重试情况，再观察冷却后的缩零。
6. 重复发送相同业务 ID，验证业务副作用只发生一次；或者明确说明当前未实现幂等及重复风险。

建议记录：消息总数、每副本并发、scaler 目标、max、峰值副本、处理耗时、最终业务成功数、重复次数。此笔记未替你执行实验，不填虚构数据。

## 3. 必须掌握的命令

> 以下以已存在的 Storage Account、ACA Worker 和用户分配身份为前提。中文名称均需替换，采用 PowerShell 单行写法；仅供参考，不代表已执行。若选择 Service Bus，使用第3.6节替换队列规则。

### 3.1 创建实验队列

```powershell
az storage queue create --account-name 存储账户名称 --name 队列名称 --auth-mode login
```

作用：在已有账户中创建队列。`--auth-mode login` 使用当前 CLI 登录身份，该身份需要对应数据权限，而不只是订阅 Reader。

### 3.2 查看并绑定托管身份

```powershell
az identity show -n 托管身份名称 -g 身份资源组名称 --query "{resourceId:id,principalId:principalId,clientId:clientId}"
az containerapp identity assign -n Worker应用名称 -g 资源组名称 --user-assigned 托管身份资源ID
```

作用：获取身份标识，并让应用可使用该身份。

关键参数：资源 ID 用于 ACA 身份绑定和 scaler；principalId 用于 RBAC；clientId 用于 Worker SDK 显式选择用户分配身份。三者不要混填。

### 3.3 为同一身份分别授予指标与消费权限

```powershell
az role assignment create --assignee-object-id 托管身份principalId --assignee-principal-type ServicePrincipal --role "Storage Queue Data Reader" --scope 队列资源ID
az role assignment create --assignee-object-id 托管身份principalId --assignee-principal-type ServicePrincipal --role "Storage Queue Data Message Processor" --scope 队列资源ID
```

作用：本实验复用一个身份，分别允许 scaler 读指标、Worker 消费消息。`--scope` 尽量限定目标队列；执行者需有角色分配权限。

队列资源 ID 形式为：`存储账户资源ID/queueServices/default/queues/队列名称`。发消息的 CLI 登录用户另需 Sender 权限，不能借用 Worker 已获授权的事实。

### 3.4 配置 Worker 的连接信息

```powershell
az containerapp update -n Worker应用名称 -g 资源组名称 --set-env-vars "QUEUE_URL=https://存储账户名称.queue.core.windows.net/队列名称" "AZURE_CLIENT_ID=托管身份clientId"
```

作用：提供队列地址和身份选择信息；示例地址适用于 Azure 公有云。

关键参数：这些变量必须由 Worker 实际读取。C# 可使用 `Azure.Storage.Queues` 和 `Azure.Identity` 创建 QueueClient，并显式选择对应托管身份；仅设置变量不会自动生成消费代码。

### 3.5 配置 Storage Queue 扩缩容

```powershell
az containerapp revision set-mode -n Worker应用名称 -g 资源组名称 --mode single
az containerapp update -n Worker应用名称 -g 资源组名称 --min-replicas 0 --max-replicas 最大副本数 --scale-rule-name 队列规则名称 --scale-rule-type azure-queue --scale-rule-metadata "accountName=存储账户名称" "queueName=队列名称" "queueLength=每副本队列目标" --scale-rule-identity 托管身份资源ID
```

作用：让队列积压触发 Worker 扩缩容，不需要为纯队列 Worker 开 HTTP ingress。

关键参数：`queueLength` 是扩容目标，不是 Worker 并发；`--scale-rule-identity` 指定 scaler 身份。修改后检查实际规则，避免旧规则仍影响结果。若 CLI 未识别参数，先查看本机帮助及 containerapp 扩展版本。

### 3.6 Service Bus 路线的配置差异

```powershell
az containerapp update -n Worker应用名称 -g 资源组名称 --min-replicas 0 --max-replicas 最大副本数 --scale-rule-name 队列规则名称 --scale-rule-type azure-servicebus --scale-rule-metadata "namespace=ServiceBus命名空间名称" "queueName=队列名称" "messageCount=每副本消息目标" --scale-rule-identity 托管身份资源ID
```

作用：改用 Service Bus 队列作为指标源；无需同时配置两种队列。

关键参数：类型是 `azure-servicebus`，目标字段是 `messageCount`；Topic 场景则配置 `topicName` 和 `subscriptionName`。Worker 也必须改为相应 SDK、地址和 Service Bus 数据权限，Storage 的角色不能复用。scaler 的具体认证要求按所用版本文档核对。

### 3.7 发送与查看 Storage Queue 测试消息

```powershell
az storage message put --account-name 存储账户名称 --queue-name 队列名称 --content "测试消息内容" --auth-mode login
az storage message peek --account-name 存储账户名称 --queue-name 队列名称 --num-messages 5 --auth-mode login
```

作用：投入消息、只查看少量可见消息。内容格式和编码需与 Worker 约定一致。

关键参数：`peek` 不改变可见性；`get` 会取走消息并暂时隐藏，不能当作无副作用的查看命令。peek 结果也不是整个队列长度。

批量实验用自己的生产者程序或循环发送；每条消息应携带业务 ID。做重复测试时重复的是同一业务 ID，而不只是相同显示文本。队列长度可从 Portal 指标或 SDK 的 `GetPropertiesAsync()` 读取近似消息数。

### 3.8 查看规则、副本和两类日志

```powershell
az containerapp show -n Worker应用名称 -g 资源组名称 --query properties.template.scale -o yaml
az containerapp revision list -n Worker应用名称 -g 资源组名称 -o table
az containerapp replica list -n Worker应用名称 -g 资源组名称 --revision 修订版完整名称 -o table
az containerapp logs show -n Worker应用名称 -g 资源组名称 --type system --tail 100 --follow
az containerapp logs show -n Worker应用名称 -g 资源组名称 --type console --revision 修订版完整名称 --tail 100 --follow
```

作用：核对规则和实例变化；system 看平台/scaler 事件，console 看实际消费、异常及幂等处理。

关键参数：`--revision` 避免观察错版本；`--follow` 持续查看，按 Ctrl+C 结束。零副本时没有实时 Worker 输出，不能因此判定平台 scaler 也停止了。

命令依据：[ACA CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp)、[Storage Queue CLI](https://learn.microsoft.com/en-us/cli/azure/storage/queue)、[Storage Message CLI](https://learn.microsoft.com/en-us/cli/azure/storage/message)。

## 4. 我的薄弱知识点总结

根据实际答案，优先复习：

- **第9、10、51题：**先向上取整，再应用min/max，不能停在小数。
- **第12、48、56题：**target是每副本目标，不是事件源或最大积压量；过低会放大成本与下游压力。
- **第20、23、25、54题：**system身份、RBAC作用域、Secret认证映射，以及指标读取与消息消费权限的区别。
- **第28、31、33题：**scaler先读指标才启动Worker；扩容步长不等于冷启动；30秒/300秒各管什么。
- **第39、41、42题：**持久化唯一键和事务如何补足“先查数据库”和本地HashSet的不足。
- **第43～45题：**竞争消费者不是广播；可见性超时不会强行结束原进程，可能造成并发重复。
- **第53题：**按照已知队列场景查规则、身份和指标证据，避免无关分支。
- **第55题：**异步不等于低CPU，必须按任务实际负载判断。
- **第59题：**未见独立完成，建议自己串起“积压→扩容→消费→确认→重投递→幂等”。

已答对的业务积压价值、min/max边界、重复处理风险、Revision变更等不重复列为错题。

## 5. 最终复习清单

- [ ] 我能为一种队列配置正确的 scaler 元数据和身份。
- [ ] 我能解释并观察 0→N→0，而不是只看到容器启动就算通过。
- [ ] 我能根据任务耗时、并发及下游容量设置目标与副本上限。
- [ ] 我能分别验证指标读取成功与消息处理成功。
- [ ] 我能用稳定业务 ID 解释或验证重复处理防护。
- [ ] 我知道 KEDA 不负责消息消费，也不保证业务恰好执行一次。
