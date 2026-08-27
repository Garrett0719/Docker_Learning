# C# 开发者的容器与 Azure Container Apps 42 天学习路径

> 制定日期：2026-08-25  
> 建议投入：普通学习日 1.5～2 小时，项目日 2～3 小时；如果每天只能投入 1 小时，可把计划中的“一天”拆成两个自然日。  
> 主线：容器原理 → Docker 工程实践 → 可迁移的编排知识 → Azure Container Apps → C# 综合项目。  
> 范围控制：Kubernetes 只学习 Pod、Deployment、Service、配置、探针、资源和滚动发布等可迁移概念；不展开集群安装、etcd、CNI、Operator、Service Mesh 等运维专题。

## 最终目标

完成本路线后，你应该能够：

1. 解释容器与虚拟机的区别，以及 namespaces、cgroups、OCI 镜像/运行时规范分别解决什么问题。
2. 为 ASP.NET Core 应用编写体积合理、缓存友好、非 root、可观测且可安全发布的生产型镜像。
3. 独立处理容器网络、端口、DNS、持久化、配置、密钥、资源限制、健康检查和故障排查。
4. 用 Compose 组织本地多容器应用，并把同一套概念迁移到 Kubernetes、Podman 和云容器平台。
5. 掌握 Azure Container Registry 与 Azure Container Apps 的环境、应用、修订版、入口、服务发现、扩缩容、作业、身份、安全、日志、监控和发布流程。
6. 独立把一个 C# 多服务应用从本地容器部署到 Azure Container Apps，并完成灰度发布、回滚、扩缩容和故障演练。

## 使用方式

- 每天先完成“资料”，再完成“实践/验收”。只看完资料不算完成。
- 建议从第一天起维护一个自己的 GitHub 仓库 `container-learning-csharp`，保存 Dockerfile、Compose、Kubernetes YAML、Azure YAML、实验记录和问题复盘。
- 所有云上实验使用独立资源组。当天实验结束后，不再使用的资源应立即删除，并在 Azure Cost Management 中设置预算提醒。
- 示例中的版本号以你学习当天的 .NET LTS/STS 和文档当前版本为准，不机械照抄旧版本标签。

## 建议软件

