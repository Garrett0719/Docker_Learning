# 第26天学习笔记：配置、Secret、Key Vault 与托管身份

> 根据第26天的学习资料及10道验收题整理。已合并重复知识点；“容易混淆”重点记录实际答错、回答不完整和反复混淆的内容。

## 1. 核心知识点

### 1.1 托管身份与 RBAC：身份不是权限

**托管身份（Managed Identity）**是 Azure 为应用管理的身份，让应用不必保存用于访问 Azure 的账号密码。

**RBAC**决定这个身份可以对哪些资源执行哪些操作。

```text
Managed Identity → 我是谁
Role             → 我能做什么
Scope            → 可以对哪些资源做
```

**容易混淆：**

你最初能区分身份和权限，但最终综合题又把配置中的 `identity` 理解成“这个身份有没有读取权限”。

正确理解：

- `identity`／`identityref`：**选择使用哪个身份**。
- 角色分配：**给这个身份授权**。
- 把身份绑定到 App，不等于它自动拥有 ACR 或 Key Vault 权限。

身份正确、权限正确之外，目标服务的网络访问条件也必须满足。[托管身份说明](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity)

### 1.2 系统分配与用户分配身份

| 对比 | 系统分配 System-assigned | 用户分配 User-assigned |
| --- | --- | --- |
| 生命周期 | 与所属 App 绑定 | 独立 Azure 资源 |
| 删除 App 后 | 身份随之删除 | 身份仍存在，除非另行删除 |
| 能否提前创建并授权 | 不能在所属 App 存在前准备该身份 | 可以 |
| 能否供多个 App 使用 | 不能作为共享身份绑定给其他 App | 可以 |
| 常见用途 | 单个已有应用使用自己的身份 | 提前授权、首次部署就访问私有资源 |

**容易混淆：**

你曾认为“创建 App 时直接拉私有镜像，系统分配身份更方便”。

通常 **用户分配身份更方便**：

```text
先创建身份 → 提前授予拉取权限 → 创建 App 时绑定并指定该身份
```

系统分配身份也支持拉镜像，但需要安排好身份创建和授权顺序，不是不能用。[托管身份拉取镜像](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull)

### 1.3 最小权限：角色要够用，范围要小

本日常见授权：

| 需求 | 角色 | 常见授权范围 |
| --- | --- | --- |
| 拉取私有 ACR 镜像 | `AcrPull` | 目标 ACR |
| 读取 Key Vault Secret 值 | `Key Vault Secrets User` | 目标 Key Vault |

注意两个前提：

- `AcrPull` 适用于传统 ACR RBAC 模式；启用 ABAC 仓库权限模式时，应使用 `Container Registry Repository Reader` 等适用角色。
- 本文 Key Vault 命令以 **Azure RBAC 权限模型**为前提，不与旧访问策略模型混用。[ACR 权限说明](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-rbac-built-in-roles-overview)

**容易混淆：**

你对最小权限和共享身份的判断基本正确：

- 多个 App 可以共享用户分配身份，但共享其权限也会扩大影响范围。
- 一个服务只需拉镜像，另一个还需读业务密钥，通常应拆分身份。
- `AcrPull` 不是推送权限，也不是读取 Key Vault 的权限。

**补正原批改：**订阅 Owner 权限很大，但不等于直接拥有所有数据读取权限。在 Key Vault RBAC 模式下，读取 Secret 值仍需相应数据权限；Owner 可以分配角色，不代表应给应用 Owner。[Key Vault RBAC](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)

### 1.4 普通配置、ACA Secret 与 Key Vault

| 内容 | 放在哪里 |
| --- | --- |
| 非敏感地址、开关等 | 普通环境变量或配置 |
| 敏感值 | ACA Secret，或优先保存在 Key Vault |
| 密钥的来源及使用关系 | 配置中保存引用，不保存真实值 |

**容易混淆：**

把密码直接写成普通环境变量配置，问题不只是“可能进入日志”，还可能出现在 YAML、部署脚本、配置导出和调试输出中。

用 Secret 引用后，**部署配置保存引用关系，容器运行时仍会得到真实值**。它不是让应用永远看不到密钥。

ACA Secret 属于单个 App，不是同环境所有应用自动共享的 Secret。[Secret 管理](https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets)

### 1.5 三个 ref 的职责必须分清

