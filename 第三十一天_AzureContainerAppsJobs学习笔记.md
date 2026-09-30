# 第31天学习笔记：Azure Container Apps Jobs

> 已核对[共享会话](https://chatgpt.com/share/6aafa711-7484-83ee-8776-2d85e52f886e)中的第31天题目、答案、批改，以及退出码和 Event Job 的后续追问。第67题请求代答、第68题未见作答，不作为独立掌握依据。以下合并知识与薄弱点，不保留临时实验过程。

## 1. 核心知识点

### 1.1 App 与 Job：持续服务，还是做完退出

| 类型 | 程序生命周期 | 常见用途 |
| --- | --- | --- |
| Container App | 持续提供服务，或不断等待下一份工作 | Web API、持续消费消息的 Worker |
| Container Apps Job | 启动 → 完成有限工作 → 退出 | 报表、数据迁移、视频转码、单批任务 |

App 缩容到0，不会因此变成 Job。区别在于程序的工作方式，不在于当前有没有副本。

**容易混淆：**Job 中无限等待消息的 `while(true)` 会让执行迟迟不结束；如果目标是持续消费，通常应使用 App Worker。反过来，Job 完成后正常退出不是容器故障。

依据：[Apps 与 Jobs 对比](https://learn.microsoft.com/en-us/azure/container-apps/jobs#compare-container-apps-and-jobs)。

### 1.2 Job、Execution、Replica 各是什么

```text
Job：任务定义（镜像、资源、触发方式等）
├─ Execution A：一次实际执行
│  ├─ Replica 1：实际运行容器的一份实例
│  └─ Replica 2
└─ Execution B：另一次实际执行
```

- 创建 Manual Job 只创建定义，不自动执行。
- 每次触发产生一次 Execution；Replica 失败后的重试不等于创建新的 Job 资源。
- 一次 Execution 可以有多个 Replica；Replica 通常只有一个业务容器，也支持多容器。

依据：[Job concepts](https://learn.microsoft.com/en-us/azure/container-apps/jobs#concepts)。

### 1.3 三种触发方式

| 类型 | 谁决定开始 | 例子 |
| --- | --- | --- |
| Manual | 人、CLI 或 API | 手动补数、临时批处理 |
| Schedule | Cron 时间表 | 每晚生成报表 |
| Event | KEDA 读取事件源指标 | 队列出现待处理任务 |

Schedule 使用五段 Cron，按 **UTC** 计算。`*/5 * * * *` 是每5分钟；北京时间每天08:00对应 UTC 的 `0 0 * * *`。

**容易混淆：**每次计划触发是新的 Execution，不是把上一次失败任务继续执行。`parallelism=1` 只约束单次 Execution，不表示多次计划执行之间自动互斥；需要避免重叠时，用业务锁或任务领取机制。

依据：[Scheduled jobs](https://learn.microsoft.com/en-us/azure/container-apps/jobs#scheduled-jobs)。

### 1.4 重试、超时、并行度、完成数

| 参数 | 实际控制什么 |
| --- | --- |
| `replicaRetryLimit` | 失败 Replica 最多重试多少次，不保证一定重试满 |
| `replicaTimeout` | 等待 Replica 完成的超时秒数 |
| `parallelism` | 一次 Execution 内最多并行运行多少 Replica |
| `replicaCompletionCount` | 一次 Execution 需要多少 Replica 成功才满足完成条件 |

**你的薄弱点（第33题）：**ACA 当前要求 `replicaCompletionCount ≤ parallelism`，所以 parallelism=3、completionCount=5 不符合要求。不要把通用 Kubernetes Job 的参数组合直接套给 ACA。[配置要求](https://learn.microsoft.com/en-us/azure/container-apps/jobs#advanced-job-configuration)

**第27、66题补足：**timeout 不是 Job 资源的存活期限；应参考正常较慢情况下的耗时并留缓冲，不只是“大于平均5分钟”。太短会杀掉正常长任务，太长又会让卡死任务长期占用资源。

重试次数也不是时间保证：timeout 到期可能先于剩余重试，不能认为 retry=10 就一定执行满10次。暂时性故障可以重试；错误镜像、错误命令等确定性问题需修根因。

长任务还可能被平台维护中断，应允许安全重试；重试不能撤销已经发生的数据库修改或外部扣款，仍需幂等。

### 1.5 并行启动不等于自动分工

**你的薄弱点（第34题）：**知道要分片，但还不清楚怎么落实。把 parallelism 改成5，只是允许5份程序同时运行；如果程序都扫描全部文件，就可能做5遍。

可选分工方式：

- **队列领取：**每份 Replica 原子地领取一条任务，处理成功后确认；适合任务可独立处理的情况。
- **数据库领取：**用事务/锁领取待处理记录，避免两个实例同时占用同一任务。
- **明确分片：**事先拆成任务包，让每份执行取得明确的分片标识；不能假定平台自动给每个 Replica 分配业务分区。

完成数也只是平台成功条件，不证明所有业务分片都处理完。需要所有分片成功时，应让完成条件和程序的分工、结果核验一致。

### 1.6 Event Job：KEDA 决定执行数量，程序自己取消息

```text
队列出现积压
→ scaler 读取消息数量
→ 平台创建 Execution
→ 容器启动
→ C# 程序连接队列并 Receive
→ 处理、持久化结果、Delete/Complete
→ 正常退出
```

**你的薄弱点（第41题）：**“一条消息一个 Job”是应用设计，不是 KEDA 把某条消息直接塞给某次 Execution。即使 target=1，代码仍要自己领取、确认消息，并处理领不到消息的情况。

scaler 读取指标的身份与程序消费消息的权限要分别正确配置。多个 Execution 竞争领取消息，仍可能遇到重投递；超时、重试和确认失败都要求业务具备幂等性。

依据：[事件作业端到端教程](https://learn.microsoft.com/en-us/azure/container-apps/tutorial-event-driven-jobs)。

### 1.7 ScaledObject 与 ScaledJob

| 模型 | 主要扩什么 | 实例怎么工作 |
| --- | --- | --- |
| ScaledObject / App Worker | 长运行服务的副本数 | 一份实例持续处理多条消息 |
| ScaledJob / Event Job | 可完成的任务执行 | 处理一条或一小批任务，然后退出 |

**你的薄弱点（第51题）：**今天的扩缩对象是 Execution，不要继续套昨天“扩大常驻 Worker 副本池”的全部逻辑。ACA 已托管相关能力，不需要向底层集群提交 ScaledJob。

依据：[KEDA Scaling Jobs](https://keda.sh/docs/latest/concepts/scaling-jobs/)。

### 1.8 期望规模与正在运行数量

在 KEDA ScaledJob **默认策略的简化例子**中：

```text
目标规模 = min(maxReplicaCount, ceil(队列长度 / target))
本轮新增数量 = max(0, 目标规模 - runningJobCount)
```

| 队列长度 | target | 上限 | 当前运行数 | 本轮新增 |
| --- | --- | --- | --- | --- |
| 10 | 1 | 3 | 0 | 3 |
| 10 | 1 | 3 | 1 | 2 |

**你的错点（第52题）：**`runningJobCount=0` 是“当前没有运行的Job”，不是“只允许运行0个”。因此第一行应新增3个，不是0个。

这只是解释通用默认策略；不同 scaler 的指标口径、运行中任务和扩容策略都会影响结果，不能当作所有 ACA 场景的绝对计算器。[ScaledJob scaling strategy](https://keda.sh/docs/latest/reference/scaledjob-spec/#scalingstrategy)

ACA 的 `minExecutions/maxExecutions` 是事件扩缩配置，CLI 按每次轮询的执行数量界限描述；`parallelism` 则是单个 Execution 内部并行度。若确有10次执行同时运行、每次并行度3，概念上可有30份 Replica；不能把 maxExecutions=10 直接说成最多10份 Replica，也不能把它当整个 Job 历史累计执行上限。

### 1.9 Event Job 与常驻 Worker 怎么选

**你的薄弱点（第47、50、67题）：**不只问“做完能否释放”，还要看频率、时长、启动开销和隔离要求。

| 场景 | 倾向选择 | 主要原因 |
| --- | --- | --- |
| 每秒大量短消息、持续流入 | App Worker | 复用进程和连接，避免每条消息都承担启动成本 |
| 低频长任务、每次需独立资源或超时重试 | Event Job | 按次管理执行与失败，完成后退出 |
| 固定时间的报表 | Schedule Job | 时间触发且有明确完成点 |
| 人工发起的一次性操作 | Manual Job | 按需启动、查看单次结果 |

“长任务”不等于“永久运行”；40分钟的视频转码有完成点，仍可用 Job。反过来，App Worker 也能缩零，所以省空闲资源不是 Job 独有优势。

### 1.10 退出码：定位线索，不是固定故障字典

| 退出码/现象 | 先如何理解 |
| --- | --- |
| 0 | 进程报告成功；仍需满足 Execution 完成条件并核验业务结果 |
| 非0，例如实验的23 | 失败线索；23可能只是程序自定义返回值 |
| 126 / 127 | 在 shell/Docker 约定中常指无法执行 / 命令找不到，查入口命令与镜像 |
| 137 | 常与 SIGKILL 有关；检查 OOM、资源与强制终止证据，不能直接认定 OOM |
| 139 | 常与段错误有关，检查 native库、运行时和日志 |
| 143 | 常与 SIGTERM 有关，不等于一定超时 |

**你的薄弱点（第17、55题及追问）：**只回答 Exit Code 23 不足以解释根因。应定位具体 Execution/Replica，再看退出状态和对应 Console stdout/stderr。

若进程尚未启动、镜像拉取失败，可能根本没有应用退出码或应用日志，应看平台事件。若已有明确应用异常，则先查应用日志，不必从 Cron、Ingress 开始。

**补正原追问示例：**`dotnet` 已启动但 DLL 不存在，不应保证一定返回127；具体退出码由宿主程序决定。信号码与应用自定义码也可能重合，要结合 reason 和日志判断。[Docker 退出状态说明](https://docs.docker.com/engine/containers/run/#exit-status)

### 1.11 怎样确认是超时

**你的薄弱点（第59题）：**不能看见 Failed 就断言 Timeout。组合检查：

1. 本次 Execution 使用的 timeout 和程序参数。
2. 平台原因/事件是否提示 timeout、deadline 等。
3. 实际持续时间是否与超时设置相符。
4. 应用日志停在处理阶段，还是已主动记录异常并返回非0。

例如正常需60秒，但timeout=10秒，约10秒被终止且平台有超时证据，才支持“超时终止”判断。单凭缺少 SUCCESS 日志或退出码143都不够。

## 2. 核心机制与验收

### 2.1 执行与失败处理

```text
Manual / Schedule / Event
→ 创建 Execution
→ 启动 Replica（受 parallelism 约束）
→ 业务成功并正常退出：累计成功完成数
→ 满足 replicaCompletionCount：Execution 成功

Replica 失败
→ 根据重试次数与超时限制决定是否继续
→ 业务可能重复执行，因此需要幂等
```

不要把单个 Replica 一次失败，直接等同于整个 Execution 最终失败；其他完成结果和后续重试仍需查看。

### 2.2 最小验收清单

- Manual：创建定义、手动启动，保存返回的 Execution 名；观察运行到成功退出。
- 失败：让程序主动返回非0，核对退出信息、错误日志与重试表现。
- 超时：让任务耗时超过timeout，用平台原因和运行时间证明是超时，而不是只看Failed。
- 并行：增加parallelism，确认不是所有副本重复处理全部数据。
- Schedule或Event：验证自动产生新Execution，并能查到每次结果。
- 长期保存需要的日志和业务结果；执行历史不是永久审计存储，容器本地文件也不应作为唯一结果。

C# 程序至少输出任务/业务ID、开始时间、阶段、成功或异常；正常路径退出0，失败路径记录stderr并返回非0。不要捕获异常后仍无条件返回0。

## 3. 必须掌握的命令

> PowerShell 单行写法；中文名称是占位符。前提：Environment、镜像已准备好，私有镜像拉取身份已授权。以下为笔记命令，未执行云端操作。

### 3.1 创建 Manual Job

```powershell
az containerapp job create -n 作业名称 -g 资源组名称 --environment 环境名称 --trigger-type Manual --image 镜像地址 --cpu CPU核数 --memory 内存大小 --replica-timeout 超时秒数 --replica-retry-limit 重试次数 --parallelism 1 --replica-completion-count 1
```

作用：部署有限运行的 Console 任务定义。内存使用如 `0.5Gi` 的合法值；本命令不会自动启动Manual执行。私有ACR可按已有配置补充 `--registry-server`、`--registry-identity` 等参数。

### 3.2 启动并查询执行

```powershell
az containerapp job start -n 作业名称 -g 资源组名称
az containerapp job execution list -n 作业名称 -g 资源组名称 -o table
az containerapp job execution show -n 作业名称 -g 资源组名称 --job-execution-name 执行名称 -o json
```

作用：开始新Execution、查看历史和指定执行状态。保存start返回的名称，避免误查另一轮；start返回成功不代表业务已完成。

关键参数：详情命令使用 `--job-execution-name`，不要与日志命令的 `--execution` 混用。[Execution CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/job/execution)

### 3.3 查看 Replica 与日志

```powershell
az containerapp job replica list -n 作业名称 -g 资源组名称 --execution 执行名称 -o json
az containerapp job logs show -n 作业名称 -g 资源组名称 --execution 执行名称 --replica 副本名称 --container 容器名称 --tail 100
```

作用：查看实际实例信息，并定位一份容器的输出；实时观察时给日志命令加 `--follow`。

关键参数：execution、replica、container逐层缩小范围，不要把一份容器日志当作整个Execution的全部日志。Replica列表不是保证永久保存退出码的接口；已结束实例不可读时，到Portal执行详情、系统事件及已配置的Log Analytics中查终止原因、退出码与历史日志。

命令可能需要containerapp扩展，当前logs/replica命令为预览能力。[Replica CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/job/replica)、[Logs CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/job/logs)

### 3.4 修改超时、重试与并行配置

```powershell
az containerapp job update -n 作业名称 -g 资源组名称 --replica-timeout 超时秒数 --replica-retry-limit 重试次数 --parallelism 并行副本数 --replica-completion-count 成功副本数
az containerapp job show -n 作业名称 -g 资源组名称 --query properties.configuration -o yaml
```

作用：更新任务默认配置并核对结果，随后启动新的Execution验证；不要期待已结束的执行被改写。

关键参数：timeout单位是秒；成功副本数不超过parallelism。需要更改程序测试模式时可用 `--set-env-vars 变量名称=变量值`，但程序必须实际读取该变量。

### 3.5 改为 Schedule

```powershell
az containerapp job show -n 作业名称 -g 资源组名称 -o yaml
az containerapp job update -n 作业名称 -g 资源组名称 --yaml 作业配置文件路径
```

作用：查看配置，整理可部署YAML后更改触发方式。当前 `job update` 命令没有通用的 `--trigger-type` 参数，不要照搬create参数。

在完整配置中将 `properties.configuration` 的触发部分改为以下内容，并移除原来的manualTriggerConfig；保留环境、容器模板、资源、超时等必要配置：

```yaml
triggerType: Schedule
scheduleTriggerConfig:
  cronExpression: "*/5 * * * *"
  parallelism: 1
  replicaCompletionCount: 1
```

这是配置片段，不是完整文件。已是Schedule时，可直接修改计划：

```powershell
az containerapp job update -n 作业名称 -g 资源组名称 --cron-expression "Cron表达式"
```

关键参数：Cron按UTC解释；配置文件不保留只读运行状态，也不提交真实凭据。

### 3.6 Event Job 的配置结构（可选路线）

```powershell
az containerapp job create -n 事件作业名称 -g 资源组名称 --environment 环境名称 --trigger-type Event --image 镜像地址 --cpu CPU核数 --memory 内存大小 --replica-timeout 超时秒数 --replica-retry-limit 重试次数 --parallelism 1 --replica-completion-count 1 --polling-interval 轮询秒数 --min-executions 0 --max-executions 执行数量上限 --mi-user-assigned 托管身份资源ID --scale-rule-name 队列规则名称 --scale-rule-type azure-queue --scale-rule-metadata "accountName=存储账户名称" "queueName=队列名称" "queueLength=1" --scale-rule-identity 托管身份资源ID
```

作用：展示新建Storage Queue事件作业的结构。若改现有Manual Job，则通过YAML设置 `triggerType: Event` 与 `eventTriggerConfig`，不必删除历史资源。

关键参数：`queueLength=1` 是一份执行对应一条任务的目标，不是自动消息派送。还必须配置程序的队列地址、身份选择与消费权限，以及镜像拉取权限；仅运行这条配置命令不保证消费成功。托管身份扩容参数依赖支持它的containerapp扩展。

### 3.7 停止指定执行

```powershell
az containerapp job stop -n 作业名称 -g 资源组名称 --job-execution-name 执行名称
```

作用：停止指定Execution，不删除Job定义，也不阻止未来Schedule/Event触发。停止可能留下未确认任务或已完成的业务副作用，因此仍需幂等与正确消息确认。

配置命令参考：[Job CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/job)。

## 4. 我的薄弱知识点总结

- **第17、55、59题：**退出码与日志共同定位；用原因、时间与配置证据确认超时。
- **第27、66题：**timeout的作用范围和正常慢任务的缓冲，不把平均耗时当上限。
- **第33、34题：**completionCount与parallelism的约束；并行副本需要真正的业务分工。
- **第41题：**KEDA算执行数量，程序自己领消息，不是一条消息被平台精准指定给某次执行。
- **第51～53题：**ScaledObject与ScaledJob不同；runningJobCount是当前运行数，不是允许数量。
- **第47、50、67题：**选择Job/Worker要考虑消息频率、任务时长、启动开销和独立执行需求。
- **第68题：**未见独立画出完整结构，建议自行串起“触发→Execution→Replica→完成/重试/超时”。

已掌握的基本生命周期、三种触发方式、重试与幂等关系不重复列为错题。退出码追问作为知识补充，不把“主动追问”本身记为错误。

## 5. 最终复习清单

- [ ] 我能区分Job定义、一次Execution和其中的Replica。
- [ ] 我能解释超时、重试、并行度与成功完成数各控制什么。
- [ ] 我知道业务分片、消息确认和幂等必须由程序正确实现。
- [ ] 我能解释队列积压如何触发Event Job，以及为何不是自动派送消息。
- [ ] 我能根据任务特点选择常驻Worker或Event Job。
- [ ] 我能创建、启动、查看和停止指定执行，联合日志与状态判断失败原因。
- [ ] 我能把Manual配置改为Schedule或Event，并验证新执行记录。
