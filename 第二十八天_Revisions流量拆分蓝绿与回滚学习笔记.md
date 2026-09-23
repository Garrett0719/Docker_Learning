# 第28天学习笔记

> 主题：ACA Revisions、流量拆分、蓝绿与回滚。根据[第28天问答记录](https://chatgpt.com/share/6aafa711-7484-83ee-8776-2d85e52f886e)及官方资料整理；只保留可复用知识，不记录临时实验故障。

## 1. 核心知识点

### 1.1 Revision：版本快照，不是运行实例

Revision 保存一次部署的镜像与容器运行配置。首次创建 App 就会产生初始 Revision；修改版本级配置，则创建新的 Revision。

| 配置或操作 | 是否创建新 Revision |
| --- | --- |
| 镜像、容器环境变量、CPU/内存、探针 | 是，属于 `properties.template` |
| 扩缩容规则、revision suffix | 是，属于 `properties.template` |
| Single/Multiple、Ingress、流量权重、Label | 否，属于应用级配置 |
| 应用 Secret 值 | 否；已有容器使用新值可能需要重启 |
| 按已有规则增加/减少 Replica | 否，只改变实例数量 |

**容易混淆：**你漏选了 revision suffix，也曾说“改变配置就会产生新 Revision”。判断依据是配置的作用范围，不是有没有发生修改。

不可变指版本级快照不原地修改，不代表流量、激活状态等也被冻结。修复 v2 的代码或版本级配置后，应部署 v3，而不是“把 v2 原地修好”。共享依赖恢复则不一定需要新 Revision。

依据：[Revisions](https://learn.microsoft.com/en-us/azure/container-apps/revisions)。

### 1.2 Single 与 Multiple：谁控制切流

| 模式 | 发布时的行为 | 适合什么情况 |
| --- | --- | --- |
| Single（默认） | 新版本准备好后平台自动切流，旧版本随后停用；新版本准备失败时旧版本继续服务 | 不需要手动灰度的发布 |
| Multiple | 可以保留多个 Active Revision，自己管理权重和停用时机 | 灰度、蓝绿、版本对比和快速回滚 |

Single 不代表历史上只能存在一个 Revision；发布交接期间新旧版本也可能短暂共存。

**容易混淆：**平台认为 Ready，不等于业务没有 Bug。若要先独立测试、再给 10%，应在发布候选版之前切到 Multiple，并把主流量明确固定到旧版本。

依据：[Revision 管理](https://learn.microsoft.com/en-us/azure/container-apps/revisions-manage)。

### 1.3 Active、Healthy、Traffic、Replica 是四件事

| 概念 | 回答的问题 |
| --- | --- |
| Active / Inactive | 这个版本是否启用、能否参与服务？ |
| 健康/就绪状态 | 运行和探针检查是否正常？ |
| Traffic Weight | 主应用入口分多少比例给它？ |
| Replica | 这个版本当前运行多少份实例？ |

**容易混淆：**你曾把 Active 理解成“已经接收主流量”。实际可以是 `Active + 主流量 0% + 测试 Label`。

Active 也不保证始终有运行中的 Replica：允许缩容到 0 时，可能等待请求或事件触发扩容。10% 是路由比例，不是 Replica 数量，也不表示此时才创建 Revision。

Inactive 版本若要恢复服务，应先 Activate，再确认运行/就绪和业务验证，最后分配流量。不要把历史健康状态当作当前可服务的保证。

依据：[Revision 生命周期](https://learn.microsoft.com/en-us/azure/container-apps/revisions#lifecycle)。

### 1.4 主应用 URL 与 Label URL

| 入口 | 路由方式 |
| --- | --- |
| Application FQDN（主应用域名） | 按配置的权重分给各 Revision |
| Label URL（标签域名） | 直达标签对应的 Revision，不按主入口比例抽签 |

例如 v1=100%、v2=0%，把测试标签指向 v2：普通用户从主域名进入 v1，测试者从标签域名进入 v2。

标签可移动到其他 Revision，标签 URL 保持不变；它不是镜像 tag。若主流量规则使用的是具体 Revision 名，移动测试标签不会自动改变该规则；若规则按标签指定，则应关注标签移动带来的路由变化。

**容易混淆：**你两次把 Label URL 称为“私密链接”。它只是专用入口，不自带保密或鉴权能力。外部 Ingress 下不能靠“别人不知道地址”保护测试版本。

0% 只是不分主入口流量，不等于停用、隔离、免费或绝对没人访问。测试版本若共享生产数据库，测试仍可能写入生产数据。

依据：[标签机制](https://learn.microsoft.com/en-us/azure/container-apps/revisions#labels)、[Traffic splitting](https://learn.microsoft.com/en-us/azure/container-apps/traffic-splitting)。

### 1.5 灰度与蓝绿：控制发布风险

- **灰度（Canary）：**先少量真实流量，再逐步扩大，如旧/新 `90/10 → 50/50 → 0/100`。
- **蓝绿（Blue-Green）：**新旧两套版本并存，候选版独立验证后切换生产流量，异常时切回。也可结合灰度逐步切换。
- Blue、Green 只是两套部署的名称，不是永远固定的“旧/新”身份。

所有权重合计必须为 100%。90/10 不保证每 10 次请求恰好 9 次旧版、1 次新版，应观察足够多的请求。

**容易混淆：**你给出了正确的增流顺序，但漏了最前面的独立验证。首次业务验证应通过候选版 Label URL 完成，而不是直接拿生产用户测试。

每一阶段观察版本返回值、错误率、延迟和关键业务结果；不是机械等待几秒就加流量。

依据：[Traffic splitting](https://learn.microsoft.com/en-us/azure/container-apps/traffic-splitting)、[Blue-green deployment](https://learn.microsoft.com/en-us/azure/container-apps/blue-green-deployment)。

### 1.6 回滚：先恢复服务，再修复代码

Known-good 指已经验证稳定、适合恢复服务的版本，不一定是最早的版本，也不只是“Healthy”的版本。

发生明显业务异常时：停止增流 → 已知良好版 100%、故障版 0% → 验证业务恢复 → 保留诊断证据并处理故障版 → 修复后发布新 Revision。

**容易混淆：**你在多道题里说“修好原来的 v2/v3 后继续灰度”。若改了代码或版本级配置，应产生新的 Revision，不能原地替换旧快照。

已有可用历史版本时，切流回滚通常不需要重新构建、推送镜像。若它 Inactive，先激活并确认可服务，再切流；不能先停掉唯一可用版本。

回滚只恢复流量目的版本，不会自动还原数据库数据、迁移、共享 Secret 或其他应用级配置。旧镜像也应保留并避免覆盖版本 tag。

依据：[蓝绿切流与回滚](https://learn.microsoft.com/en-us/azure/container-apps/blue-green-deployment)、[Revision 管理](https://learn.microsoft.com/en-us/azure/container-apps/revisions-manage)。

### 1.7 失败版本与“健康但业务出错”的区别

- 镜像拉取、启动或就绪失败：不要给它生产流量，先确认版本状态和平台日志。
- 探针正常，但关键接口返回 500：仍可能有业务 Bug；停止扩大流量，必要时立即切回 Known-good。
- 业务异常优先查对应版本的 Console 日志、请求日志和业务指标，不必机械地先查镜像或 System 日志。

**容易混淆：**你知道 Healthy 不等于无 Bug，但处理流程里缺少“恢复流量后验证业务恢复”。修改权重成功只是配置成功，不等于回滚验收完成。

## 2. 核心机制与关系

### 2.1 完整发布链路

```text
旧版本已知良好
→ 先启用 Multiple，并将旧版本明确固定为 100%
→ 发布候选 Revision，确认主入口仍只指向旧版本
→ 候选版本就绪，主流量 0%，通过 Label URL 验证
→ 旧/新 90/10 → 观察 → 50/50 → 观察 → 0/100
→ 持续验证，新版本成为下一次回滚的 Known-good

任一阶段出现严重异常
→ 停止增流 → Known-good 100%、故障版 0%
→ 验证主域名业务恢复 → 收集证据并停用故障版
→ 修复代码/配置 → 新 Revision → 重新验证与灰度
```

不要先在 Single 下发布候选版，等它正常后才切 Multiple：它可能已经被自动切到生产。

### 2.2 三个“不是”

- Revision 不可变 ≠ 整个 App 的所有配置冻结。
- Active ≠ 主入口有流量；主流量 0% ≠ 无法通过 Label 访问。
- Healthy ≠ 业务正确；切流成功 ≠ 已验证回滚成功。

## 3. 必须掌握的命令

以下使用中文占位名称，执行前替换。命令采用单行，适合直接放入 PowerShell；引号使用英文直引号。`-n` 指 App 名，`-g` 指资源组。

### 3.1 查看修订版、状态和流量

```powershell
az containerapp revision list -n 容器应用名称 -g 资源组名称 --all -o table
az containerapp revision show -n 容器应用名称 -g 资源组名称 --revision 修订版完整名称
az containerapp ingress traffic show -n 容器应用名称 -g 资源组名称
```

作用：确认版本是否存在、是否激活和健康，以及主入口权重。

关键参数：`--all` 包含 Inactive；`--revision` 使用完整名称，不只是 suffix。

### 3.2 发布前切换 Multiple，固定旧版本

```powershell
az containerapp revision set-mode -n 容器应用名称 -g 资源组名称 --mode multiple
az containerapp ingress traffic set -n 容器应用名称 -g 资源组名称 --revision-weight 已知良好修订版完整名称=100
```

作用：获得手动切流控制权，并防止发布候选版后自动接主流量。

关键参数：`--mode multiple` 允许多个活跃版本；`--revision-weight` 明确指定版本和比例。不要用 `latest=100` 代替固定旧版本，因为 latest 会随后续部署变化。

### 3.3 发布新镜像及版本标识

```powershell
az containerapp update -n 容器应用名称 -g 资源组名称 --image 新版本镜像地址 --revision-suffix 新修订版后缀 --set-env-vars APP_VERSION=版本标识
```

作用：部署候选版本。API 若读取 `APP_VERSION` 并通过版本接口返回，就能区分命中的版本。

关键参数：`--image` 使用可追踪的版本镜像；`--revision-suffix` 使用新的合法后缀；`--set-env-vars` 修改容器环境变量。不要仅覆盖同一个镜像 tag 来冒充版本发布。

发布后重新查看实际 Revision 名及流量配置，确认新版本未获得生产流量。

### 3.4 添加测试标签并独立访问

```powershell
az containerapp revision label add -n 容器应用名称 -g 资源组名称 --revision 新修订版完整名称 --label 测试标签名称
```

作用：提供直达候选版本的标签入口。

关键参数：`--label` 是标签名；标签实际值需符合命名要求。标签 URL 可从 Portal 的 Revision 详情复制，不必手工拼接。

```powershell
Invoke-RestMethod -Uri "https://标签域名/版本接口路径"
```

作用：验证候选版本返回的版本号及业务行为。路径需替换成 API 实际提供的接口。

### 3.5 灰度到 10%、50%，再全量

```powershell
az containerapp ingress traffic set -n 容器应用名称 -g 资源组名称 --revision-weight 旧修订版完整名称=90 新修订版完整名称=10
az containerapp ingress traffic set -n 容器应用名称 -g 资源组名称 --revision-weight 旧修订版完整名称=50 新修订版完整名称=50
az containerapp ingress traffic set -n 容器应用名称 -g 资源组名称 --revision-weight 旧修订版完整名称=0 新修订版完整名称=100
```

作用：分阶段扩大新版本流量。三条命令之间必须观察与验证，不应连续盲目执行。

关键参数：每项为 `修订版完整名称=权重`，合计 100。本文统一按具体 Revision 名切流，避免混淆标签移动与权重变更。

### 3.6 激活历史版本、回滚和停用故障版本

```powershell
az containerapp revision activate -n 容器应用名称 -g 资源组名称 --revision 已知良好修订版完整名称
az containerapp ingress traffic set -n 容器应用名称 -g 资源组名称 --revision-weight 已知良好修订版完整名称=100 故障修订版完整名称=0
```

作用：切回已验证的稳定版本。第一条仅在目标 Inactive 时需要；激活后先检查就绪，再执行切流。

```powershell
az containerapp revision deactivate -n 容器应用名称 -g 资源组名称 --revision 故障修订版完整名称
```

作用：业务恢复且诊断证据保留后，停用不再需要的故障版本。若还要通过 Label 复现，先保留 Active，但不要给主流量；注意访问控制和测试副作用。

### 3.7 验证主入口与读取故障版本日志

```powershell
az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.configuration.ingress.fqdn -o tsv
Invoke-RestMethod -Uri "https://主应用域名/版本接口路径"
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type console --revision 故障修订版完整名称 --tail 100
```

作用：检查真实入口返回的版本及业务结果，并读取目标版本日志。

关键参数：`--revision` 防止看错版本；`--tail` 控制日志条数。多副本或多容器时，根据需要再指定 `--replica`、`--container`。HTTP 请求日志需预先启用，不能假定始终存在。

命令参考：[Revision CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/revision)、[Label CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/revision/label)、[Traffic CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/ingress/traffic)、[Container App CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp)。

## 4. 我的薄弱知识点总结

以下对应已核实的第28天答题记录；正确题不重复列出。

- **配置边界（第4、5、31题）：**漏掉 suffix 和首次部署的初始 Revision；并非所有配置变更都创建 Revision。
- **独立验证（第13题）：**灰度之前，先通过候选版本入口验证，不直接给生产用户。
- **标签不是安全措施（第18、34题）：**Label 是定向入口，不是私密链接。
- **回滚闭环（第19、26、35题）：**先降低用户影响，切回后验证恢复；业务异常优先查应用证据。
- **修复产生新版本（第26、35题）：**修改代码/版本级配置后发布新 Revision，不是修改原来的故障 Revision。
- **状态分离（第29、30题）：**Inactive 需先激活；Active、健康、权重和副本数互不等同。
- **完整表达（第36题）：**当时请助手直接画图，尚不能据此认定已独立掌握；需要自己复述一次第2节发布链路。

## 5. 最终复习清单

- [ ] 我能判断哪些配置创建新 Revision，哪些只影响应用级设置。
- [ ] 我能区分 Single 的自动切换与 Multiple 的手动灰度。
- [ ] 我能解释 Active、Healthy、Traffic、Replica 的区别。
- [ ] 我知道 Label URL 为什么能访问 0% 版本，也知道它不自带安全隔离。
- [ ] 我能先独立验证，再按 10% → 50% → 100% 发布，并观察业务指标。
- [ ] 我能切回 Known-good、验证恢复，并将代码修复发布为新 Revision。
- [ ] 我知道回滚不会自动恢复数据库和共享配置。
