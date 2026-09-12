# 第二十天：扩缩容、滚动发布、回滚和 Job

> 日期：2026-09-12。依据：今天分享对话中的 13 道简答题、Kubernetes 官方资料、本地项目和 Minikube 实际状态。以下操作命令用于复习或重做实验。

## 一、先分清三个问题

| 问题 | 对应概念 | 例子 |
|---|---|---|
| 同时跑多少份？ | replicas / Scale | API 从 3 个实例扩到 6 个 |
| 跑哪个版本？ | Rolling Update / Rollback | 发布新镜像，失败后恢复旧模板 |
| 一直在线还是做完退出？ | Deployment / Job | Web API 长期运行，Console 做完退出 |
| 按固定时间执行吗？ | CronJob | 每天凌晨创建一个 Job |

水平扩容就是多开几份实例；垂直扩容是给一个实例更多 CPU、内存。今天重点是水平扩容。

资料：[应用扩缩容](https://kubernetes.io/docs/tutorials/kubernetes-basics/scale/scale-intro/)、[滚动更新](https://kubernetes.io/docs/tutorials/kubernetes-basics/update/update-intro/)。

## 二、今天实践的实际结果

本次读取的 context 是 minikube，资源位于当前默认 namespace。

| 验收项 | 观察到的证据 |
|---|---|
| API 三副本 | study-security 的 READY 为 3/3，UP-TO-DATE 为 3，AVAILABLE 为 3 |
| 故障版本 | revision 2 的镜像为 study-security:bad |
| 恢复正常版本 | revision 3 的镜像为 study-security:v3，当前三个副本可用 |
| Console Job 完成 | study-security-job 为 Complete，COMPLETIONS 为 1/1 |
| 正常退出和日志 | 对应 Pod 退出码为 0，原因 Completed；日志包含 All tasks completed. |

发布历史与今天对话中的故障发布、回滚过程相符。历史本身只能证明模板变化，不能单独证明当时输入的是哪条命令。

三个 API Pod 都在同一个 Minikube 节点上。因此三副本可应对部分实例故障，但不能抵御这个节点整体故障。

## 三、扩缩容：改数量

你的 deployment.yaml 已经有：

```yaml
spec:
  replicas: 3
```

命令方式：

```powershell
kubectl scale deployment/study-security --replicas=3
kubectl get deployment study-security
kubectl get pods -l app=study-security -o wide
```

- scale：修改期望副本数。
- --replicas=3：希望运行三个 Pod。
- -l：按标签筛选，只查看这个 API 的 Pod。
- -o wide：额外显示 IP、节点等信息。

只改 replicas 通常是在现有 ReplicaSet 下增减 Pod，不产生新的 Deployment revision。节点资源不足、镜像或配置有问题时，期望数量不一定能立刻实现，要检查 READY 和 AVAILABLE。

Service 根据标签选择后端，通常把流量交给就绪的 Pod。扩容不需要为每个 Pod 新建 Service，也不保证每个 HTTP 请求严格轮流分配，长连接可能一直使用同一个后端。

命令扩到六个后，如果文件仍写三个，下次 apply 可能又改回三个。长期配置要同步维护。手动 scale 也不等于已经设置自动扩容。

## 四、滚动发布：逐步替换模板

Deployment 的 spec.template 是创建 Pod 的模板。修改其中的镜像、启动参数或环境变量等内容，会触发发布。

```powershell
kubectl set image deployment/study-security study-security=study-security:bad
```

这条命令中：

- deployment/study-security：要修改的 Deployment。
- 等号左边 study-security：Pod 模板中的容器名。
- 等号右边 study-security:bad：新镜像名称和标签。

常见过程是创建新 ReplicaSet，增加新 Pod，逐步减少旧 Pod。不会进入原来的 Pod 里直接替换 DLL。

### 更新时为什么可能出现四个 Pod

默认 RollingUpdate 中两个参数都是 25%。期望副本数为 3 时：

| 参数 | 含义 | 结果 |
|---|---|---|
| maxSurge | 临时允许增加多少副本 | 3×25%，向上取整为 1 |
| maxUnavailable | 允许多少副本不可用 | 3×25%，向下取整为 0 |

典型现象是三个旧 Pod 加一个新 Pod。新 Pod 可用后再减少旧 Pod。正在终止的 Pod 可能尚未清理，因此观察到的总数也可能暂时超过四个。

“新 Pod 没 Ready，旧 Pod 一定一个都不删”需要结合策略判断。如果允许不可用副本数大于零，控制器可能先减少一部分旧副本。

### Readiness 决定何时能接流量

Running 不能单独证明应用已经能服务。Readiness 用于判断是否可以接流量；新版本一直不就绪，滚动发布可能卡住。

你当前 Program.cs 有 /health/ready 路由，但 deployment.yaml 没有 readinessProbe。仅仅提供路由，Kubernetes 不会自动调用它。

后续可在 API 容器配置下补充并验证：

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  periodSeconds: 5
```

健康检查的内容也要符合业务需要；探针和 rollout 成功后，仍要测试实际 API。

资料：[Deployment 更新策略与发布状态](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)。

## 五、故障镜像与排错

你的 Dockerfile.bad 使用：

```dockerfile
ENTRYPOINT ["dotnet", "this-file-does-not-exist.dll"]
```

镜像能构建成功，但启动时 DLL 不存在，进程退出。反复重启后可能出现 CrashLoopBackOff。它表示重启退避状态，不直接告诉你根本原因；这次原因是启动命令错误。

重做故障实验的命令：

```powershell
Set-Location E:\Docker_Learning\study_security\study_security
docker build -f Dockerfile.bad -t study-security:bad .
minikube image load study-security:bad
kubectl set image deployment/study-security study-security=study-security:bad
kubectl rollout status deployment/study-security --timeout=60s
kubectl get pods -l app=study-security
```

- -f：使用指定 Dockerfile。
- minikube image load：将镜像加载到 Minikube；宿主 Docker 有镜像，不代表节点也有。
- rollout status：观察发布进度。
- --timeout=60s：客户端最多等待一分钟；超时不会取消发布，也不会自动回滚。

找到故障 Pod 后：

```powershell
kubectl describe deployment study-security
kubectl describe pod <故障Pod名称>
kubectl logs <故障Pod名称>
kubectl logs <故障Pod名称> --previous
```

describe 看状态、退出原因和 Events；logs 看程序输出；--previous 看同一 Pod 中上一轮已终止容器的日志，仅在日志仍存在时可用。

ImagePullBackOff 是拉取镜像出问题；这次 DLL 不存在是启动失败，应分别排查。

Deployment 的 ProgressDeadlineExceeded 表示发布长时间没有进展。默认控制器只报告问题，不会自动恢复旧版本。

## 六、回滚：先查历史，再选健康模板

```powershell
kubectl rollout history deployment/study-security
kubectl rollout history deployment/study-security --revision=2
kubectl rollout history deployment/study-security --revision=3
```

当前实际历史：

```text
revision 2 → study-security:bad
revision 3 → study-security:v3
```

目前已经恢复，不能机械地再执行一次不带目标的 undo，否则可能回到故障版本。

通用命令：

```powershell
# 使用前先确认上一个模板是健康的
kubectl rollout undo deployment/study-security

# 指定已经核实的健康 revision
kubectl rollout undo deployment/study-security --to-revision=<健康编号>
kubectl rollout status deployment/study-security --timeout=120s
kubectl get deployment study-security
```

尖括号要替换为真实值。题目要求恢复 revision 5，就写 --to-revision=5。

需要记住：

1. revision 是发布历史编号，镜像标签是镜像标识，数字不一定相同。
2. 回滚把旧 Pod 模板重新设为目标，通常形成新的历史修订；不是删除后面所有历史。
3. revisionHistoryLimit 决定保留多少历史。清理掉的模板不能直接 undo 回去。
4. 回滚不恢复数据库内容，也不会自动恢复外部 ConfigMap、Secret 的旧值。
5. 回滚后检查镜像、副本和 API，并同步本地 YAML，防止下次 apply 又应用坏配置。

## 七、Job：完成工作就是成功

你的 Console 程序打印开始时间，循环处理三次，每次等待约一秒，再打印 All tasks completed. 和结束时间。

```csharp
Console.WriteLine($"Job started: {DateTime.UtcNow:O}");
for (int i = 1; i <= 3; i++)
{
    Console.WriteLine($"Processing task {i}/3...");
    await Task.Delay(1000);
}
Console.WriteLine("All tasks completed.");
Console.WriteLine($"Job finished: {DateTime.UtcNow:O}");
```

顶层语句正常结束会以退出码 0 退出。业务失败时应报告非零退出码或未处理的失败，不能捕获异常后仍把失败当成功。

同一个程序放进 Deployment，正常退出后也会按 Always 重启，可能反复处理同一批数据。Job 则以完成任务为目标。

当前 job.yaml：

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: study-security-job
spec:
  backoffLimit: 2
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: study-security-job
          image: study-security-job:v1
          imagePullPolicy: Never
```

| 设置 | 作用 |
|---|---|
| backoffLimit: 2 | Job 的失败重试上限 |
| restartPolicy: Never | 容器退出后，不在原 Pod 内重启它 |
| imagePullPolicy: Never | 禁止拉取，节点必须已有镜像 |
| completions | 需要累计成功多少次，当前简单配置默认 1 |
| parallelism | 希望同时运行多少个任务 Pod，当前默认 1 |

程序循环三次，仍然只算一个 Pod 成功，不是三个 completions。

构建、提交与检查：

```powershell
Set-Location E:\Docker_Learning\study_security\job-demo\StudySecurityJob
docker build -t study-security-job:v1 .
minikube image load study-security-job:v1
kubectl apply -f job.yaml
kubectl wait --for=condition=complete job/study-security-job --timeout=120s
kubectl get jobs
kubectl get pods -l job-name=study-security-job
kubectl logs job/study-security-job
```

本次实际日志包含三个 Processing task，随后 All tasks completed.；退出码为 0。

Job 为 Complete 1/1、Pod 为 0/1 Completed，是成功结果。程序结束了，不需要继续保持 Ready。

对已完成的同名 Job 再次 apply 不会自动重跑。复做实验可以用新名称创建 Job。已有 Job 的 Pod 模板通常不能直接修改，不能照搬 Deployment 的更新方式。

## 八、重点薄弱点：Never 为什么仍然出现新 Pod

第 8 题中，你能解释两个参数，但对多个失败 Pod 的原因表示不清楚。

```text
Pod A 中程序失败
    ↓
Never：原 Pod 内不重启容器
    ↓
Job 还允许重试
    ↓
Job 控制器创建 Pod B 再执行
```

| 配置 | 控制范围 | 可能看到什么 |
|---|---|---|
| Never | 原 Pod 内的容器重启行为 | Pod A Failed，随后出现 Pod B |
| OnFailure | 允许失败容器在原 Pod 内重启 | 同一个 Pod 的 RESTARTS 增加 |
| backoffLimit | 整个 Job 的失败重试预算 | 达到失败条件后标记 Failed |

不要把 backoffLimit 理解成最终一定恰好有几个 Pod；实际计数与重启策略、失败策略、控制器观察过程有关。

如果有多个失败 Pod，应分别执行 kubectl logs <Pod名称>。kubectl logs job/名称 不应被当成自动合并全部尝试的日志。

completions: 10、parallelism: 2 表示目标成功十次、通常同时运行两个。Kubernetes 不会自动为普通 Job 划分十份不同业务数据，工作分配仍要由程序等机制处理。

即使这两个值都是 1，任务也可能因故障恢复被重复启动。涉及写库、通知、扣款时，可以通过任务编号避免重复处理，这就是“重复执行也不产生重复结果”的设计。

资料：[Job 的完成与重试规则](https://kubernetes.io/docs/concepts/workloads/controllers/job/)。

## 九、今天 13 道题的个人复盘

多数概念回答正确。原对话评价约 92～95 分，属于教学参考，不是标准化考试成绩。

| 问题 | 你的回答或遗漏 | 应怎样修正 |
|---|---|---|
| 第 8 题：Job 重试 | 不清楚为何多个失败 Pod | Never 只限制原 Pod 内重启；Job 可新建 Pod 重试 |
| 第 11 题：回滚参数 | --tovision | --to-revision=5 |
| 第 11 题：故障判断 | 解释了保留旧 Pod，但漏了诊断步骤 | status → describe/logs → history → undo → 验证 |
| 第 12 题：查看日志 | kubectl logs job | kubectl logs job/study-security-job |
| 第 9 题：定时任务名称 | CornJob | CronJob |
| 第 13 题：机制对应 | 漏了“发布 v3” | 新版本发布对应 Rolling Update |

已经掌握得较好的部分：Deployment 与 Job 的选择、水平扩容、Scale 与模板更新的区别、Readiness 对滚动发布的影响、旧模板回滚、completions 与 parallelism、CronJob 的用途。

表达再严谨一点：不要说“replicas 保证永远有三个正常 Pod”，应说“控制器努力达到三个，能否实现还取决于资源和程序状态”。

额外实践观察：当前没有 readinessProbe，三个副本处于同一节点。这些是后续完善点，不是认定你答错的题目。

## 十、给 ACA 学习准备的通用模型

| 维度 | 今天的 Kubernetes | 以后理解 ACA 时关注 |
|---|---|---|
| 数量 | replicas / scale | 运行多少实例，什么时候增减 |
| 版本 | Pod 模板、发布、回滚 | revision 如何产生，哪个版本接流量 |
| 任务 | Job / CronJob | 长期服务还是有开始和结束的任务 |

这里只建立类比，不代表两个平台的 revision 是同一种对象。ACA 的具体配置、流量切换和 Job 触发方式，留到对应课程核对。

## 十一、复习检查

不看正文回答五个问题：

1. API 三副本扩成六副本，改哪个字段，是否新增发布版本？
2. 为什么更新期间可能看到新旧 Pod 共存？
3. 查 revision 5、回滚到 revision 5 的命令分别是什么？
4. 为什么 Job 的 0/1 Completed 可以是成功？
5. Never 和 backoffLimit 分别管什么？

答案要点：replicas；仅改数量不生成新发布；滚动策略允许额外副本并保留可用实例；history --revision=5、undo --to-revision=5；任务成功后退出；原 Pod 内是否重启与整个 Job 的重试预算。

验收应保留：三个可用副本、故障镜像和报错、恢复后的镜像与 API 响应、Job 完成状态与日志。本次整理核实了副本、发布历史、Job 日志和退出码，没有重新发送 API HTTP 请求。

## 参考资料

- [今天的 ChatGPT 对话](https://chatgpt.com/share/6a8fed97-be4c-83ee-bd9e-e30d2bd2ba36)
- [Scaling an application](https://kubernetes.io/docs/tutorials/kubernetes-basics/scale/scale-intro/)
- [Update an application](https://kubernetes.io/docs/tutorials/kubernetes-basics/update/update-intro/)
- [Kubernetes Jobs](https://kubernetes.io/docs/concepts/workloads/controllers/job/)
- [Deployment 详细说明](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)
