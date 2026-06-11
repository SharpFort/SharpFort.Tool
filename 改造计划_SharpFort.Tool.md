# SharpFort.Tool CLI 改造实施计划

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** 将 Yi.Abp.Tool 改造为 SharpFort.Tool — 一个自包含的 CLI 脚手架工具，无需远程服务器

**Architecture:** 合并 CLI 客户端与 Web 服务端为单一进程，直接内嵌 Gitee API 调用和模板生成逻辑，消除 HTTP 远程调用层

**Tech Stack:** .NET 8, ABP Framework, Autofac, Microsoft.Extensions.CommandLineUtils, HtmlAgilityPack

**改造前架构:**
```
yi-abp CLI → HTTP 代理 → ccnetcore.com:19009 (远程服务器) → Gitee API
```

**改造后架构:**
```
sharpfort CLI → Gitee API (直接调用)
```

---

## 前置准备

### 准备 A: 创建 Git 分支

```bash
cd C:/Users/财务/Desktop/Yi-main/Yi.Abp.Net8
git checkout -b feature/sharpfort-tool-standalone
git status
```

### 准备 B: Fork 模板仓库

1. 登录 Gitee，Fork `ccnetcore/yi-template` 到你的账号
2. Clone 到本地
3. 全局替换命名空间（详见 Phase 1）

### 准备 C: 获取 Gitee Access Token

1. Gitee → 设置 → 私人令牌 → 生成新令牌
2. 权限勾选: `projects` (读仓库)、`user_info`
3. 保存令牌备用

---

## Phase 1: 模板仓库改造

line1
line2

## Phase 2: CLI 项目依赖链改造 (核心变更)

这是整个改造最关键的部分 — 将 CLI 从 HTTP 远程调用模式变为直接内嵌业务逻辑模式。

### Task 2.1: 修改 Yi.Abp.Tool.csproj 引用链

**Objective:** 删除 HTTP 代理层引用，添加 Application 层直接引用

**Files:**
- Modify: `tool/Yi.Abp.Tool/Yi.Abp.Tool.csproj`

**Step 1: 删除旧的引用**

定位并删除：
```xml
<ProjectReference Include="..\Yi.Abp.Tool.HttpApi.Client\Yi.Abp.Tool.HttpApi.Client.csproj" />
```

**Step 2: 添加新的引用**

在同一位置添加：
```xml
<ProjectReference Include="..\Yi.Abp.Tool.Application\Yi.Abp.Tool.Application.csproj" />
```

**Step 3: 添加 appsettings.json 复制到输出目录**

