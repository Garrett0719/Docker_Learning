# 第六天：Dockerfile 与 `dotnet publish` 容器发布对比

> 学习目标：能为同一个 ASP.NET Core API 分别用 Dockerfile 和 .NET SDK 生成镜像；能比较两种镜像的层、体积、用户、入口和可定制性；能正确识别镜像名、Tag 与 Digest；能根据项目需求选择发布方式。

## 一、先记住结论

- **标准 .NET API，没有额外系统依赖**：优先考虑 SDK 内置容器发布，配置少、维护简单。
- **需要安装系统包、执行自定义构建步骤、使用 BuildKit Secret 或组织复杂多阶段构建**：使用 Dockerfile。
- 两种方式生成的都是 OCI 容器镜像。区别主要在于**构建过程由谁描述，以及能改到什么程度**。

## 二、两种发布方式对比

| 对比项 | Dockerfile | `dotnet publish /t:PublishContainer` |
| --- | --- | --- |
| 是否需要写 Dockerfile | 需要 | 不需要 |
| 上手成本 | 稍高 | 较低 |
| 可定制性 | 很高，可使用 `RUN`、多阶段、不同工具镜像等 | 可配置基础镜像、名称、Tag、用户、端口、环境变量等，但不能随意执行 `RUN` |
| 安装 `ffmpeg`、字体、证书等系统包 | 适合 | 不适合直接处理 |
| Node 前端与 .NET 后端联合构建 | 适合 | 不适合复杂流程 |
| 构建期密钥 | 可使用 BuildKit Secret | 没有 Dockerfile 的 BuildKit 构建步骤 |
| 构建缓存 | 可通过指令顺序精细控制层缓存 | 主要依赖 MSBuild/.NET 的构建过程，不使用 Dockerfile 层缓存写法 |
| 团队可读性 | 构建步骤清楚写在文件中 | 配置集中在项目文件或命令参数中 |
| 适合场景 | 有定制需求的生产项目 | 标准、简单的 .NET 服务 |

注意：不能只凭“层数少”判断镜像好坏。还要看最终体积、是否带入多余工具、默认用户以及维护成本。

## 三、SDK 内置容器发布

.NET SDK 8.0.200 及以上版本已内置容器镜像生成功能。生成镜像不需要 Dockerfile；如果要把镜像直接装入本机 Docker，则本机 Docker 需要正在运行。也可以输出归档文件或直接推送到镜像仓库。

### 1. 生成本地镜像

```powershell
dotnet publish -c Release --os linux --arch x64 /t:PublishContainer `
  -p:ContainerRepository=day6-api `
  -p:ContainerImageTag=sdk
```

得到的镜像名示例：

```text
day6-api:sdk
```

常见配置包括：

- `ContainerRepository`：仓库名，可包含命名空间，如 `garrett0719/my-api`。
- `ContainerImageTag`：Tag，如 `1.4.2`。
- `ContainerRegistry`：镜像仓库地址，如 `ghcr.io`。
- `ContainerBaseImage`：自定义基础镜像。
- `ContainerUser`：容器默认用户。

这些配置适合修改镜像元数据和常规运行设置，但不能代替 Dockerfile 中任意的系统命令。

### 2. 输出镜像归档文件

```powershell
dotnet publish -c Release /t:PublishContainer `
  -p:ContainerArchiveOutputPath=./day6-api.tar.gz
```

### 3. 推送到镜像仓库

```powershell
dotnet publish -c Release /t:PublishContainer `
  -p:ContainerRegistry=ghcr.io `
  -p:ContainerRepository=garrett0719/my-api `
  -p:ContainerImageTag=1.4.2
```

推送前必须先完成仓库认证。不要把用户名、密码或 Token 写进项目文件或提交到 Git。

## 四、Dockerfile 发布

标准 ASP.NET Core API 可使用以下多阶段结构：

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MyApi.csproj ./
RUN dotnet restore MyApi.csproj

COPY . .
RUN dotnet publish MyApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

