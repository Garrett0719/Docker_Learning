# 第十天：Docker Compose 与多容器应用

> 学习目标：能读懂并编写 `compose.yaml`，用一条命令启动 API 和数据库；只向宿主机发布 API 端口；正确处理服务依赖、网络、配置和数据持久化。

## 一、先记住这张图

```text
Host
  │ localhost:8080
  ▼
API Service
  │ db:5432（服务名 + 容器端口）
  ▼
Database Service
  │
  ▼
Named Volume
```

- Host 访问 API：使用 `localhost:宿主机端口`。
- API 访问数据库：使用 `db:数据库容器端口`，不要写 `localhost`。
- 数据库不写 `ports`，仍可被同网络的 API 访问。
- 数据写入 Named Volume，删除并重建数据库容器后仍可保留。

## 二、Dockerfile 与 Compose 的分工

| 文件 | 主要回答的问题 | 常见内容 |
|---|---|---|
| `Dockerfile` | 一个镜像怎样构建 | `FROM`、`COPY`、`RUN`、`ENTRYPOINT` |
| `compose.yaml` | 多个容器怎样一起运行 | 服务、端口、网络、卷、环境变量、依赖 |

```text
Dockerfile → 制作 API Image
Compose    → 让 API Image + Database Image 组成完整应用
```

看起来重合的配置，侧重点不同：

| Dockerfile | Compose | 区别 |
|---|---|---|
| `EXPOSE 8080` | `ports: "8080:8080"` | `EXPOSE` 是说明；`ports` 才发布 Host Port |
| `ENV` | `environment` | `ENV` 是镜像默认值；`environment` 是本次运行配置，可覆盖默认值 |
| `ENTRYPOINT` / `CMD` | `command` / `entrypoint` | Dockerfile 给默认启动方式；Compose 可按环境覆盖 |

Compose 可以只使用现成镜像而不需要 Dockerfile；单个容器也可以只用 Dockerfile 和 `docker run`，不使用 Compose。

## 三、Compose 核心字段

### 1. `services`

定义应用包含哪些服务，以及每个服务怎样运行。它不只是“统计服务数量”。

```yaml
services:
  api:
    # API 的运行配置
  db:
    # 数据库的运行配置
```

通常一个 Service 创建一个容器；服务也可以扩展为多个同配置容器。

### 2. `build`

告诉 Compose 怎样从源码构建镜像。

```yaml
build:
  context: .
  dockerfile: Dockerfile
```

短写：

```yaml
build: .
```

相对路径以 Compose 项目目录为基准。`context` 还决定发送给构建器的构建上下文。

### 3. `image`

指定服务使用的镜像引用，不等于“一定从 Docker Hub 下载”。

```yaml
image: portableorders-api:1.0
```

- 本地已有符合条件的镜像时，可以使用本地镜像。
- 本地没有时，Compose 会按 `pull_policy` 决定是否拉取。
- `build` 和 `image` 同时存在时：`build` 描述怎样构建，`image` 还可为构建结果命名；具体先拉取还是构建受 `pull_policy` 影响。

最简单的记法：

```text
build → 镜像怎么构建
image → 服务使用哪个镜像
```

### 4. `ports`

把容器端口发布给 Host 或外部客户端。

```yaml
ports:
  - "8080:8080"
```

```text
宿主机 8080 → 容器 8080
```

端口映射不会修改应用实际监听的端口。未指定 Host IP 时通常绑定所有主机网卡；只想让本机访问可写：

```yaml
ports:
  - "127.0.0.1:8080:8080"
```

本次实践只给 API 写 `ports`，数据库不要写。

### 5. `environment`

向容器注入运行时环境变量。

```yaml
environment:
  ASPNETCORE_HTTP_PORTS: "8080"
  ConnectionStrings__DefaultConnection: "Host=db;Port=5432;..."
```

.NET 配置中的 `ConnectionStrings:DefaultConnection` 可写成环境变量 `ConnectionStrings__DefaultConnection`。

### 6. `volumes`

服务内部的 `volumes` 表示“挂载”，顶层 `volumes` 表示“声明 Named Volume”。

```yaml
services:
  db:
    volumes:
      - pg-data:/var/lib/postgresql/data

volumes:
  pg-data:
```

```text
顶层声明 pg-data
        ↓
db 服务把它挂载到数据库数据目录
```

`docker compose down` 默认不删除 Named Volume；`docker compose down -v` 会连卷和数据一起删除。

### 7. `networks`

服务内部的 `networks` 表示“加入网络”，顶层 `networks` 表示“声明网络”。

```yaml
services:
  api:
    networks:
      - backend
  db:
    networks:
      - backend

networks:
  backend:
```