在 `<ItemGroup>` 中添加：
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**完整修改后的 csproj（关键部分）：**
```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.CommandLineUtils" Version="1.1.1" />
    <PackageReference Include="Volo.Abp.Autofac" Version="$(AbpVersion)" />
</ItemGroup>

<ItemGroup>
  <!-- 修改前: HttpApi.Client -->
  <!-- 修改后: Application (直接引用真实服务) -->
  <ProjectReference Include="..\Yi.Abp.Tool.Application\Yi.Abp.Tool.Application.csproj" />
</ItemGroup>

<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**Step 4: 验证**

```bash
cd tool/Yi.Abp.Tool
dotnet restore
# 预期: 成功，无报错
```

---

### Task 2.2: 修改 YiAbpToolModule.cs 依赖声明

**Objective:** 切换 ABP 模块依赖从 HttpApiClientModule 到 ApplicationModule

**Files:**
- Modify: `tool/Yi.Abp.Tool/YiAbpToolModule.cs`

**当前代码:**
```csharp
[DependsOn(typeof(YiAbpToolHttpApiClientModule))]
public class YiAbpToolModule : AbpModule
{
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices.Default =
                new RemoteServiceConfiguration("https://ccnetcore.com:19009");
        });
    }
}
```

**改为:**
```csharp
[DependsOn(typeof(YiAbpToolApplicationModule))]  // 依赖改为 Application 模块
public class YiAbpToolModule : AbpModule
{
    // 删除 PostConfigureServices 整个方法
    // 不再需要配置远程服务地址
}
```

**Step 1: 修改 [DependsOn] 特性**

改一行：`typeof(YiAbpToolHttpApiClientModule)` → `typeof(YiAbpToolApplicationModule)`

**Step 2: 删除 PostConfigureServices 方法**

移除整个方法体。

**Step 3: 验证编译**

```bash
cd tool/Yi.Abp.Tool
dotnet build
# 预期: 编译成功
```


---

### Task 2.3: 创建 CLI 项目的 appsettings.json

**Objective:** 合并后 Domain 层需要从配置文件读取 GiteeAccession 和 ToolOptions

**Files:**
- Create: `tool/Yi.Abp.Tool/appsettings.json`

**Step 1: 创建配置文件**

```json
{
  "ToolOptions": {
    "TempDirPath": "temp"
  },
  "GiteeAccession": "你的Gitee访问令牌"
}
```

**配置说明:**
- `ToolOptions.TempDirPath`: 模板下载和生成的临时目录。因为是本地运行，设相对路径即可。程序启动时会自动创建该目录
- `GiteeAccession`: Gitee 个人访问令牌，用于调用 Gitee API v5

**Step 2: 将 appsettings.json 设为始终复制**

已在 Task 2.1 的 csproj 中添加了 `<CopyToOutputDirectory>Always</CopyToOutputDirectory>`。

**Step 3: 配置 .gitignore**

确认 `.gitignore` 包含：
```
appsettings.json
```
或至少排除包含令牌的配置：
```
**/appsettings.json
!**/appsettings.Development.json
```

> 生产令牌不应提交到 Git。建议开发时使用 `appsettings.Development.json` 存储令牌，`appsettings.json` 仅保留模板结构。

---

### Task 2.4: 修改 GiteeManager.cs 仓库常量

**Objective:** 将 Gitee API 调用指向你自己的模板仓库

**Files:**
- Modify: `tool/Yi.Abp.Tool.Domain/GiteeManager.cs`

**当前常量:**
```csharp
private const string Owner = "ccnetcore";
private const string Repo = "yi-template";
```

**改为:**
```csharp
private const string Owner = "你的Gitee用户名";
private const string Repo = "yi-template";  // Fork 后的仓库名可保持不变
```

**Step 1: 修改 Owner**

将 `"ccnetcore"` 改为你的 Gitee 用户名。

**Step 2: 确认 Repo**

如果 Fork 后仓库名仍是 `yi-template`，不需要改。如果改名了，同步修改。

---

### Task 2.5: 修改 SetNameReplace() 替换规则

**Objective:** 匹配新模板中的命名空间占位符

**Files:**
- Modify: `tool/Yi.Abp.Tool.Application.Contracts/Dtos/TemplateGenCreateInputDto.cs`

**当前代码:**
```csharp
public void SetNameReplace()
{
    ReplaceStrData.Add("Yi.Abp", Name);
    ReplaceStrData.Add("YiAbp", Name.Replace(".", ""));
}
```

**改为:**
```csharp
public void SetNameReplace()
{
    // 主命名空间替换
    ReplaceStrData.Add("SharpFort", Name);
    ReplaceStrData.Add("SharpFort", Name.Replace(".", ""));
    
    // 框架命名空间替换
    ReplaceStrData.Add("SharpFort.Framework", Name + ".Framework");
    ReplaceStrData.Add("SharpFortFramework", Name.Replace(".", "") + "Framework");
}
```

**说明:**

| 模板中的占位符 | 将被替换为 | 示例 (Name="MyCompany.Crm") |
|----------------|-----------|---------------------------|
| `SharpFort` | 模块名 | `MyCompany.Crm` |
| `SharpFort.Framework` | 模块名.Framework | `MyCompany.Crm.Framework` |
| `SharpFortFramework` | 模块名(去点)Framework | `MyCompanyCrmFramework` |

> 注意：原代码中两个 key 都是 "Yi.Abp" → Name 和 "YiAbp" → Name.Replace(".","")
> 新模板中占位符是 "SharpFort"（不是 "SharpFort.Abp"），所以两个 key 都是 "SharpFort"
> 但因为 Dictionary 不允许重复 key，实际只需保留一个。
> **请根据你模板中的实际占位符调整！**

**修正版（避免重复 key）：**
```csharp
public void SetNameReplace()
{
    // SharpFort 出现在命名空间、目录名、文件名中 → 替换为模块名
    ReplaceStrData.Add("SharpFort", Name);
    
    // SharpFort.Framework 出现在框架引用中 → 替换为模块名.Framework
    ReplaceStrData.Add("SharpFort.Framework", Name + ".Framework");
}
```


---

### Task 2.6: 修改 CloneCommand.cs 仓库地址

**Objective:** clone 命令指向你自己的源码仓库

**Files:**
- Modify: `tool/Yi.Abp.Tool/Commands/CloneCommand.cs`

**当前代码:**
```csharp
private const string CloneAddress = "https://gitee.com/ccnetcore/Yi";
```

**改为:**
```csharp
private const string CloneAddress = "https://gitee.com/你的用户名/Yi";
```

---

### Task 2.7: 修复 NewCommand.cs 的路径创建 Bug

**Objective:** 修复当 `-p` 指定目录不存在时，创建目录后直接 return 跳过模板生成的 Bug

**Files:**
- Modify: `tool/Yi.Abp.Tool/Commands/NewCommand.cs`

**当前代码（有 Bug 的部分）:**
```csharp
if (pathOption.HasValue())
{
    path = pathOption.Value();
    if (!Directory.Exists(path))
    {
        Directory.CreateDirectory(path);
        return 0;   // ← BUG! 创建目录后直接返回，跳过模板生成
    }
}
```

**修改为:**
```csharp
if (pathOption.HasValue())
{
    path = pathOption.Value();
    if (!Directory.Exists(path))
    {
        Directory.CreateDirectory(path);
        // 删除 return 0;  ← 继续执行模板生成
    }
}
```

**Verification:** 执行 `sharpfort new TestMod -p ./newdir`，目录不存在时应正常生成模板。

---

### Task 2.8: 修改 AddModuleCommand.cs 子项目列表

**Objective:** 对齐 add-module 期望的子项目与你模板实际生成的子项目

**Files:**
- Modify: `tool/Yi.Abp.Tool/Commands/AddModuleCommand.cs`

**当前代码中的子项目列表:**
```csharp
var dotnetSlnCommandPart = new List<string>() { 
    "Application", 
    "Application.Contracts", 
    "Domain", 
    "Domain.Shared", 
    "SqlSugarCore"       // ← 你的模板是否有这个？
};
```

**修改为（根据你模板实际生成的项目）:**
```csharp
var dotnetSlnCommandPart = new List<string>() { 
    "Application", 
    "Application.Contracts", 
    "Domain", 
    "Domain.Shared", 
    "SqlSugarCore",       // 如果你的模板有这个 → 保留
    // 如果有其他子项目，在这里添加
};
```

**Verification:** 用 `new` 命令生成一个模块后，检查目录结构是否与这个列表匹配。

---

### Task 2.9: 处理 NugetCrawlerManager 缓存依赖

**Objective:** Domain 层的 `NugetCrawlerManager` 依赖 `IDistributedCache`（通常是 Redis），合并后本地没有 Redis 会报错

**Files:**
- Inspect: `tool/Yi.Abp.Tool.Domain/NugetCrawlerManager.cs`

**当前代码:**
```csharp
public NugetCrawlerManager(IDistributedCache<NugetResult> cache)
{
    this.NugetResult = cache.GetOrAdd("NugetResult", ...);
}
```

**问题分析:**
- `IDistributedCache` 在 ABP 中默认需要 Redis
- 这个爬虫在 CLI 中未被任何命令使用（只被 Web 的 NueGetInfoService 调用）
- 合并后 DI 容器仍会尝试解析它

**方案 A（推荐）: 移除 NugetCrawlerManager**
```csharp
// 删除: tool/Yi.Abp.Tool.Domain/NugetCrawlerManager.cs
// 删除: tool/Yi.Abp.Tool.Application/NueGetInfoService.cs
```

**方案 B: 改用内存缓存**
```csharp
// 改用 IMemoryCache 或静态字段替代 IDistributedCache
// 但这需要修改 NugetCrawlerManager 构造函数
```

**推荐选方案 A** — 这个爬虫功能在自包含 CLI 中没有实际用途。如果未来需要，可以再加回来。

**如果选择方案 A，额外需要:**
- 删除 `tool/Yi.Abp.Tool.Domain/Yi.Abp.Tool.Domain.csproj` 中的:
  ```xml
  <PackageReference Include="Volo.Abp.Caching" Version="$(AbpVersion)" />
  ```


---

## Phase 3: 项目重命名与清理

### Task 3.1: 重命名项目文件和目录（可选但建议）

**Objective:** 将项目从 `Yi.Abp` 命名改为 `SharpFort`

**涉及的项目目录:**
```
tool/
├── Yi.Abp.Tool/                    → SharpFort.Tool/
├── Yi.Abp.Tool.Application/        → SharpFort.Tool.Application/
├── Yi.Abp.Tool.Application.Contracts/ → SharpFort.Tool.Application.Contracts/
├── Yi.Abp.Tool.Domain/             → SharpFort.Tool.Domain/
├── Yi.Abp.Tool.Domain.Shared/      → SharpFort.Tool.Domain.Shared/
├── Yi.Abp.Tool.HttpApi.Client/     → 可删除 (不再需要)
└── Yi.Abp.Tool.Web/                → 可删除 (不再需要)
```

**Step 1: 重命名目录**

```bash
cd tool
mv Yi.Abp.Tool SharpFort.Tool
mv Yi.Abp.Tool.Application SharpFort.Tool.Application
mv Yi.Abp.Tool.Application.Contracts SharpFort.Tool.Application.Contracts
mv Yi.Abp.Tool.Domain SharpFort.Tool.Domain
mv Yi.Abp.Tool.Domain.Shared SharpFort.Tool.Domain.Shared
```

**Step 2: 更新所有 .csproj 中的 ProjectReference 路径**
- 每个 csproj 中的 `<ProjectReference Include="..\Yi.Abp.Tool.xxx\...">` → `..\SharpFort.Tool.xxx\...`

**Step 3: 更新所有命名空间和 using 语句**

```bash
# 批量替换（在 tool/ 目录下）
find . -type f -name "*.cs" -exec sed -i \
    -e "s/Yi\.Abp\.Tool/SharpFort.Tool/g" \
    {} \;
