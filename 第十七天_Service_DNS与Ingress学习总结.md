# 第十七天：Service、DNS 与 Ingress

> 依据官方资料、本地 YAML，以及共享会话中已核实的实践疑问和第 13～15 题批改整理。共享页前面的题目未能展开，不推定其对错；会话给出的 90～95% 是估评，不是精确答题统计。仅收录第十七天相关内容，以下命令未替你执行。

## 1. 先串起你自己的 API

你本地的配置是：Service 名 `study-security`，Service 端口 `80`，API 实际监听 `8080`。

```text
集群内：调用方 → study-security:80 → 当前就绪的 PodIP:8080
                 ↑ DNS 将服务名解析为 ClusterIP

外部 HTTP：客户端 → 可达的入口 → Ingress Controller
                               按 Ingress 的 Host/Path 规则
                               → study-security Service → Pod
```

这是逻辑关系，不代表每种控制器都必须先经过 ClusterIP 再访问 Pod；具体实现也可以根据后端信息直接转发。**Ingress 是规则，不是请求额外经过的一台服务器。**

## 2. Service 与 DNS：地址变化，调用方式不变

- Pod 重建后 IP 可能变；Service 在自身不被删除重建时提供稳定地址，调用方优先使用服务名。
- 普通 Service 用 `selector` 匹配**同一 namespace** 的 Pod 标签，不是匹配 Deployment 名。
- 控制面维护与 Service 关联的 EndpointSlice，记录后端 IP、端口、就绪状态等。Pod 更换后，后端信息随之更新。
- Service 不创建 Pod，也不修复应用。通常只向就绪后端分发，前提是没有特意发布未就绪地址。

