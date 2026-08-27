# Dockerfile常用命令:

Dockerfile 是用于描述镜像构建步骤的文本文件。构建器按从上到下的顺序执行指令，并生成容器镜像。

## 基本书写规则:

1. `# syntax=docker/dockerfile:1`：指定 Dockerfile 语法版本，通常放在文件第一行。
2. `# 注释内容`：添加注释。
3. 指令名通常使用大写，参数按指令要求填写。
4. Dockerfile 通常以 `FROM` 开始；全局 `ARG`、语法指令和注释可以写在 `FROM` 前。
5. 每条会改变文件系统的构建指令通常都会形成可复用的缓存层。

## FROM:

1. `FROM 镜像名:标签`：指定基础镜像，并开始一个构建阶段。
2. `FROM 镜像名@sha256:摘要`：按摘要固定基础镜像版本，可重复构建出更稳定的结果。
3. `FROM 镜像名:标签 AS 阶段名`：创建具名构建阶段，供多阶段构建引用。
4. 一个 Dockerfile 可以包含多个 `FROM`。

示例：

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
```

## WORKDIR:

1. `WORKDIR /路径`：设置后续 `RUN`、`COPY`、`ADD`、`CMD` 和 `ENTRYPOINT` 的工作目录。
2. 目录不存在时会自动创建。
3. 建议使用绝对路径，避免工作目录依赖基础镜像的设置。

示例：

```dockerfile
WORKDIR /app
```

## COPY:

1. `COPY 源路径 目标路径`：把构建上下文中的文件或目录复制到镜像中。
2. `COPY ["源路径", "目标路径"]`：JSON 数组写法，适合路径中含空格的情况。
3. `COPY --from=阶段名 源路径 目标路径`：从其他构建阶段复制文件。
4. 普通本地文件复制优先使用 `COPY`，语义比 `ADD` 更明确。

示例：

```dockerfile
COPY *.csproj ./
COPY --from=build /app/publish ./
```

## ADD:

1. `ADD 源路径 目标路径`：复制文件，并支持本地 tar 自动解压等附加功能。
2. 不需要附加功能时，优先使用 `COPY`。

示例：

```dockerfile
ADD app.tar.gz /app/
```

## RUN:

1. `RUN 命令 参数`：在镜像构建期间执行命令，并把结果写入镜像层。
2. `RUN ["程序", "参数1", "参数2"]`：exec form，直接执行程序。
3. 多个有关联的安装、清理命令可写在同一条 `RUN` 中，避免无用中间层。
4. `RUN` 是构建时执行；它不是容器启动命令。

示例：

```dockerfile
RUN dotnet restore
RUN ["dotnet", "publish", "-c", "Release", "-o", "/app/publish"]
```

## ARG:

1. `ARG 名称=默认值`：声明构建参数。
2. 构建时使用 `docker build --build-arg 名称=值 .` 传入。
3. `ARG` 主要在构建期间使用，不会像 `ENV` 一样自动成为运行时环境变量。
4. 不要通过 `ARG` 传递密码、令牌等秘密信息。

示例：

```dockerfile
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION
```

## ENV:

1. `ENV 名称=值`：设置环境变量。
2. 变量对后续构建指令有效，并会保留到由该镜像创建的容器中。
3. 运行容器时可以使用 `docker run -e 名称=新值` 覆盖。

示例：

```dockerfile
ENV ASPNETCORE_HTTP_PORTS=8080
```

## EXPOSE:

1. `EXPOSE 端口`：声明容器中的应用预计监听哪个端口。
2. `EXPOSE` 只提供镜像元数据，不会自动把端口发布到宿主机。
3. 实际映射端口仍需使用 `docker run -p 宿主机端口:容器端口`。

示例：

```dockerfile
EXPOSE 8080
```

## USER:

1. `USER 用户名`：设置后续 `RUN` 以及容器启动时使用的默认用户。
2. `USER 用户名:组名`：同时指定用户和组。
3. 应用不需要管理员权限时，优先使用非 root 用户运行。

示例：

```dockerfile
USER app
```

## ENTRYPOINT:

1. `ENTRYPOINT ["程序", "参数"]`：设置容器启动时执行的主程序。
2. 推荐使用 exec form，使主程序直接接收停止信号。
3. `docker run 镜像名 额外参数` 默认会把额外参数追加到 exec form 的 `ENTRYPOINT` 后。
4. Dockerfile 中存在多个 `ENTRYPOINT` 时，只有最后一个生效。

示例：

```dockerfile
ENTRYPOINT ["dotnet", "aspnetapp.dll"]
```

## CMD:

1. `CMD ["程序", "参数"]`：设置容器默认执行的命令。
2. `CMD ["参数1", "参数2"]`：配合 `ENTRYPOINT`，为主程序提供默认参数。
3. `docker run 镜像名 新命令或参数` 可以覆盖 `CMD`。
4. Dockerfile 中存在多个 `CMD` 时，只有最后一个生效。

示例：

```dockerfile
CMD ["--urls", "http://0.0.0.0:8080"]
```

## ENTRYPOINT与CMD的关系:

1. `ENTRYPOINT`：定义固定的主程序。
2. `CMD`：定义可被运行参数替换的默认命令或默认参数。
3. 两者配合时，常写为：

```dockerfile
ENTRYPOINT ["dotnet", "aspnetapp.dll"]
CMD ["--urls", "http://0.0.0.0:8080"]
```

最终默认执行：

```text
dotnet aspnetapp.dll --urls http://0.0.0.0:8080
```

## exec form与shell form:

1. exec form：`RUN ["程序", "参数"]`，不经过 shell，参数边界清晰。
2. shell form：`RUN 程序 参数`，经过默认 shell，支持变量展开、管道和重定向。
3. `ENTRYPOINT` 和 `CMD` 推荐使用 exec form，便于主进程接收停止信号。
4. exec form 必须使用 JSON 双引号，不能使用单引号。

示例：

```dockerfile
# exec form
ENTRYPOINT ["dotnet", "aspnetapp.dll"]

