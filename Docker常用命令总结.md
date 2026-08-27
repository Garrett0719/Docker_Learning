# Docker 常用命令：

> 说明：`容器名`通常也可以替换为容器 ID；未指定镜像标签时默认使用 `latest`。带 `rm`、`prune`、`down -v` 的命令会删除数据，使用前应确认目标。

## 一、帮助与基本信息

1. `docker --help`：查看 Docker CLI 帮助和可用子命令。
2. `docker 命令 --help`：查看某条命令的用法与参数，例如 `docker run --help`。
3. `docker version`：查看 Docker 客户端、服务端及 API 版本。
4. `docker info`：查看 Docker Engine、容器、镜像、存储驱动等系统信息。

## 二、Registry 与镜像仓库

1. `docker login`：登录 Docker Hub。
2. `docker login Registry地址`：登录指定的镜像仓库。
3. `docker logout Registry地址`：退出指定镜像仓库的登录状态。
4. `docker search 关键词`：在 Docker Hub 搜索镜像。
5. `docker pull 镜像名:标签`：从 Registry 下载指定镜像。
6. `docker push 仓库地址/镜像名:标签`：把镜像上传到 Registry。

## 三、镜像构建与管理

1. `docker build -t 镜像名:标签 .`：使用当前目录作为 build context，根据 Dockerfile 构建镜像。
2. `docker build -f Dockerfile路径 -t 镜像名:标签 构建上下文`：使用指定 Dockerfile 和构建上下文构建镜像。
3. `docker build --pull --no-cache -t 镜像名:标签 .`：拉取较新的基础镜像并忽略构建缓存，完整重新构建。
4. `docker images`：列出本地镜像；等价于 `docker image ls`。
5. `docker images -a`：列出本地全部镜像，包括中间镜像。
6. `docker image inspect 镜像名:标签`：查看镜像的配置、层、架构和默认启动参数等详细信息。
7. `docker history 镜像名:标签`：查看镜像的构建历史和各层信息。
8. `docker tag 原镜像:标签 新仓库/新镜像:新标签`：为已有镜像创建新的名称或标签。
9. `docker rmi 镜像名:标签`：删除一个或多个本地镜像；等价于 `docker image rm`。
10. `docker image prune`：删除悬空镜像。
11. `docker image prune -a`：删除没有被任何容器使用的镜像，使用前应确认。
12. `docker save -o 镜像包.tar 镜像名:标签`：把镜像及其层和标签保存为 tar 文件。
13. `docker load -i 镜像包.tar`：从 `docker save` 生成的 tar 文件恢复镜像。

## 四、创建和运行容器

1. `docker run -d --name 容器名 -p 127.0.0.1:8080:8080 镜像名:标签`：根据镜像创建并后台运行指定名称的容器，把宿主机本地地址的 8080 端口映射到容器 8080。
2. `docker run -d --name 容器名 -p 8080:8080 镜像名:标签`：后台运行容器，并把宿主机所有网络接口的 8080 映射到容器 8080；可能允许外部访问。
3. `docker run --rm -it 镜像名:标签 /bin/sh`：创建交互式临时容器并进入 shell，退出后自动删除容器。
4. `docker run -d --name 容器名 -e KEY=VALUE 镜像名:标签`：启动容器并设置环境变量。
5. `docker run -d --name 容器名 --env-file .env 镜像名:标签`：从文件读取环境变量并启动容器。
6. `docker run -d --name 容器名 --mount type=volume,src=卷名,dst=/容器目录 镜像名:标签`：把命名 Volume 挂载到容器目录。
7. `docker run -d --name 容器名 --mount type=bind,src=宿主路径,dst=/容器目录 镜像名:标签`：把宿主机文件或目录绑定挂载到容器。
8. `docker run -d --name 容器名 --network 网络名 镜像名:标签`：启动容器并连接到指定 Docker 网络。
9. `docker run -d --name 容器名 --restart unless-stopped 镜像名:标签`：设置容器异常退出或 Docker 重启后的自动重启策略。
10. `docker run -d --name 容器名 --memory 512m --cpus 1.0 镜像名:标签`：限制容器最多使用约 512MB 内存和 1 个 CPU 的计算配额。
11. `docker create --name 容器名 镜像名:标签`：只创建容器但不启动。