- [Docker Desktop](https://docs.docker.com/desktop/)：前半程的主要容器引擎，并可提供本地 Kubernetes。
- [.NET SDK](https://dotnet.microsoft.com/download)：使用学习时仍受支持的 .NET 版本；旧教程中的版本号只作参考。
- [Visual Studio](https://visualstudio.microsoft.com/) 或 [Visual Studio Code + C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)：C# 开发环境。
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) 与 [Container Apps 扩展](https://learn.microsoft.com/en-us/azure/container-apps/azure-cli)：ACA 日常创建、更新和诊断。
- [Azure Developer CLI (`azd`)](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)：结课项目的可重复部署工作流。
- [Podman](https://podman.io/docs/installation)：只在最后一天用来验证 OCI 可移植性，前期不必安装。

## 第一阶段：容器原理与单容器 C# 实践（第 1～7 天）

### 第一天：建立学习基线并补齐 Docker 入门实操

资料1：[Docker Workshop](https://docs.docker.com/get-started/workshop/)，Docker 官方 45 分钟工作坊  
资料2：[Docker CLI Cheat Sheet](https://docs.docker.com/get-started/docker_cheatsheet.pdf)，Docker 官方命令速查表  
资料3：[.NET 官方容器镜像仓库](https://github.com/dotnet/dotnet-docker)，官方 .NET 镜像和示例（约 4.9k stars，2026-08-25 核对）

学习内容：镜像、容器、仓库、端口映射、容器生命周期，以及 `docker run/build/exec/logs/inspect/rm` 的用途。  
实践/验收：创建一个 ASP.NET Core Minimal API；在不看笔记的情况下运行官方 ASP.NET 镜像、查看日志、进入容器、停止并删除容器；把命令和现象写进学习仓库。

### 第二天：理解容器不是“小型虚拟机”

资料1：[Containers From Scratch（Liz Rice）](https://www.youtube.com/watch?v=8fi7uSYlOdc)，从零演示 namespaces、文件系统和 cgroups  
资料2：[Linux namespaces(7)](https://man7.org/linux/man-pages/man7/namespaces.7.html)，Linux namespaces 权威说明  
资料3：[Linux cgroups(7)](https://man7.org/linux/man-pages/man7/cgroups.7.html)，Linux 资源分组、统计与限制

学习内容：容器本质上是受隔离和资源约束的进程；PID、网络、挂载、用户等 namespace 与 cgroup 的职责；Windows/macOS 上 Linux 容器为何仍需要 Linux 虚拟机。  
实践/验收：分别在宿主机和容器内查看进程、主机名、网络接口和用户；用自己的话写出“namespace 负责隔离，cgroup 负责什么”的两句话答案。

### 第三天：理解 OCI 与镜像可移植性

资料1：[Open Container Initiative](https://opencontainers.org/)，OCI 的 Runtime、Image、Distribution 三类规范  
资料2：[OCI Image Configuration](https://specs.opencontainers.org/image-spec/config/)，镜像层、配置和执行参数  
资料3：[OCI FAQ](https://opencontainers.org/faq/)，为什么不同引擎、仓库和云平台能够互通

学习内容：镜像层、manifest、config、digest、tag、registry distribution；Docker、containerd、runc、Podman、Kubernetes 之间的职责边界。无需通读规范，只读概览、术语和示意结构。  
实践/验收：对一个 .NET 镜像执行 `docker image inspect` 和 `docker history`；画出“Dockerfile → OCI 镜像 → Registry → Runtime → 进程”的一页流程图。

### 第四天：为 ASP.NET Core 编写正确的 Dockerfile

资料1：[Tutorial: Containerize a .NET app](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container)，微软官方 .NET 容器化教程  
资料2：[Dockerfile reference](https://docs.docker.com/reference/dockerfile/)，Dockerfile 指令参考  
资料3：[Docker build best practices](https://docs.docker.com/build/building/best-practices/)，构建上下文、缓存、基础镜像和临时容器原则

学习内容：`FROM`、`WORKDIR`、`COPY`、`RUN`、`ENV`、`EXPOSE`、`USER`、`ENTRYPOINT`、`CMD`；exec form 与 shell form；`.dockerignore`；为什么容器只运行一个前台主进程。  
实践/验收：为第一天的 API 编写 Dockerfile 和 `.dockerignore`，构建并运行；验证应用监听 `0.0.0.0` 而不是只监听 `localhost`。

### 第五天：多阶段构建、缓存和 .NET 镜像选择

资料1：[Multi-stage builds](https://docs.docker.com/build/building/multi-stage/)，Docker 官方多阶段构建  
资料2：[.NET container images](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images)，SDK、ASP.NET Runtime、runtime-deps、chiseled 等镜像  
资料3：[Lab: Building Container Images](https://docs.docker.com/guides/lab-building-images/)，镜像层、缓存、非 root、多阶段和构建密钥实验

学习内容：restore/build/publish/runtime 分层；先复制项目文件再 restore 的缓存原因；常规、Alpine、chiseled/distroless 的兼容性与攻击面权衡；tag 与 digest 的差异。  
实践/验收：把第四天的 Dockerfile 改成多阶段构建；连续构建两次比较耗时；改变一个 `.cs` 文件再构建，确认依赖恢复层被复用；记录最终镜像大小。

### 第六天：比较 Dockerfile 构建与 `dotnet publish` 直接生成镜像

资料1：[Containerize a .NET app with dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish)，无需手写 Dockerfile 的 .NET SDK 容器发布  
资料2：[.NET SDK container creation overview](https://learn.microsoft.com/en-us/dotnet/core/containers/overview)，容器发布属性、仓库认证和配置  
资料3：[Build, tag, and publish an image](https://docs.docker.com/get-started/docker-concepts/building-images/build-tag-and-publish-an-image/)，镜像命名、tag 和发布

学习内容：Dockerfile 与 SDK 内置容器发布的优缺点；镜像名结构；可变 tag 与不可变 digest；何时需要自定义系统包和构建步骤。  
实践/验收：用两种方式为同一 API 生成镜像，比较层、体积、用户、入口和可定制性；写下你在生产项目中的默认选择及理由。

### 第七天：第一周复盘项目——单容器生产基线

资料1：[Official .NET Docker samples](https://github.com/dotnet/dotnet-docker/tree/main/samples)，微软维护的 .NET 容器样例  
资料2：[Docker building best practices](https://docs.docker.com/build/building/best-practices/)，复查生产构建清单

学习内容：把前六天知识收束成一套可重复模板。  
实践/验收：从空目录创建 `ContainerLearning.Api`，完成多阶段构建、`.dockerignore`、版本 tag、非 root 运行、端口映射和 README；删除本地容器和镜像后，仅根据 README 从零重建成功。

## 第二阶段：网络、存储、Compose、安全与可观测性（第 8～14 天）

### 第八天：容器存储与无状态设计

资料1：[Docker Storage](https://docs.docker.com/engine/storage/)，writable layer、volume、bind mount、tmpfs  
资料2：[Docker bind mounts](https://docs.docker.com/engine/storage/bind-mounts/)，宿主机目录映射的用途和风险  
资料3：[Docker volumes](https://docs.docker.com/engine/storage/volumes/)，Docker 管理的持久化卷

学习内容：镜像只读层、容器可写层、容器删除后数据为何丢失；开发代码挂载与生产数据卷的区别；为什么云原生 Web API 应尽量无状态。  
实践/验收：让 API 写一个文件，分别测试容器 writable layer、bind mount 和 named volume；删除并重建容器，记录三种方式的数据结果。

### 第九天：容器网络、DNS 与端口

资料1：[Docker networking overview](https://docs.docker.com/engine/network/)，容器网络、DNS、端口发布  
资料2：[Bridge network driver](https://docs.docker.com/engine/network/drivers/bridge/)，自定义 bridge 网络和服务名解析  
资料3：[B 站：阿里云与 CNCF 云原生课程](https://www.bilibili.com/video/BV1Kz411i788/)，只看“容器基本概念、Kubernetes 网络概念、Service”相关选集

学习内容：容器端口与宿主机端口；bridge、NAT、DNS；为什么容器间通信应使用服务名而不是临时 IP；`EXPOSE` 不等于公开端口。  
实践/验收：运行两个容器加入同一个自定义网络，让 API 通过容器名访问第二个服务；用 `inspect` 验证网络、DNS 和 IP，并删除重建目标容器后再次访问。

### 第十天：Compose 与多容器应用

资料1：[Docker Compose Quickstart](https://docs.docker.com/compose/gettingstarted/)，Compose 服务、网络、卷和环境变量  
资料2：[Compose file reference](https://docs.docker.com/reference/compose-file/)，Compose 规范参考  
资料3：[docker/awesome-compose](https://github.com/docker/awesome-compose)，高星多容器样例集合（约 46.2k stars）

学习内容：`services`、`build`、`image`、`ports`、`environment`、`volumes`、`networks`、`depends_on`；声明依赖不等于应用已就绪。  
实践/验收：运行 [awesome-compose 的 ASP.NET + SQL Server 样例](https://github.com/docker/awesome-compose/tree/master/aspnet-mssql)；然后为自己的 API + PostgreSQL/SQL Server 编写 `compose.yaml`，只暴露 API 端口。

### 第十一天：配置、密钥与环境差异

资料1：[Dockerfile `RUN --mount=type=secret`](https://docs.docker.com/reference/dockerfile/#run---mounttypesecret)，构建时密钥  
资料2：[ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)，环境变量和配置优先级  
资料3：[Docker Compose environment variables](https://docs.docker.com/compose/how-tos/environment-variables/)，Compose 插值、`.env` 与容器环境

学习内容：构建时参数、运行时配置和密钥的区别；为什么密码不能写进 Dockerfile、镜像层、Git 或普通日志；双下划线映射到 C# 分层配置。  
实践/验收：从镜像中清除所有环境特定配置；数据库地址通过环境变量注入，密码通过 Compose secret 或本地未提交文件注入；用 `docker history` 确认没有密钥痕迹。

### 第十二天：健康检查、优雅退出与 PID 1

资料1：[Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)，ASP.NET Core 健康检查中间件  
资料2：[Dockerfile HEALTHCHECK](https://docs.docker.com/reference/dockerfile/#healthcheck)，容器级健康检查  
资料3：[Kubernetes probes](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/#container-probes)，startup、readiness、liveness 的通用语义

学习内容：存活、就绪和启动检查的差别；检查依赖时避免昂贵查询；SIGTERM、停止超时和请求排空；exec-form 入口为何更容易收到信号。  
实践/验收：给 C# API 添加 `/health/live` 与 `/health/ready`；配置容器健康检查；执行停止命令，确认应用优雅退出且没有新请求进入。

### 第十三天：容器安全与供应链基础

资料1：[Docker Engine security](https://docs.docker.com/engine/security/)，namespaces、cgroups、daemon、capabilities  
资料2：[Docker Rootless mode](https://docs.docker.com/engine/security/rootless/)，无 root daemon/容器模式  
资料3：[Docker Scout](https://docs.docker.com/scout/)，SBOM、CVE 与镜像策略

学习内容：最小权限、非 root、只读文件系统、capability、不要使用 privileged、可信基础镜像、SBOM 和漏洞扫描；扫描结果不等于所有漏洞都可利用。  
实践/验收：以 `USER app` 运行 API；尝试只读根文件系统并为临时目录单独挂载；生成 SBOM、扫描 CVE；记录可修复项和接受风险的理由。

### 第十四天：第二阶段项目——本地两服务系统

资料1：[docker/awesome-compose](https://github.com/docker/awesome-compose)，Compose 结构参考  
资料2：[Docker Compose production considerations](https://docs.docker.com/compose/how-tos/production/)，开发与生产配置差异

学习内容：把网络、存储、配置、健康和安全集中到一个可运行项目。  
实践/验收：完成 `Catalog.Api + Worker + Database` 或 `Web API + Database`：自定义网络、named volume、健康检查、非 root、资源限制、结构化控制台日志、无明文密钥；用一条 Compose 命令启动，并故意停止数据库观察恢复行为。

## 第三阶段：可迁移的容器编排知识（第 15～21 天）

### 第十五天：为什么需要编排，以及 Kubernetes 最小心智模型

资料1：[Kubernetes Basics](https://kubernetes.io/docs/tutorials/kubernetes-basics/)，官方交互式基础教程  
资料2：[Kubernetes Workloads](https://kubernetes.io/docs/concepts/workloads/)，Pod、Deployment、Job 等工作负载概览  
资料3：[B 站：Kubernetes 入门指南（中文字幕）](https://www.bilibili.com/video/BV1hZ421a7n7/)，只看 Node、Pod、Deployment、Service、kubectl 部分

学习内容：期望状态、控制循环、自愈、调度；container、Pod、Deployment、Service 的关系；Compose 与编排平台的边界。  
实践/验收：启用 Docker Desktop Kubernetes 或 minikube；完成 Kubernetes Basics 的创建、查看、暴露、扩容和更新模块。

### 第十六天：Pod 与 Deployment

资料1：[Pods](https://kubernetes.io/docs/concepts/workloads/pods/)，Pod 生命周期和共享网络/存储  
资料2：[Deployments](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)，副本、滚动更新与回滚  
资料3：[Using kubectl to create a Deployment](https://kubernetes.io/docs/tutorials/kubernetes-basics/deploy-app/deploy-intro/)，官方部署练习

学习内容：Pod 是可替换的部署单元；Deployment 管理 ReplicaSet 和副本；不要依赖 Pod 名/IP 或本地可写层。  
实践/验收：为自己的 C# API 编写 Deployment YAML，启动两个副本；手动删除一个 Pod，观察自动补齐；更新镜像 tag 并观察滚动发布。

### 第十七天：Service、DNS 与 Ingress

资料1：[Kubernetes Service](https://kubernetes.io/docs/concepts/services-networking/service/)，稳定端点、选择器和 DNS  
资料2：[Services, Load Balancing, and Networking](https://kubernetes.io/docs/concepts/services-networking/)，Kubernetes 网络模型  
资料3：[Ingress](https://kubernetes.io/docs/concepts/services-networking/ingress/)，外部 HTTP 路由概念

学习内容：Pod IP 易变，Service 提供稳定发现；ClusterIP、LoadBalancer、Ingress 与容器端口的关系；这些概念如何对应 Azure Container Apps 的内部/外部 ingress。  
实践/验收：为 API 创建 ClusterIP Service；从临时容器用服务名访问；再选择 port-forward 或本地 Ingress 暴露 API。

### 第十八天：配置、Secret 与声明式资源

资料1：[Kubernetes ConfigMaps](https://kubernetes.io/docs/concepts/configuration/configmap/)，非敏感配置注入  
资料2：[Kubernetes Secrets](https://kubernetes.io/docs/concepts/configuration/secret/)，Secret 的使用和安全边界  
资料3：[Declarative Management of Kubernetes Objects](https://kubernetes.io/docs/tasks/manage-kubernetes-objects/declarative-config/)，声明式 YAML

学习内容：镜像与配置分离；ConfigMap、Secret 的文件/环境变量注入；base64 不是加密；声明式期望状态与命令式操作的取舍。  
实践/验收：把 API 配置移入 ConfigMap、敏感值移入 Secret；修改配置并滚动重启；确保 YAML 仓库中没有真实凭据。

### 第十九天：探针、资源请求与限制

资料1：[Kubernetes liveness, readiness, startup probes](https://kubernetes.io/docs/concepts/workloads/pods/probes/)，三类探针的行为  
资料2：[Resource Management for Pods and Containers](https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/)，CPU/内存 request 与 limit  
资料3：[ASP.NET Core health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)，C# 探针端点实现

学习内容：CPU throttling、OOM、调度请求；错误探针如何造成重启风暴；readiness 不应等同于 liveness。  
实践/验收：配置三类探针与资源 request/limit；故意填错路径观察事件；恢复后制造一次超内存或启动延迟实验并解释现象。

### 第二十天：扩缩容、滚动发布、回滚和 Job

资料1：[Scaling an application](https://kubernetes.io/docs/tutorials/kubernetes-basics/scale/scale-intro/)，副本扩缩容  
资料2：[Update an application](https://kubernetes.io/docs/tutorials/kubernetes-basics/update/update-intro/)，滚动更新和回滚  
资料3：[Kubernetes Jobs](https://kubernetes.io/docs/concepts/workloads/controllers/job/)，运行到完成的任务

学习内容：长运行服务与一次性任务；副本、水平扩容、版本发布、回滚；为后续 ACA revisions、scale 和 jobs 建立通用模型。  
实践/验收：把 API 扩到三个副本，发布一个故障镜像并回滚；创建一个 C# Console Job，执行后正常退出并查看日志。

### 第二十一天：编排知识收束与迁移对照

资料1：[Comparing Azure container options](https://learn.microsoft.com/en-us/azure/container-apps/compare-options)，ACA、ACI、AKS、App Service 等选型  
资料2：[OCI FAQ](https://opencontainers.org/faq/)，重温开放标准和互操作目标

学习内容：理解“应用容器化”和“平台编排”是两层问题；知道何时选择 Compose、ACA、AKS、ACI，而不是把所有场景都放进 Kubernetes。  
实践/验收：制作一张对照表：Docker container、Compose service、Kubernetes Pod/Deployment/Service、ACA replica/revision/app/environment；写出三个适合 ACA 和三个更适合 AKS 的场景。

## 第四阶段：Azure Container Apps 核心能力（第 22～28 天）

### 第二十二天：Azure Container Apps 架构与选型

资料1：[Azure Container Apps overview](https://learn.microsoft.com/en-us/azure/container-apps/overview)，ACA 官方总览  
资料2：[Azure Container Apps environments](https://learn.microsoft.com/en-us/azure/container-apps/environment)，环境作为安全、网络和日志边界  
资料3：[Introduction to ACA for .NET Developers](https://learn.microsoft.com/en-us/shows/azure-developers/introduction-to-azure-container-apps-for-dotnet-developers-getting-started-with-dotnet-on-aca)，微软 .NET 视频系列第 1 集

学习内容：Environment、Container App、Revision、Replica、Job、workload profile；ACA 基于 Kubernetes 能力但不开放底层 Kubernetes API。  
实践/验收：在纸上画出一个环境内两个应用、多个修订版和副本的关系；根据第二十一天场景重新做一次选型判断。

### 第二十三天：创建 ACR，并部署第一个 C# 容器应用

资料1：[Create an Azure Container Registry with Azure CLI](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-get-started-azure-cli)，ACR 创建、推送和拉取  
资料2：[Deploy with `az containerapp up`](https://learn.microsoft.com/en-us/azure/container-apps/containerapp-up)，从镜像或源码快速部署  
资料3：[Microsoft Learn: Deploy and manage apps on Azure Container Apps](https://learn.microsoft.com/en-us/training/paths/deploy-manage-apps-azure-container-apps/)，完整官方学习路径

学习内容：ACR、repository、tag、digest；Environment 与 App 的创建；CLI、Portal、YAML 的角色；目标端口和 ingress。  
实践/验收：为自己的 API 构建版本化镜像，推送到 ACR，部署到 ACA；确认 FQDN 可访问、日志可读取；记录所有命令并在结束时检查费用。

### 第二十四天：容器配置、CPU/内存、sidecar 与 init container

资料1：[Containers in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/containers)，容器模板、注册表、多容器、资源组合  
资料2：[Microsoft Learn：Deploy containers to Azure Container Apps](https://learn.microsoft.com/en-us/training/modules/deploy-containers-azure-container-apps/)，环境、YAML、配置和注册表认证练习

学习内容：`properties.template`、镜像、命令、参数、环境变量、CPU/内存；应用级与修订版级设置；sidecar/init container 的适用边界。  
实践/验收：导出应用 YAML，改用 YAML 更新 CPU/内存和环境变量；验证修改是否创建新 revision，并写下原因。

### 第二十五天：Ingress、内部服务发现与多服务通信

资料1：[Ingress in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)，内部/外部入口、HTTP/TCP、TLS、FQDN  
资料2：[Communicate between microservices in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/communicate-between-microservices)，同一环境中的服务通信  
资料3：[Configure ingress](https://learn.microsoft.com/en-us/azure/container-apps/ingress-how-to)，CLI、Portal 和模板配置

学习内容：external/internal/disabled ingress、target port、HTTPS、应用名发现；公网入口只留在边缘 API，内部服务不直接暴露。  
实践/验收：部署 `Gateway.Api` 和 `Catalog.Api` 两个 C# 服务；仅 Gateway 外部可见，Catalog 使用内部 ingress；Gateway 通过应用名调用 Catalog。

### 第二十六天：配置、Secret、Key Vault 与托管身份

资料1：[Manage secrets in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets)，应用密钥和 Key Vault 引用  
资料2：[Security overview for Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/security)，托管身份、RBAC 和密钥管理  
资料3：[ACA image pull with managed identity](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull)，不用管理员密码拉取 ACR 镜像

学习内容：系统分配与用户分配身份；最小权限；Secret 引用与环境变量；Key Vault 引用；ACR `AcrPull` 权限。  
实践/验收：禁用 ACR 管理员账户；让 ACA 通过托管身份拉取镜像；为应用添加一个 Key Vault 引用密钥；确认仓库、YAML 和日志中没有真实密钥。

### 第二十七天：健康探针、日志和故障排查

资料1：[Health probes in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/health-probes)，ACA 对 startup、liveness、readiness 的支持和限制  
资料2：[Application logging in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/logging)，console、system、HTTP 日志  
资料3：[Diagnose and solve problems](https://learn.microsoft.com/en-us/azure/container-apps/diagnose-solve)，镜像拉取、启动、探针、网络诊断

学习内容：ACA 默认探针与自定义探针；stdout/stderr 日志；revision provisioning、ErrImagePull、ContainerCrashing 和端口错误的诊断顺序。  
实践/验收：配置三个 C# 健康端点和 ACA 探针；依次制造错误镜像名、错误端口、错误探针路径，使用系统日志和诊断工具定位并修复。

### 第二十八天：Revisions、流量拆分、蓝绿与回滚

资料1：[Revisions in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/revisions)，单/多修订版模式和 revision-scope 变更  
资料2：[Traffic splitting](https://learn.microsoft.com/en-us/azure/container-apps/traffic-splitting)，按权重分配流量  
资料3：[Blue-green deployment](https://learn.microsoft.com/en-us/azure/container-apps/blue-green-deployment)，标签、切流和回滚

学习内容：revision 不可变快照；single/multiple 模式；标签 URL；0/100、10/90、50/50 灰度；失败 revision 不应接流量。  
实践/验收：发布返回不同版本号的 v1/v2；给新 revision 先分 10% 流量，观察后切到 100%；再故意发布故障版并回滚到已知良好 revision。

## 第五阶段：扩缩容、作业、Dapr、监控与运营（第 29～35 天）

### 第二十九天：HTTP 与资源指标扩缩容

资料1：[Scaling in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)，副本上下限、HTTP/TCP/CPU/内存/自定义规则  
资料2：[KEDA concepts](https://keda.sh/docs/latest/concepts/)，事件驱动扩缩容的底层通用模型  
资料3：[Microsoft Learn：Scale containers in Azure Container Apps](https://learn.microsoft.com/en-us/training/modules/scale-containers-azure-container-apps/)，KEDA 和 revision 模式练习

学习内容：min/max replicas、scale to zero、冷启动、轮询和冷却；多个规则为 OR；没有 ingress/事件规则时从零恢复的问题。  
实践/验收：给 Gateway 配置 HTTP 扩缩容；分别测试 `minReplicas=0` 与 `1` 的冷启动；观察副本数量和延迟，写下生产选择。

### 第三十天：事件驱动扩缩容与 KEDA

资料1：[Azure Container Apps custom scale rules](https://learn.microsoft.com/en-us/azure/container-apps/scale-app#custom)，队列和自定义 KEDA scaler  
资料2：[KEDA scalers](https://keda.sh/docs/latest/scalers/)，事件源、触发元数据与身份  
资料3：[Scale Dapr applications with KEDA](https://learn.microsoft.com/en-us/azure/container-apps/dapr-keda-scaling)，队列积压驱动副本扩容示例

学习内容：用业务积压而不是只用 CPU 扩容；队列长度、每副本并发、最大副本；触发器身份和密钥。  
实践/验收：给 C# Worker 连接 Azure Service Bus 或 Storage Queue，配置队列长度扩缩容；批量放入消息，观察 0→N→0；确认消息处理具备幂等性或至少能解释重复处理风险。

### 第三十一天：Azure Container Apps Jobs

资料1：[Jobs in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/jobs)，手动、计划和事件触发作业  
资料2：[Tutorial: Deploy an event-driven job](https://learn.microsoft.com/en-us/azure/container-apps/tutorial-event-driven-jobs)，官方端到端练习  
资料3：[KEDA Scaling Jobs](https://keda.sh/docs/latest/concepts/scaling-jobs/)，事件驱动作业的可迁移原理

学习内容：App 与 Job 的长运行/运行到完成区别；execution、replica、重试、超时、并行度；何时每条消息一个 Job，何时使用常驻 Worker。  
实践/验收：把第二十天的 C# Console Job 部署成手动作业，再改成计划或事件触发；查看 execution 历史、退出码和日志。

### 第三十二天：Dapr 服务调用——可选但推荐的可移植抽象

资料1：[Dapr quickstarts](https://docs.dapr.io/getting-started/quickstarts/)，Dapr 官方快速入门  
资料2：[C# Service Invocation quickstart](https://docs.dapr.io/getting-started/quickstarts/serviceinvocation-quickstart/)，两个 C# 服务的可靠调用  
资料3：[Dapr on Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/dapr-overview)，ACA 托管的 Dapr APIs 和限制

学习内容：sidecar、app-id、服务发现、mTLS、重试；Dapr 是可选的分布式应用抽象，不是所有 ACA 应用的必需层。  
实践/验收：从 [dapr/quickstarts](https://github.com/dapr/quickstarts)（约 1.1k stars）只完成 C# service invocation；比较“直接 ACA 服务名调用”和“Dapr 调用”的复杂度与收益。

### 第三十三天：Dapr Pub/Sub 与异步通信

资料1：[Dapr Publish and Subscribe quickstart](https://docs.dapr.io/getting-started/quickstarts/pubsub-quickstart/)，C# 发布者与订阅者  
资料2：[Dapr Pub/Sub overview](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/)，消息代理抽象、至少一次投递  
资料3：[dapr/dapr](https://github.com/dapr/dapr)，Dapr 主项目（约 25.8k stars，用于理解生态，不要求读源码）

学习内容：同步调用与异步消息的取舍；至少一次投递、幂等、dead-letter；组件替换体现技术互通。  
实践/验收：完成 C# Pub/Sub quickstart；让 Worker 对重复订单 ID 不重复写入；只在确有收益时再部署到 ACA。

### 第三十四天：可观测性、指标与 Log Analytics

资料1：[Observability in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/observability)，日志流、控制台、指标、告警  
资料2：[Monitor logs with Log Analytics](https://learn.microsoft.com/en-us/azure/container-apps/log-monitoring)，系统、控制台、HTTP 日志和查询  
资料3：[Monitor ACA metrics](https://learn.microsoft.com/en-us/azure/container-apps/metrics)，副本、CPU、内存、请求等指标

学习内容：日志、指标、追踪三者区别；按 app/revision/replica 过滤；结构化日志、关联 ID、5xx 和延迟；基础告警。  
实践/验收：为 C# 服务输出结构化日志和 correlation ID；写三条 Log Analytics 查询：最近错误、各 revision 请求/日志、启动失败；创建一个错误率或副本异常告警。

### 第三十五天：成本、容量、安全与日常运营复盘

资料1：[Azure Container Apps plan types](https://learn.microsoft.com/en-us/azure/container-apps/plans)，Consumption 与 Dedicated 计划  
资料2：[Workload profiles](https://learn.microsoft.com/en-us/azure/container-apps/workload-profiles-overview)，Consumption、Dedicated 与 Flex 的资源模型  
资料3：[Azure Container Apps Well-Architected guidance](https://learn.microsoft.com/en-us/azure/well-architected/service-guides/azure-container-apps)，可靠性、安全、成本、运营和性能清单

学习内容：scale-to-zero、冷启动与成本；资源 right-sizing；最小副本与可用性；托管身份、只允许 HTTPS、内部服务不暴露、镜像更新和告警。  
实践/验收：给当前系统写一页“生产就绪评审”：计划选择、CPU/内存、min/max replicas、区域、探针、身份、日志、告警、发布和回滚；删除不再使用的资源并核对当月成本。

## 第六阶段：C# 综合项目与技术迁移（第 36～42 天）

### 第三十六天：阅读高星 .NET 容器项目，而不是盲目复制

资料1：[dotnet/eShop](https://github.com/dotnet/eShop)，微软当前 .NET 电商参考应用（约 10.8k stars，当前主分支使用 .NET 10/Aspire）  
资料2：[已归档的 eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)，历史高星容器微服务项目（约 24.3k stars，仅用于对照，不作为新项目基线）  
资料3：[.NET eShop architecture](https://learn.microsoft.com/en-us/dotnet/architecture/cloud-native/introduce-eshoponcontainers-reference-app)，架构阅读材料

学习内容：识别服务边界、容器资源、配置、健康、可观测性和本地编排；区分“容器知识”与业务架构细节，不要求完整重写 eShop。  
实践/验收：在本地运行当前 eShop；只追踪一次请求经过哪些进程/容器；选出与你结课项目直接相关的 3 个做法和不准备引入的 3 个复杂度。

### 第三十七天：设计结课项目

资料1：[Azure Container Apps architecture best practices](https://learn.microsoft.com/en-us/azure/well-architected/service-guides/azure-container-apps)，架构检查清单  
资料2：[dotnet/eShop](https://github.com/dotnet/eShop)，C# 服务组织方式参考

学习内容：设计一个不过度复杂但覆盖核心能力的系统。建议题目：`OrderHub`，包含 `Gateway.Api`、`Orders.Api`、`OrderProcessor.Worker`、数据库和消息队列。  
实践/验收：完成架构图、服务边界、端口、配置、Secret、数据持久化、健康端点、日志、扩缩容信号、发布与回滚方案；明确数据库和队列使用 Azure 托管服务，不把有状态数据库硬塞进 ACA 作为生产方案。

### 第三十八天：完成本地容器化与 Compose

资料1：[Docker Compose Quickstart](https://docs.docker.com/compose/gettingstarted/)，多容器编排复查  
资料2：[docker/awesome-compose](https://github.com/docker/awesome-compose)，高星 Compose 结构参考

学习内容：把结课项目先在本地完整跑通，再进入云平台；确保云问题与应用问题可分离。  
实践/验收：为所有 C# 服务写多阶段、非 root Dockerfile；Compose 启动 API、Worker、数据库和本地消息代理；加入 healthcheck、独立网络、持久卷和配置；执行一次端到端下单流程。

### 第三十九天：以可重复方式部署到 Azure Container Apps

资料1：[Deploy to ACA using Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/container-apps-workflows)，`azd` 的镜像与 revision 部署策略  
资料2：[Explore `azd init`](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/azd-init-workflow)，为现有项目生成 Azure 托管定义  
资料3：[Explore `azd up`](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/azd-up-workflow)，Provision、package、deploy 工作流

学习内容：可重复部署、环境参数、基础设施定义；`azd`/Bicep 只学部署本项目所需部分，不扩展成通用 IaC 课程。  
实践/验收：用 `azd`、Bicep 或 ACA YAML 部署同一环境中的多个服务；内部/外部 ingress 正确；ACR 拉取使用身份；部署文档能让另一个环境从零复现。

### 第四十天：自动构建与部署

资料1：[Build and deploy from a GitHub repository to ACA](https://learn.microsoft.com/en-us/azure/container-apps/quickstart-repo-to-cloud)，官方 repo-to-cloud 流程  
资料2：[Azure/container-apps-deploy-action](https://github.com/Azure/container-apps-deploy-action)，官方 ACA GitHub Action 及 YAML 样例  
资料3：[Getting Started with .NET on ACA 视频系列](https://aka.ms/getting-started-dotnet-aca)，只看 CI/CD 一集；配套仓库已于 2026-07 归档，视频只作概念演示

学习内容：测试 → 构建 → 扫描 → 推送不可变版本 → 创建 revision → 验证 → 切流；优先使用 OIDC/联合身份而不是长期客户端密钥。  
实践/验收：建立 GitHub Actions；提交代码后自动测试、构建和部署新 revision；生产流量不自动全量切换，先由标签 URL 验证或小流量发布。

### 第四十一天：生产故障演练

资料1：[Troubleshoot container start failures](https://learn.microsoft.com/en-us/azure/container-apps/troubleshoot-container-start-failures)，启动、镜像、端口、资源和探针问题  
资料2：[Troubleshoot health probe failures](https://learn.microsoft.com/en-us/azure/container-apps/troubleshoot-health-probe-failures)，探针故障诊断  
资料3：[Revisions](https://learn.microsoft.com/en-us/azure/container-apps/revisions)，回滚和 revision 生命周期复查

学习内容：建立固定排障顺序：revision 状态 → 系统日志 → 镜像与身份 → 启动命令/端口 → 配置/Secret → 探针 → CPU/内存 → 网络/依赖。  
实践/验收：至少完成五项演练：错误 tag、缺失 Secret、错误端口、readiness 失败、内存不足；每项记录症状、证据、根因、修复和防复发措施；完成一次 10% 灰度与一分钟内回滚。

### 第四十二天：跨运行时验证与结业评估

资料1：[Podman Getting Started](https://podman.io/docs)，另一种 OCI 容器引擎  
资料2：[Podman introduction](https://docs.podman.io/en/stable/Introduction.html)，Dockerfile/Containerfile 和 OCI 互操作  
资料3：[containers/podman](https://github.com/containers/podman)，高星开源容器引擎（约 32.7k stars）

学习内容：用不同引擎验证你学到的是容器标准和应用设计，而不是只记住 Docker 命令；总结 Compose、Kubernetes 与 ACA 的概念映射。  
实践/验收：可行时用 Podman 构建或运行同一 C# 镜像；或者把 ACR 中同一 digest 的镜像运行在本地 Kubernetes。完成最终答辩文档：架构、镜像、网络、存储、Secret、探针、扩缩容、revision、Job、监控、成本、故障演练与迁移结论。

## GitHub 项目使用顺序与选择理由

> 星标为 2026-08-25 页面显示的近似值，之后会变化；星标只代表社区关注度，不等同于教程质量。

1. [docker/awesome-compose](https://github.com/docker/awesome-compose)（约 46.2k stars）：第 10、14、38 天使用。它是高星、低门槛的多容器样例库；选做 ASP.NET + SQL Server，不需要遍历其他语言样例。官方明确说明这些样例面向本地开发，不可直接当生产配置。
2. [dotnet/eShop](https://github.com/dotnet/eShop)（约 10.8k stars）：第 36～37 天选择性阅读。它是微软当前维护的 C# 参考应用，适合学习成熟项目如何组织服务和容器，但不要求完整重写。
3. [dotnet/dotnet-docker](https://github.com/dotnet/dotnet-docker)（约 4.9k stars）：第 1、7 天使用。它是微软官方 .NET 镜像和样例来源，比随机博客中的 Dockerfile 更适合作为基线。
4. [dapr/dapr](https://github.com/dapr/dapr)（约 25.8k stars）与 [dapr/quickstarts](https://github.com/dapr/quickstarts)（约 1.1k stars）：第 32～33 天只做 C# service invocation 和 pub/sub；不阅读 Dapr 主仓库源码。
5. [podman-container-tools/podman](https://github.com/containers/podman)（约 32.7k stars）：第 42 天用于 OCI 可移植性验证，不把路线扩展成 Podman 管理课程。
6. [dotnet-architecture/eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers)（约 24.3k stars）：高星但已归档，只作历史对照；新项目使用 `dotnet/eShop`。

## 阶段验收标准

### 第 7 天后

- 能独立为 ASP.NET Core 写出多阶段、非 root、缓存友好的 Dockerfile。
- 能解释镜像、容器、registry、OCI、namespace 和 cgroup 的关系。

### 第 14 天后

- 能用 Compose 运行 C# API + Worker/数据库。
- 能处理 DNS、端口、卷、配置、密钥、健康、日志、资源限制和漏洞扫描。

### 第 21 天后

- 能解释 Pod、Deployment、Service、探针、资源、扩缩容和 Job。
- 能把 Compose、Kubernetes 与 ACA 的概念进行映射，而不是死记某个平台命令。

### 第 28 天后

- 能把 C# 多服务应用部署到 ACA。
- 能配置内部/外部 ingress、托管身份、Key Vault、健康探针、修订版、灰度和回滚。

### 第 35 天后

- 能配置 HTTP/事件驱动扩缩容和 Container Apps Jobs。
- 能用日志、指标和诊断工具定位常见启动、入口、探针和资源问题。
- 能说明 Dapr 何时值得使用、何时会增加不必要复杂度。

### 第 42 天结业

- 从代码提交到镜像、ACR、ACA revision 和灰度发布形成可重复流程。
- 能在 15 分钟内定位并处理错误镜像、端口、Secret、探针或资源限制导致的故障。
- 同一 OCI 镜像能在 Docker、Podman 或本地 Kubernetes 中至少两种环境运行。
- 能针对一个新需求合理选择 ACA、AKS、ACI、App Service 或本地 Compose，并解释取舍。

## 不需要在本路线中深入的内容

- Kubernetes 控制平面安装、高可用集群、etcd 运维、CNI/CSI 插件开发、Operator、Helm 深度使用。
- Istio/完整 Service Mesh、复杂 API Gateway、数据库集群管理、全面微服务领域建模。
- Terraform/Bicep 语言体系的完整学习。结课项目只掌握可重复部署所需的最小部分。
- 为了“微服务”而拆分服务。先保证容器边界、可观测性、发布和恢复能力，再讨论架构拆分。

## 建议保留的最终成果

学习仓库至少包含：

```text
container-learning-csharp/
├─ notes/                 # 每日笔记、概念映射、故障复盘
├─ src/                   # C# API 与 Worker
├─ tests/                 # 单元/集成测试
├─ deploy/
│  ├─ compose/            # 本地多容器配置
│  ├─ kubernetes/         # 最小 Kubernetes YAML
│  └─ azure/              # ACA YAML、Bicep 或 azd 配置
├─ .github/workflows/     # 测试、构建、扫描、部署
├─ Dockerfile
├─ .dockerignore
└─ README.md              # 从零运行、部署、回滚与清理说明
```

这套成果比“看过多少视频”更能证明你真正掌握了容器和 Azure Container Apps。