同一 Compose 网络中的服务可通过服务名解析：

```text
API → db:5432 → Docker DNS → db 当前 IP
```

没有显式配置 `networks` 时，Compose 会创建并使用项目的默认网络。学习阶段显式写出来更容易理解隔离关系。

### 8. `depends_on`

短写只表达依赖和启动顺序：

```yaml
depends_on:
  - db
```

它只能保证先启动 `db` 容器，不能保证数据库已经能处理 SQL。

```text
Container Started ≠ Application Ready
```

需要等待健康检查通过时，使用长写：

```yaml
depends_on:
  db:
    condition: service_healthy
```

同时必须在 `db` 中定义有效的 `healthcheck`。

## 四、`healthcheck` 到底检查什么

Docker 不理解“数据库是否真正可用”，只会周期执行你写的检测命令。

```text
执行自定义检测命令
        ↓
连续成功或失败
        ↓
healthy / unhealthy
```

检测质量决定 `healthy` 是否接近真正 Ready：

| 检测方式 | 能证明什么 |
|---|---|
| 只检查进程存在 | 进程还活着，不保证能处理请求 |
| `pg_isready` | PostgreSQL 已能接受连接 |
| 执行 `SELECT 1` | 数据库已能执行简单 SQL，更接近业务 Ready |

即使使用 `service_healthy`，应用仍应有连接重试，因为数据库可能在运行过程中短暂不可用。

## 五、`.env`、`environment` 与 `env_file`

这是本次答题最需要修正的知识点。

| 配置 | 主要用途 | 是否自动进入容器 |
|---|---|---|
| `.env` | 给 Compose 中的 `${变量}` 做替换 | 否 |
| `environment` | 直接给容器设置环境变量 | 是 |
| `env_file` | 从指定文件批量给容器设置环境变量 | 是 |

例如：

```dotenv
# .env
DB_PASSWORD=change-me
```

```yaml
services:
  db:
    environment:
      POSTGRES_PASSWORD: "${DB_PASSWORD}"
```

过程是：

```text
.env 提供 DB_PASSWORD
        ↓ Compose 替换 ${DB_PASSWORD}
environment 把替换后的值传入 db 容器
```

验证 Compose 最终解析结果：

```powershell
docker compose config
docker compose config --environment
```

不要提交含真实密码的 `.env`。生产密码也不应直接写进 `compose.yaml`。

## 六、实践一：运行 awesome-compose 的 ASP.NET + SQL Server 样例

当前样例的重点结构：

```text
web
├─ build: app/aspnetapp
└─ ports: 80:80

db
├─ image: Azure SQL Edge
├─ environment
└─ healthcheck
```

只有 `web` 发布 Host Port，`db` 没有发布 `1433`。样例使用硬编码演示密码，且未给数据库配置持久卷，因此只适合学习和本地演示，不要直接当生产配置。

```powershell
git clone --depth 1 https://github.com/docker/awesome-compose.git
cd awesome-compose/aspnet-mssql

docker compose config
docker compose up -d --build
docker compose ps
docker compose logs -f
```

浏览或请求：

```powershell
curl.exe http://localhost:80
```

结束实验：

```powershell
docker compose down
```

验收记录：

| 检查项 | 结果 |
|---|---|
| `web` 和 `db` 均已启动 | 通过 / 未通过 |
| Host 能访问 Web | 通过 / 未通过 |
| 只有 Web 显示 Host Port 映射 | 通过 / 未通过 |
| 能从日志区分两个 Service | 通过 / 未通过 |

## 七、实践二：自己的 API + PostgreSQL

下面的 `compose.yaml` 展示本日需要掌握的全部核心字段：

```yaml
services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    image: portableorders-api:local
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_HTTP_PORTS: "8080"
      ConnectionStrings__DefaultConnection: >-
        Host=db;Port=5432;Database=orders;Username=app;Password=${DB_PASSWORD:?请设置 DB_PASSWORD}
    depends_on:
      db:
        condition: service_healthy
    networks:
      - backend

  db:
    image: postgres:18
    environment:
      POSTGRES_DB: orders
      POSTGRES_USER: app
      POSTGRES_PASSWORD: "${DB_PASSWORD:?请设置 DB_PASSWORD}"
    volumes:
      - pg-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    networks:
      - backend

volumes:
  pg-data:

networks:
  backend:
```

配套 `.env`：

```dotenv
DB_PASSWORD=replace-with-a-local-password
```

这里写 `$$POSTGRES_USER` 是为了把 `$POSTGRES_USER` 留给容器内的 Shell 展开，而不是让 Compose 提前替换。

启动与验证：

```powershell
docker compose config
docker compose up -d --build
docker compose ps
docker compose logs -f api db
```