# shell form
RUN echo "building" > /tmp/build.txt
```

## LABEL:

1. `LABEL 键="值"`：为镜像添加名称、版本、维护者等元数据。

示例：

```dockerfile
LABEL org.opencontainers.image.title="aspnetapp"
LABEL org.opencontainers.image.version="1.0.0"
```

## HEALTHCHECK:

1. `HEALTHCHECK CMD 命令`：定义容器健康检查命令。
2. `--interval`：两次检查之间的间隔。
3. `--timeout`：单次检查的超时时间。
4. `--retries`：连续失败多少次后判定为不健康。
5. 健康检查所用工具必须真实存在于镜像中。

示例：

```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
```

## VOLUME:

1. `VOLUME ["/data"]`：声明容器中的数据挂载点。
2. 它不负责指定宿主机目录；实际挂载位置由 `docker run -v` 或 `--mount` 决定。

示例：

```dockerfile
VOLUME ["/app/data"]
```

## 其他指令:

1. `STOPSIGNAL SIGTERM`：设置停止容器时发送给主进程的信号。
2. `SHELL ["可执行文件", "参数"]`：修改 shell form 指令使用的默认 shell。
3. `ONBUILD 指令`：当前镜像作为其他镜像的基础镜像时，再触发指定指令；普通业务镜像较少使用。

## 多阶段构建:

1. 构建阶段使用包含编译工具的 SDK 镜像。
2. 运行阶段只保留程序产物和运行时，减小最终镜像体积与攻击面。
3. 使用 `COPY --from=构建阶段` 把发布产物复制到最终阶段。
4. 调整某个阶段前面的指令会使该阶段后续缓存失效，因此应先复制依赖描述文件，再恢复依赖，最后复制经常变化的源码。

## .dockerignore:

`.dockerignore` 用于排除不需要发送到构建上下文的文件，可减少传输内容并避免无关文件导致缓存失效。

示例：

```dockerignore
**/bin/
**/obj/
.git/
.vs/
.vscode/
*.user
*.suo
.env
*.pfx
```

## 完整示例：ASP.NET Core多阶段构建:

假设项目结构如下：

```text
项目目录/
├─ aspnetapp.sln
├─ aspnetapp/
│  ├─ aspnetapp.csproj
│  └─ 其他源代码
├─ Dockerfile
└─ .dockerignore
```

Dockerfile：

```dockerfile
# syntax=docker/dockerfile:1

# 第一阶段：使用 SDK 恢复依赖并发布程序
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# 先复制项目描述文件，使依赖未变化时可以复用 restore 缓存
COPY *.sln ./
COPY aspnetapp/*.csproj ./aspnetapp/
RUN dotnet restore

# 再复制源码并发布
COPY aspnetapp/. ./aspnetapp/
WORKDIR /source/aspnetapp
RUN dotnet publish -c Release -o /app/publish --no-restore

# 第二阶段：只保留 ASP.NET Core 运行时和发布产物
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "aspnetapp.dll"]
```

配套 `.dockerignore`：

```dockerignore
**/bin/
**/obj/
.git/
.vs/
.vscode/
*.user
*.suo
.env
*.pfx
```

构建镜像：

```bash
docker build -t aspnetapp:1.0 .
```

启动容器：

```bash
docker run -d --name aspnetapp -p 127.0.0.1:8080:8080 aspnetapp:1.0
```

访问地址：`http://127.0.0.1:8080`

## 常见错误:

1. 把 `RUN` 当成容器启动命令：`RUN` 只在构建镜像时执行。
2. 认为 `EXPOSE 8080` 会自动开放端口：它只声明端口，仍需使用 `-p`。
3. 同时写多个 `CMD` 或 `ENTRYPOINT`：只有最后一个生效。
4. 使用 shell form 启动主程序：可能导致停止信号无法正确传递。
5. 一开始就 `COPY . .`：源码任意变化都可能导致依赖恢复缓存失效。
6. 把密码写入 `ARG`、`ENV` 或镜像文件：镜像历史或配置中可能留下敏感信息。
7. 构建阶段和运行阶段都使用 SDK 镜像：会使最终镜像包含不必要的工具和文件。
8. 忘记 `.dockerignore`：会把 `bin`、`obj`、Git 文件或本地配置发送到构建上下文。
9. 使用不存在于镜像中的 `curl`、`wget` 编写健康检查：检查会持续失败。

## 参考资料:

1. [Dockerfile reference](https://docs.docker.com/reference/dockerfile/)
2. [Docker build best practices](https://docs.docker.com/build/building/best-practices/)
3. [Microsoft：使用 Docker 构建 ASP.NET Core 镜像](https://learn.microsoft.com/aspnet/core/host-and-deploy/docker/building-net-docker-images)