USER $APP_UID
ENTRYPOINT ["dotnet", "MyApi.dll"]
```

构建命令：

```powershell
docker build -t day6-api:dockerfile .
```

关键顺序：

```text
复制项目文件 → restore → 复制源码 → publish → 复制发布产物到运行镜像
```

先复制 `.csproj` 再 restore，是为了让只修改 `.cs` 文件时仍能复用依赖恢复层。`dotnet publish` 默认包含 build，因此简单项目可以直接采用 `restore → publish --no-restore`；只有需要单独测试或检查编译结果时，才有必要再拆出独立 build 阶段。

### 什么时候必须优先用 Dockerfile

- 运行镜像需要 `apt-get install ffmpeg`、字体或其他系统包。
- 需要 Node、Java 等其他工具参与构建。
- 需要复制并配置原生 `.so` 文件，或执行 `ldconfig`。
- 需要 BuildKit Secret 安全提供私有 NuGet、npm 等构建期凭据。
- 需要复杂的多阶段、缓存挂载或组织统一的基础镜像处理。

## 五、镜像名、Tag 与 Digest

镜像名常用结构：

```text
Registry/Namespace/Repository:Tag
```

示例：

```text
ghcr.io/garrett0719/my-api:1.4.2
```

| 部分 | 值 |
| --- | --- |
| Registry | `ghcr.io` |
| Namespace/Owner | `garrett0719` |
| Repository | `my-api` |
| Tag | `1.4.2` |

判断口诀：

- `/` 分隔仓库层级。
- `:` 后面是 Tag。
- `@` 后面是 Digest。

Digest 示例：

```text
ghcr.io/garrett0719/my-api@sha256:abcdef...
```

- **Tag 是可移动标签**：同一个 `latest` 或 `1.4.2` 可以被重新推送并指向新镜像。
- **Digest 是内容标识**：固定指向完全相同的镜像内容。
- 生产中可同时保留版本 Tag 方便阅读，并记录或固定 Digest 保证部署内容不变。

## 六、基础镜像不要选错

| 镜像 | 主要用途 |
| --- | --- |
| `mcr.microsoft.com/dotnet/sdk` | restore、build、test、publish |
| `mcr.microsoft.com/dotnet/aspnet` | 运行 ASP.NET Core Web API |
| `mcr.microsoft.com/dotnet/runtime` | 运行普通的框架依赖型 .NET 控制台或 Worker 应用 |
| `mcr.microsoft.com/dotnet/runtime-deps` | 自包含或 Native AOT 应用，不包含完整 .NET Runtime |

ASP.NET Core API 的最终阶段通常应选 `aspnet`，不能因为 `runtime-deps` 更小就直接替换。镜像越精简，对应用打包方式和原生依赖兼容性的要求通常越高。

## 七、构建期密钥与运行期密钥

两类密钥不要混用：

- **构建期密钥**：私有 NuGet Token、npm Token。Dockerfile 构建时使用 BuildKit Secret。
- **运行期密钥**：数据库密码、第三方 API Key。容器启动时由 Secret Manager、Kubernetes Secret 等注入。

构建期示例：

```dockerfile
RUN --mount=type=secret,id=nuget_config,target=/root/.nuget/NuGet/NuGet.Config \
    dotnet restore MyApi.csproj
```

```powershell
docker buildx build `
  --secret id=nuget_config,src=NuGet.Config `
  -t day6-api:dockerfile .
```

不要用 `ENV`、普通 `ARG` 或 `COPY` 把 Token 放进镜像。它可能留在镜像层、构建记录或最终配置中。

## 八、实践与验收

### 1. 为同一个 API 生成两个镜像

```powershell
docker build -t day6-api:dockerfile .

dotnet publish -c Release --os linux --arch x64 /t:PublishContainer `
  -p:ContainerRepository=day6-api `
  -p:ContainerImageTag=sdk
```

必须使用同一个项目、相同发布配置和相同 CPU 架构，比较才有意义。

### 2. 比较镜像

```powershell
# 体积
docker image ls day6-api

# 层和每层大小
docker history day6-api:dockerfile
docker history day6-api:sdk

# 默认用户
docker image inspect day6-api:dockerfile --format '{{json .Config.User}}'
docker image inspect day6-api:sdk --format '{{json .Config.User}}'

