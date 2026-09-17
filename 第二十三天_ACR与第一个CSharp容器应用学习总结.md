# 第二十三天：创建 ACR，部署第一个 C# 容器应用

> 已根据共享会话中第 23 天的 8 道验收题补充第 8 节「错题与薄弱点」，不记录其他天的题目和实验过程性问题。命令是模板，中文部分需要替换为实际值；不使用变量赋值，便于查阅。

## 1. ACR 存镜像，ACA 运行镜像

| 名称 | 作用 | 例子 |
| --- | --- | --- |
| ACR（容器注册表） | 存放镜像，不负责运行 API | 一个公司或项目的镜像库 |
| repository（镜像仓库） | ACR 内的一组相关镜像 | `study-api` |
| tag（标签） | 给某个镜像版本起的名字，可重新指向其他内容 | `v1`、`v2` |
| digest（摘要） | 按内容计算的标识，内容变了，摘要也变 | `sha256:...` |
| ACA Environment（环境） | 为一组应用提供共同的网络边界等运行基础 | 测试环境 |
| Container App（容器应用） | 一个可部署、配置入口和扩缩容的应用资源 | C# API |

镜像地址有两种常见写法：

```text
ACR登录服务器/仓库名字:版本标签
ACR登录服务器/仓库名字@sha256:摘要值
```

`v1` 也是可变 tag，并非天生不可覆盖；`latest` 只是标签名，不保证它就是最新发布版本。学习时每次发布使用新标签，正式发布还可以用 digest 固定内容。

`docker tag` 只增加镜像引用，`docker push` 才上传。第一次推送时会创建对应 repository，不必先去 Portal 手动创建。[ACR 概念](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-concepts)

ACR 的**资源名**与 **loginServer** 不是同一个值。登录服务器可能包含自动生成的哈希，应查询后使用，不要总是假定为 `ACR名字.azurecr.io`。[ACR 快速入门](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-get-started-azure-cli)

一个 Environment 可以放多个 App。App 也不是某一个运行中的容器：更新镜像会创建新的修订版本（Revision），运行时可以有多个副本。Environment 不是 C# 的 `ASPNETCORE_ENVIRONMENT` 配置。[ACA Environment](https://learn.microsoft.com/en-us/azure/container-apps/environment)

## 2. 本地推送权限与 ACA 拉取权限分开

- **你推送**：本机登录 Azure 和 ACR，你的身份需要推送权限。
- **ACA 拉取**：ACA 自己需要访问私有 ACR 的身份和权限，不能借用你本机的 Docker 登录状态。
- 优先用托管身份拉取镜像，不把 ACR 管理员密码写进命令、YAML、Git。

下面的实验使用 ACR 的 `rbac` 权限模式：推送常用 `AcrPush`，拉取用 `AcrPull`。已有 ACR 若采用 `rbac-abac` 模式，应使用对应的仓库 Reader/Writer 等角色，不能机械照搬这两个角色。[ACR 权限](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-rbac-built-in-roles-overview)

身份权限和网络可达性都要满足；有 `AcrPull` 不代表能绕过 ACR 的防火墙。托管身份拉取还要求 ACR 允许 ARM audience token。[托管身份拉取镜像](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull)

## 3. 目标端口与 ingress

- `target-port`：API **在容器内实际监听的端口**，不是你希望浏览器访问的端口。
- HTTP ingress：平台接收外部请求，再转给容器目标端口。
- FQDN：平台分配的完整域名，通常通过 `https://域名` 访问。

例如 API 监听 `8080`，设置 `--target-port 8080`，用户仍访问 `https://应用域名`，通常不需要加 `:8080`。

| 配置 | 通常允许谁访问 |
| --- | --- |
| App ingress 为 `internal` | 同一个 ACA Environment 内的其他应用 |
| App ingress 为 `external` | Environment 外部的客户端，具体受环境网络边界限制 |