```

**Step 4: 更新 csproj 中的项目元数据**

在每个 csproj 中更新：
```xml
<!-- SharpFort.Tool.csproj -->
<Description>SharpFort 框架配套工具</Description>
<PackageProjectUrl>https://你的网站</PackageProjectUrl>
<RepositoryUrl>https://gitee.com/你的用户名/Yi</RepositoryUrl>
<PackageTags>abp;sharpfort</PackageTags>
<ToolCommandName>sharpfort</ToolCommandName>  <!-- 命令名从 yi-abp 改为 sharpfort -->
```

**Step 5: 编译验证**

```bash
cd SharpFort.Tool
dotnet build
# 预期: 编译成功
```

> **如果暂时不想改名**，可以跳过 Phase 3，先完成 Phase 2 的功能改造。命名可以后续处理。

---

### Task 3.2: 断开 Web 和 HttpApi.Client 项目

**Objective:** 这两个项目在自包含模式下不再需要，从解决方案中移除

**Step 1: 从解决方案中移除（如果有 .sln 引用）**

```bash
# 查看当前解决方案
dotnet sln list

# 如果有 Web 和 HttpApi.Client 引用，移除它们
dotnet sln remove tool/Yi.Abp.Tool.Web/Yi.Abp.Tool.Web.csproj
dotnet sln remove tool/Yi.Abp.Tool.HttpApi.Client/Yi.Abp.Tool.HttpApi.Client.csproj
```

**Step 2: 可选择保留代码**

也可以不移除，只是不再引用。如果后续需要恢复 C/S 架构，代码还在。

---

## Phase 4: 配置与构建

### Task 4.1: 完善配置文件

**Objective:** 确保所有必需配置到位

**CLI 项目需要的配置（appsettings.json）:**

```json
{
  "ToolOptions": {
    "TempDirPath": "temp"
  },
  "GiteeAccession": "你的令牌"
}
```

**环境变量替代方案（更安全）:**

可以不使用 appsettings.json 存储令牌，改用环境变量：

```bash
# Windows
setx GiteeAccession "你的令牌"