# 入口与默认参数
docker image inspect day6-api:dockerfile --format '{{json .Config.Entrypoint}} {{json .Config.Cmd}}'
docker image inspect day6-api:sdk --format '{{json .Config.Entrypoint}} {{json .Config.Cmd}}'
```

还应分别启动两个镜像并访问同一个健康检查或 API 地址，确认功能一致。

### 3. 验收记录表

| 项目 | Dockerfile 镜像 | SDK 镜像 |
| --- | --- | --- |
| 镜像名 | `day6-api:dockerfile` | `day6-api:sdk` |
| 大小 | 待记录 | 待记录 |
| 层数 | 待记录 | 待记录 |
| 默认用户 | 待记录 | 待记录 |
| Entrypoint/Cmd | 待记录 | 待记录 |
| API 是否正常 | 待验证 | 待验证 |
| 能否安装系统包 | 可以 | 不能直接执行安装命令 |
| 构建配置维护位置 | Dockerfile | `.csproj` 或命令参数 |

不要预先认定某一种方式一定更小。基础镜像、发布模式、应用内容和配置都会影响最终结果，应以实际命令输出为准。

## 九、今天的错题与不完整答案解析

| 题目知识点 | 错误或遗漏 | 正确理解 |
| --- | --- | --- |
| 镜像名拆分（第 3、18 题） | 把 `my-api` 认为是 Tag，或把四部分都用 `/` 连接 | `ghcr.io/garrett0719/my-api:1.4.2` 中，`my-api` 是 Repository，`1.4.2` 才是 Tag；Tag 前用 `:` |
| ASP.NET Core 运行镜像（第 9 题） | 选择 `runtime-deps` | 框架依赖型 ASP.NET Core API 通常使用 `aspnet`；`runtime-deps` 主要服务于自包含或 Native AOT 应用 |
| `.dockerignore`（第 13 题） | 漏掉本地 `out/` 或 `publish/` | 容器内自行 publish 时，本地 `bin/`、`obj/`、`out/`、`publish/`、`.git/`、`.vs/` 通常都应排除；不要排除构建需要的 `.csproj` |
| 私有源密钥（第 19、23 题） | 用环境变量笼统处理构建期 Token | 私有 NuGet/npm 的构建期 Token 用 BuildKit Secret；数据库密码等运行期密钥在启动容器时注入 |
| restore 顺序（第 12、23 题） | 在复制 `.csproj` 前 restore，或没有说明依赖文件必须先存在 | restore 必须先读取项目文件；正确顺序是“复制项目文件 → restore → 复制源码” |
| build 与 publish（第 23 题） | 认为每次都必须先独立 build 再 publish | `dotnet publish` 默认会 build。简单镜像可使用 `restore → publish --no-restore`；独立 build 是按测试和流水线需要选择的 |

## 十、生产项目中的默认选择

可以直接写成以下结论：

> 对没有额外系统依赖的标准 ASP.NET Core API，我默认使用 .NET SDK 内置容器发布，因为配置更少，镜像命名、基础镜像和入口仍可配置，也减少了 Dockerfile 维护工作。一旦项目需要安装系统包、联合 Node 构建、处理原生库、使用 BuildKit Secret 或执行特殊构建步骤，我会改用多阶段 Dockerfile。无论选择哪种方式，生产部署都使用明确版本 Tag，并记录或固定 Digest。

## 十一、最终检查清单

- [ ] 能说明 Dockerfile 与 SDK 容器发布各自适合什么场景。
- [ ] 能正确拆分 `Registry/Namespace/Repository:Tag`。
- [ ] 能说明 Tag 可变、Digest 固定。
- [ ] ASP.NET Core 最终阶段能正确选择 `aspnet` 镜像。
- [ ] 知道系统包、自定义步骤和复杂多阶段构建需要 Dockerfile。
- [ ] 知道构建期密钥使用 BuildKit Secret，运行期密钥在启动时注入。
- [ ] 已用两种方式为同一 API 生成镜像并成功启动。
- [ ] 已记录两种镜像的大小、层数、用户和入口。
- [ ] 已写下生产项目的默认选择及理由。

## 参考资料

- [.NET：Containerize a .NET app with dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/containers/sdk-publish)
- [.NET SDK container creation overview](https://learn.microsoft.com/en-us/dotnet/core/containers/overview)
- [Docker：Build, tag, and publish an image](https://docs.docker.com/get-started/docker-concepts/building-images/build-tag-and-publish-an-image/)