## 五、查看容器

1. `docker ps`：列出正在运行的容器；等价于 `docker container ls`。
2. `docker ps -a`：列出全部容器，包括已停止的容器。
3. `docker ps -q`：只输出正在运行容器的 ID，便于脚本处理。
4. `docker inspect 容器名`：查看容器的配置、状态、网络、挂载和端口等底层信息。
5. `docker top 容器名`：查看容器中正在运行的进程。
6. `docker stats`：实时查看所有运行中容器的 CPU、内存、网络和 I/O 使用情况。
7. `docker stats 容器名`：只实时查看指定容器的资源使用情况。
8. `docker port 容器名`：查看容器端口与宿主机端口的映射关系。
9. `docker diff 容器名`：查看容器文件系统相对镜像发生的新增、修改和删除。

## 六、启动、停止与删除容器

1. `docker start 容器名`：启动已经存在但处于停止状态的容器。
2. `docker start -ai 容器名`：启动停止的容器并连接其输入、输出。
3. `docker stop 容器名`：向容器主进程发送停止信号，等待后再强制终止。
4. `docker stop -t 30 容器名`：给容器最多 30 秒进行优雅退出。
5. `docker restart 容器名`：重启容器。
6. `docker kill 容器名`：立即向容器发送强制终止信号；通常仅在无法正常停止时使用。
7. `docker pause 容器名`：暂停容器中的全部进程。
8. `docker unpause 容器名`：恢复被暂停的容器进程。
9. `docker rename 原容器名 新容器名`：修改容器名称。
10. `docker rm 容器名`：删除已经停止的容器。
11. `docker rm -f 容器名`：强制停止并删除容器，使用前应确认。
12. `docker container prune`：删除全部已停止容器，使用前应确认。
13. `docker wait 容器名`：等待容器停止并输出退出码。

## 七、日志、命令执行与文件复制

1. `docker logs 容器名`：查看容器日志。
2. `docker logs -f 容器名`：持续跟踪容器的新日志。
3. `docker logs --tail 100 容器名`：只查看最后 100 行日志。
4. `docker logs -f --tail 100 容器名`：先显示最后 100 行，再持续跟踪新日志。
5. `docker exec -it 容器名 /bin/sh`：在正在运行的容器中启动交互式 shell。
6. `docker exec 容器名 命令 参数`：在正在运行的容器中执行一次命令。
7. `docker attach 容器名`：连接到容器主进程的标准输入、输出和错误流；它不同于启动新进程的 `docker exec`。
8. `docker cp 本地路径 容器名:/容器路径`：把本地文件或目录复制到容器。
9. `docker cp 容器名:/容器路径 本地路径`：把容器中的文件或目录复制到本地。
10. `docker export -o 容器文件系统.tar 容器名`：导出容器当前文件系统；不包含镜像历史、标签和 Volume 数据。
11. `docker commit 容器名 新镜像名:标签`：把容器可写层变化保存为镜像；正式项目通常更推荐用 Dockerfile 保证可复现。

## 八、Volume 数据卷

1. `docker volume ls`：列出本地 Volume。
2. `docker volume create 卷名`：创建命名 Volume。
3. `docker volume inspect 卷名`：查看 Volume 的挂载位置、驱动和标签等信息。
4. `docker volume rm 卷名`：删除未被容器使用的指定 Volume；其中的数据会被删除。
5. `docker volume prune`：删除未被容器引用的匿名 Volume，使用前应确认。
6. `docker volume prune -a`：删除全部未被容器引用的匿名和命名 Volume，可能造成数据丢失，必须谨慎。

## 九、Docker 网络