# Linux/macOS
export GiteeAccession="你的令牌"
```

然后修改 `GiteeManager.cs` 的读取方式：
```csharp
_accessToken = configuration.GetValue<string>("GiteeAccession") 
    ?? Environment.GetEnvironmentVariable("GiteeAccession");
```

---

### Task 4.2: 完整构建

**Objective:** 编译整个 CLI 项目

```bash
cd tool/Yi.Abp.Tool  # 或 SharpFort.Tool（如果已重命名）
dotnet build -c Release

# 预期: Build succeeded
```

**常见编译错误排查:**

| 错误 | 原因 | 解决 |
|------|------|------|
| `CS0246: 未能找到类型或命名空间名` | 引用链未更新 | 检查 csproj 的 ProjectReference |
| `CS1061: 不包含定义` | 模块依赖未改 | 检查 [DependsOn] 特性 |
| `缺少 appsettings.json` | 文件未复制 | 检查 CopyToOutputDirectory |
| Redis 连接错误 | 未处理缓存依赖 | 执行 Task 2.9 |


---

## Phase 5: 功能测试

### Task 5.1: 测试 new 命令

**命令:** 
```bash
sharpfort new TestModule -csf -p ./output
```

**验证点:**
- [ ] 无网络错误（说明 Gitee API 调用成功）
- [ ] 输出 "恭喜~模块已生成！"
- [ ] `./output/testmodule/` 目录存在
- [ ] 模板内的 `SharpFort` 已替换为 `TestModule`
- [ ] 模板内的 `SharpFort.Framework` 已替换为 `TestModule.Framework`

---

### Task 5.2: 测试 new list 命令

**命令:**
```bash
sharpfort new list
```

**验证点:**
- [ ] 输出 "正在远程搜索中..."
- [ ] 显示所有模板分支列表（不含 master）
- [ ] 列表包含你的自定义模板分支

---

### Task 5.3: 测试 new -s 指定分支

**命令:**
```bash
sharpfort new TestMod2 -s sharpfort -csf
```

**验证点:**
- [ ] 正确从 `sharpfort` 分支下载模板
- [ ] 分支不存在时显示友好的错误提示

---

### Task 5.4: 测试 add-module 命令

**前置:** 先用 `new` 生成模块，然后：
```bash
cd output/testmodule
sharpfort add-module TestModule -s ../
```

**验证点:**
- [ ] 找到 .sln 文件
- [ ] 校验子项目目录存在
- [ ] 成功执行 `dotnet sln add`
- [ ] 输出 "恭喜~模块添加成功！"

---

### Task 5.5: 测试 clear 命令

**命令:**
```bash
cd output/testmodule
sharpfort clear
```

**验证点:**
- [ ] 递归删除所有 bin/ 和 obj/ 目录
- [ ] 输出每个被删除的目录

---

### Task 5.6: 测试 clone 命令

**命令:**
```bash
sharpfort clone
```

**验证点:**
- [ ] 从你的 Gitee 仓库克隆代码

---

## Phase 6: 打包发布

### Task 6.1: 修改打包元数据

**Files:**
- Modify: `tool/Yi.Abp.Tool/Yi.Abp.Tool.csproj`（或 `SharpFort.Tool.csproj`）

**需要更新的属性:**
```xml
<Version>1.0.0</Version>                          <!-- 从 2.0.5 改为 1.0.0 -->
<Authors>你的名字</Authors>                         <!-- 从 橙子老哥 改为你的名字 -->
<Description>SharpFort 框架配套工具</Description>    <!-- 更新描述 -->
<PackageProjectUrl>https://你的网站</PackageProjectUrl>
<RepositoryUrl>https://gitee.com/你的用户名/Yi</RepositoryUrl>
<PackageTags>abp;sharpfort</PackageTags>
<ToolCommandName>sharpfort</ToolCommandName>       <!-- 命令名 -->
```

---

### Task 6.2: 打包为 NuGet 工具

```bash
cd tool/Yi.Abp.Tool
dotnet pack -c Release -o ./nupkg

