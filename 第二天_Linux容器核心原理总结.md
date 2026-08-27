# 第二天：Linux 容器核心原理总结

> 已忽略“手搓 Linux 容器”的具体实现步骤和命令，只保留核心原理与错题复盘。

# Namespace 的概念：

Namespace 是 Linux 内核提供的资源视图隔离机制。它把原本全局的系统资源包装起来，使处于不同 Namespace 的进程看到各自独立的资源视图。

# Namespace 的作用：

1. 隔离进程能“看到什么”，如进程号、网络、挂载点和主机名。
2. 让多个容器可以在同一宿主机上运行而互不干扰。
3. Namespace 主要负责隔离资源视图，不负责限制 CPU、内存等资源用量。

# 常见的 Namespace：

1. **PID Namespace**：隔离进程号和进程可见范围；同一进程在宿主机和容器中可显示不同 PID。
2. **Network Namespace**：隔离网卡、IP、路由、端口和网络协议栈。
3. **Mount Namespace**：隔离挂载点和文件系统挂载视图。
4. **User Namespace**：隔离并映射 UID、GID及相关权限能力。
5. **UTS Namespace**：隔离主机名和 NIS 域名。
6. **IPC Namespace**：隔离 System V IPC、POSIX 消息队列等进程间通信资源。
7. **Cgroup Namespace**：隔离进程看到的 Cgroup 根目录视图；它不等同于 Cgroup 的资源限制功能。
8. **Time Namespace**：隔离部分系统时钟视图，如启动时间和单调时钟偏移。

# Cgroup 的概念：

Cgroup（Control Group）是 Linux 内核把进程按层级分组，并通过资源控制器管理这些进程资源使用情况的机制。

# Cgroup 的作用：

1. **限制**：限制 CPU、内存、I/O、进程数量等资源用量。
2. **统计**：记录一组进程实际使用了多少资源。
3. **监控与控制**：便于观察、暂停或管理整组进程。
4. Cgroup 限制的是进程可从宿主机资源中使用多少，并不是给容器创建一套新的物理资源。

# OverlayFS 的概念：

OverlayFS 是 Linux 的联合文件系统。它把一个或多个 lower 目录树与 upper 目录树叠加，并通过 merged 挂载点提供统一视图。

# OverlayFS 的作用：

1. 复用镜像的只读层，减少重复存储。
2. 为每个容器提供独立的可写层，避免修改镜像本身。
3. 通过 Copy-on-Write（写时复制）快速创建容器并节省空间。

# Lower、Upper、Workdir、Merged 之间的关系：

```text
一个或多个 lower（镜像只读内容）
                 +
upper（当前容器的最终可写变化）
                 +
workdir（OverlayFS 内部工作目录）
                 ↓ 挂载
merged（进程看到的统一文件系统视图）
```

1. **lower**：底层目录树；Docker 场景中通常对应镜像的一个或多个只读层。
2. **upper**：可写层；当前容器的新建、修改和删除记录主要保存在这里。
3. **workdir**：OverlayFS 内部使用的辅助工作目录；需与 upper 位于同一文件系统。它不是用户编辑文件的普通存储层，也不是所有写操作都必须先经过的“缓存层”。
4. **merged**：lower 与 upper 叠加后的挂载视图，不是单独保存数据的一层。相同路径同时存在时，upper 会遮盖 lower。

# 创建、读取、修改文件时会发生什么：

1. **读取文件**
   - upper 中存在：读取 upper 版本。
   - upper 中不存在：读取 lower 版本。
   - 同名目录可能合并展示；同名普通文件优先显示 upper。

2. **创建文件**
   - lower 中原本不存在的新文件，直接创建到 upper。
   - 通过 merged 可以立即看到该文件。

3. **第一次修改 lower 中已有文件**
   - lower 只读，不能原地修改。
   - OverlayFS 执行 **copy-up**：把文件的数据与元数据复制到 upper，再修改 upper 中的副本。
   - lower 原文件保持不变。

4. **再次修改同一文件**
   - upper 已有副本，后续直接修改 upper，不再从 lower 重复 copy-up。

5. **补充：删除 lower 中的文件**
   - lower 不会被真正删除；upper 会用 whiteout 等标记将其在 merged 中隐藏。

# “昨天”问答中的重点：

1. `docker exec` 不是“打开容器”，而是在目标容器已有的 Namespace 和 Cgroup 中再启动一个进程。
2. 多个容器可以共享相同的镜像 lower，但各自拥有独立 upper，因此文件修改互不影响。
3. **Mount Namespace** 决定进程看到哪套挂载视图；**OverlayFS** 决定 lower 与 upper 如何叠加和写入。二者不能互相替代。
4. **PID Namespace** 管进程号与进程可见性；**Network Namespace** 管网络栈和端口空间；**Cgroup** 管资源用量。
5. 容器内显示 `uid=0` 不代表一定启用了 User Namespace，也不代表拥有宿主机 root 的全部权限。
6. 容器内部监听地址、Docker 宿主端口发布是两件事：`-p` 管端口转发，应用仍需监听合适的容器地址和端口。

# “昨天”答题错误与纠正：

1. **错误：把所有隔离现象都归因于 Namespace。**  
   纠正：容器修改文件而镜像不变，核心是 OverlayFS 和独立 upper；Mount Namespace 主要负责挂载视图。

2. **错误：把 User Namespace 叫作“UID Namespace”。**  
   纠正：正确名称是 User Namespace；仅看到容器内 `uid=0` 不能证明启用了 UID/GID 映射。

3. **错误：把 workdir 当成“先编辑，再传到 upper”的读写层。**  
   纠正：最终变化保存在 upper；workdir 只是 OverlayFS 内部辅助目录。

4. **错误：认为容器 PID 1 退出后，只要 `docker exec` 的 shell 还在，容器仍算运行。**  
   纠正：容器生命周期主要绑定初始主进程；PID 1 退出后，容器会停止。

5. **错误：认为两个进程共享同一 Network Namespace 时仍可同时监听 `0.0.0.0:8080`。**  
   纠正：共享 Network Namespace 就是共享同一端口空间；无端口复用配置时会冲突。

6. **错误：端口映射写成 `-p :5001:8080`。**  
   纠正：通常写成 `-p 5001:8080`，格式为“宿主端口:容器端口”。

7. **错误：分析文件系统隔离时漏掉 Mount Namespace，认为仅靠 OverlayFS 就足够。**  
   纠正：OverlayFS 提供分层与 Copy-on-Write；容器还需要独立挂载视图，二者职责不同。

8. **错误：看到访问失败便直接断定端口没有映射。**  
   纠正：如果已经指定 `-p`，应继续核对映射是否生效、应用是否启动、实际监听端口，以及监听的是 `0.0.0.0` 还是 `127.0.0.1`。

# 参考资料：

- [Containers From Scratch（Liz Rice）](https://www.youtube.com/watch?v=8fi7usYlodc)
- [Linux namespaces(7)](https://man7.org/linux/man-pages/man7/namespaces.7.html)
- [Linux cgroups(7)](https://man7.org/linux/man-pages/man7/cgroups.7.html)
- [Linux Kernel：OverlayFS](https://docs.kernel.org/filesystems/overlayfs.html)
- [共享对话：整理 Linux Namespace Cgroup](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)
