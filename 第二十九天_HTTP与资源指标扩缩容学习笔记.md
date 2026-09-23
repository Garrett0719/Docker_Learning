# 第29天学习笔记：HTTP 与资源指标扩缩容

> 已核对[共享会话](https://chatgpt.com/share/6aafa711-7484-83ee-8776-2d85e52f886e)中第29天题目、1～40题答案及批改。薄弱点已合并进相关知识；第41题未见作答，不作为已掌握依据。

## 1. 核心知识点

### 1.1 扩缩容改变的是 Replica 数量

水平扩容是“多运行几份相同版本”，不是让一份容器自动获得更多 CPU/内存。

- 修改扩缩容规则、min/max：属于模板配置，会创建新 Revision。
- 按现有规则自动增减 Replica：不创建新 Revision。
- Multiple 模式中，每个 Revision 保留自己的规则；观察时要确认请求进入哪个版本。

**容易混淆：**副本变多，不代表镜像或配置变了；修改 CPU/内存配置，也不是自动水平扩容。

依据：[ACA 扩缩容](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)。

### 1.2 min/max replicas：下限与上限

| 设置 | 含义 | 不代表什么 |
| --- | --- | --- |
| `minReplicas=0` | 允许空闲时缩到零 | 不代表发布后立即变成零 |
| `minReplicas=1` | 正常情况下至少保留一份实例 | 不保证永不重启、永无冷启动或高可用 |
| `maxReplicas=5` | 单个 Revision 自动扩容的配置上限为五份 | 不代表一直运行五份，也不是五个请求的限制 |

**容易混淆：**上下限按 Revision 计算。两个活跃 Revision 各自保留一份时，整个 App 就可能有两份；平台维护、交接时也可能短暂出现额外副本。

上限限制扩容空间，但不能替代容量规划：达到上限后，更多请求仍可能造成排队、延迟或失败。

依据：[Scale definition](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#scale-definition)。

### 1.3 HTTP 规则：看请求压力，不是累计访问次数

`concurrentRequests` 是 HTTP 扩容阈值，用于衡量每副本的请求负载；不是“一天累计达到多少次就扩容”，也不是超过阈值就立即拒绝请求的限流器。

阈值越低，同样负载下通常越积极扩容；过低可能浪费资源，过高可能让响应变慢。扩容还需要指标采样、调度、启动和就绪时间，不会瞬间完成。

**容易混淆：**不能用“我连续点了十次，但没扩容”判断规则失效。串行访问快接口，未必形成足够的持续压力。也不要把 HTTP 阈值当成严格的 QPS 配额。

**你的薄弱点（第9、10、34题）：**测试接口的 `delayMs=2000` 让请求停留更久，配合并发发送形成持续的在途请求，便于观察扩容；它不是让程序更耗 CPU，也不是生产优化。HTTP 指标约按 15 秒窗口计算，并非“默认每5分钟扩一次”。跨过阈值也不保证立即新增可用副本。

依据：[HTTP scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#http)。

### 1.4 CPU/内存规则与 HTTP 规则怎么选

| 规则 | 关注什么 | 使用提醒 |
| --- | --- | --- |
| HTTP | 请求负载 | Web API 的直接选择 |
| CPU | 计算压力 | 适合计算密集任务；等待数据库时 CPU 可能并不高 |
| Memory | 内存使用 | 扩容不一定解决单请求内存过大或内存泄漏 |

CPU/内存的 `Utilization` 表示相对所请求资源的平均使用百分比，不是整台宿主机的占用率。`AverageValue` 则是平均资源用量，不能混用数值含义。

**容易混淆：**资源分配量和扩容阈值是两件事。例如给容器分配 CPU，再设置 70% 的使用率目标，不是把 CPU 直接改成 70%。

仅靠 CPU/内存规则不能完成从零唤醒：没有运行实例，就没有实例 CPU/内存负载可供触发。只用资源指标时保留至少一份；需要缩零时搭配支持唤醒的 HTTP 或事件规则。

依据：[KEDA CPU](https://keda.sh/docs/2.20/scalers/cpu/)、[KEDA Memory](https://keda.sh/docs/2.20/scalers/memory/)。

### 1.5 Scale to zero：零副本不等于应用被删除

缩到零时，应用配置、Revision 和平台入口仍在，但业务容器没有运行实例。

```text
HTTP 请求到达有效入口
→ 平台触发从零扩容
→ 容器启动、应用初始化、就绪
→ 应用处理请求
```

唤醒依靠平台，不是已经停止的 C# 进程自己检测请求。事件应用也可以通过队列等外部信号唤醒。

**容易混淆：**Ingress 关闭、min=0，又没有可用事件规则时，缩零后没有自动恢复的触发来源。应保留 min≥1，或配置正确的事件规则。仅添加 CPU 规则不能解决这个问题。

依据：[默认规则与从零恢复](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#default-scale-rule)、[KEDA 扩缩容模型](https://keda.sh/docs/2.20/concepts/scaling-deployments/)。

### 1.6 冷启动：第一批请求可能需要等应用起来

冷启动可能包含实例调度、镜像准备、.NET 启动、依赖初始化和就绪检查，因此从零唤醒的首请求通常比热请求慢，但时间不是固定值。

- min=0：适合低频、可接受首请求等待的场景。
- min=1：保留热实例，适合对延迟敏感的入口 API；仍可能因重启、新版发布或扩容出现新实例启动。
- Gateway 常驻不代表整条调用链都热：Catalog 若缩零，Gateway 的业务接口仍可能等待它启动。

**容易混淆：**不能仅凭“首请求慢”认定一定是冷启动。要结合请求前的副本数、启动日志、网络及下游耗时判断。

**你的薄弱点（第13～16题）：**先有请求到达 Ingress，平台才触发启动；冷启动是实例准备到应用可服务的等待，不是“用户发出请求”本身。Warm 请求变快，是因为已有运行实例，不再经历这次 0→1。选择 min=0/1 主要权衡空闲成本与首请求延迟要求；峰值容量主要靠 max、规则和单副本能力判断。

你在第35、37题中还用了“有副本肯定成功”“min=1一直稳定快速”的绝对说法：副本存在仍需就绪，业务和下游也可能失败；min=1不能保证请求一定成功或始终低延迟。

### 1.7 轮询、冷却与缩容稳定窗口

| 概念 | 通俗理解 |
| --- | --- |
| Polling interval | 多久检查一次外部事件源有没有工作 |
| Cooldown period | 触发条件不活跃后，缩回零之前等多久 |
| Scale-down stabilization window | 缩容决策参考一段时间的需求，避免负载稍降就减少副本 |

KEDA 常见默认轮询为 30 秒、缩零冷却为 300 秒。ACA 文档明确：事件轮询间隔不适用于 HTTP/TCP，不能解释成“HTTP 请求必须先等 30 秒”。

**你的薄弱点（第10、23题）：**曾把5分钟当成扩容间隔，把 Cooldown 当成另一种检查频率。记成：HTTP/TCP 约15秒指标窗口；Custom/KEDA 默认30秒轮询；默认300秒冷却主要影响最后缩零。它们不是三个可以互换的定时器。

**容易混淆：**冷却不是每次扩容之前的等待时间，也不是所有 `N→1` 缩容都由它控制。停止压测后没有立刻缩零，可能是正常保护机制。

实际时间还受采样、缩容决策和实例退出影响，不保证停请求后恰好第 300 秒变零。

依据：[KEDA ScaledObject：轮询与冷却](https://keda.sh/docs/2.20/reference/scaledobject-spec/)、[ACA scale behavior](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#scale-behavior)。

### 1.8 多规则是 OR，不是 AND 或数量相加

只要一个有效规则要求扩容，就可能扩容，不需要 HTTP、CPU、内存同时达到阈值。

通常按各指标计算的期望副本数取较大需求，再受上下限及扩缩容行为约束；不是把每条规则的副本数相加。比如一条需要 2 份，另一条需要 4 份，不是因此需要 6 份。

**容易混淆：**HTTP 请求减少，但其他规则仍有负载时，不应期待立即缩容。判断缩零条件要看所有相关规则。

依据：[KEDA 多触发器模型](https://keda.sh/docs/2.20/concepts/scaling-deployments/)。ACA 使用 KEDA 能力，但不等于可以直接对其底层 Kubernetes 执行 `kubectl apply`，也不能假定 KEDA 最新版所有参数都已被 ACA 暴露。

## 2. 核心机制与验收方法

### 2.1 本日主线

```text
请求/资源/事件指标
→ 扩缩容规则判断需求
→ min/max 与稳定机制约束
→ 当前 Revision 的 Replica 增减
→ 新实例通过就绪检查后参与服务
```

配置规则产生新 Revision，运行规则改变 Replica；流量分配又是另一层设置。

**补足第29、39题：**不能从零唤醒时，最直接的选择是保留 `min≥1`，或配置有效的外部事件规则；不必为了后台 Worker 专门开放 HTTP 入口。解释 KEDA 时要说出“队列等外部指标在零副本时仍存在”，不能只解释 CPU 为什么不行。通用 KEDA 模型中，KEDA 负责激活/缩零，HPA 主要处理运行后的 1↔N；在 ACA 中由平台托管。

### 2.2 比较 min=0 与 min=1

1. 使用相同镜像、资源、接口和 HTTP 阈值，只改变 min；确认流量指向本轮测试版本。
2. min=0 时停止业务访问，等待并确认副本真的为零。不要持续访问接口，否则会干扰缩零。
3. 发起一次请求，记录状态码、总耗时和副本变化；紧接着测热请求。
4. min=1 时等实例稳定就绪，在相同条件下重复测试。
5. 对自己的测试接口施加持续并发负载，观察扩容；停止后观察缩容。重复几轮，不只记单次结果。
6. Gateway 调用下游时，固定下游状态，或单独记录其冷启动影响。

| 测试条件 | 请求前副本数 | 首请求耗时/状态码 | 热请求耗时 | 负载时峰值副本数 |
| --- | --- | --- | --- | --- |
| min=0 | 待实测 | 待实测 | 待实测 | 待实测 |
| min=1 | 待实测 | 待实测 | 待实测 | 待实测 |

这里不填写虚构的实验结果。可在 Portal 的 Metrics 中按时间、Revision 观察副本数、请求、CPU/内存与延迟；瞬时 CLI 结果可能错过短暂峰值。

### 2.3 生产选择怎么写

> 若 Gateway 面向用户且要求稳定低延迟，我会从 min=1 开始评估；需要更强可用性时再评估多个副本。低频且可接受等待的服务可选择 min=0。最终用实测延迟、可用性要求和成本决定，并为 max 设置经过压测的上限。

这是一份选择依据，不是“所有生产服务都必须 min=1”。扩容也不能无限补救数据库瓶颈或应用 Bug。

## 3. 必须掌握的命令

> PowerShell 单行写法。中文名称是占位符，执行前替换；`-n` 为应用名称，`-g` 为资源组。以下仅提供命令笔记，不代表已执行云端修改。

### 3.1 配置 HTTP 扩缩容

```powershell
az containerapp update -n Gateway应用名称 -g 资源组名称 --min-replicas 0 --max-replicas 最大副本数 --scale-rule-name HTTP规则名称 --scale-rule-type http --scale-rule-http-concurrency 并发阈值
```

作用：允许 Gateway 根据 HTTP 压力扩缩容。

关键参数：min/max 控制范围；`--scale-rule-http-concurrency` 设置扩容阈值，不是限流值。应用需有可用 HTTP ingress。修改后检查实际规则，避免重复添加规则干扰实验。

**第33题纠正：**题目要求 min=0、max=5、concurrency=10，你写成了5，并使用 create。已有应用应写 update，例如：

```powershell
az containerapp update -n 容器应用名称 -g 资源组名称 --min-replicas 0 --max-replicas 5 --scale-rule-name HTTP规则名称 --scale-rule-type http --scale-rule-http-concurrency 10
```

显式写 `--scale-rule-type http` 更清楚；不要把“未显式写 type”单独认定为必然语法错误，CLI 也提供省略它的 HTTP 示例。

### 3.2 切换常驻实例对照组

```powershell
az containerapp update -n Gateway应用名称 -g 资源组名称 --min-replicas 1
```

作用：保留至少一份实例，比较空闲后请求延迟。

关键参数：改回 `0` 可恢复缩零对照组；每次修改都会创建新 Revision，应等待就绪并确认访问版本。

### 3.3 查看规则、Revision 与实际副本

```powershell
az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.template.scale -o yaml
az containerapp revision list -n 容器应用名称 -g 资源组名称 --all -o table
az containerapp revision show -n 容器应用名称 -g 资源组名称 --revision 修订版完整名称 --query properties.template.scale -o yaml
az containerapp replica list -n 容器应用名称 -g 资源组名称 --revision 修订版完整名称 -o table
az containerapp ingress traffic show -n 容器应用名称 -g 资源组名称
```

作用：分别核对应用模板、指定版本规则、运行实例和流量指向。

关键参数：`--revision` 指定实际观察版本；`--all` 包含停用版本。不要只看最新模板，就认为旧的活跃版本也采用了新规则。

### 3.4 测量首请求与热请求

```powershell
curl.exe -sS -o NUL -w "status=%{http_code} total=%{time_total}s\n" "https://应用域名/测试接口路径"
```

作用：输出状态码和完整请求耗时。确认缩零后执行一次，再重复测热请求。

关键参数：`-w` 输出测量结果；`-o NUL` 不显示正文。`time_total` 包含网络、TLS 和服务处理，不是纯应用启动时间。这里是 Windows 写法，不用 PowerShell 的 `curl` 别名替代。

### 3.5 查看启动及扩缩容相关日志

```powershell
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type system --tail 100 --follow
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type console --revision 修订版完整名称 --tail 100 --follow
```

作用：system 看平台事件，console 看应用初始化和业务日志。

关键参数：`--follow` 持续跟随，按 `Ctrl+C` 停止；没有运行副本时，不应期待读到实时容器输出。日志应与副本数和指标一起判断。

### 3.6 用 YAML 管理多条规则

```powershell
az containerapp show -n 容器应用名称 -g 资源组名称 -o yaml
az containerapp update -n 容器应用名称 -g 资源组名称 --yaml 配置文件路径
```

作用：查看现有配置，整理声明式文件后更新。多规则时统一检查 `properties.template.scale.rules`，比反复追加更容易核对。

关键参数：`--yaml` 指向整理好的配置文件；不要把只读状态字段或真实凭据直接提交仓库。这里只查看输出，不自动导出文件。

命令参考：[Container App CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp)、[Revision CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/revision)、[Replica CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/replica)。

## 4. 我的薄弱知识点总结

根据实际答案，优先复习：

- **第9题：**延迟测试接口配合并发请求，为什么能形成持续压力。
- **第10、23、34题：**15秒指标窗口、30秒轮询、300秒冷却的用途；阈值不是即时开关。
- **第13～16题：**冷启动的正确顺序、Warm 为什么快，以及 min 的成本/延迟取舍。
- **第29、39题：**保留 min≥1 与配置外部唤醒规则这两条路线；补全 KEDA 从零恢复的正面解释。
- **第33题：**按题意填写 concurrency=10，区分修改现有应用与创建应用。
- **第35、37题：**副本存在、min=1，都不能保证业务必然成功或始终快速。
- **第41题：**未见独立完成架构题，建议不看笔记复述一次完整链路。

min/max 边界、CPU/内存不能单独唤醒、多个规则 OR、修改规则产生 Revision 等已答对，不重复归为错题。

## 5. 最终复习清单

- [ ] 我能为 Gateway 配置 HTTP 规则及合理的副本上下限。
- [ ] 我能解释从零唤醒由谁触发，而不是认为停止的程序自己唤醒。
- [ ] 我能区分水平扩容、资源配置和版本发布。
- [ ] 我能说明为何停止压测后不一定立即缩零。
- [ ] 我能查看指定 Revision 的规则、副本和日志。
- [ ] 我能记录 min=0/1 的真实数据，并说明生产选择的理由。

补充学习入口：[KEDA concepts](https://keda.sh/docs/latest/concepts/)、[Microsoft Learn 扩缩容练习](https://learn.microsoft.com/en-us/training/modules/scale-containers-azure-container-apps/)。
