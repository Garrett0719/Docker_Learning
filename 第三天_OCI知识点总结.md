# 第三天：OCI 与镜像可移植性

# OCI 的概念：

OCI（Open Container Initiative，开放容器倡议）是一套开放、厂商中立的容器标准，主要规定容器镜像格式、运行方式和分发方式。

# OCI 的作用：

1. 让不同工具能够构建、传输和运行同一种标准镜像。
2. 降低对 Docker 等单一厂商或工具的依赖。
3. 提高镜像在不同运行时、操作系统和云平台之间的可移植性与互操作性。
4. OCI 只定义底层标准，不负责 Kubernetes 式编排、业务部署流程或完整网络方案。

# OCI 的三类规范：

1. **Image Specification 镜像规范**：规定镜像由哪些对象组成，以及这些对象的格式。
2. **Runtime Specification 运行时规范**：规定如何根据 `rootfs + config.json` 组成的 OCI Bundle 创建、启动和管理容器进程。
3. **Distribution Specification 发布规范**：规定客户端与 Registry 之间 push、pull、查询和管理内容的 API。

# OCI 镜像的组成：

1. **Layer（镜像层）**
   - 每层是相对上一层的文件系统变更集，记录文件的新增、修改和删除。
   - 多层按顺序应用后形成容器的根文件系统。
   - Layer 不保存 `Env`、`Entrypoint` 等运行配置。

2. **Manifest (清单)**
   - 描述一份镜像由哪个 config 和哪些 layers 组成。
   - 通过 descriptor 引用这些对象，而不是直接包含对象内容。
   - 每个 descriptor 通常包含 `mediaType`、`digest` 和 `size`。

3. **Config (配置)**
   - 保存镜像级元数据和默认运行参数。
   - 常见内容：操作系统、CPU 架构、`User`、`Env`、`Entrypoint`、`Cmd`、`WorkingDir`、`ExposedPorts`。
   - 还包含有序的 layer DiffID、构建历史等信息。
   - 这些运行参数是默认值，创建容器时可以被覆盖。

4. **Image Index（补充）**
   - 指向多份 manifest，常用于同一镜像名称支持不同 CPU 架构或操作系统。

# Manifest、Config、Layer 的关系：

```text
Image Index（可选，多平台入口）
                ↓
             Manifest
          ┌─────┴─────┐
          ↓           ↓
       Config       Layers
     运行默认值     文件系统内容
```

1. Manifest 是“目录清单”。
2. Config 回答“默认怎么运行”。
3. Layers 回答“根文件系统里有什么”。

# Digest 的概念：

Digest 是根据内容计算出的加密哈希标识，常见格式为 `sha256:...`。

# Digest 的作用：

1. 内容发生变化，digest 就会变化。
2. 可用于校验下载内容是否完整、是否被篡改。
3. 通过 digest 拉取可以精确锁定同一份内容。
4. Registry 可按 digest 存储和复用相同 blob，减少重复数据。

# Tag 与 Digest 的区别：

1. **Tag**：便于人阅读的可变指针，如 `1.0`、`latest`；以后可以指向另一份 manifest。
2. **Digest**：由内容决定的不可变标识；内容不变，digest 不变。
3. 需要方便升级时使用 tag；需要严格可复现时使用 digest。
4. `latest` 只是默认 tag，不表示它一定是“最新构建”或“最稳定版本”。

# Registry 与 Distribution 的概念：

1. **Registry**：存放和提供镜像内容的服务，如 Docker Hub 或私有 Registry。
2. **Repository**：Registry 中某个镜像名称对应的内容集合，包含 manifests、blobs 和 tags。
3. **Blob**：按 digest 寻址的二进制内容，镜像层和 config 都可作为 blob 存储。
4. **Pull**：通常先获取 manifest，再根据其中的 digest 获取 config 和 layers。
5. **Push**：通常先上传 blobs，最后上传引用这些 blobs 的 manifest。
6. Distribution Spec 规定上述交互 API，不规定镜像内部格式；镜像内部格式由 Image Spec 规定。

# Docker、containerd、runc、Podman、Kubernetes 的职责：

1. **Docker**
   - 面向用户的完整容器平台。
   - 负责镜像构建、push/pull、容器、网络和卷等高级操作。
   - Docker CLI 把请求发送给 `dockerd`；底层通常继续调用 containerd 和 OCI runtime。

2. **containerd**
   - 管理宿主机上的容器完整生命周期。
   - 负责镜像传输与存储、快照、容器执行与监督等。
   - 可被 Docker 使用，也可通过 CRI 被 Kubernetes 使用。

3. **runc**
   - 低层 OCI Runtime 实现。
   - 根据 OCI Bundle 创建和运行真正的容器进程，并落实 Namespace、Cgroup 等内核配置。
   - 通常由 containerd、Docker 或 Podman 间接调用，不是主要面向普通用户的工具。

4. **Podman**
   - 无常驻守护进程的容器引擎，命令风格与 Docker 相近。
   - 负责构建、运行、分享 OCI 镜像和容器。
   - 底层同样依赖 runc、crun 等 OCI Runtime。

5. **Kubernetes**
   - 容器编排系统，负责调度、扩缩容、故障恢复和期望状态管理。
   - 通过 CRI 调用 containerd、CRI-O 等容器运行时。
   - Kubernetes 本身不是镜像格式，也不直接完成低层容器进程创建。

# 各组件的调用关系：

```text
用户 / Kubernetes
        ↓
Docker、Podman 或 CRI Runtime
        ↓
containerd / CRI-O 等生命周期管理层
        ↓
runc / crun 等 OCI Runtime
        ↓
Linux Namespace、Cgroup、文件系统
```

> 实际组合会因工具而异。例如 Podman 不必须经过 containerd；Kubernetes 通常通过 CRI 连接运行时。

# 参考资料：

- [Open Container Initiative](https://opencontainers.org/)
- [OCI Image Configuration](https://specs.opencontainers.org/image-spec/config/)
- [OCI FAQ](https://opencontainers.org/faq/)
- [OCI Image Specification](https://github.com/opencontainers/image-spec/blob/main/spec.md)
- [OCI Distribution Specification](https://github.com/opencontainers/distribution-spec/blob/main/spec.md)