# 预期输出:
# Successfully created package ./nupkg/Yi.Abp.Tool.1.0.0.nupkg
```

---

### Task 6.3: 本地安装测试

```bash
# 卸载旧版本（如果安装过）
dotnet tool uninstall -g yi-abp

# 从本地包安装
dotnet tool install -g sharpfort --add-source ./tool/Yi.Abp.Tool/nupkg

# 验证安装
sharpfort -v
# 预期: 1.0.0

sharpfort -h
# 预期: 显示命令列表
```

---

### Task 6.4: 推送到 NuGet（可选）

```bash
dotnet nuget push ./nupkg/SharpFort.Tool.1.0.0.nupkg \
    --api-key 你的NuGet API Key \
    --source https://api.nuget.org/v3/index.json
```


---

## 改造涉及文件总览

### 必须修改的文件（6个）

| 文件 | 改动类型 | 说明 |
|------|----------|------|
| `Yi.Abp.Tool.csproj` | 修改 | 切换引用链 + 添加配置文件复制 |
| `YiAbpToolModule.cs` | 修改 | 改 DependsOn + 删 PostConfigureServices |
| `GiteeManager.cs` | 修改 | 改 Owner/Repo 常量 |
| `TemplateGenCreateInputDto.cs` | 修改 | 改 SetNameReplace() 替换规则 |
| `CloneCommand.cs` | 修改 | 改仓库地址 |
| `NewCommand.cs` | 修改 | 修复路径创建 Bug |

### 可选修改的文件（3个）

| 文件 | 改动类型 | 说明 |
|------|----------|------|
| `AddModuleCommand.cs` | 修改 | 对齐子项目列表（视模板而定） |
| `NugetCrawlerManager.cs` | 删除 | 移除不需要的爬虫功能 |
| `NueGetInfoService.cs` | 删除 | 移除对应的应用服务 |

### 新建的文件（1个）

| 文件 | 说明 |
|------|------|
| `Yi.Abp.Tool/appsettings.json` | CLI 配置文件 |

### 不再需要的项目（2个）

| 项目 | 处理方式 |
|------|----------|
| `Yi.Abp.Tool.HttpApi.Client` | 从解决方案移除（不再引用） |
| `Yi.Abp.Tool.Web` | 从解决方案移除（不再引用） |

---

## 风险与注意事项

| 风险 | 级别 | 应对 |
|------|:--:|------|
| Yi.Framework.* NuGet 包依赖 | 中 | Domain 层引用 `Yi.Framework.Core` 等包，来自橙子老哥的 NuGet 源。如不可用需替换或 fork |
| Gitee API 频率限制 | 低 | 未认证请求 5000次/天，认证后更高。 `new list` 每次消耗 1 次 |
| SetNameReplace 重复 key | 低 | 原代码两个 key 值相同（都是 "Yi.Abp" → Name），修正为不同 key |
| appsettings.json 令牌泄露 | 中 | 确保 .gitignore 排除配置文件或使用环境变量 |
| `dotnet pack` 路径问题 | 低 | `PackAsTool` 打包的 csproj 引用 ProjectReference，需确保相对路径正确 |

---

## 快速实施检查清单

```
Phase 1: 模板仓库
  [ ] Fork ccnetcore/yi-template 到自己的 Gitee
  [ ] 创建 sharpfort 分支
  [ ] 全局替换 Yi.Abp → SharpFort, Yi.Framework → SharpFort.Framework
  [ ] 推送所有分支