| 写法 | 作用 |
| --- | --- |
| `keyvaultref:密钥URI` | 去 Key Vault 的哪个地址取值 |
| `identityref:身份资源ID` | 用哪个托管身份访问 Key Vault |
| `secretref:ACA密钥名称` | 环境变量引用哪个 ACA Secret |

**容易混淆：**

你在第5、8题反复混淆：

- `keyvaultref` **不存放密钥**，只是指向密钥地址。
- `identityref` **不创建身份、不授予权限**，只是选择身份。
- `secretref` **不直接查 Key Vault**，它先引用当前 App 中的 ACA Secret。

CLI 中的 `keyvaultref`、`identityref`，在 YAML 中对应 `keyVaultUrl`、`identity`；环境变量使用 `secretRef`。[Secret 命令格式](https://learn.microsoft.com/en-us/cli/azure/containerapp/secret?view=azure-cli-latest)

### 1.6 C# 读取环境变量名，得到真实值

假设三个名字故意不同：

```text
Key Vault Secret：payment-provider-key
ACA Secret：payment-key
环境变量：PAYMENT_KEY
```

配置：

```text
PAYMENT_KEY=secretref:payment-key
```

C# 读取：

```csharp
var key = Environment.GetEnvironmentVariable("PAYMENT_KEY");
```

结果是密钥的**真实值**，不是 `payment-key`，也不是 Key Vault URL。

**容易混淆：**

你曾不确定 C# 应该读取哪个名字，最终综合题还回答“运行时拿不到真实 Secret”。

应该记住：

```text
仓库、YAML 中没有明文
≠
应用运行时拿不到真实值
```

本日采用平台 Key Vault 引用：**ACA 负责获取并注入，C# 只读取环境变量**，不是每次执行 `GetEnvironmentVariable` 都去访问 Key Vault。

三个名字可以不同，只要引用关系正确。[环境变量引用 Secret](https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets#referencing-secrets-in-environment-variables)

### 1.7 通过托管身份拉镜像，需要三步

1. App 绑定托管身份。
2. 身份拥有目标 ACR 的拉取权限。
3. Registry 配置明确指定使用该身份。

**容易混淆：**

“App 已绑定身份”不等于“拉镜像时已经选中了这个身份”。

另外：

- 禁用 ACR 管理员账户，只关闭管理员用户名／密码认证，不关闭托管身份认证。
- ACA 托管身份拉取要求 ACR 允许 ARM audience token。
- 本机 `az acr login` 成功，不代表 ACA 已具备拉取权限。
- 权限不能绕过 ACR 防火墙或网络限制。

ACR 拉取身份和 Key Vault 读取身份可以相同，也可以不同；两处都要正确配置。[镜像拉取说明](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull)

### 1.8 Secret 更新与 Revision 的关系

| 改动 | 是否创建新 Revision／如何生效 |
| --- | --- |
| 添加、修改 ACA Secret | 不创建；已有实例通常需重启或部署新版读取 |
| 修改环境变量的 `secretRef` | 属于模板变更，创建新 Revision |
| Key Vault 引用带版本号 | 固定使用指定版本 |
| Key Vault 引用不带版本号 | 跟随最新版本；平台在30分钟内获取，并自动重启通过环境变量引用它的活动 Revision |

**容易混淆：**

“不创建新 Revision”不等于“不需要重启”，也不等于程序会立刻读到新值。

普通 ACA Secret 更新与 Key Vault 无版本引用的自动轮换，不能混为一谈。[密钥轮换说明](https://learn.microsoft.com/en-us/azure/container-apps/manage-secrets#key-vault-secret-uri-and-secret-rotation)

### 1.9 应用可以使用密钥，但不能输出密钥

推荐只验证“是否加载”：

```csharp
app.Logger.LogInformation(
    "Payment key loaded: {Loaded}",
    !string.IsNullOrWhiteSpace(key));
```

不要记录：

```csharp
app.Logger.LogInformation("Payment key: {Key}", key);
```

**容易混淆：**

你知道第二种写法危险，但不清楚泄露路径：

```text
Key Vault → 应用取得真实值 → 日志写出真实值 → 日志读取者也能看到
```

结构化日志中的 `{Key}` **不会自动脱敏**。密钥也不能通过接口响应、异常信息或完整环境变量转储输出。

`Loaded: True` 只证明值非空，不能证明凭据有效；真正验收还应完成一次不泄密的业务调用。

如果真实密钥已经提交到 Git 或写入日志，应撤销／轮换，不能只删除当前文件里的那一行。

## 2. 核心机制与关系

### 2.1 两条访问路径，分别授权

```text
ACA 拉镜像
→ Registry 配置选择身份
→ 检查 ACR 拉取权限
→ 拉取私有镜像

ACA 获取业务密钥
→ Key Vault 引用选择身份
→ 检查 Key Vault 读取权限
→ 获取 Secret 值
→ 注入应用环境变量
```

### 2.2 Secret 的完整对应关系

```text
Key Vault：payment-provider-key
          │ keyVaultUrl + identity
          ▼
ACA Secret：payment-key
          │ secretRef
          ▼
环境变量：PAYMENT_KEY
          │
          ▼
C# 读取 PAYMENT_KEY，得到真实值
```

以下仅为配置结构示意，不是完整部署文件：

```yaml
properties:
  configuration:
    registries:
      - server: ACR登录服务器
        identity: 托管身份资源ID
    secrets:
      - name: payment-key
        keyVaultUrl: https://KeyVault名称.vault.azure.net/secrets/payment-provider-key
        identity: 托管身份资源ID
  template:
    containers:
      - name: 容器名称
        image: 镜像地址
        env:
          - name: PAYMENT_KEY
            secretRef: payment-key
```

这里只保存地址、身份和引用，没有真实密钥值；身份绑定与角色分配仍需另外完成。

## 3. 必须掌握的命令

> 以下是可在 PowerShell 中执行的 Azure CLI 单行模板。中文部分需替换，资源 ID 要填完整路径。身份名称、身份资源 ID、principalId 不是同一个值。
>
> 默认已有 App、ACR、Key Vault 和 Key Vault Secret；不重复资源创建、镜像构建流程，也不把真实密钥写进命令。

### 3.1 创建、查询并绑定用户分配身份

```powershell
az identity create -n 身份名称 -g 资源组名称

az identity show -n 身份名称 -g 资源组名称 --query "{resourceId:id,principalId:principalId,clientId:clientId}" -o yaml

az containerapp identity assign -n 容器应用名称 -g 资源组名称 --user-assigned 身份资源ID
```

作用：创建独立身份，取得标识，并让 App 使用它。

关键参数：`--user-assigned` 填身份资源 ID；后续授权使用 `principalId`。

如果选择系统分配身份，改用：

```powershell
az containerapp identity assign -n 容器应用名称 -g 资源组名称 --system-assigned

az containerapp identity show -n 容器应用名称 -g 资源组名称
```

作用：为已有 App 启用并查看系统身份。两种方案按需选择，不要求全部执行。[身份 CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/identity?view=azure-cli-latest)

### 3.2 查询资源范围并授权

```powershell
az acr show -n ACR名称 --query "{id:id,loginServer:loginServer,mode:roleAssignmentMode}" -o yaml

az keyvault show -n KeyVault名称 --query "{id:id,rbac:properties.enableRbacAuthorization}" -o yaml
```

作用：取得授权范围，并核对权限模型。

```powershell
az role assignment create --assignee-object-id 身份principalId --assignee-principal-type ServicePrincipal --role AcrPull --scope ACR资源ID

az role assignment create --assignee-object-id 身份principalId --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User" --scope KeyVault资源ID
```

作用：分别授予拉镜像、读密钥权限。

关键参数：

- `--assignee-object-id`：被授权身份的 principalId。
- `--scope`：限定到目标资源。
- ABAC 模式的 ACR 不照搬 `AcrPull`。
- 执行这些命令的人必须有角色分配权限；应用本身不需要 Owner。

### 3.3 配置 Registry 使用托管身份

```powershell
az containerapp registry set -n 容器应用名称 -g 资源组名称 --server ACR登录服务器 --identity 身份资源ID
```

作用：明确拉取该 Registry 镜像时使用哪个身份。

关键参数：用户分配身份填资源 ID；系统分配身份填 `system`。[Registry CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/registry?view=azure-cli-latest)

首次创建就拉取私有镜像时，可使用已提前授权的用户分配身份：

```powershell
az containerapp create -n 容器应用名称 -g 资源组名称 --environment 环境名称 --image 私有镜像地址 --user-assigned 身份资源ID --registry-server ACR登录服务器 --registry-identity 身份资源ID
```

作用：创建时同时绑定身份并指定镜像拉取身份。其他入口、资源配置按应用需要补充。

### 3.4 检查认证前提并关闭管理员账户

```powershell
az acr config authentication-as-arm show -r ACR名称
```

作用：检查是否允许 ARM audience token。若确实禁用且需要此拉取方式，再执行：

```powershell
az acr config authentication-as-arm update -r ACR名称 --status enabled
```

确认其他使用者也不再依赖管理员凭据后：

```powershell
az acr update -n ACR名称 --admin-enabled false

az acr show -n ACR名称 --query adminUserEnabled -o tsv
```

作用：禁用并确认管理员账户；结果应为 `false`。

### 3.5 添加 Key Vault 引用并注入环境变量

```powershell
az containerapp secret set -n 容器应用名称 -g 资源组名称 --secrets "ACA密钥名称=keyvaultref:KeyVault密钥URI,identityref:身份资源ID"

az containerapp update -n 容器应用名称 -g 资源组名称 --set-env-vars "环境变量名称=secretref:ACA密钥名称"
```

作用：先建立 ACA Secret，再让容器环境变量引用它。

关键参数：

- `KeyVault密钥URI`：密钥地址，不是密钥值。
- `identityref`：用户身份资源 ID，或已启用的系统身份 `system`。
- C# 读取的是左侧“环境变量名称”。[Secret CLI](https://learn.microsoft.com/en-us/cli/azure/containerapp/secret?view=azure-cli-latest)

### 3.6 核对配置与角色，不打印真实密钥

```powershell
az containerapp show -n 容器应用名称 -g 资源组名称 --query properties.configuration.registries -o yaml

az containerapp secret list -n 容器应用名称 -g 资源组名称

az containerapp show -n 容器应用名称 -g 资源组名称 --query "properties.template.containers[].env[].{name:name,secretRef:secretRef}" -o yaml

az role assignment list --assignee 身份principalId --scope 目标资源ID --include-inherited -o table
```

作用：分别核对拉取身份、Secret 名称、环境变量引用和授权。

关键参数：`--include-inherited` 包含上级范围继承的角色。Secret 列表**不要加 `--show-values`**，验收不需要打印密钥。

### 3.7 让现有 Revision 重新读取 Secret

```powershell
az containerapp revision list -n 容器应用名称 -g 资源组名称 -o table

az containerapp revision restart -n 容器应用名称 -g 资源组名称 --revision 修订版本名称
```

作用：更新普通 ACA Secret 后，按需重启使用它的 Revision。多个活动版本引用同一 Secret 时，都要考虑。

### 3.8 查看运行证据

```powershell
az containerapp logs show -n 容器应用名称 -g 资源组名称 --type system --tail 50

az containerapp logs show -n 容器应用名称 -g 资源组名称 --type console --tail 50
```

作用：system 看镜像拉取／启动事件，console 看应用的非敏感验证结果。

只看到旧副本还在运行，不能证明禁用管理员后仍可拉镜像。应通过一次新 Revision 部署或新副本启动验证。

## 4. 我的薄弱知识点总结

重点复习：

1. **身份选择与权限授予不同**：`identity` 指定“用谁”，角色决定“允许做什么”。
2. **三个 ref 不同**：`keyvaultref` 指地址，`identityref` 指身份，`secretref` 指 ACA Secret。
3. **C# 读取环境变量名**：得到真实值，不是 Secret 名或 URI。
4. **配置不含密钥，不代表运行时拿不到密钥**：这是安全注入的目的。
5. **首次部署的身份顺序**：用户分配身份可提前创建、提前授权。
6. **日志泄密的路径**：密钥一旦被应用输出，就进入另一套可读取、保存和导出的系统。

最小权限、按需求拆分共享身份，以及拉镜像时明确指定身份，你已经基本掌握，不重复列为错题。

## 5. 第26天最终复习清单

- [ ] 我能区分系统分配与用户分配身份，并解释首次部署如何选择。
- [ ] 我能区分身份资源 ID、principalId 和角色。
- [ ] 我能为目标资源配置最小权限，而不是给应用订阅 Owner。
- [ ] 我能解释三个 ref，并画出 Key Vault → ACA Secret → 环境变量 → C# 的关系。
- [ ] 我能禁用 ACR 管理员账户，并验证新实例仍可通过托管身份拉镜像。
- [ ] 我能添加 Key Vault 引用，验证应用取得并使用密钥，但不输出真实值。
- [ ] 我知道普通 Secret 更新与 Key Vault 自动轮换的生效方式不同。
- [ ] 我检查了源码、Git 历史、YAML、脚本和日志，没有真实凭据；若曾泄露，已轮换。
