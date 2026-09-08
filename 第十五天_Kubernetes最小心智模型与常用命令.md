# 第十五天：为什么需要编排，以及 Kubernetes 最小心智模型

> 范围：第十五天核心知识、相关实践疑问、验收命令，以及两轮问答验收的错题与知识点（见第 8 节）。不收录其他学习日的错题。以下命令供你练习，本文不代表已在本机执行验收。

## 1. 为什么需要编排

把 API 启动起来之后，还要有人持续处理：实例崩溃、机器故障、增加副本、更新版本、把请求送到可用实例。

Kubernetes 的方式是：**你声明想要什么，它持续检查实际情况并尝试补齐差距。**

| 概念 | 通俗理解 | 例子 |
| --- | --- | --- |
| 期望状态 | 你规定的目标 | API 使用 v2 镜像，运行 3 个副本 |
| 控制循环 | 反复比较目标与现状，再采取动作 | 实际只剩 2 个，控制器补建一个 Pod |
| 自愈 | 自动恢复部分运行故障 | 容器退出后按策略重启；受管理的 Pod 丢失后补建 |
| 调度 | 为待运行的 Pod 选一台合适的 Node | 根据资源请求和放置条件选择节点 |

目标写成 3，不代表马上就能跑起 3 个：资源不足、镜像拉取失败、配置错误都可能阻止目标实现。自愈也不会修复代码 bug 或自动找回丢失的数据。[控制循环](https://kubernetes.io/docs/concepts/architecture/controller/)、[调度器](https://kubernetes.io/docs/concepts/scheduling-eviction/kube-scheduler/)

## 2. 先记住这些对象各管什么

| 对象 | 负责什么 |
| --- | --- |
| Cluster（集群） | 一套 Kubernetes 管理环境，包含控制平面和节点 |
| Node（节点） | 运行 Pod 的机器，可以是物理机或虚拟机；本地实验也可用容器模拟节点 |
| Container（容器） | 实际运行 API 等程序的地方 |
| Pod | Kubernetes 最小部署、调度单位，装一个或多个紧密配合的容器 |
| Deployment | 管理应用副本和版本更新，常用于无状态 Web API |
| ReplicaSet | 维持指定数量的 Pod；通常由 Deployment 管理 |
| Service | 为一组 Pod 提供稳定的访问入口，通过标签选择后端 |
| kubectl | 操作集群的命令行客户端，通过 API Server 提交或查询对象 |

```text
管理关系：Deployment → ReplicaSet → 多个 Pod → 各自的容器
放置关系：每个 Pod 被调度到一台 Node 上
访问关系：客户端 → Service → 符合标签且就绪的后端 Pod
```

Service 不负责创建 Pod，也不是“包住 Deployment 的外壳”。它通过 `selector` 匹配 Pod 的 `labels`；名字相同并不能自动建立关系。

同一 Pod 内的容器共享网络，可通过 `localhost` 通信，也可共同挂载卷；不同 Pod 通常通过 Service 名称通信。API 和数据库只是存在依赖，一般仍应放在不同 Pod，方便分别扩容与更新。[工作负载](https://kubernetes.io/docs/concepts/workloads/)、[Pod 与 Node](https://kubernetes.io/docs/tutorials/kubernetes-basics/explore/explore-intro/)、[Service](https://kubernetes.io/docs/concepts/services-networking/service/)

其他工作负载今天只需认识用途：`Job` 跑完就结束，`CronJob` 定时执行，`StatefulSet` 适合需要稳定身份与存储关联的应用，`DaemonSet` 在符合条件的各节点上运行副本。

## 3. 自愈、扩容、更新：发生了什么

| 操作或故障 | Kubernetes 的行为 |
| --- | --- |
| 容器进程崩溃 | kubelet 按重启策略在原 Pod 中重启容器 |
| 删除 Deployment 管理的一个 Pod | ReplicaSet 发现副本不足，创建新 Pod；不是把旧 Pod 复活 |
| 删除一个没有控制器管理的裸 Pod | 不会仅因为它被删除就自动补建 |
| 节点故障 | 相关控制器在故障检测、处理后补建 Pod；有其他合适节点时才能调度过去 |
| 把 replicas 从 1 改为 4 | 增加 Pod 数量；不等于增加节点，也不等于给原容器加 CPU |
| 修改 Deployment 的容器镜像 | 通常创建新 ReplicaSet，逐步用新 Pod 替换旧 Pod |

Pod 不会原封不动“搬家”到另一台 Node；替代它的是新 Pod，IP 可能变化，所以客户端应使用 Service 的稳定名称。

调度器负责选节点，节点上的 kubelet 协同容器运行时启动容器。单节点实验可以验证补建，但不能证明机器坏掉后还能跨节点恢复。

滚动更新能减少中断，但是否平滑还取决于副本、可用资源、就绪检查和退出行为；也不保证发布失败就自动回滚。[创建 Deployment](https://kubernetes.io/docs/tutorials/kubernetes-basics/deploy-app/deploy-intro/)、[扩容](https://kubernetes.io/docs/tutorials/kubernetes-basics/scale/scale-intro/)、[更新](https://kubernetes.io/docs/tutorials/kubernetes-basics/update/update-intro/)

## 4. Compose 与 Kubernetes 的边界

| 需要解决的问题 | Compose | Kubernetes |
| --- | --- | --- |
| 描述 API、数据库、网络、卷 | 可以，适合本地和单机部署 | 可以，用多种资源对象描述 |
| 单机容器退出后重启 | 可配合 Docker 重启策略 | 由 kubelet 等组件按策略处理 |
| 跨节点放置应用、节点故障后补建 | 普通 `docker compose up` 不提供这套集群能力 | 支持，前提是集群有可用资源 |
| 持续维持副本、滚动更新 | 主要依靠命令和部署流程管理 | 由控制器持续管理 |

Compose 也能用于生产中的单机应用；需要多节点调度、故障恢复与持续管理时，编排平台更有价值。[Compose 应用模型](https://docs.docker.com/compose/intro/compose-application-model/)

## 5. 现有会话中，与今天相关的实践疑问

### “程序已经对外暴露了吗？”

要看你用了哪种入口，创建 Deployment 本身不会提供外部访问地址。

| 方式 | 访问范围或用途 |
| --- | --- |
| `containerPort: 8080` | 描述容器端口；不会创建宿主机映射或让程序自动监听 |
| `ClusterIP` Service | 默认类型，主要供集群内部使用 |
| `NodePort` Service | 通过可达的节点 IP 和 NodePort 访问；不自动等于公网开放 |
| `LoadBalancer` Service | 需要云平台或本地负载均衡实现提供入口 |
| `kubectl proxy` | 默认开启本机到 Kubernetes API 的代理，属于本地访问工具 |
| `kubectl port-forward` | 临时把本机端口转发到选中的 Pod，适合调试 |

Windows + minikube Docker 驱动下，宿主机不能直接访问节点 IP 时，可用 `minikube service 名称 --url` 访问 NodePort。若命令提示保持终端运行，就保留该窗口。[暴露服务教程](https://kubernetes.io/docs/tutorials/kubernetes-basics/expose/expose-intro/)、[minikube Docker 驱动](https://minikube.sigs.k8s.io/docs/drivers/docker/)

### “已经有 4 个 Pod，为什么 curl 总是同一个？”

不能只凭刷新结果判定扩容失败，也不能直接断言是 tunnel 导致。

1. 看 Service 的 selector 是否选中这些 Pod，以及 EndpointSlice 中是否有对应的就绪后端。
2. 看是否设置了 `sessionAffinity: ClientIP`，它会让同一客户端倾向固定后端；默认是 `None`。
3. 确认访问路径：`port-forward service/...` 最终选中一个 Pod，不适合验证 Service 的多后端分发。
4. 从集群内用多个新连接请求 Service，观察返回的 Pod 名称。

Service 通常在连接层面选择后端，不保证每个 HTTP 请求按 A→B→C→D 轮询。多个不同 Pod 的响应能证明发生了分发；少量请求没有覆盖所有副本，不能直接证明异常。[Service](https://kubernetes.io/docs/concepts/services-networking/service/)、[端口转发](https://kubernetes.io/docs/tasks/access-application-cluster/port-forward-access-application-cluster/)

### “教程里的 export 在 PowerShell 中不能用”

`export` 是 Bash 等 shell 的写法。PowerShell 普通变量用 `$变量名 = 值`；需要传给子进程的环境变量用 `$env:变量名 = 值`。下面的实验统一使用 PowerShell，避免混用 Bash 循环与多层引号。

## 6. 命令实践：创建 → 查看 → 暴露 → 扩容 → 更新

示例延续会话和官方教程的 `kubernetes-bootcamp` 应用，监听 8080。若同名资源已存在，跳过创建，先查看现状。旧教程镜像仅供学习；若出现 `ImagePullBackOff`，先查镜像地址、架构和网络。

### 6.1 启动并确认集群

本地方案二选一。已有 minikube 就继续使用；Docker Desktop 则在 Kubernetes 页面启用或创建集群。

```powershell
# minikube 方案：Docker 已运行，相关命令已安装
minikube start --driver=docker
minikube status

# 检查 kubectl 及它当前连接的集群
kubectl version --client
kubectl config get-contexts
kubectl config current-context

# 二选一：切到实际使用的环境
kubectl config use-context minikube
# kubectl config use-context docker-desktop

kubectl cluster-info
kubectl get nodes
```

看到目标集群的 Node 为 `Ready`。`kubectl` 是客户端，安装成功不代表集群已启动；没有独立 kubectl 时，minikube 可用 `minikube kubectl -- get nodes`。

后续命令使用当前 context 的 namespace（通常为 `default`）。找不到资源时，先检查 context，再用 `kubectl get pods -A` 查看所有 namespace。[Docker Desktop 集群](https://docs.docker.com/desktop/use-desktop/kubernetes/)、[kubectl 速查](https://kubernetes.io/docs/reference/kubectl/quick-reference/)

### 6.2 创建、查看与排查

```powershell
kubectl create deployment kubernetes-bootcamp --image=gcr.io/google-samples/kubernetes-bootcamp:v1
kubectl rollout status deployment/kubernetes-bootcamp --timeout=120s
kubectl get deployment,rs,pods
kubectl get pods -l app=kubernetes-bootcamp -o wide

# 保存其中一个 Pod 的名称，供后续命令使用
$podName = kubectl get pods -l app=kubernetes-bootcamp -o jsonpath='{.items[0].metadata.name}'
kubectl describe pod $podName
kubectl logs $podName --tail=50
kubectl exec $podName -- printenv
```

`get` 看概况；`describe` 看配置和事件；`logs` 看程序输出；`exec` 在容器内执行命令。Pod 有多个容器时，用 `-c 容器名` 指定目标，且目标镜像必须有要执行的命令。

`-o wide` 多显示 IP、节点等信息；`-l` 按标签筛选；`jsonpath` 提取指定字段。上面的 `.items[0]` 是取列表第一个元素。

验收：Deployment 显示 `READY 1/1`；Pod 通常显示 `1/1 Running`。注意前者是“就绪副本/期望副本”，后者的 `1/1` 是“就绪容器/容器数”。

### 6.3 暴露服务与本地访问

```powershell
kubectl expose deployment kubernetes-bootcamp --type=NodePort --port=8080 --target-port=8080
kubectl get service kubernetes-bootcamp
kubectl describe service kubernetes-bootcamp

# minikube：获取本地访问地址，必要时保持此窗口运行
minikube service kubernetes-bootcamp --url
```

在另一终端用 `curl.exe` 请求上一步实际输出的 URL。`--port` 是 Service 端口，`--target-port` 是后端应用端口；NodePort 默认另行分配，查看 `PORT(S)` 中冒号后的数值。

通用的本地调试方式，Docker Desktop 和 minikube 都可以用：

```powershell
kubectl port-forward service/kubernetes-bootcamp 18080:8080
# 另一终端执行
curl.exe http://127.0.0.1:18080
```

这里 `18080` 是本机端口，`8080` 是 Service 端口。关闭转发进程后入口失效；它不是永久发布，也不是负载均衡验收方式。

### 6.4 扩容并验证 Service 分发

```powershell
kubectl scale deployment/kubernetes-bootcamp --replicas=4
kubectl rollout status deployment/kubernetes-bootcamp --timeout=120s
kubectl get deployment kubernetes-bootcamp
kubectl get pods -l app=kubernetes-bootcamp -o wide
kubectl get endpointslices -l kubernetes.io/service-name=kubernetes-bootcamp -o wide
kubectl get service kubernetes-bootcamp -o jsonpath='{.spec.sessionAffinity}'
```

验收：Deployment 为 `4/4`，有 4 个就绪应用 Pod；EndpointSlice 后端地址与这些 Pod 对应。需要确认后端是否就绪时，追加查看 `-o yaml` 中的 `conditions.ready`。

用临时客户端直接访问集群内 Service，PowerShell 负责循环：

```powershell
kubectl run curl-test --image=curlimages/curl --restart=Never --command -- sleep 3600
kubectl wait --for=condition=Ready pod/curl-test --timeout=120s

1..20 | ForEach-Object {
    kubectl exec curl-test -- curl -sS --max-time 5 http://kubernetes-bootcamp:8080
    Write-Host ""
}
```

每次启动新的 curl 进程，发起新连接。观察响应中的 `Running on`，记录是否出现多个 Pod 名称。`curl-test` 是额外的测试 Pod，不算在 API 的 4 个副本里。

### 6.5 删除一个 Pod，观察自愈

仅在本次学习应用上做此实验：

```powershell
$podName = kubectl get pods -l app=kubernetes-bootcamp -o jsonpath='{.items[0].metadata.name}'
kubectl delete pod $podName
kubectl get pods -l app=kubernetes-bootcamp -w
```

看到新 Pod 出现、最终恢复 4 个就绪副本后，按 `Ctrl+C` 停止观察。`-w` 表示持续观察，停止观察不会删除资源。Pod 名称已改变，之后使用前需重新获取 `$podName`。

### 6.6 更新、观察与回滚

```powershell
kubectl set image deployment/kubernetes-bootcamp kubernetes-bootcamp=docker.io/jocatalin/kubernetes-bootcamp:v2
kubectl rollout status deployment/kubernetes-bootcamp --timeout=120s
kubectl get rs
kubectl get pods -l app=kubernetes-bootcamp
kubectl describe deployment kubernetes-bootcamp
kubectl rollout history deployment/kubernetes-bootcamp

# 需要练习恢复旧版本时执行
kubectl rollout undo deployment/kubernetes-bootcamp
kubectl rollout status deployment/kubernetes-bootcamp --timeout=120s
```

`set image` 中等号左侧是 **Pod 模板里的容器名**，不一定等于 Deployment 名。`rollout status` 等待并报告结果，不负责修复发布；`undo` 回滚 Pod 模板，不回滚数据库数据。

验收：观察新旧 Pod 替换，`describe` 中镜像已更新，API 返回 v2；回滚后再次核对镜像和响应。更新可能让旧的 `port-forward` 断开，重新运行即可。[官方滚动更新练习](https://kubernetes.io/docs/tutorials/kubernetes-basics/update/update-intro/)

### 6.7 声明式配置与常用参数

长期管理应用时，把期望状态写进 YAML，再提交到集群：

```powershell
kubectl diff -f app.yaml
kubectl apply -f app.yaml
kubectl explain deployment.spec.replicas
kubectl get pods -n default
kubectl get events --sort-by=.metadata.creationTimestamp
```

`app.yaml` 代表你自己准备的配置文件；`diff` 先预览差异，`apply` 再提交目标配置。`-f` 在 `apply` 中表示文件，在 `logs -f` 中表示跟随日志；`-n` 指定 namespace，`-A` 查看所有 namespace，`--` 在 `exec` 中分隔 kubectl 参数和容器内命令。

`scale`、`set image` 会修改集群中的对象，但不会替你改本地 YAML。若文件仍声明旧副本数、旧镜像，再次 `apply` 可能把设置改回去，应同步维护文件。

### 6.8 清理学习资源（可选）

确认这些资源仅用于本次实验后执行：

```powershell
kubectl delete pod curl-test
kubectl delete service kubernetes-bootcamp
kubectl delete deployment kubernetes-bootcamp
minikube stop
```

只删除 Service 不会停止 Deployment 的 Pod；删除 Deployment 通常会级联删除它管理的 ReplicaSet 和 Pod。`minikube stop` 停止本地集群、保留其数据，且仅用于 minikube 方案。

## 7. 今日验收记录

| 验收项 | 应保存的结果 |
| --- | --- |
| 集群 | 当前 context、Node 为 Ready |
| 创建和查看 | Deployment 为 1/1；能查看 Pod、事件和日志 |
| 暴露 | Service 类型、端口对应关系、实际访问成功的 URL |
| 扩容 | Deployment 为 4/4；后端地址对应 4 个就绪 Pod |
| 分发 | 集群内多次新连接返回多个 Pod 名称，不要求严格轮询 |
| 自愈 | 删除前后的 Pod 名称与副本恢复结果 |
| 更新和回滚 | 镜像变化、rollout 结果、应用响应版本 |

## 8. 第十五天问答验收：错题与知识点

依据[共享会话中的两轮验收](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)：基础题 **13/15**，场景题 **15/15**。实际错题只有基础题第 5、11 题；场景题知识点用于复习，不记为错题。后面的第十六天验收不纳入本节。

### 8.1 错题一：稳定访问入口不是 Node

**原题要点：** 一组 Pod 会被替换，IP 可能改变，应该用什么提供稳定入口？

- 你的答案：D，Node。
- 正确答案：C，Service。
- 错在把“程序跑在哪里”和“客户端访问谁”混在了一起。

**Node 是运行地点，Service 是访问入口。** 同一个 Node 可以运行 API、Redis 等不同应用；同一个 API 的多个 Pod 也可以分散在不同 Node。节点地址本身不能自动代表某个应用。

你追问的 Node，可以这样记：它提供 CPU、内存等资源；Scheduler 为 Pod 选节点，节点上的 kubelet 调用容器运行时启动容器。增加 Pod 副本不等于增加 Node。[Node 官方说明](https://kubernetes.io/docs/concepts/architecture/nodes/)

例如 API 的一个 Pod 重建后从 Node A 换到 Node B，客户端仍访问 `my-api-service`，不用跟着改 Pod IP。若用 NodePort，访问的是 `NodeIP:NodePort`，背后仍依靠 Service 对应的转发规则找到 Pod，不是 Node 替代了 Service。

### 8.2 错题二：单机多容器不一定需要 Kubernetes

**原题要点：** 只想在一台开发机上一起启动 API、Redis、PostgreSQL，用什么更合适？

- 你的答案：A，Kubernetes Deployment。
- 正确答案：B，Docker Compose。
- 题目强调的是“单台开发机、方便启动”，Compose 已经能满足需求，配置和维护更简单。

不是 Kubernetes 做不到，而是要按需求选：**单机开发联调，通常选 Compose；需要跨节点调度、持续维持副本和滚动更新，再考虑 Kubernetes。** 如果目的就是练习 Kubernetes 或验证其部署配置，本地使用它当然合理。详细边界见第 4 节。

### 8.3 场景题速查：先看证据，再判断问题在哪

下面这些题你都答对了。复习重点不是背状态名，而是知道下一步查什么。

| 现象或证据 | 应该怎样判断、排查 |
| --- | --- |
| `Pending`，事件有 `FailedScheduling`、`Insufficient cpu` | 看节点可分配资源和 Pod 的 `requests`；不是只看 CPU 当前忙不忙 |
| CPU、内存够，但事件说不匹配 node affinity/selector | 调度条件不满足；查 Pod 的节点要求与 Node 标签 |
| `Running`，但 `READY 0/1` | 运行不等于就绪；查 readiness 结果和事件，不能认为已经可以接流量 |
| `CrashLoopBackOff`，上次日志提示连接字符串为空 | 容器反复退出；本题是应用配置错误，查日志和退出原因，不是 Service 故障 |
| 新 Pod 为 `ImagePullBackOff`，更新卡住 | 先查拉取镜像的事件：地址、tag、认证、网络等；不是一看到发布卡住就改 Service |
| Pod 已就绪，但 Service 没有后端 | 优先核对同一 namespace 下的 Service selector 与 Pod labels；再查后端就绪状态 |
| 已找到正确后端，访问仍失败 | 继续查 `targetPort`、应用实际监听端口及地址；有后端不保证请求一定成功 |
| 重建 Pod 后，写死旧 Pod IP 的调用失败 | 改用 Service 名；不要把可替换实例的地址当固定入口 |
| Deployment 期望 3 个，按标签却查到 5 个 Pod | 标签筛选结果不等于归属关系；查 ReplicaSet、`Controlled By` / `ownerReferences`，也检查是否正在更新 |
| 3 个 Pod 没有均匀分到 3 个 Node | 不一定是故障；调度不保证平均分配，明确需要分散时才配置相应放置规则 |

这些是排查方向，不是仅凭一个状态就能确定的唯一原因。例如 `CrashLoopBackOff` 也可能与探针或资源限制有关。[Pod 状态与故障](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/)、[Service 排查](https://kubernetes.io/docs/tasks/debug/debug-application/debug-service/)

**两处题目表述需要留意（不算你的错题）：**

- 场景题第 6 题已在会话中纠正：数值型 `targetPort: 5000` 对应的自动生成后端端口应是 `5000`，不是题目最初写的 `8080`。若 API 实际监听 `8080`，应把 `targetPort` 改为 `8080`。
- 场景题第 8 题不能扩写成“手动创建的 Pod 永远不归 ReplicaSet 管”。没有其他控制器管理、且匹配 ReplicaSet **完整 selector** 的 Pod，可能被它接管。仅有相同的 `app` 标签，不一定匹配完整 selector；最终要查归属字段。[ReplicaSet 接管规则](https://kubernetes.io/docs/concepts/workloads/controllers/replicaset/#non-template-pod-acquisitions)

### 8.4 更新与回滚：补齐三个容易漏掉的判断

1. **同名 tag 的镜像内容变了，不等于自动发布。** 场景题中重新推送 `my-api:v1`，但 `.spec.template` 没变，所以再次 `apply` 显示 `unchanged`，旧 Pod 不会自动替换。可使用新的版本 tag 或 digest，并更新模板里的镜像引用。
2. **旧 ReplicaSet 为 `0/0/0`，通常不是异常。** 它可能保留着旧的 Pod 模板，供回滚使用；并不是旧 Pod 还在后台运行。
3. **回滚是恢复旧模板并替换 Pod，不是在现有容器里改文件。** 对应旧版本的 ReplicaSet 再扩容，新版本的缩容；前提是所需历史仍保留。滚动更新卡住也不代表 Kubernetes 会自动替你回滚。[Deployment 更新与回滚](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)

结合基础题再记一句：**控制器补数量，Scheduler 选节点，kubelet 管节点上的容器运行，Service 提供稳定入口。** 节点故障后的恢复需要时间和可用资源，不是原 Pod 瞬间搬家。

### 8.5 本轮新增的排查命令

创建、扩容、更新命令见第 6 节，这里只补常用排查项。`my-api-service`、`my-api` 和变量值需换成自己的名称；不同 namespace 时追加 `-n 名称`。

```powershell
# 先从列表取得真实名称，再填写；不要直接照抄示例名称
kubectl get pods -o wide
kubectl get nodes -o wide
$podName = '替换为实际Pod名'
$nodeName = '替换为实际Node名'

# 调度失败、反复退出：看事件、上次退出信息及日志
kubectl describe pod $podName
kubectl logs $podName --previous --tail=100
kubectl describe node $nodeName
kubectl get nodes --show-labels

# Service：对照标签、目标端口、后端地址和就绪状态
kubectl get service my-api-service -o yaml
kubectl get pods --show-labels
kubectl get endpointslices -l kubernetes.io/service-name=my-api-service -o yaml

# 副本数量异常：查控制器与 Pod 的真实归属
kubectl get deployment,rs,pods
kubectl get pod $podName -o jsonpath='{.metadata.ownerReferences}'
```

`--previous` 看同一 Pod 中上一次终止的容器实例日志，不是被删除旧 Pod 的日志；有多个容器时追加 `-c 容器名`。EndpointSlice 的 `conditions.ready` 用来核对后端是否就绪，不能只数 IP。

资料入口：[Kubernetes Basics](https://kubernetes.io/docs/tutorials/kubernetes-basics/)、[Workloads](https://kubernetes.io/docs/concepts/workloads/)、[kubectl 官方速查](https://kubernetes.io/docs/reference/kubectl/quick-reference/)。B 站 [Kubernetes 入门指南](https://www.bilibili.com/video/BV1hZ421a7n7/) 本次未能读取视频或字幕；所需 Node、Pod、Deployment、Service、kubectl 概念已按官方资料覆盖，不作为已观看的视频摘要。