Phase 2: 核心改造
  [ ] Task 2.1: 修改 Yi.Abp.Tool.csproj 引用链
  [ ] Task 2.2: 修改 YiAbpToolModule.cs
  [ ] Task 2.3: 创建 appsettings.json
  [ ] Task 2.4: 修改 GiteeManager.cs 仓库常量
  [ ] Task 2.5: 修改 SetNameReplace()
  [ ] Task 2.6: 修改 CloneCommand.cs
  [ ] Task 2.7: 修复 NewCommand.cs Bug
  [ ] Task 2.8: 修改 AddModuleCommand.cs
  [ ] Task 2.9: 处理 NugetCrawlerManager 缓存依赖

Phase 3: 重命名与清理（可选）
  [ ] Task 3.1: 重命名项目目录与命名空间
  [ ] Task 3.2: 断开 Web/HttpApi.Client

Phase 4: 构建
  [ ] dotnet build -c Release 成功

Phase 5: 测试
  [ ] sharpfort new TestModule -csf 成功
  [ ] sharpfort new list 成功
  [ ] sharpfort new TestMod -s sharpfort 成功
  [ ] sharpfort add-module 成功
  [ ] sharpfort clear 成功
  [ ] sharpfort clone 成功

Phase 6: 打包
  [ ] dotnet pack 成功
  [ ] dotnet tool install -g sharpfort 成功
