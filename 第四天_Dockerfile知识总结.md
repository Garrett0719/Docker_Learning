# 第四天：为 ASP.NET Core 编写正确的 Dockerfile

# Dockerfile 的概念：

Dockerfile 是描述镜像构建过程的文本文件。构建器按顺序执行指令，并生成镜像层及镜像运行配置。

# 常用 Dockerfile 指令：

1. **`FROM`**：指定基础镜像并开始新的构建阶段；多次 `FROM` 可组成多阶段构建。
2. **`WORKDIR`**：设置后续 `RUN`、`COPY`、`CMD`、`ENTRYPOINT` 等指令的工作目录；目录不存在时会创建。
3. **`COPY`**：把构建上下文、其他阶段或其他镜像中的文件复制进当前镜像。
4. **`RUN`**：在构建阶段执行命令，并把文件系统变化写入新的镜像层。
5. **`ENV`**：设置环境变量；会保存在镜像中，并影响后续构建指令和容器运行环境。
6. **`EXPOSE`**：声明应用预期监听的端口，只是镜像元数据，不会自动发布端口；运行时仍需 `-p` 或其他网络配置。
7. **`USER`**：设置后续 `RUN` 以及运行时 `ENTRYPOINT`、`CMD` 使用的默认用户和组；应用应尽量使用非 root 用户。
8. **`ENTRYPOINT`**：指定容器主要执行程序，适合相对固定、不希望被普通参数轻易替换的部分。
9. **`CMD`**：指定默认命令，或为 `ENTRYPOINT` 提供默认参数；`docker run 镜像 参数` 通常会替换 CMD。

# `RUN`、`ENTRYPOINT`、`CMD` 的区别：

1. `RUN` 在 **构建镜像时**执行，结果写入镜像层。
2. `ENTRYPOINT` 和 `CMD` 在 **启动容器时**生效。
3. 常见组合：`ENTRYPOINT` 固定主程序，`CMD` 提供可覆盖的默认参数。
4. Dockerfile 中多个 `CMD` 或 `ENTRYPOINT` 只有最后一个生效。

# Exec Form 与 Shell Form：

1. **Exec form**：`["程序", "参数1", "参数2"]`
   - 直接启动指定程序，不自动经过 shell。
   - 不自动展开 `$变量`、管道或重定向。
   - 更利于主进程接收停止信号，适合 `ENTRYPOINT` 和 `CMD`。

2. **Shell form**：`程序 参数1 参数2`
   - Linux 下通常通过 `/bin/sh -c` 执行。
   - 支持变量展开、管道、重定向和 `&&` 等 shell 功能。
   - shell 可能成为 PID 1，导致真实应用的信号接收和退出处理变复杂。

# `.dockerignore` 的概念与作用：

1. 从 build context 中排除不参与构建的文件，语法类似 `.gitignore`。
2. 减少发送给构建器的数据量，提高构建速度。
3. 避免 `bin/`、`obj/`、`.git/` 等本地文件污染镜像或使缓存失效。
4. 降低把密钥、配置文件等敏感内容误复制进镜像的风险。

# Build Context 与构建缓存：

1. Build context 是构建器允许 `COPY` 等指令读取的文件范围，不等于镜像本身。
2. Docker 按 Dockerfile 顺序检查缓存；前面某层失效后，后续相关层通常需要重新构建。
3. 不常变化的步骤放前面，频繁变化的源码放后面，可以提高缓存命中率。
4. .NET 常见顺序：先复制 `.csproj` 并 restore，再复制源码并 publish；源码改变时可复用依赖缓存。

# 多阶段构建：

1. 构建阶段使用包含 SDK 和编译工具的较大镜像。
2. 运行阶段使用较小的 ASP.NET Runtime 镜像。
3. 通过 `COPY --from=build` 只复制发布产物，不把 SDK、源码和临时文件带入最终镜像。
4. 作用：减小镜像体积、依赖数量和攻击面。

# 基础镜像原则：

1. 选择可信、维护活跃且满足需求的最小基础镜像。
2. 不安装与运行无关的软件包。
3. Tag 便于获得更新但可变；digest 可锁定内容但需主动更新。
4. 镜像是构建时快照，应定期重新构建以获取依赖和安全修复。

# 临时容器原则：

1. 容器应尽量做到可停止、删除并重新创建。
2. 重要持久数据不应只保存在容器可写层，应放到 Volume、数据库或外部存储。
3. 配置应尽量通过环境变量、配置文件挂载或部署系统注入。

# 为什么容器通常运行一个前台主进程：

1. 容器生命周期主要绑定 PID 1；主进程退出，容器通常停止。
2. 前台运行便于 Docker 收集标准输出和错误日志。
3. PID 1 能直接接收停止信号，便于优雅关闭。
4. “一个进程”是经验原则，更准确的说法是“一个主要职责”；主程序仍可创建必要的子进程。

# 参考资料：

- [Microsoft Learn：Containerize a .NET app](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container)
- [Dockerfile reference](https://docs.docker.com/reference/dockerfile/)
- [Docker build best practices](https://docs.docker.com/build/building/best-practices/)
