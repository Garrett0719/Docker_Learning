# Docker 入门知识整理（官方 Get Started 简明中文版）

> 整理范围：Docker 官方 Get Started 中截图所示的“介绍”“Docker 概念”“Docker 研讨会”三部分及其展开页面。  
> 整理日期：2026-08-25  
> 官方入口：https://docs.docker.com/get-started/

---

## 0. 先用一分钟认识 Docker

Docker 解决的核心问题可以概括成一句话：

> 把“程序 + 运行程序需要的环境”一起打包，让程序换一台电脑也能以相同方式运行。

例如，一个 C# 程序可能需要特定版本的 .NET、配置文件和系统依赖。如果只把源代码发给别人，对方还要自己安装并配置环境；如果把它做成 Docker 镜像，对方只要安装 Docker，就可以直接启动。

可以把 Docker 想成一套标准化的“应用集装箱系统”：

- **应用程序**像货物。
- **镜像（Image）**像封装好的集装箱模板。
- **容器（Container）**像根据模板启动、正在工作的集装箱。
- **Dockerfile**像制作集装箱模板的说明书。
- **Docker Hub / Registry**像存放和分发模板的仓库。
- **Docker Compose**像一张调度清单，规定多个集装箱怎样一起工作。

它们的关系是：

```text
项目代码 + Dockerfile
        ↓ docker build
      Docker 镜像
        ↓ docker run
      Docker 容器

镜像 ← docker pull / docker push → 镜像仓库

多个容器 ← compose.yaml 统一配置和管理
```

注意：Docker 不是把程序代码“变成另一种语言”，也不是完整虚拟机。它主要负责隔离、打包、分发和运行程序。

---

# 第一部分：介绍（Introduction）

官方相关页面：