```

---

## 改造前后对比

| 维度 | 改造前 | 改造后 |
|------|--------|--------|
| 部署模式 | C/S (CLI + 远程服务器) | 单进程自包含 |
| 网络依赖 | 依赖 ccnetcore.com:19009 | 仅依赖 Gitee API |
| 离线使用 | 不可用 | 不可用（仍需网络获取模板） |
| 启动速度 | 快（HTTP 调用） | 相同（HTTP 调用 Gitee） |
| 模板仓库 | ccnetcore/yi-template | 你的 Gitee 账号/yi-template |
| 命令名 | yi-abp | sharpfort |
| 数据流 | CLI → HTTP → Web → Gitee | CLI → Gitee |
| 项目数量 | 7 | 5（移除 HttpApi.Client, Web） |

---

## 长期改进建议（不在此次范围内）

1. **本地模板缓存**: 首次下载后缓存模板，后续离线使用
2. **配置文件外置**: 支持 `~/.sharpfort/config.json` 全局配置
3. **交互式模式**: 类似 `dotnet new` 的交互式模板选择
4. **模板预览**: 下载前预览模板结构
5. **多仓库支持**: 支持从多个 Gitee/GitHub 仓库获取模板
6. **数据库模板**: 真正根据 `-dbms` 参数生成不同的数据库配置代码

---

**文档创建时间:** 2026-06-11
**基于分析文档:** `00_总分析文档_Yi.Abp.Tool.md` ~ `07_Yi.Abp.Tool.Web_Web服务端分析.md`

---

## Phase 0: 安全设计（在改造前决策）🔥

### ⚠️ 关键安全决策：Gitee 令牌处理

**问题:** 如果把令牌写入 `appsettings.json` 打包进 NuGet，所有安装 `sharpfort` 的人都能获取你的令牌。

**决策树:**

```
你的模板仓库是公开的吗？
  ├── 是 → 不需要令牌，Gitee API 对公开仓库的只读操作免费
  │        方案: 修改 GiteeManager，令牌为空时跳过认证参数
  │        优点: 零配置，包内无敏感信息
  │
  └── 否 → 需要令牌，但绝不能打包进 NuGet
           方案A: 首次运行交互式输入 → 存到 ~/.sharpfort/config.json
           方案B: 环境变量 SHARPFORT_GITEE_TOKEN
           方案C: 用户手动放到 ~/.sharpfort/appsettings.json