1. `docker network ls`：列出 Docker 网络。
2. `docker network create 网络名`：创建用户自定义网络；默认使用 bridge 驱动。
3. `docker network inspect 网络名`：查看网络配置及已连接的容器。
4. `docker network connect 网络名 容器名`：把运行中的容器连接到指定网络。
5. `docker network disconnect 网络名 容器名`：断开容器与指定网络的连接。
6. `docker network rm 网络名`：删除未被容器使用的网络。
7. `docker network prune`：删除全部未使用的网络，使用前应确认。

## 十、磁盘占用与清理

1. `docker system df`：查看镜像、容器、Volume 和构建缓存的磁盘占用。
2. `docker system df -v`：查看更详细的磁盘占用信息。
3. `docker system prune`：删除已停止容器、未使用网络、悬空镜像和未使用构建缓存。
4. `docker system prune -a`：在普通清理基础上，删除所有未被容器使用的镜像，使用前应确认。
5. `docker system prune -a --volumes`：进一步清理未使用的镜像及匿名 Volume 等资源，可能造成数据丢失，必须谨慎。
6. `docker builder prune`：删除未使用的构建缓存。

## 十一、Docker Compose

1. `docker compose up`：根据 Compose 文件创建并启动多容器应用，在前台显示日志。
2. `docker compose up -d`：在后台创建并启动多容器应用。
3. `docker compose up -d --build`：重新构建需要构建的服务镜像，然后后台启动。
4. `docker compose ps`：查看当前 Compose 项目的服务容器状态。
5. `docker compose logs -f`：持续查看当前项目所有服务的日志。
6. `docker compose logs -f 服务名`：持续查看指定服务的日志。
7. `docker compose exec 服务名 命令`：在指定服务的运行中容器内执行命令。
8. `docker compose run --rm 服务名 命令`：为指定服务创建一次性容器执行命令，结束后删除。
9. `docker compose build`：构建或重新构建 Compose 服务镜像。
10. `docker compose pull`：拉取 Compose 文件中引用的服务镜像。
11. `docker compose stop`：停止服务容器，但保留容器和网络等资源。
12. `docker compose start`：重新启动由 `stop` 停止的服务容器。
13. `docker compose restart`：重启服务容器。
14. `docker compose down`：停止并删除项目容器及默认创建的网络；默认不删除命名 Volume。
15. `docker compose down -v`：在 `down` 基础上同时删除相关 Volume，其中的数据会丢失。
16. `docker compose config`：解析、校验并输出合并后的 Compose 配置。

## 十二、Context

1. `docker context ls`：列出可用的 Docker 连接上下文。
2. `docker context show`：显示当前正在使用的 context。
3. `docker context use Context名`：切换 Docker CLI 当前连接的 Docker Engine。

# 常用参数速记：

1. `-d`：后台运行容器。
2. `-i`：保持标准输入打开。
3. `-t`：分配伪终端；通常与 `-i` 合写成 `-it`。
4. `--name`：指定容器名称。
5. `--rm`：容器退出后自动删除容器及关联的匿名 Volume。
6. `-p 宿主IP:宿主端口:容器端口`：发布并映射容器端口。
7. `-e KEY=VALUE`：设置容器环境变量。
8. `--env-file 文件`：从文件读取环境变量。
9. `--mount`：挂载 Volume、宿主目录或 tmpfs；官方更推荐其明确的键值语法。
10. `-v`：以简写语法挂载 Volume 或宿主路径。
11. `--network`：指定容器连接的网络。
12. `-f`：在不同命令中常表示指定文件或持续跟踪，具体含义应结合命令查看 `--help`。
13. `-a`：不同命令中可能表示全部资源或附加流，具体含义应结合命令判断。

# 参考资料：

- [Docker CLI reference](https://docs.docker.com/reference/cli/docker/)
- [docker container](https://docs.docker.com/reference/cli/docker/container/)
- [docker image](https://docs.docker.com/reference/cli/docker/image/)
- [docker network](https://docs.docker.com/reference/cli/docker/network/)
- [docker volume](https://docs.docker.com/reference/cli/docker/volume/)
- [docker compose](https://docs.docker.com/reference/cli/docker/compose/)
