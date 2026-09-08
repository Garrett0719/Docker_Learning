# 第十六天：Pod 与 Deployment

> 根据共享会话中已读取的第十六天问答、官方资料和本地 C# API 整理。批改记录显示 15 题只错 1 题，约 93%；不收录其他学习日的错题。本文提供练习步骤，不代表已替你运行验收。

## 1. 今天先记住这条关系

```text
Deployment：声明副本数和 Pod 模板，管理更新、回滚
└─ ReplicaSet：维持指定数量的 Pod
   ├─ Pod A → API 容器
   └─ Pod B → API 容器
```

`replicas: 2` 是希望运行两个 Pod，不是一个 Pod 内装两个容器，也不是增加两台 Node。Service 提供访问入口，不负责补副本。[官方部署练习](https://kubernetes.io/docs/tutorials/kubernetes-basics/deploy-app/deploy-intro/)

## 2. Pod：可以替换，不要当成固定机器

### 2.1 重启容器与替换 Pod，不是一回事

| 发生什么 | 应该怎样理解 |
| --- | --- |
| 同一 Pod 的 `RESTARTS` 增加 | 里面的容器重启过，不一定换了 Pod |
| 删除受 Deployment 管理的 Pod | ReplicaSet 补建一个新 Pod，不是复活旧 Pod |
| 节点故障后在其他节点补建 | 新 Pod 替代旧 Pod，不是原 Pod 带着身份搬家 |
| 删除没有控制器管理的裸 Pod | 不会仅因为删除就自动补建 |

**判断是不是同一个 Pod，UID 比名字更可靠。** 名字可能被复用，新 Pod 的 UID 不同，IP 也可能变化。应用间通信使用 Service 名，不要写死某个 Pod 名或 IP。

生命周期先认识：`Pending` 等待调度或启动准备；`Running` 已有容器运行或正在启动、重启；`Succeeded` 全部成功结束；`Failed` 至少一个失败结束。`Running` 不等于 Ready，也不代表 API 一定可用。[Pod 生命周期](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/)

### 2.2 同一个 Pod，共享什么？

- **共享网络：** 使用同一个 Pod IP，可通过 `localhost` 通信；端口空间也共享，两个程序不能同时占用相同监听地址和端口。
- **文件不会自动共享：** 各容器仍有自己的文件系统。需要共享文件时，把同一个 Volume 挂载到双方。
- **共享不等于持久：** `emptyDir` 可跨同一 Pod 内的容器重启保留，但 Pod 删除后数据也会删除。

例如：API 把文件写进自己的 `/app/uploads`，另一个容器不会因为“在同一个 Pod”就自动看见它。[Pods：网络与存储](https://kubernetes.io/docs/concepts/workloads/pods/)

### 2.3 为什么不能依赖本地可写层？

Pod 被替换后，新容器不会自动带回旧容器写入的文件。两个 API 副本的内存、可写层也不是自动同步的。

临时文件可以本地放；订单、上传文件、需要跨实例共享的会话等，应放数据库、对象存储、共享缓存或合适的持久卷。不要把重要状态绑在某个 API 实例上。[Volumes](https://kubernetes.io/docs/concepts/storage/volumes/)

## 3. 今天唯一的错题：删除 Deployment 后会怎样？

**题目要点：** 删除 Deployment 后，它管理的 ReplicaSet 和 Pod 默认会发生什么？

**正确理解：默认会级联删除下属 ReplicaSet 和 Pod，不是只删除一个管理外壳、让它们一直保留。**

| 操作 | 为什么结果不同 |
| --- | --- |
| 只删一个 Pod | Deployment、ReplicaSet 还在，期望副本数没变，所以会补建 |
| 删除 Deployment | 连同下属对象一起清理，原来的这条控制链不再负责补副本 |
| 删除 Deployment 时显式使用 `--cascade=orphan` | 这是特意保留下属对象的例外，不是默认行为 |

记法：**删一个实例，目标还在；删掉 Deployment，连同这组实例的管理目标一起撤销。**

默认删除通常由后台继续清理，因此命令返回时，Pod 可能还在 `Terminating`。单独创建的 Service 不会仅因选择了这些 Pod 就一起删除。[官方级联删除说明](https://kubernetes.io/docs/concepts/architecture/garbage-collection/#cascading-deletion)

## 4. 其余答对的题：更新与回滚要记住什么？

| 知识点 | 通俗理解 |
| --- | --- |
| 更新镜像 `v1 → v2` | Pod 模板改变，通常创建新 ReplicaSet，再逐步替换旧 Pod |
| 只调整副本数 | 改数量，不是发布新版本，不会仅因此产生新的发布修订 |
| 旧 ReplicaSet 显示 `0/0/0` | 可以是保留的版本历史，不代表异常，也不代表旧 Pod 仍在运行 |
| `rollout undo` | 恢复之前的 Pod 模板，通过副本调整替换 Pod；不是进容器修改文件 |
| 回滚的范围 | 不会撤回数据库变更，也不会恢复已丢失的业务数据；需要保留可用的历史和镜像 |

`replicas: 2` 是稳定目标，不是更新期间的硬上限。`maxSurge: 1` 允许额外增加 1 个副本；`maxUnavailable: 0` 表示更新时不主动减少目标可用副本。因此常见过程是“先起新 Pod，再删旧 Pod”。处于终止过程的 Pod 还可能暂时占用资源，不能把 3 当成任何时刻的绝对总数上限。

滚动更新不保证零中断，也不会默认替你自动回滚。要看新 Pod 是否就绪、资源是否足够、旧请求是否能正常结束。[Deployment 官方说明](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)

补充一个发布习惯：使用新的版本 tag 或 digest，并更新 YAML。仅覆盖仓库里同名 tag、模板不变，不会自动替换正在运行的 Pod。

## 5. Deployment YAML：能写，也能解释

本地现有文件 `study_security/study_security/deployment.yaml` 已使用 `study-security:v2`、两个副本。下面是**从 v1 重新练习**的独立示例，请另存为工作区根目录下的 `deployment-day16.yaml`，不要覆盖现有文件。

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: study-security
spec:
  replicas: 2
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: study-security
  template:
    metadata:
      labels:
        app: study-security
    spec:
      containers:
        - name: study-security
          image: study-security:v1
          imagePullPolicy: IfNotPresent
          env:
            - name: ASPNETCORE_HTTP_PORTS
              value: "8080"
          ports:
            - containerPort: 8080
          readinessProbe:
            httpGet:
              path: /
              port: 8080
            periodSeconds: 3
```

本地 API 已有 `/` 路由，因此这里用它做简易就绪检查；生产应用应按实际情况设计健康端点。

| 字段 | 你需要知道的意思 |
| --- | --- |
| `apiVersion` + `kind` | 指定资源类型及其 API 版本；Deployment 使用 `apps/v1`，Pod 使用 `v1`，不能随意填写 |
| `metadata.name` | Deployment 的名字，不是将来每个 Pod 的固定名字 |
| `selector.matchLabels` | 选择要管理的 Pod；必须匹配模板的标签，创建后不能随便修改 selector |
| `template` | 创建新 Pod 时使用的模板，包括标签、镜像、端口等 |
| `containers[].name` | 容器名；`kubectl set image` 等号左侧填它 |
| `containerPort` | 描述容器端口，不是宿主机端口映射，也不会让程序自动监听 |
| `imagePullPolicy: IfNotPresent` | 节点已有该镜像就使用，否则尝试拉取；适合本次导入本地镜像的实验 |

会话中你问过的 `kind` **不是无作用的备注**。它与 `apiVersion` 一起告诉 Kubernetes：这份配置应当作为哪一种资源处理。查字段不用猜：

```powershell
kubectl api-resources
kubectl explain deployment --api-version=apps/v1
kubectl explain deployment.spec.template
```

## 6. 实践：两个副本 → 删除补齐 → 滚动更新

以下统一使用 **PowerShell + minikube**。Docker 和 minikube 需已启动。使用单独的 `day16-lab` namespace，避免影响原来的学习部署；若它已存在，先确认里面没有其他用途的资源。

### 6.1 构建镜像并启动两个副本

```powershell
Set-Location D:\Docker_Learning
kubectl config current-context
# 确认本次使用 minikube；若不是，再切换
kubectl config use-context minikube
kubectl get nodes
kubectl create namespace day16-lab

docker build -t study-security:v1 -f study_security/study_security/Dockerfile study_security/study_security
minikube image load study-security:v1

# 先按第 5 节保存 deployment-day16.yaml
kubectl apply -n day16-lab -f deployment-day16.yaml
kubectl rollout status -n day16-lab deployment/study-security --timeout=120s
kubectl get deployment,rs,pods -n day16-lab
kubectl get pods -n day16-lab -l app=study-security -o wide
```

namespace 已存在时跳过创建。**宿主机 Docker 有镜像，不等于 minikube 节点就有镜像**，所以要导入；多个节点要确保各节点都可用。若使用其他集群，应推送到其可访问的仓库，并相应更改镜像地址和认证。[minikube 镜像使用方式](https://minikube.sigs.k8s.io/docs/handbook/pushing/)

验收：Deployment `READY 2/2`，两个 Pod 均为 `1/1 Running`。前者数 Pod 副本，后者数该 Pod 中就绪的容器。

可选：临时访问 API，不要求今天另写 Service：

```powershell
kubectl port-forward -n day16-lab deployment/study-security 18080:8080
# 另一个终端
curl.exe http://127.0.0.1:18080/
```

这个入口选中一个 Pod，只用于调试；不是多副本负载均衡测试。

### 6.2 只删一个 Pod，观察补齐

```powershell
$podName = kubectl get pods -n day16-lab -l app=study-security -o jsonpath='{.items[0].metadata.name}'
kubectl get pod $podName -n day16-lab -o jsonpath='{.metadata.uid}'
kubectl delete pod $podName -n day16-lab
kubectl get pods -n day16-lab -l app=study-security -w
```

观察到新 Pod 出现并恢复两个就绪副本后，按 `Ctrl+C`。它只停止观察，不会停止应用。新 Pod 的名称、UID 应与被删除的不同；IP 可能不同，不要求一定变化。之后不要继续使用过期的 `$podName`。

### 6.3 更新 v2，观察新旧副本替换

先对 API 做一个容易看到的改动，例如让 `/` 的响应带上 `v2`，再构建。如果代码不变，仅换 tag 也能触发模板更新，但无法从响应看出业务版本差异。

```powershell
docker build -t study-security:v2 -f study_security/study_security/Dockerfile study_security/study_security
minikube image load study-security:v2
```

先在另一个终端开启观察，避免发布太快而错过：

```powershell
kubectl get pods -n day16-lab -l app=study-security -w
```

把 `deployment-day16.yaml` 中镜像改为 `study-security:v2`，再执行：

```powershell
kubectl apply -n day16-lab -f deployment-day16.yaml
kubectl rollout status -n day16-lab deployment/study-security --timeout=120s
kubectl get rs -n day16-lab
kubectl get pods -n day16-lab -l app=study-security -o wide
kubectl describe deployment study-security -n day16-lab
```

验收：出现新 ReplicaSet，新 Pod 逐步就绪，旧副本缩到 0，最终仍为两个就绪副本，模板镜像是 v2。若要验证响应，更新后重新开启端口转发，再请求 `/`。

也要认识这个替代命令：

```powershell
kubectl set image -n day16-lab deployment/study-security study-security=study-security:v2
```

等号左边是容器名，右边是镜像。它只改集群对象，不会修改本地 YAML；使用后要同步文件，避免下次 `apply` 改回旧值。

### 6.4 查看历史与回滚（补充练习）

```powershell
kubectl rollout history -n day16-lab deployment/study-security
kubectl rollout undo -n day16-lab deployment/study-security
kubectl rollout status -n day16-lab deployment/study-security --timeout=120s
kubectl get rs -n day16-lab
```

`undo` 默认回到上一修订；确认回滚后，也要让本地 YAML 与打算保留的版本一致。否则再次提交仍写 v2 的文件，又会更新为 v2。

## 7. 遇到异常，最少会查这几项

先用 `kubectl get pods -n day16-lab` 找到实际 Pod 名，填入 `$podName`：

```powershell
$podName = '替换为实际Pod名'
kubectl describe pod $podName -n day16-lab
kubectl logs $podName -n day16-lab --tail=50
kubectl logs $podName -n day16-lab --previous --tail=50
kubectl get pod $podName -n day16-lab -o yaml
```

- `describe`：看事件、镜像拉取失败、调度失败及探针结果。
- `logs`：看应用输出；`--previous` 看同一 Pod 中上次终止的容器日志。
- `-o yaml`：看完整配置；`metadata.ownerReferences` 可查它由谁管理。
- `rollout status` 超时：只是等待超时，不会自动撤销发布。先查新 Pod 为什么没就绪。

多容器 Pod 查看日志时，可用 `-c 容器名` 指定目标。

## 8. 最终验收清单

- [ ] 能解释 Deployment → ReplicaSet → Pod 的关系。
- [ ] YAML 启动两个就绪副本，能说清 selector 与模板标签如何匹配。
- [ ] 删除一个 Pod 后出现新实例，最终恢复两个副本。
- [ ] v1 → v2 后观察到新旧 ReplicaSet 和 Pod 的替换过程。
- [ ] 能区分容器重启与 Pod 替换，不依赖固定 Pod IP 或本地数据。
- [ ] 能解释错题：为什么删除 Pod 会补齐，删除 Deployment 默认不会。

记录三组结果即可：**初始两个副本；删除前后 Pod 名/UID；更新前后镜像与 ReplicaSet 数量。**

仅在练习完成、不再需要本次 API 时清理实验 Deployment；这个命令会停止本次实验应用：

```powershell
kubectl delete deployment study-security -n day16-lab
```

问答来源：[第十六天共享会话](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)。本次整理沿用此前已读取的验收记录；重新打开共享页超时，未据此推定有新的答题内容。