```

### Task 0.1: 修改 GiteeManager 支持免认证模式

**Objective:** 当未配置令牌时，公开仓库 API 调用不附加 access_token 参数

**Files:**
- Modify: `tool/Yi.Abp.Tool.Domain/GiteeManager.cs`

**修改方案（推荐 — 公开仓库免认证）:**

将 URL 构建逻辑改为条件追加令牌：

```csharp
public class GiteeManager : ITransientDependency
{
    private readonly string _accessToken;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string GiteeHost = "https://gitee.com/api/v5";
    private const string Owner = "你的Gitee用户名";  // Task 2.4
    private const string Repo = "yi-template";

    public GiteeManager(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        // 优先环境变量 → 配置文件
        _accessToken = Environment.GetEnvironmentVariable("SHARPFORT_GITEE_TOKEN")
            ?? configuration.GetValue<string>("GiteeAccession");
    }

    /// <summary>
    /// 构建 API URL，有令牌则附加 access_token 参数
    /// </summary>
    private string BuildAuthenticatedUrl(string path)
    {
        var url = $"{GiteeHost}{path}";
        if (!string.IsNullOrEmpty(_accessToken))
        {
            url += $"{(path.Contains("?") ? "&" : "?")}access_token={_accessToken}";
        }
        return url;
    }

    // 然后修改三个方法使用 BuildAuthenticatedUrl():

    public async Task<bool> IsExsitBranchAsync(string branch)
    {
        using var client = _httpClientFactory.CreateClient();
        var url = BuildAuthenticatedUrl($"/repos/{Owner}/{Repo}/branches/{branch}");
        var response = await client.GetAsync(url);
        return response.StatusCode != HttpStatusCode.NotFound;
    }

    public async Task<List<string>> GetAllBranchAsync()
    {
        using var client = _httpClientFactory.CreateClient();
        var url = BuildAuthenticatedUrl(
            $"/repos/{Owner}/{Repo}/branches?sort=name&direction=asc&page=1&per_page=100");
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        JArray jsonArray = JArray.Parse(result);
        List<string> names = new List<string>();
        foreach (JObject obj in jsonArray)
        {
            string name = obj["name"]?.ToString();
            if (name != null) names.Add(name);
        }
        return names;
    }

    public async Task<Stream> DownLoadFileAsync(string branch)
    {
        using var client = _httpClientFactory.CreateClient();
        var url = BuildAuthenticatedUrl($"/repos/{Owner}/{Repo}/zipball?ref={branch}");
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }
}
```

**Step 1: 重构三个 API 方法**

将原有内联 URL 改为调用 `BuildAuthenticatedUrl()`。

**Step 2: 修改 appsettings.json（去掉令牌要求）**

```json
{
  "ToolOptions": {
    "TempDirPath": "temp"
  }
}
```

不再包含 `GiteeAccession`。用户如需更高频率限制可通过环境变量配置。

**Step 3: 更新 Task 2.3 的 appsettings.json 内容**

将 Task 2.3 中 `appsettings.json` 的 `GiteeAccession` 字段移除。

**Verification:**

```bash
# 不配置任何令牌
sharpfort new list
# 预期: 正常列出模板分支

# 可选: 配置令牌（用于私密仓库或更高频率）
export SHARPFORT_GITEE_TOKEN="你的令牌"
sharpfort new list
# 预期: 正常（使用认证请求）
```

