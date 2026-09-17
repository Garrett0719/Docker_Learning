# 第二十二天：Azure Container Apps 架构与选型

> 已根据共享会话中第 22 天的 6 道验收题补充第 11 节「错题与薄弱点」，保留原有知识与命令，不记录其他天的错题和过程性问题。中文参数是占位符，使用前替换；文中的数量只是示例，不是实际部署结果。

## 1. ACA 替你管理什么？

ACA 用来托管容器化的 API、后台服务和任务，提供入口、版本管理、健康检查及扩缩容等能力。你主要管理应用，不需要自己维护底层服务器。[ACA 总览](https://learn.microsoft.com/en-us/azure/container-apps/overview)

**基于 Kubernetes，不等于向你提供一个 Kubernetes 集群。**ACA 不开放底层 Kubernetes API；使用 Azure CLI、Portal 或资源配置管理它，而不是用 `kubectl` 创建底层 Pod。

平台负责基础设施，不代表你不用负责镜像、配置、权限、数据、健康检查和费用。单体 C# API 也能部署到 ACA，不必先拆成微服务。[平台选型边界](https://learn.microsoft.com/en-us/azure/container-apps/compare-options)

## 2. 六个概念，一次分清

| 概念 | 通俗解释 | 不要混淆 |
| --- | --- | --- |
| Environment | 一组 App 和 Job 的共同运行边界 | 不是单个应用，也不是 C# 的 Production/Development 变量 |
| Container App | 一个长期提供服务的应用资源，例如订单 API | 不是某一个正在运行的容器 |
| Revision | 应用某一版的不可变版本配置快照 | 不是副本，也不只是镜像 tag |
| Replica | 某个 revision 的运行副本，可以包含多个容器 | 同一版本可以运行多份 |
| Job | 执行有限时长、完成后退出的任务定义 | 不靠一直运行来提供 HTTP 服务 |
| Workload profile | 环境中的计算资源配置，决定可用资源类型和容量 | 不是应用版本，也不是副本数量 |

记法：**Environment 放应用，App 管版本，Revision 跑副本；Job 按次执行，profile 提供计算资源。**

## 3. 实践关系图：一个环境、两个应用

```text
Environment：生产环境
│
├─ Workload profiles：Consumption、后台专用池（Dedicated）
│   └─ App / Job 选择使用哪个 profile，不是每个 App 独占一个池
│
├─ App A：订单 API（使用 Consumption）
│   ├─ Revision v1：接收 80% HTTP 流量
│   │   ├─ Replica 1
│   │   └─ Replica 2
│   └─ Revision v2：接收 20% HTTP 流量
│       └─ Replica 1
│
├─ App B：库存 API（使用 Consumption）
│   └─ Revision v1
│       ├─ Replica 1
│       └─ Replica 2
│
└─ Job：夜间报表（使用后台专用池）
    └─ Execution：某一次执行
        └─ Job replica：执行本次任务，完成后退出
```

图中有 **2 个 App、3 个 App revision、5 个 App replica**，Job 的执行副本另算。App A 的灰度发布示例要求使用多修订版本模式并配置流量分配；80%/20% 不由副本数量自动决定。

profile 是资源选择，不是把 App 改成 Job 的开关。Job 与 App 同属 Environment，Job 的执行记录也不是 App revision。[Revision](https://learn.microsoft.com/en-us/azure/container-apps/revisions)、[Job](https://learn.microsoft.com/en-us/azure/container-apps/jobs)

## 4. Environment：安全、网络与日志边界

- 相关应用可以放在同一环境，共享网络基础和日志目标，方便内部调用与观察。
- 开发、测试、生产需要隔离时，应考虑分别使用环境，而不是只改 App 名字。
- “同一环境”不代表共享一个容器、共享内存或自动获得对方的 Secret、数据库权限；业务鉴权仍需配置。
- 日志目标可在环境层配置，常见为共同的 Log Analytics 工作区；不等于日志永久保存，也不意味着谁都能读取。

Environment 也不等于 Azure 资源组：资源组用于组织和管理资源，不决定这些应用是否共享 ACA 运行边界。[Environment](https://learn.microsoft.com/en-us/azure/container-apps/environment)

应用是否对外开放还要看 ingress。Environment 本身的网络边界与 App 的 internal/external 设置共同决定可达范围；不能只看到 external 就认定公网可访问。[Ingress](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)

## 5. Revision 与 Replica：版本和数量是两回事

- 更新镜像、容器环境变量或缩放规则等版本级配置，会创建新 revision。
- 增减同一 revision 的运行副本，不等于发布新版本。
- ingress 流量配置、Secret 值等应用级变更，不会创建新 revision；Secret 更新后还要让使用它的容器重新读取，不能假定立即生效。
- 某个 revision 可以暂时没有运行副本；“版本记录存在”和“现在有容器运行”不同。

| 模式 | 本日需要知道的行为 |
| --- | --- |
| Single | 通常由平台在新版就绪后切换，旧版再停用；发布期间可以短暂共存 |
| Multiple | 允许多个版本保持活动，自行安排流量，适合灰度或蓝绿发布 |

revision 的版本配置不可原地修改，但其活动状态、流量和副本数量可以变化。[版本与变更范围](https://learn.microsoft.com/en-us/azure/container-apps/revisions#change-types)

## 6. App 与 Job 怎么选？

| 需求 | 选择 |
| --- | --- |
| C# API，持续响应请求 | App |
| Worker Service，持续循环消费队列 | App，可按积压扩缩容 |
| 每晚生成报表，完成后退出 | Schedule Job |
| 手动执行一次数据处理 | Manual Job |
| 来一批事件，启动处理，做完退出 | Event Job |

Job 是任务定义，Execution 是一次执行，执行中可以有一个或多个 Job replica。**Job 不等于每次只能跑一个容器，也不等于只能手动触发。**[App 与 Job 比较](https://learn.microsoft.com/en-us/azure/container-apps/jobs#compare-container-apps-and-jobs)

## 7. Workload profile：选资源，不是选副本数

| 类型 | 简单理解 | 本日选型重点 |
| --- | --- | --- |
| Consumption | 按需提供资源，适合波动负载 | 常见 API、事件处理的起点；符合条件时可缩零 |
| Dedicated | 使用专用计算池，可由多个应用共享池容量 | 持续负载、较大资源或特定硬件需求 |
| Flex / Flexible（预览） | 单租户计算池与按副本计费等能力结合 | 知道它存在即可；当前不支持应用缩零 |

当前默认的 workload profiles 环境支持选择多种资源配置，旧的 Consumption-only 环境能力不同。

要分清三件事：

- **profile 类型／规格**：底层提供什么资源。
- **应用的 CPU／内存配置**：一个副本要用多少资源。
- **replica 数量**：运行多少份应用；Dedicated 池的实例数又是另一层容量。

例如 API 扩为三个副本，不代表必然增加三台专用实例。缩零也不代表 ACR、日志或专用池等费用同时归零。具体 profile、区域、配额和计费要在部署时核对，不必背完整 SKU 表。[Workload profiles](https://learn.microsoft.com/en-us/azure/container-apps/workload-profiles-overview)

## 8. 用第 21 天的场景重新判断

| 场景 | 本日判断 |
| --- | --- |
| 小团队无状态 C# API，流量波动大 | ACA App，先评估 Consumption，不需要维护 Kubernetes 集群 |
| 持续队列消费者 | ACA App；如果是处理一批即退出，改评估 Event Job |
| 夜间报表 | ACA Schedule Job，不必让 Web 服务常驻等待时间到来 |
| 必须安装 CRD／Operator | 更适合 AKS，需要 Kubernetes API |
| 必须按节点部署自定义 DaemonSet | 更适合评估 AKS Standard，需要节点级能力 |
| 必须自行组合集群网络、存储和调度组件 | 更适合 AKS，并承担相应运维工作 |

补充判断：**只是需要更多 CPU、内存或支持的 GPU，不应直接推导为“必须 AKS”**，先看 ACA profile 是否满足；只有确实需要底层控制时，再转向 AKS。以上为根据官方能力归纳的选型示例。[Azure 容器选型](https://learn.microsoft.com/en-us/azure/container-apps/compare-options)

## 9. 必须会查的命令

本日是架构验收，不必创建付费资源。下面用于查询已有资源：`-n` 是名字，`-g` 是资源组，`-o table` 用表格显示，`--query` 选择返回字段。

### 查看 Environment、App、Revision、Replica

```powershell
az account show -o table
az containerapp env show -n 环境名字 -g 资源组名字
az containerapp show -n 容器应用名字 -g 资源组名字
az containerapp revision list -n 容器应用名字 -g 资源组名字 -o table
az containerapp replica list -n 容器应用名字 -g 资源组名字 --revision 修订版本名字 -o table
```

### 查看 profile 与资源分配

```powershell
az containerapp env workload-profile list -n 环境名字 -g 资源组名字 -o table
az containerapp env workload-profile list-supported -l 区域名字 -o table
az containerapp show -n 容器应用名字 -g 资源组名字 --query properties.workloadProfileName -o tsv
az containerapp show -n 容器应用名字 -g 资源组名字 --query properties.template.containers -o json
```

第一条看“环境已经配置什么”，第二条看“区域支持什么”，不是同一个清单。[profile CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/env/workload-profile)

### 查看 Job 定义与执行记录

```powershell
az containerapp job show -n 任务名字 -g 资源组名字
az containerapp job execution list -n 任务名字 -g 资源组名字 -o table
az containerapp job execution show -n 任务名字 -g 资源组名字 --job-execution-name 执行记录名字
```

`任务名字` 标识任务定义，`执行记录名字` 标识其中一次运行。[Job execution CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/job/execution)

### 查看日志

```powershell
az containerapp logs show -n 容器应用名字 -g 资源组名字 --type console --tail 50
az containerapp logs show -n 容器应用名字 -g 资源组名字 --type system --tail 50
```

console 看程序输出，system 看平台事件。这里使用 `az containerapp`，不使用 `kubectl` 管理 ACA 底层对象。[ACA 日志](https://learn.microsoft.com/en-us/azure/container-apps/log-streaming)

## 10. 验收时能做到这些就够了

- [ ] 不看笔记画出一个 Environment、两个 App、多个 revision 和 replica。
- [ ] 在图上说明 Job → Execution → Job replica，并标出使用的 profile。
- [ ] 解释“发布新版”和“增加副本”为什么不同。
- [ ] 解释 Environment 的边界，不把它当作业务鉴权或 Secret 自动共享。
- [ ] 根据是否需要 Kubernetes API／节点级控制，重新判断 ACA 或 AKS。

视频复习入口：[微软 .NET on ACA 第 1 集](https://learn.microsoft.com/en-us/shows/azure-developers/introduction-to-azure-container-apps-for-dotnet-developers-getting-started-with-dotnet-on-aca)。页面章节列出 `02:25` 平台选项、`09:04` ACA 总览；本篇技术细节以当前官方文档为准，不把视频简介当作完整字幕。

## 11. 第 22 天问答：错题与薄弱点

依据：[共享会话中的第 22 天验收题](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)。只记录实际答错或表达不完整的部分，不把扣分一律当作知识错误。

### ① Workload profile 不是负载均衡器（第 4 题，重点纠错）

原回答：控制负载均衡，把空闲资源分给需求大的应用，可以一个 20%、一个 80%。

**修正：profile 解决副本跑在什么计算资源上，不负责给两个应用按百分比分流。**你回答「解决 Replica 跑在什么计算资源上」是对的，但后面的解释与它矛盾。

| 要决定什么 | 看什么配置 |
| --- | --- |
| 跑哪个版本 | Revision |
| 这个版本运行几份 | Replica 数量／扩缩容规则 |
| 每个副本需要多少 CPU、内存 | 容器资源配置 |
| 副本使用哪类计算资源 | Workload profile |
| 同一个 App 的 v1、v2 各接多少 HTTP 请求 | Revision 流量权重，例如 80%／20% |

例如普通 API 可以选择 Consumption；持续图片处理服务可以评估 Dedicated。**同一 Environment 内，不同 App 不必使用同一 profile**，也不是资源需求一高就必须换 profile，先看现有配置是否满足。[Workload profiles](https://learn.microsoft.com/en-us/azure/container-apps/workload-profiles-overview)

第 6 题已正确回答「两个应用不必使用相同 profile」，保留这里用于巩固原因。

### ② 新版出现，不代表旧版一定被关闭（第 1、2 题，缺少条件）

原回答：v1、v2 共存是因为滚动更新；随着 v2 创建，v1 的副本会逐渐关闭删除。

- **Single 模式**：新版就绪后切换流量，再停用旧版；新版失败时，旧版继续服务。
- **Multiple 模式**：新旧版可以持续保持活动，按配置分流；不是创建 v2 就自动关掉 v1。
- **保留旧 Revision 记录 ≠ 保留旧版运行副本**。版本记录留下来用于恢复，不代表旧容器一直在运行。

所以回答旧版会怎样时，先说明修订版本模式，不能只套用 Kubernetes 滚动更新的印象。[Revision 模式](https://learn.microsoft.com/en-us/azure/container-apps/revisions#revision-modes)

你已经答对「自动从 3 个副本扩到 5 个，不创建新 Revision」。注意区分：**按现有规则自动扩缩容**只改变运行数量；**修改缩放规则配置**属于版本级变更，会创建新 Revision。

### ③ Environment 不是资源组里所有 App 的边界（第 1 题）

原回答：Environment 是这个资源组里面所有 Container App 的资源边界。

修正：它只包含**部署到该 Environment 的 App / Job**。一个资源组里可以有多个 Environment，也可以有 ACR 等其他资源。

记忆：**资源组负责组织资源，Environment 提供 ACA 共同运行边界；在同一个资源组，不等于在同一个 Environment。**[Environment](https://learn.microsoft.com/en-us/azure/container-apps/environment)

### ④ App 管应用版本，Revision 不只是镜像版本（第 1 题，定义不完整）

原回答：Container App 管理一个或多个 Container；Revision 相当于容器版本。

更准确的关系是：

```text
Container App：应用资源
└─ Revision：某一版运行配置的快照
   └─ Replica：这一版的运行实例
      └─ Container：实例中的一个或多个容器
```

Revision 不只记录镜像，还包括环境变量、CPU／内存和缩放规则等版本级配置。**镜像没变，修改这些配置也可能创建新 Revision。**它也不是整个 App 所有设置的快照，应用级设置要另看变更范围。[Revision 变更范围](https://learn.microsoft.com/en-us/azure/container-apps/revisions#change-types)

你把 Replica 类比 Pod 的方向正确，不列为错题，但两者不是完全相同的资源。

### ⑤ 场景选对了，ACA 资源名要改过来（第 3 题）

你正确区分了「持续运行」和「做完退出」，但把 API、持续 Worker 叫作 `Deployment`。

- ACA 中：API、持续消费队列的 Worker → **Container App**。
- 定时清理、事件触发后处理一批就退出 → **Container Apps Job**。
- `Deployment` 是 Kubernetes 的资源名，不是这里应创建的 ACA 资源。

特别记住：**使用消息队列并不自动等于 Job，要看程序是持续消费，还是每次完成后退出。**[App 与 Job 的区别](https://learn.microsoft.com/en-us/azure/container-apps/jobs#compare-container-apps-and-jobs)

### ⑥ 用已有命令核对，不必另建实验资源

```powershell
# 查看当前修订版本模式
az containerapp show -n 容器应用名字 -g 资源组名字 --query properties.configuration.activeRevisionsMode -o tsv

# 分别查看版本记录和某一版的运行副本
az containerapp revision list -n 容器应用名字 -g 资源组名字 -o table
az containerapp replica list -n 容器应用名字 -g 资源组名字 --revision 修订版本名字 -o table

# 查看环境可用的 profile，以及应用选择的 profile
az containerapp env workload-profile list -n 环境名字 -g 资源组名字 -o table
az containerapp show -n 容器应用名字 -g 资源组名字 --query properties.workloadProfileName -o tsv
```

第 5 题的「ACA 不开放底层 Kubernetes API」以及第 6 题的综合选型已答对，不额外列为错题。