必须得到的结果：

- Host 可以访问 `http://localhost:8080`。
- API 连接地址使用 `db:5432`，不是 `localhost:5432`。
- `docker compose ps` 只显示 API 的 Host Port 映射。
- `db` 健康后 API 才启动。
- 执行 `docker compose down` 再 `up`，数据库数据仍存在。
- 只有明确要清空实验数据时才执行 `docker compose down -v`。

### 如果改用 SQL Server

主要替换项：

| PostgreSQL | SQL Server |
|---|---|
| `postgres:<固定版本>` | `mcr.microsoft.com/mssql/server:2022-latest` |
| 内部端口 `5432` | 内部端口 `1433` |
| 数据目录 `/var/lib/postgresql/data` | 数据目录 `/var/opt/mssql` |
| `POSTGRES_PASSWORD` | `MSSQL_SA_PASSWORD` |
| `pg_isready` | 使用镜像内的 `sqlcmd` 执行 `SELECT 1` |

连接字符串中的主机仍写服务名：`Server=db,1433`。数据库仍不需要 `ports`。

## 八、常用命令

| 命令 | 作用 |
|---|---|
| `docker compose config` | 检查并显示最终解析后的配置 |
| `docker compose up -d --build` | 构建并在后台启动 |
| `docker compose ps` | 查看服务容器、状态和端口 |
| `docker compose logs -f api` | 持续查看指定服务日志 |
| `docker compose exec api <命令>` | 在运行中的 API 容器内执行命令 |
| `docker compose stop` | 停止但保留容器 |
| `docker compose down` | 删除服务容器和项目网络，默认保留 Named Volume |
| `docker compose down -v` | 连 Named Volume 一起删除，数据会丢失 |

## 九、本次错题与易混点

| 来源 | 原理解或不够准确的地方 | 正确理解 |
|---|---|---|
| 第 16 题 | 混淆 `.env` 与 `environment` | `.env` 主要做 Compose 插值；`environment` / `env_file` 才把变量送入容器 |
| 简答题 | `healthcheck` 可以直接判断服务是否真的 Ready | Docker 只执行自定义检测；检查条件足够有效时，`healthy` 才接近 Ready |
| 简答题 | `image` 表示从远程拉取镜像 | `image` 指定使用哪个镜像；可能使用本地，也可能按拉取策略访问 Registry |
| 简答题 | `services` 只是声明有几个服务 | 它定义服务名称，以及每个服务完整的运行配置 |
| 课后追问 | Dockerfile 和 Compose 功能重复 | Dockerfile 负责制作单个镜像；Compose 负责组织一个或多个服务的运行关系 |

## 十、排错顺序

```text
1. docker compose config 能否通过？
2. docker compose ps 中容器是否运行、健康？
3. docker compose logs api db 是否有明确错误？
4. API 是否监听 0.0.0.0:8080？
5. API 连接字符串是否使用 db 和容器端口？
6. api 与 db 是否在同一网络？
7. 数据库 healthcheck 是否真的检查可用性？
8. Host 访问失败时再检查 API 的 ports 和防火墙。
```

## 十一、最终检查清单

- [ ] 能解释 Dockerfile 与 Compose 的分工。
- [ ] 能解释 `services`、`build`、`image`、`ports`、`environment`。
- [ ] 能区分服务内和顶层的 `volumes`、`networks`。
- [ ] 知道服务间使用服务名和容器端口。
- [ ] 知道数据库不写 `ports` 仍可被 API 访问。
- [ ] 知道短写 `depends_on` 不保证应用 Ready。
- [ ] 能使用 `healthcheck` + `condition: service_healthy`。
- [ ] 能区分 `.env`、`environment` 与 `env_file`。
- [ ] 已运行 awesome-compose 的 ASP.NET + SQL Server 样例。
- [ ] 已让自己的 API + 数据库通过一条 Compose 命令启动。
- [ ] 已验证只有 API 对 Host 暴露端口。
- [ ] 已验证 `down` 后 Named Volume 中的数据仍存在。

## 参考资料

- [Docker Compose Quickstart](https://docs.docker.com/compose/gettingstarted/)
- [Compose file reference](https://docs.docker.com/reference/compose-file/)
- [Compose services reference](https://docs.docker.com/reference/compose-file/services/)
- [Compose Build Specification](https://docs.docker.com/reference/compose-file/build/)
- [Compose variable interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/)
- [Compose startup order](https://docs.docker.com/compose/how-tos/startup-order/)
- [docker/awesome-compose](https://github.com/docker/awesome-compose)
- [ASP.NET + SQL Server sample](https://github.com/docker/awesome-compose/tree/master/aspnet-mssql)