例如 Pod 从 `10.244.0.10` 换成 `10.244.0.15`，调用方仍访问 `http://study-security`。[Service 官方说明](https://kubernetes.io/docs/concepts/services-networking/service/)

### 服务名怎么写？

假设 Service 位于 `default` namespace：

| 调用位置 | 地址示例 |
| --- | --- |
| 同一 namespace 的 Pod | `http://study-security` |
| 其他 namespace 的 Pod | `http://study-security.default` |
| 完整集群域名 | `http://study-security.default.svc.cluster.local` |
| Windows 宿主机 | 通常不能直接使用上述集群 DNS 名，改用端口转发或可达的 Ingress 入口 |

`cluster.local` 是常见集群域名，不是不可修改的固定值。普通 ClusterIP Service 的 DNS 解析到 ClusterIP；Headless Service 是例外，今天只需知道不能把所有 Service 都理解成有一个虚拟 IP。

**DNS 只解决名称到地址，不负责把 HTTP 的 80 端口自动改成 8080。** 不写端口的 HTTP 请求默认使用 80。[Service DNS](https://kubernetes.io/docs/concepts/services-networking/dns-pod-service/)

## 3. 端口和入口：最容易混的地方

| 配置或类型 | 解决什么问题 |
| --- | --- |
| `containerPort: 8080` | 描述容器端口，不会让程序自动监听，也不等于发布到宿主机 |
| Service `port: 80` | 客户端访问这个 Service 时使用的端口 |
| Service `targetPort: 8080` | 把请求转发到 Pod 的哪个端口；要与应用实际监听相符 |
| `ClusterIP` | 稳定的集群内部入口，默认 Service 类型 |
| `NodePort` | 通过可达的 `NodeIP:NodePort` 访问 Service |
| `LoadBalancer` | 请求平台提供负载均衡入口；需要对应实现，入口也可以是私网地址 |
| `Ingress` | 声明 HTTP/HTTPS 的域名、路径路由，交给 Ingress Controller 执行 |
| `port-forward` | 临时把本机端口转发到选中的 Pod，适合本地调试 |

因此你当前应访问 `http://study-security:80`，也就是 `http://study-security`，**不是因为容器监听 8080，就访问 Service 的 8080**。

你问过：“某个 Pod 实际监听 8000，`targetPort` 却是 8080，是否永远分不到它？”

更准确是：它仍可能被选中，但流量送到了它的 8080，那里没人监听就会失败。Service 不会自动试探正确端口。可以统一应用端口，或使用命名端口：Service 写 `targetPort: http`，各 Pod 分别声明名为 `http` 的实际容器端口。[Service 端口配置](https://kubernetes.io/docs/concepts/services-networking/service/#port-definitions)

没有 Service，Pod 之间通常也能通过 Pod IP 通信，仍受网络策略等限制。Service 增加的是稳定发现和访问入口，不是凭空创造 Pod 网络。[Kubernetes 网络模型](https://kubernetes.io/docs/concepts/services-networking/)

## 4. Ingress、Host、hosts 和 tunnel 分别干什么？

你现有的 Ingress 规则可以读成：

> 当请求的 Host 是 `study-security.local`，路径匹配 `/` 时，交给 `study-security` Service 的 **80** 端口。

- **Ingress Controller**：实际接收并转发请求的程序；只创建 Ingress YAML，不安装/配置控制器，不会自动生效。
- **`ingressClassName`**：指定由哪类控制器处理，需要对应的 IngressClass 与控制器。
- **Host/Path**：按请求里的域名和路径匹配；不是根据请求内容猜应该找哪个 API。
- **本机 hosts / DNS**：让客户端知道域名指向哪个入口 IP；不负责转发，也不会自动安装路由。
- **tunnel**：在特定本地环境下提供通往集群入口的访问路径；不负责解析域名或选择业务 Service。

Ingress YAML 里的 `host` 不会自动替你配置域名解析。反过来，仅改 hosts，也不能让未匹配的 Ingress 规则生效。`pathType: Prefix` 是按路径段前缀匹配，也不表示自动删除或重写路径。[Ingress 官方说明](https://kubernetes.io/docs/concepts/services-networking/ingress/)

**版本提醒（2026-09）：** Ingress API 仍可用，但已冻结，新能力主要发展在 Gateway API。社区 `kubernetes/ingress-nginx` 项目已于 2026 年 3 月退役；这不等于所有 NGINX 产品都退役。你文件中的 `nginx` 只是 class 名，需检查实际控制器。不要把旧教程安装步骤直接当成新的生产方案。[官方退役说明](https://kubernetes.io/blog/2026/03/30/kubernetes-v1-36-sneak-peek/)

## 5. 对应 Azure Container Apps（ACA）怎么理解？

这是**功能上的类比，不是 Kubernetes 资源一一对应**。ACA 帮你管理入口，不要求你手写 Kubernetes Service / Ingress YAML。

| Kubernetes 中的理解 | ACA 中对应的理解 |
| --- | --- |
| 稳定 Service 名，后端 Pod 可以更换 | 稳定应用名/FQDN，后端副本和修订可以变化 |
| 内部服务入口 | 应用的 internal ingress：应用自身入口限同一 ACA Environment 内调用 |
| 对外提供入口 | 应用的 external ingress：把应用发布到 Environment 的入口边界 |
| 后端目标端口 | ACA `targetPort` 同样要匹配容器内应用监听端口，例如 8080 |
| 外部 HTTPS 与内部应用端口不同 | 客户端通常访问 HTTPS 443，平台再转到目标端口 8080 |

**external 不一定等于公网。** 还要看 Environment：公网环境的 external 应用可通过公网入口访问；内部环境没有公网入口，external 应用发布在其私网入口。internal 应用自身 FQDN 默认只供同环境内使用。

还有一个例外要记住：internal 应用若被环境级 HTTP 路由作为后端引用，仍可能经那个入口收到外部流量。是否真正隔离，要连同环境和路由一起看。[ACA ingress](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)

今天掌握映射即可，不需要为了验收创建 Azure 资源。

## 6. 本次问答中需要修正的内容

下列综合题在会话中被评价为“整体正确”，这里记录的是需修正的表达，**不把整题算成全错**。

| 题目/疑问 | 原理解的问题 | 修正后怎么说 |
| --- | --- | --- |
| 第 13 题批改：404 / 502 | 把 404 理解成没找到 Service，502 理解成找到但不匹配 | 先判断响应来自哪里；路由未匹配和后端转发失败是不同层的问题，不能只凭状态码断定 |
| 第 14 题：DNS 与 Pod 替换 | 把 ClusterIP 入口说成统一的外部地址 | 它是稳定的集群内部入口，外部访问需要额外路径 |
| 第 14 题：EndpointSlice | 说 Service“包含”EndpointSlice | 它们是关联的独立资源；控制面维护后端列表，网络组件据此转发 |
| 第 15 题：ClusterIP | 说它让集群内部能够通信 | Pod 网络本身提供通信基础，ClusterIP 提供稳定服务入口 |
| 第 15 题：targetPort | 说它就是 Pod 监听端口 | 它是配置的转发目标端口；真正监听的是容器内应用，二者必须对得上 |
| 实践疑问：访问端口 | 认为要按容器的 8080 访问 Service | 按 Service 的 `port` 访问；你的配置应使用 80 |

第 15 题还涉及数据卷共享，但它属于前一天的存储/Pod 内容，本篇不重复收录。

### 状态码是排查线索，不是唯一结论

| 现象 | 先检查 |
| --- | --- |
| 域名无法解析 | Service 名、namespace、集群 DNS，或本机 DNS/hosts |
| Connection refused / 超时 | 入口地址、监听端口、转发/tunnel 是否运行、网络是否可达 |
| 404 | 可能是 Ingress Host/Path 未匹配，也可能是 API 自己没有该路由 |
| 502 | 常见于网关访问后端失败，核对 targetPort、监听地址和后端响应 |
| 503 | 常见于没有可用后端，也可能是应用自己返回；查 EndpointSlice 的就绪状态和日志 |

不同控制器的返回行为可能不同。会话里“404 就是路由没匹配、502 就证明所有路由都正确”的简化说法不能当定律。[Service 排查指南](https://kubernetes.io/docs/tasks/debug/debug-application/debug-service/)

## 7. 实践/验收命令（PowerShell）

沿用本地 `study_security/study_security` 的配置，不重新部署 API。先确认已有 Deployment 所在的 namespace；后续 `$ns` 必须与它一致。

### 7.1 创建 ClusterIP Service

```powershell
Set-Location D:\Docker_Learning\study_security\study_security
kubectl config current-context
kubectl get deployment -A
$ns = 'default'  # 若 API 在 day16-lab 等其他 namespace，改成实际值
kubectl get pods -n $ns -l app=study-security -o wide
```

你现有 `service.yaml` 的核心内容已经符合本次要求：

```yaml
apiVersion: v1
kind: Service
metadata:
  name: study-security
spec:
  type: ClusterIP
  selector:
    app: study-security
  ports:
    - protocol: TCP
      port: 80
      targetPort: 8080
```

```powershell
kubectl apply -n $ns -f service.yaml
kubectl get service study-security -n $ns
kubectl get pods -n $ns --show-labels
kubectl get endpointslices -n $ns -l kubernetes.io/service-name=study-security -o yaml
```

验收：类型为 ClusterIP，Service 端口为 80；EndpointSlice 后端对应 API Pod，端口为 8080，`conditions.ready` 为 true。`EXTERNAL-IP` 为 `<none>` 对 ClusterIP 是正常结果。

### 7.2 从临时 Pod 用服务名访问

```powershell
# 本次测试专用名称；若已存在，先确认用途，不要覆盖别人的 Pod
kubectl run day17-client -n $ns --image=curlimages/curl --restart=Never --command -- sleep 3600
kubectl wait -n $ns --for=condition=Ready pod/day17-client --timeout=120s
kubectl exec -n $ns day17-client -- curl -v --max-time 10 http://study-security/
```

`curl -v` 可看到服务名解析出的地址及连接端口；API 返回你当前代码的响应即通过。若 Pod 拉取镜像失败，先 `describe pod` 查事件，不能把它算成 DNS 失败。

需要单独验证 DNS 时，可用另一个临时 Pod：

```powershell
kubectl run day17-dns -n $ns --image=busybox:1.37 --restart=Never --rm -i -- nslookup "study-security.${ns}.svc.cluster.local"
```

若集群使用了其他域名，替换 `cluster.local`。`--rm` 在命令退出后删除这次测试 Pod。

### 7.3 本地访问：选 port-forward 即可完成验收

```powershell
kubectl port-forward -n $ns service/study-security 18080:80
```

保留窗口，另开终端：

```powershell
curl.exe -v http://127.0.0.1:18080/
```

这里是 **本机 18080 → Service 的 80 → 选中 Pod 的 8080**。关闭转发窗口入口就失效；Pod 被替换时可能需要重开。它不是长期发布，也不能用来证明 Service 已向所有副本分流。[端口转发文档](https://kubernetes.io/docs/tasks/access-application-cluster/port-forward-access-application-cluster/)

### 7.4 已有 Ingress 时，检查你自己的规则（可选）

```powershell
kubectl get ingressclass
kubectl get pods,service -A
kubectl get ingress -n $ns
kubectl describe ingress study-security-ingress -n $ns
```

核对现有 `ingress.yaml`：`host: study-security.local`、路径 `/`、后端 Service `study-security`、后端 Service 端口 **80**，不是 8080。Ingress 和后端 Service 应位于同一 namespace；确认控制器与 class 后再提交配置。

如果入口确实有一个本机可达的 HTTP 80 地址，可不修改 hosts，直接测试：

```powershell
$ingressIP = '替换为实际可达的入口IP'
curl.exe -v --resolve "study-security.local:80:${ingressIP}" http://study-security.local/
```

`--resolve` 只对这次 curl 指定“域名对应哪个 IP”，同时保留正确 Host。不要盲目填 `127.0.0.1` 或 `minikube ip`：先确认你的控制器通过哪条路径暴露。若当前方案依赖 `minikube tunnel`，需保持其窗口运行。

`curl -v` 显示的是客户端连接的入口地址，**不能直接告诉你最终选中了哪个 Pod**。后者需要应用返回 Pod 名，或查看控制器/应用日志。

### 7.5 清理测试客户端

确认测试结束后，只删本次创建的客户端，不删除 API：

```powershell
kubectl delete pod day17-client -n $ns
```

## 8. 最终需要留下的验收结果

- [ ] Service 是 ClusterIP，`selector` 能选中 API Pod。
- [ ] EndpointSlice 中有正确的后端地址、8080 端口和就绪状态。
- [ ] 临时 Pod 通过 `http://study-security` 成功得到响应。
- [ ] 本机通过 port-forward 或已有 Ingress 成功访问。
- [ ] 能解释 `80 → 8080`，以及 DNS、hosts、tunnel、Ingress 各自的职责。
- [ ] 能说明 ACA 的 internal/external 还要结合 Environment 判断访问范围。

问答来源：[第十七天共享会话](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)。未能展开的前段题目不编造错题编号或选择答案。