1. [Introduction](https://docs.docker.com/get-started/introduction/)
2. [Get Docker Desktop](https://docs.docker.com/get-started/introduction/get-docker-desktop/)
3. [Develop with containers](https://docs.docker.com/get-started/introduction/develop-with-containers/)
4. [Build and push your first image](https://docs.docker.com/get-started/introduction/build-and-push-first-image/)
5. [What's next](https://docs.docker.com/get-started/introduction/whats-next/)

## 1. Docker Desktop 是什么

Docker Desktop 是适合个人电脑使用的一整套 Docker 工具。它通常包含：

- Docker Engine：真正负责创建和运行容器的核心引擎。
- Docker CLI：在终端中使用的 `docker` 命令。
- Docker Compose：统一管理多个容器。
- Docker Desktop Dashboard：通过图形界面查看镜像、容器、卷和日志。

简单说：**Docker Engine 是发动机，Docker CLI 和 Dashboard 是两种操作方式，Docker Desktop 把它们打包安装好了。**

Windows 一般使用 Docker Desktop 配合 WSL 2。WSL 2 为 Linux 容器提供 Linux 内核运行环境。安装完成后，可用下面的命令检查：

```bash
docker version
docker info
```

- `docker version`：查看客户端与服务端版本。
- `docker info`：查看 Docker 当前运行状态和总体配置。

如果命令存在但提示无法连接 daemon，通常表示 Docker Desktop 尚未启动，而不是命令安装失败。

## 2. 用容器进行开发是什么意思

传统开发常见问题是：

- 你的电脑是 Node 22，同事是 Node 20。
- 你的数据库配置和测试环境不同。
- 新成员要花很久安装依赖。
- 项目 A 和项目 B 需要互相冲突的依赖版本。

容器化开发会把前端、后端、数据库等放进各自容器。这样每个项目拥有自己的环境，减少对宿主机的污染。

例如一个待办事项应用可以拆成：

- `app` 容器：运行 Node.js 应用。
- `mysql` 容器：运行数据库。
- `phpmyadmin` 容器：提供数据库管理界面。
- `traefik` 容器：按规则转发请求。

开发代码一般仍保存在 Windows 本地，通过 **bind mount（绑定挂载）** 映射进容器。你在编辑器中保存代码后，容器可以立即看到变化，因此不必每改一行都重新制作镜像。

## 3. 第一次构建并推送镜像

完整过程是：

```text
Dockerfile → docker build → 本地镜像 → docker push → Docker Hub
```

常用命令：

```bash
docker build -t 用户名/项目名:1.0 .
docker login
docker push 用户名/项目名:1.0
```

逐项解释：

- `docker build`：根据 Dockerfile 构建镜像。
- `-t`：给镜像设置名称和标签，`t` 可以理解为 tag。
- `用户名/项目名`：通常对应 Docker Hub 中的命名空间和仓库名。
- `:1.0`：镜像标签，一般用来表示版本。
- 最后的 `.`：构建上下文是当前目录。不要漏掉。
- `docker login`：登录镜像仓库。
- `docker push`：上传镜像。

如果没有写标签，Docker 通常使用 `latest`。但 `latest` 只是默认标签，并不自动保证它真的是版本最高或发布时间最新的镜像。

## 4. Docker Hub 中几个容易混淆的词

- **Registry（注册中心/镜像仓库服务）**：存放大量镜像仓库的平台，例如 Docker Hub。
- **Repository（仓库）**：某一个项目镜像的集合，例如 `yuhao/my-api`。
- **Image（镜像）**：具体的镜像内容。
- **Tag（标签）**：同一仓库中区分版本，例如 `1.0`、`1.1`、`latest`。

例如：

```text
docker.io/yuhao/my-api:1.0
│         │      │    └─ 标签
│         │      └────── 仓库名
│         └───────────── 用户/命名空间
└─────────────────────── Registry 地址（Docker Hub）
```

从公共仓库选择基础镜像时，初学者优先考虑维护来源明确的镜像，例如 Docker Official Images。生产环境还应关注镜像更新、安全漏洞和固定版本，而不是随意使用来源不明的镜像。

---

# 第二部分：Docker 概念（Docker Concepts）

## 5. 容器是什么

官方页面：[What is a container?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-container/)

容器本质上是一个**被隔离的进程**。它不是一台真正的电脑，但它有自己看到的文件系统、网络、进程和配置。

容器通常具有这些特点：

- **自包含**：运行所需文件和依赖可以随镜像提供。
- **隔离**：一个容器中的程序通常不会直接干扰其他容器。
- **独立**：可以单独启动、停止、更新和删除。
- **便携**：同一个镜像可以在不同 Docker 环境中以相近方式运行。

### 5.1 容器和虚拟机的区别

| 对比项 | 容器 | 虚拟机 |
| --- | --- | --- |
| 隔离对象 | 进程和运行环境 | 整套操作系统 |
| 内核 | 通常与宿主机共享内核 | 每台虚拟机拥有自己的内核 |
| 启动速度 | 通常很快 | 通常较慢 |
| 资源开销 | 较小 | 较大 |
| 适合场景 | 应用打包、开发测试、微服务 | 强隔离、多操作系统、完整机器环境 |

容器和虚拟机并不互相排斥。云服务器本身可能是一台虚拟机，而虚拟机里面又运行多个容器。

### 5.2 第一个容器命令

```bash
docker run -d -p 8080:80 docker/welcome-to-docker
```

- `docker run`：根据镜像创建并启动一个新容器。
- `-d`：让容器在后台运行（detached）。
- `-p 8080:80`：把宿主机的 8080 端口映射到容器的 80 端口。
- `docker/welcome-to-docker`：要使用的镜像。

然后访问 `http://localhost:8080`。

常用管理命令：

```bash
docker ps                 # 只看正在运行的容器
docker ps -a              # 查看全部容器，包括已停止的
docker stop 容器名或ID     # 停止容器
docker start 容器名或ID    # 再次启动已有容器
docker rm 容器名或ID       # 删除已停止的容器
docker rm -f 容器名或ID    # 强制停止并删除容器
```

**`docker run` 与 `docker start` 不一样：**

- `run`：创建一个新容器并启动。
- `start`：启动之前已经存在、目前停止的容器。

## 6. 镜像是什么

官方页面：[What is an image?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-an-image/)

镜像是用于创建容器的只读模板，里面包含程序、运行库、依赖、配置和默认启动命令。

最重要的关系：

> 镜像是模板，容器是模板启动后的实例。

这类似于“类和对象”，但不是完全相同的技术：一个镜像可以创建多个互相独立的容器。

镜像有两个重要特点：

1. **镜像本身不可直接修改。** 变化会形成新的层或新镜像。
2. **镜像由多个层组成。** Dockerfile 中的许多指令都会产生或影响层。

常用命令：

```bash
docker search nginx       # 在默认仓库中搜索镜像
docker pull nginx:1.28    # 下载指定标签的镜像
docker images             # 查看本地镜像
docker image inspect nginx:1.28
docker image history nginx:1.28
docker rmi nginx:1.28     # 删除本地镜像
```

删除镜像不等于删除由它创建的容器。如果仍有容器引用该镜像，Docker 可能会阻止删除。

## 7. Registry 与 Repository

官方页面：[What is a registry?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-registry/)

Registry 是集中存储和分发镜像的服务。Docker Hub 是默认 Registry，也可以使用云厂商的 Registry 或公司内部私有仓库。

可以把层级理解为：

```text
Registry（大型仓库中心，例如 Docker Hub）
└── Repository（某个项目的镜像仓库）
    ├── my-api:1.0
    ├── my-api:1.1
    └── my-api:latest
```

相关命令：

```bash
docker login
docker tag 本地镜像 用户名/仓库名:标签
docker push 用户名/仓库名:标签
docker pull 用户名/仓库名:标签
```

`docker tag` 并不会重新复制一整份镜像数据，它主要是为已有镜像增加一个新名称/标签。

## 8. Docker Compose 是什么

官方页面：[What is Docker Compose?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-docker-compose/)

当项目只有一个容器时，`docker run` 还比较简单；当项目有前端、后端、数据库、缓存等多个容器时，大量命令会很难记。Compose 允许把这些配置写进一个 YAML 文件。

示例 `compose.yaml`：

```yaml
services:
  app:
    build: .
    ports:
      - "3000:3000"
    environment:
      DB_HOST: db
    depends_on:
      - db

  db:
    image: mysql:8.4
    environment:
      MYSQL_ROOT_PASSWORD: example-password
    volumes:
      - db-data:/var/lib/mysql

volumes:
  db-data:
```

这里表示：

- 定义 `app` 和 `db` 两个服务。
- `app` 从当前目录的 Dockerfile 构建。
- `app` 的 3000 端口映射给宿主机。
- `DB_HOST` 的值是 `db`，应用可通过服务名找到数据库。
- `db` 使用 MySQL 镜像。
- 数据库存进名为 `db-data` 的卷。

常用命令：

```bash
docker compose up                 # 前台启动
docker compose up -d              # 后台启动
docker compose up -d --build      # 需要时重新构建后启动
docker compose ps                 # 查看服务状态
docker compose logs -f            # 持续查看日志
docker compose exec app sh        # 进入 app 服务容器执行 shell
docker compose down               # 停止并删除该项目的容器和网络
docker compose down -v            # 额外删除 Compose 管理的卷，数据会丢失
```

### 8.1 Dockerfile 和 Compose 文件的区别

| 文件 | 回答的问题 |
| --- | --- |
| `Dockerfile` | “这个镜像应该怎样制作？” |
| `compose.yaml` | “这些容器应该怎样一起运行？” |

它们不是二选一。Compose 经常通过 `build: .` 调用 Dockerfile 构建镜像。

---

## 9. 镜像层（Image Layers）

官方页面：[Understanding image layers](https://docs.docker.com/get-started/docker-concepts/building-images/understanding-image-layers/)

镜像不是一个毫无结构的巨大压缩包，而是由一层层文件变化叠起来的。

例如：

```dockerfile
FROM node:24-alpine
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev
COPY . .
CMD ["node", "src/index.js"]
```

可以粗略理解为：

1. 从 Node 基础镜像开始。
2. 设置工作目录。
3. 放入依赖清单。
4. 安装依赖。
5. 放入项目代码。
6. 设置容器默认启动命令。

多个镜像可以共享相同的基础层，所以 Docker 不必为每个镜像重复保存所有文件。

容器启动后，会在只读镜像层上增加一个可写层。容器里临时产生的文件通常写在这一层；删除容器时，这一层也会消失。因此重要数据不能只放在容器可写层里。

## 10. Dockerfile 的常用指令

官方页面：[Writing a Dockerfile](https://docs.docker.com/get-started/docker-concepts/building-images/writing-a-dockerfile/)

Dockerfile 是纯文本文件，文件名通常就是 `Dockerfile`，没有扩展名。

| 指令 | 最简单的解释 |
| --- | --- |
| `FROM` | 选择基础镜像，通常是第一条指令 |
| `WORKDIR` | 设置后续命令使用的容器内工作目录 |
| `COPY` | 把构建上下文中的文件复制到镜像里 |
| `RUN` | 构建镜像时执行命令，例如安装依赖 |
| `ENV` | 设置镜像/容器内环境变量 |
| `EXPOSE` | 声明程序预计监听的端口，但不会自动发布端口 |
| `USER` | 指定后续构建或运行使用的用户 |
| `CMD` | 设置容器默认启动命令或默认参数 |
| `ENTRYPOINT` | 设置容器的主要可执行程序 |

`RUN` 与 `CMD` 最容易混淆：

- `RUN npm ci`：在**构建镜像时**执行，把依赖安装进镜像。
- `CMD ["node", "app.js"]`：在**容器启动时**执行，运行应用。

推荐使用 exec 数组形式：

```dockerfile
CMD ["node", "src/index.js"]
```

而不是：

```dockerfile
CMD node src/index.js
```

数组形式通常能更清晰地处理参数和系统信号。

### 10.1 构建上下文

```bash
docker build -t my-app:1.0 .
```

最后的 `.` 表示 Docker 可以读取当前目录作为构建上下文。`COPY . .` 复制的是构建上下文中的内容，而不是电脑上的任意文件。

可用 `.dockerignore` 排除不需要传入构建器的文件：

```gitignore
.git
node_modules
bin
obj
*.log
.env
```

这样可以减少构建数据、提高速度，并避免把密钥或垃圾文件误放进镜像。不要只依赖 `.dockerignore` 保护秘密；敏感信息本来就不应该写进镜像层。

## 11. 构建、命名、打标签和发布镜像

官方页面：[Build, tag, and publish an image](https://docs.docker.com/get-started/docker-concepts/building-images/build-tag-and-publish-an-image/)

```bash
docker build -t my-app:1.0 .
docker tag my-app:1.0 用户名/my-app:1.0
docker push 用户名/my-app:1.0
```

镜像名的一般结构：

```text
[Registry地址/][命名空间/]仓库名[:标签]
```

示例：

```text
docker.io/yuhao/my-app:1.0
```

发布失败的常见原因：

- 没有执行 `docker login`。
- 镜像名称中的用户名和 Docker Hub 用户名不一致。
- 没有仓库权限。
- 本地镜像没有正确标签。
- 指定标签的镜像实际上不存在。

## 12. 构建缓存（Build Cache）

官方页面：[Using the build cache](https://docs.docker.com/get-started/docker-concepts/building-images/using-the-build-cache/)

Docker 按 Dockerfile 从上到下构建。某一步的输入没有变化时，Docker 可以复用之前的结果；某一层失效后，后面的层通常也要重新构建。

低效写法：

```dockerfile
COPY . .
RUN npm install
```

只要任意源代码变化，`COPY . .` 就变化，`npm install` 也要重跑。

更合理的写法：

```dockerfile
COPY package.json package-lock.json ./
RUN npm ci --omit=dev
COPY . .
```

现在只有依赖清单变化时才需要重新安装依赖；普通代码变化可以继续复用依赖层。

通用原则：

- 不常变化、耗时的步骤尽量放前面。
- 经常变化的源代码尽量后复制。
- 使用 `.dockerignore` 缩小构建上下文。
- 合理固定依赖版本，保证构建结果可重复。

## 13. 多阶段构建（Multi-stage Builds）

官方页面：[Multi-stage builds](https://docs.docker.com/get-started/docker-concepts/building-images/multi-stage-builds/)

编译程序时可能需要 SDK、编译器和测试工具，但运行程序时不需要。多阶段构建允许在第一阶段完成编译，只把最终产物复制到更小的运行阶段。

C# / .NET 示例：

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

- `build` 阶段使用较大的 SDK 镜像完成编译。
- `final` 阶段只使用运行时镜像。
- `COPY --from=build` 只拿走发布结果。
- 最终镜像不包含完整 SDK 和源代码，通常更小、更安全。

---

## 14. 端口发布与 EXPOSE

官方页面：[Publishing and exposing ports](https://docs.docker.com/get-started/docker-concepts/running-containers/publishing-ports/)

容器有自己的网络空间。容器中的 Web 服务监听 80 端口，不代表 Windows 可以直接访问；要使用 `-p` 发布端口。

```bash
docker run -d -p 8080:80 nginx
```

记忆方法：

```text
-p 宿主机端口:容器端口
```

也就是访问 Windows 的 `localhost:8080`，流量会转到容器的 80 端口。

更谨慎的本机开发写法：

```bash
docker run -d -p 127.0.0.1:8080:80 nginx
```

这样明确只绑定到本机回环地址，避免无意中通过其他网络接口暴露服务。

`EXPOSE 80` 只是镜像元数据，表示作者预计程序使用 80 端口。它不会代替 `-p`，也不会自动把端口开放给宿主机。

其他方式：

```bash
docker run -P nginx
```

`-P` 会把镜像声明的所有暴露端口映射到宿主机的临时端口。可用 `docker ps` 或 `docker port 容器名` 查看实际端口。

## 15. 覆盖容器默认设置

官方页面：[Overriding container defaults](https://docs.docker.com/get-started/docker-concepts/running-containers/overriding-container-defaults/)

镜像提供默认值，但启动容器时可以调整：

```bash
docker run -d \
  --name my-db \
  -e POSTGRES_PASSWORD=secret \
  -p 5433:5432 \
  --memory="512m" \
  --cpus="0.5" \
  postgres:18
```

- `--name my-db`：设置便于记忆的容器名。
- `-e`：设置容器内环境变量。
- `-p 5433:5432`：宿主机用 5433，容器仍监听 5432。
- `--memory="512m"`：最多使用约 512 MB 内存。
- `--cpus="0.5"`：CPU 配额相当于半个核心。

可用下面的命令观察实时资源：

```bash
docker stats
```

也可以在 `docker run` 最后追加命令，覆盖镜像的默认 `CMD`。Compose 中则可使用 `command` 或 `entrypoint`。这类覆盖应清楚镜像原本如何启动，否则很容易让容器立即退出。

## 16. 数据持久化：Volume

官方页面：[Persisting container data](https://docs.docker.com/get-started/docker-concepts/running-containers/persisting-container-data/)

容器可以删除和重建，因此不能把数据库等重要数据只保存在容器可写层中。Volume（卷）是由 Docker 管理、生命周期独立于容器的数据空间。

```bash
docker volume create postgres-data

docker run -d \
  --name db \
  -e POSTGRES_PASSWORD=secret \
  -v postgres-data:/var/lib/postgresql \
  postgres:18
```

即使删除 `db` 容器，只要 `postgres-data` 卷还在，之后把同一个卷挂载给新容器，数据仍可继续使用。

常用命令：

```bash
docker volume ls
docker volume inspect postgres-data
docker volume rm postgres-data
docker volume prune
```

`docker volume prune` 会清理未被容器使用的卷，执行前必须确认其中没有还需要的数据。

## 17. 共享本地文件：Bind Mount

官方页面：[Sharing local files with containers](https://docs.docker.com/get-started/docker-concepts/running-containers/sharing-local-files/)

Bind mount 是把宿主机上的具体文件或目录直接映射到容器中。

```bash
docker run --rm \
  --mount type=bind,src="当前项目绝对路径",target=/app \
  node:24-alpine
```

Windows PowerShell 常见思路是把当前目录映射进去，实际路径写法应以所用终端和 Docker Desktop 文件共享配置为准。

### 17.1 Volume 与 Bind Mount 的选择

| 需求 | 更适合的方式 |
| --- | --- |
| 保存数据库数据 | Volume |
| Docker 自动管理数据位置 | Volume |
| 把本地源代码映射进容器 | Bind mount |
| 实时看到本地文件修改 | Bind mount |
| 映射本地配置文件 | Bind mount（注意权限和秘密） |

一句话记忆：**Volume 更像 Docker 管理的数据盘；Bind mount 更像打开一扇通向本地指定目录的门。**

Bind mount 会让容器接触宿主机文件。除非容器确实需要写入，否则尽量使用只读挂载，以减少误修改风险。

## 18. 多容器应用、网络和服务发现

官方页面：[Multi-container applications](https://docs.docker.com/get-started/docker-concepts/running-containers/multi-container-applications/)

不要习惯性把前端、后端、数据库、缓存全部塞进同一个容器。通常让一个容器专注一个主要职责，更容易独立升级、扩缩容和排查问题。

容器之间通过 Docker 网络通信：

```bash
docker network create app-net

docker run -d --name redis --network app-net redis
docker run -d --name api --network app-net my-api
```

在自定义网络中，`api` 通常可以直接使用主机名 `redis` 访问 Redis，而不必记住容易变化的容器 IP。

常用命令：

```bash
docker network ls
docker network inspect app-net
docker network rm app-net
```

手动执行多个 `docker run` 能帮助理解网络原理，但真实项目更适合使用 Compose，把服务、端口、环境变量、网络和卷集中写在一个文件中。

---

# 第三部分：Docker 研讨会（Workshop）

官方研讨会入口：[Overview of the Docker workshop](https://docs.docker.com/get-started/workshop/)

这一部分不是一组全新的概念，而是用一个待办事项应用把前面的知识串起来。官方学习顺序如下。

## 19. 第一步：容器化应用

官方页面：[Containerize an application](https://docs.docker.com/get-started/workshop/02_our_app/)

主要流程：

1. 获取示例项目。
2. 在项目根目录创建 Dockerfile。
3. 使用 `docker build` 创建镜像。
4. 使用 `docker run` 创建并启动容器。
5. 通过映射端口访问应用。

```bash
docker build -t getting-started .
docker run -dp 127.0.0.1:3000:3000 getting-started
```

这一步建立最基本的闭环：**代码 → 镜像 → 容器 → 浏览器访问**。

## 20. 第二步：更新应用

官方页面：[Update the application](https://docs.docker.com/get-started/workshop/03_updating_app/)

镜像是不可变模板，因此修改代码后，旧镜像和旧容器不会自动变成新版本。常规做法是：

```bash
docker build -t getting-started .
docker ps
docker rm -f 旧容器ID
docker run -dp 127.0.0.1:3000:3000 getting-started
```

即：重新构建镜像，删除旧容器，再根据新镜像创建新容器。

不要把“进入容器手动改文件”当成正常发布方式，因为这种修改难以重复、难以审计，重建容器后也会消失。

## 21. 第三步：分享应用

官方页面：[Share the application](https://docs.docker.com/get-started/workshop/04_sharing_app/)

```bash
docker login -u 你的用户名
docker tag getting-started 你的用户名/getting-started:1.0
docker push 你的用户名/getting-started:1.0
```

其他机器安装 Docker 后可运行：

```bash
docker run -dp 3000:3000 你的用户名/getting-started:1.0
```

如果本地没有该镜像，Docker 会先从 Registry 拉取。这就是“构建一次，到处运行”的基本分发流程。

## 22. 第四步：持久化数据库

官方页面：[Persist the DB](https://docs.docker.com/get-started/workshop/05_persisting_data/)

示例应用最初把 SQLite 数据文件放在容器内部，删除容器后待办事项也会消失。解决方法是挂载卷：

```bash
docker volume create todo-db

docker run -dp 127.0.0.1:3000:3000 \
  --mount type=volume,src=todo-db,target=/etc/todos \
  getting-started
```

这里将卷 `todo-db` 挂载到容器的 `/etc/todos`。应用写入该目录的数据会保存到卷中。

## 23. 第五步：使用 Bind Mount 提高开发效率

官方页面：[Use bind mounts](https://docs.docker.com/get-started/workshop/06_bind_mounts/)

如果每次改代码都重新构建镜像，开发效率会很低。可以把本地项目映射到容器 `/app`，再让开发服务器监听文件变化。

```bash
docker run -dp 127.0.0.1:3000:3000 \
  -w /app \
  -v ".:/app" \
  node:24-alpine \
  sh -c "npm install && npm run dev"
```

- `-w /app`：容器内工作目录。
- `-v ".:/app"`：当前本地目录映射到容器 `/app`。
- `node:24-alpine`：使用 Node 基础镜像。
- 最后部分：容器启动后执行的开发命令。

这是开发方式，不代表生产发布时也应直接挂载本地源代码。生产环境通常运行已经构建好的不可变镜像。

## 24. 第六步：多容器应用

官方页面：[Multi container apps](https://docs.docker.com/get-started/workshop/07_multi_container/)

研讨会把应用数据从 SQLite 迁移到独立 MySQL 容器，重点是：

1. 创建网络。
2. 启动 MySQL 并接入网络。
3. 启动应用并接入同一网络。
4. 通过环境变量告诉应用数据库地址和账号。

```bash
docker network create todo-app

docker run -d \
  --network todo-app \
  --network-alias mysql \
  -v todo-mysql-data:/var/lib/mysql \
  -e MYSQL_ROOT_PASSWORD=secret \
  -e MYSQL_DATABASE=todos \
  mysql:8.4
```

应用容器可以用 `mysql` 这个网络别名连接数据库。不要把 `localhost` 当作另一个容器：**容器里的 `localhost` 指该容器自己。**

## 25. 第七步：用 Compose 统一管理

官方页面：[Use Docker Compose](https://docs.docker.com/get-started/workshop/08_using_compose/)

前一步需要记住网络、卷、端口和大量环境变量。Compose 将这些内容变成可版本管理的 `compose.yaml`：

```bash
docker compose up -d
docker compose logs -f
docker compose down
```

优势：

- 一条命令启动整套应用。
- 配置保存在项目中，方便团队共享。
- 服务名自动参与网络服务发现。
- 修改配置后再次 `up`，Compose 会根据差异调整资源。
- 清理整套环境更加方便。

`depends_on` 能表达服务启动顺序，但不一定代表数据库已经完成初始化并可接受连接。更稳妥的应用需要重试连接或配置 healthcheck。

## 26. 第八步：镜像构建最佳实践

官方页面：[Image-building best practices](https://docs.docker.com/get-started/workshop/09_image_best/)

重点有三项：

### 26.1 合理利用层缓存

先复制依赖清单并安装依赖，再复制经常变化的源代码。

### 26.2 使用多阶段构建

编译阶段包含工具链，最终阶段只包含运行产物和运行时。

### 26.3 减小镜像并降低风险

- 选择合适、来源可信的基础镜像。
- 不安装不需要的软件。
- 使用 `.dockerignore`。
- 尽量不以 root 用户运行应用。
- 不把密码、令牌、私钥写入 Dockerfile、镜像或 Git。
- 及时更新基础镜像并扫描漏洞。
- 固定明确版本，避免不可控变化。

小镜像的意义不只是下载快：其中无关工具和软件更少，攻击面通常也更小。

## 27. 研讨会之后学什么

官方页面：[What next after the Docker workshop](https://docs.docker.com/get-started/workshop/10_what_next/)

完成研讨会后，可以按下面顺序继续：

1. 用你熟悉的语言做一个真实项目；你目前学习 C#，可优先做 ASP.NET Core Web API 容器化。
2. 学会为数据库配置 Volume、为开发代码配置 Bind mount。
3. 使用 Compose 组合 Web API、数据库和缓存。
4. 学习日志、健康检查、资源限制和容器安全。
5. 学习 CI/CD 自动构建、测试和推送镜像。
6. 再根据需求学习云部署或 Kubernetes；初学阶段不必急着上 Kubernetes。

---

# 第四部分：最常用命令速查

## 28. 镜像命令

```bash
docker pull nginx:1.28                 # 拉取镜像
docker images                          # 查看本地镜像
docker build -t my-app:1.0 .           # 构建并命名
docker tag my-app:1.0 user/my-app:1.0  # 添加新标签
docker push user/my-app:1.0            # 推送镜像
docker rmi my-app:1.0                  # 删除本地镜像
docker image history my-app:1.0        # 查看镜像层历史
```

## 29. 容器命令

```bash
docker run -d --name web -p 8080:80 nginx
docker ps
docker ps -a
docker logs -f web
docker exec -it web sh
docker stop web
docker start web
docker rm web
docker rm -f web
docker inspect web
docker stats
```

`docker exec -it web sh` 表示在正在运行的 `web` 容器里启动一个交互式 shell。并非所有镜像都安装了 `bash`，精简镜像常常只有 `sh`。

## 30. Volume 与网络命令

```bash
docker volume ls
docker volume inspect 数据卷名
docker volume rm 数据卷名

docker network ls
docker network create 网络名
docker network inspect 网络名
docker network rm 网络名
```

## 31. Compose 命令

```bash
docker compose config           # 检查并展开最终配置
docker compose up -d --build
docker compose ps
docker compose logs -f
docker compose exec 服务名 sh
docker compose restart 服务名
docker compose down
docker compose down -v          # 会额外删除卷，谨慎使用
```

---

# 第五部分：初学者最容易混淆的 12 个问题

## 32. 镜像和容器是不是一回事？

不是。镜像是只读模板，容器是镜像运行后的实例。一个镜像能创建多个容器。

## 33. 删除容器会不会删除镜像？

不会。容器和镜像是不同对象。删除镜像也不一定能成功，因为可能还有容器引用它。

## 34. 停止容器会不会丢数据？

只停止通常不会立刻删除容器可写层；再次 `start` 还能看到。但如果删除并重建容器，内部数据可能丢失。重要数据应使用 Volume 或外部存储。

## 35. `EXPOSE 80` 是否等于开放 80 端口？

不等于。`EXPOSE` 主要是声明信息；要让宿主机访问仍需 `-p` 或 Compose 的 `ports`。

## 36. `8080:80` 哪个是本机端口？

左边 8080 是宿主机端口，右边 80 是容器端口。

## 37. 为什么容器访问不到 `localhost` 上的另一个容器？

因为每个容器有自己的网络空间。容器里的 `localhost` 指它自己。应把两个容器接入同一自定义网络，并使用容器名或 Compose 服务名访问。

## 38. Dockerfile 和 Compose 应该选哪个？

通常两个都用。Dockerfile 负责制作镜像，Compose 负责定义多个容器如何运行。

## 39. `COPY` 与 Bind Mount 有什么区别？

- `COPY` 在构建时把文件放入镜像，形成可分发的固定版本。
- Bind mount 在运行时把本地目录映射给容器，适合开发和本地配置。

## 40. 为什么改了代码，容器内容没有更新？

如果代码通过 `COPY` 放进镜像，就需要重新 `docker build` 并重建容器；如果开发时使用 bind mount，容器才能直接看到本地修改。

## 41. 为什么容器一启动就退出？

容器的生命周期通常跟它的主进程绑定。主进程执行完、崩溃或命令写错，容器就会退出。先查看：

```bash
docker ps -a
docker logs 容器名
```

## 42. `latest` 是不是永远表示最新版？

不是。它只是名为 `latest` 的普通标签。是否更新、指向哪个版本由镜像发布者决定。

## 43. Docker 能否代替虚拟机或 Kubernetes？

Docker 容器适合打包和运行应用；虚拟机负责更完整的机器级隔离；Kubernetes 负责大规模容器编排。三者层级不同，不能简单互相替代。

---

# 第六部分：推荐的实际学习顺序

## 44. 第一阶段：掌握单容器

目标：能解释镜像与容器，并熟练使用：

```bash
docker pull
docker run
docker ps
docker logs
docker exec
docker stop
docker rm
```

## 45. 第二阶段：自己构建镜像

目标：能编写基本 Dockerfile，理解 `FROM`、`COPY`、`RUN`、`CMD` 和构建上下文。

```bash
docker build -t my-app:1.0 .
```

## 46. 第三阶段：掌握数据和网络

目标：理解端口映射、Volume、Bind mount、自定义网络以及容器名称解析。

## 47. 第四阶段：使用 Compose

目标：用一个 `compose.yaml` 启动应用、数据库和缓存，并能查看日志、重建和清理。

## 48. 第五阶段：优化和发布

目标：会使用缓存、多阶段构建、非 root 用户、`.dockerignore`、固定标签，以及把镜像推送到 Registry。

对于 C# 学习，可以做一个小项目来串联知识：

```text
ASP.NET Core Web API
        ↓
SQL Server 或 PostgreSQL
        ↓
Dockerfile 构建 API 镜像
        ↓
Compose 同时启动 API + 数据库
        ↓
Volume 保存数据库数据
```

做到这里，你就不只是“会敲 Docker 命令”，而是真正理解了 Docker 在开发流程里负责什么。

---

# 官方资料索引

- [Docker Get Started](https://docs.docker.com/get-started/)
- [Introduction](https://docs.docker.com/get-started/introduction/)
- [What is Docker?](https://docs.docker.com/get-started/docker-overview/)
- [What is a container?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-container/)
- [What is an image?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-an-image/)
- [What is a registry?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-registry/)
- [What is Docker Compose?](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-docker-compose/)
- [Building images](https://docs.docker.com/get-started/docker-concepts/building-images/)
- [Running containers](https://docs.docker.com/get-started/docker-concepts/running-containers/)
- [Docker workshop](https://docs.docker.com/get-started/workshop/)

> 说明：本文使用自己的话概括官方入门页面，命令和示例用于帮助理解。Docker 文档会持续更新；涉及版本、许可、安全或生产部署时，应再次查看对应官方页面。