**external 不总是等于公网。**若 Environment 本身只有内部网络入口，App 设置 external 也不会变成公网应用。本日公网验收使用可公开访问的 Environment，并给 App 开启 external ingress。[Ingress](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)

`EXPOSE 8080` 不会让 C# 自动监听 8080，也不会单独开放公网入口。应确认 Kestrel 监听类似 `0.0.0.0:8080` 的地址，而不是只监听容器内 localhost。ACA 当前要求兼容 Linux x86-64 的容器镜像。[ACA 容器要求](https://learn.microsoft.com/en-us/azure/container-apps/containers)

## 4. CLI、Portal、YAML 各做什么？

| 工具 | 本日用途 |
| --- | --- |
| Azure CLI | 执行创建、部署、查询命令，方便重复使用 |
| Azure Portal | 图形化查看资源、入口、日志、权限和费用 |
| YAML | 保存可评审、可重复应用的配置；文件本身不会自动部署 |

它们管理的是同一批 Azure 资源。ACA YAML 不是 Kubernetes Deployment YAML，不能直接拿去 `kubectl apply`。

`az containerapp up` 是快捷流程，能创建或复用 App、Environment 等资源；使用 `--source` 时还可构建并推送镜像。它可能创建额外的 ACR、Log Analytics 工作区，所以要明确资源组和环境，并检查创建结果。本日采用“自己 build/push，再用 `--image` 部署”，方便理解每一步。[containerapp up](https://learn.microsoft.com/en-us/azure/container-apps/containerapp-up)

## 5. 从构建到部署：常用命令

以下在 PowerShell 中使用。`-n` 是名字，`-g` 是资源组，`-l` 是区域；`--query` 选取返回字段，`-o table` 便于看表格，`-o tsv` 只取字段值。带空格的实际路径或名字要加引号。

### ① 登录、选择订阅

```powershell
az login
az account list -o table
az account set --subscription 订阅ID或名字
az account show -o table
az extension add --name containerapp --upgrade
```

首次使用相关服务时注册资源提供程序：

```powershell
az provider register --namespace Microsoft.App --wait
az provider register --namespace Microsoft.OperationalInsights --wait
az provider register --namespace Microsoft.ContainerRegistry --wait
```

### ② 创建资源组和 ACR

```powershell
az group create -n 资源组名字 -l 区域名字
az acr create -n ACR名字 -g 资源组名字 -l 区域名字 --sku Basic --role-assignment-mode rbac
az acr show -n ACR名字 -g 资源组名字 --query loginServer -o tsv
az acr login -n ACR名字
```

ACR 名需满足全局唯一和命名规则；Basic 适合小型学习实验，但不是免费注册表。`az acr login -n` 填资源名；后续 Docker 镜像地址用查询得到的完整 loginServer。[ACR CLI](https://learn.microsoft.com/en-us/cli/azure/acr)

### ③ 构建版本化镜像，推送并确认

```powershell
docker build --platform linux/amd64 -f Dockerfile路径 -t 本地镜像名字:版本标签 构建上下文目录
docker tag 本地镜像名字:版本标签 ACR登录服务器/仓库名字:版本标签
docker push ACR登录服务器/仓库名字:版本标签
az acr repository list -n ACR名字 -o table
az acr repository show-tags -n ACR名字 --repository 仓库名字 -o table
az acr repository show -n ACR名字 --image 仓库名字:版本标签 --query digest -o tsv
```

构建上下文目录是 Docker 可以读取文件的范围，不一定等于 Dockerfile 所在目录。记录本次 tag 和仓库返回的 digest；推送成功不等于 ACA 已部署。[ACR 仓库命令](https://learn.microsoft.com/en-us/cli/azure/acr/repository)

需要验证从 ACR 拉取时：

```powershell
docker pull ACR登录服务器/仓库名字:版本标签
```

### ④ 创建 Environment，部署 App

```powershell
az containerapp env create -n 环境名字 -g 资源组名字 -l 区域名字
az containerapp up -n 容器应用名字 -g 资源组名字 --environment 环境名字 --image ACR登录服务器/仓库名字:版本标签 --registry-server ACR登录服务器 --system-assigned --registry-identity system --ingress external --target-port 应用监听端口
```

`--system-assigned` 为 App 配置系统分配的托管身份，`--registry-identity system` 指定用它拉取镜像。CLI 会尝试为该身份分配 `AcrPull`，执行者需要角色分配权限；不能把自动授权当成无条件成功。此写法需要支持这些参数的 Container Apps 扩展。[部署命令参数](https://learn.microsoft.com/en-us/cli/azure/containerapp#az-containerapp-up)

核对身份、ACR 范围和拉取授权时用：

```powershell
az containerapp show -n 容器应用名字 -g 资源组名字 --query identity.principalId -o tsv
az acr show -n ACR名字 -g 资源组名字 --query id -o tsv
az role assignment list --assignee 身份principalId --scope ACR资源ID --include-inherited -o table
```

需要显式授权时，由有权限的人执行下面这条；`身份principalId` 不是 App 名字：

```powershell
az role assignment create --assignee-object-id 身份principalId --assignee-principal-type ServicePrincipal --role AcrPull --scope ACR资源ID
```

### ⑤ 验证 FQDN、运行状态和日志

```powershell
az containerapp show -n 容器应用名字 -g 资源组名字
az containerapp show -n 容器应用名字 -g 资源组名字 --query properties.configuration.ingress.fqdn -o tsv
az containerapp revision list -n 容器应用名字 -g 资源组名字 -o table
Invoke-RestMethod 'https://应用FQDN/实际API路径'
az containerapp logs show -n 容器应用名字 -g 资源组名字 --type console --tail 50 --follow
az containerapp logs show -n 容器应用名字 -g 资源组名字 --type system --tail 50 --follow
```

- console：程序输出，例如启动日志、请求日志、异常。
- system：平台事件，例如镜像拉取、容器启动和修订版本运行情况。
- `--follow` 持续查看，按 `Ctrl+C` 退出；多副本时可按需指定 `--revision 修订版本名字 --replica 副本名字 --container 容器名字`。

**有 FQDN 不等于 API 已正常工作。**还要实际访问一个已实现的接口。实时日志也不等于永久保存的历史日志，历史查询取决于日志存储配置。[日志流](https://learn.microsoft.com/en-us/azure/container-apps/log-streaming)

### ⑥ 发布新版本与 YAML 更新

新版本先 build、push 新标签，然后：

```powershell
az containerapp update -n 容器应用名字 -g 资源组名字 --image ACR登录服务器/仓库名字:新版本标签
```

推送新 tag 不会自动修改 App 的镜像配置。更新后重新检查修订版本、流量和接口；若开启了多修订版本模式，不要假定流量已经全部切到新版。

已有 App 使用 ACA 格式的 YAML 更新：

```powershell
az containerapp update -n 容器应用名字 -g 资源组名字 --yaml 配置文件路径
```

YAML 中只保留应管理的配置，不提交真实密码或令牌。[App 更新命令](https://learn.microsoft.com/en-us/cli/azure/containerapp#az-containerapp-update)

## 6. 结束时检查费用

先列出资源组里的资源，别只看 App：

```powershell
az resource list -g 资源组名字 -o table
```

在 Portal 的 **Cost Management → Cost analysis** 中选择订阅、筛选本次资源组，查看实际费用并按资源分组。成本数据有延迟，当天显示 0 不代表不会计费。[成本分析](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/cost-analysis-built-in-views)、[成本数据延迟](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/understand-cost-mgt-data)

- Consumption 下，副本缩为 0 可停止该修订版本的副本资源消耗计费；`min-replicas=0` 只是允许缩零，不是立即停机。
- ACR、日志工作区、网络和其他资源仍可能收费；删除 App 也不会自动删除它们。
- 本日不背具体单价，按区域、SKU、运行方式核对实际价格。[ACA 计费](https://learn.microsoft.com/en-us/azure/container-apps/billing)、[ACR SKU](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus)

确认是实验专用资源组、没有需要保留的资源后，才执行：

```powershell
az group delete -n 实验专用资源组名字
az group exists -n 实验专用资源组名字
```

删除资源组会一并删除其中的 ACR 镜像、App、Environment 等资源；先保存需要的内容，不把它当成可随意撤销的操作。第二条返回 `false` 表示资源组已不存在，后续仍应核对延迟入账费用。

## 7. 本日验收清单

- [ ] 镜像有明确版本标签，ACR 中可查到 tag 与 digest。
- [ ] 能区分本地推送身份和 ACA 拉取身份。
- [ ] App 部署在预期 Environment，目标端口与 C# 实际监听一致。
- [ ] 通过 FQDN 访问实际 API 成功，能查看程序日志和平台日志。
- [ ] 保存实际执行命令、镜像版本和资源名称，不记录凭据。
- [ ] 检查了资源清单及费用，明确哪些保留、哪些删除。

延伸学习：[Deploy and manage apps on Azure Container Apps](https://learn.microsoft.com/en-us/training/paths/deploy-manage-apps-azure-container-apps/)。本日只需掌握上述部署流程，不必提前展开整条学习路径。

## 8. 第 23 天问答：错题与薄弱点

依据：[本次共享会话中的 8 道验收题](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)。下面区分「答错」与「回答不完整」，不把已答对的部分重复记成错题。

### ① 三种 `up` 部署方式不同（第 6 题，重点纠错）

原回答认为核心参数没有区别，并把「本地源码」判断为必须提前 build、push。

| 手里有什么 | 核心参数 | 谁准备镜像 |
| --- | --- | --- |
| 已在注册表中的镜像 | `--image` | 镜像已准备好，`up` 不替你构建、推送 |
| 本地源码 | `--source` | `up` 负责构建、推送和部署 |
| GitHub 仓库源码 | `--repo` | 创建 GitHub Actions 工作流，负责构建、推送和部署 |

记忆：**镜像用 image，本地源码用 source，GitHub 用 repo。**自己的 API 走 `--image` 路线时，先 build、push；如果直接使用别人已发布的镜像，不必自己再构建。[官方说明](https://learn.microsoft.com/en-us/azure/container-apps/containerapp-up)

以下是三选一的命令模板，不是依次执行；私有 ACR 还需补齐第 2 节的认证配置：

```powershell
az containerapp up -n 容器应用名字 -g 资源组名字 --environment 环境名字 --image ACR登录服务器/仓库名字:版本标签 --ingress external --target-port 应用监听端口
az containerapp up -n 容器应用名字 -g 资源组名字 --environment 环境名字 --source 本地源码目录 --ingress external --target-port 应用监听端口
az containerapp up -n 容器应用名字 -g 资源组名字 --environment 环境名字 --repo GitHub仓库地址 --ingress external --target-port 应用监听端口
```

### ② 部署链路正确，但命令要记准（第 5、7 题）

| 原回答中的问题 | 正确记法 |
| --- | --- |
| `.igrondocker`、`.bin` | 文件叫 `.dockerignore`；常见排除目录为 `bin`、`obj`、`.git` |
| 「资源组 server 名」 | 镜像地址使用 **ACR loginServer**，不是资源组名字 |
| `docker image tag` 只写目标地址 | 需要「源镜像」和「目标引用」两个参数；只是加名字，不修改镜像内容 |
| 推送前漏了 ACR 登录 | 本地 Docker 推送前先完成注册表认证，本日使用 `az acr login` |
| `az logs show` 或 `az container app logs show` | 正确为 `az containerapp logs show`，`containerapp` 中间没有空格 |

```powershell
az acr show -n ACR名字 -g 资源组名字 --query loginServer -o tsv
az acr login -n ACR名字
docker tag 本地镜像名字:版本标签 ACR登录服务器/仓库名字:版本标签
docker push ACR登录服务器/仓库名字:版本标签
az containerapp logs show -n 容器应用名字 -g 资源组名字 --type console --follow
```

`docker tag` 和 `docker image tag` 都是有效写法，问题在于漏参数，不在于多了 `image`。[Docker 命令说明](https://docs.docker.com/reference/cli/docker/image/tag/)

### ③ FQDN 能访问，不代表应用配置正确（第 7 题答错，第 8 题已纠正）

原判断：「FQDN 能打开，就证明应用配置没问题，所以先看网络入口。」

- **完全连接不上**：先查域名解析、网络和 ingress，再确认目标端口及应用是否在监听。
- **请求到达 API，但返回 HTTP 500**：先看应用日志，检查代码异常、配置和依赖服务。不要先认定是网络入口问题。
- **平台报镜像拉取或启动失败**：看 system 日志，再检查镜像、认证和启动配置。

这些是排查优先级，不是仅凭一个状态码就能确定原因。程序输出看 `--type console`，平台事件看 `--type system`。[日志说明](https://learn.microsoft.com/en-us/azure/container-apps/log-streaming)

### ④ YAML 保存配置，Git 保存修改历史（第 4 题）

原回答把 YAML 的核心价值理解成「知道谁在什么时候改了什么」。

- **YAML**：把希望应用使用的配置写成文件，便于重复部署。
- **Git**：记录提交历史和差异，帮助追踪谁改了什么。
- **CLI**：不仅用于临时调整，也用于脚本、自动部署和查询；它可以读取 YAML，两者不是二选一。

记忆：**YAML 写配置，Git 管历史，CLI 执行操作。**恢复旧 YAML 后，还要重新应用配置，云上资源才会跟着改变。

### ⑤ 私有 ACR 拉取失败，不只检查角色（第 8 题，回答不完整）

你回答「先检查角色权限是否给了」，方向正确，但需要连着核对：

1. App 是否启用了准备使用的托管身份。
2. 该身份是否有目标 ACR 的拉取权限（本日 RBAC 模式为 `AcrPull`）。
3. App 的 Registry 配置是否真的选择了这个身份。

**镜像地址说明拉什么，认证配置说明用谁的身份拉。**本机 `az acr login` 成功，不能替代 ACA 的拉取认证。如果使用用户名、密码认证，则检查对应凭据；认证无误后仍失败，再查镜像地址、标签和网络限制。[托管身份拉取说明](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull)

### ⑥ 概念表达补齐（第 1～3 题）

- **第 1 题，术语和漏答**：`study-security` 是 repository 名；`sha256:...` 叫 **digest（摘要）**。你已理解它用于标识内容，但漏答了 tag 覆盖：同一个 `v3` 可以改指向新内容；内容变化时 digest 改变。仅「重新 build」不保证内容一定变化。
- **第 2 题，Environment 范围**：不是「资源组内所有容器的共同边界」，而是加入该 Environment 的 App / Job 的运行边界；同一资源组可以有多个 Environment。[环境说明](https://learn.microsoft.com/en-us/azure/container-apps/environment)
- **第 2 题，独立服务**：你选择独立 App 是对的，但不要解释成「一个容器出错必定重启整个 App」。同一副本内的多个容器共同部署和扩缩容；独立微服务拆成不同 App，才能独立发布、扩缩容。不在本日展开 sidecar 细节。[多容器说明](https://learn.microsoft.com/en-us/azure/container-apps/containers)
- **第 3 题，访问条件**：目标端口判断已答对；需要收紧的是「知道 FQDN 就都能访问」。还要满足网络可达、身份认证和 IP 限制等条件，域名不是通行证。[Ingress 说明](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview)
