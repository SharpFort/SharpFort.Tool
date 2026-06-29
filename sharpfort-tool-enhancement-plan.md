# SharpFort.Tool CLI 改造规划文档

> **版本**: v1.0  
> **日期**: 2026-06-28  
> **范围**: 仅涵盖 SharpFort.Tool CLI 项目改造，不涉及 SharpFort.Template 模板仓库  
> **背景**: 基于方案 A（Monorepo + 子目录），SharpFort.Net 作为 git submodule 嵌入宿主项目

---

## 一、前置约定

### 1.1 仓库关系

```
SharpFort.Tool      ← CLI 工具（本文档改造对象）
SharpFort.Template  ← 模板仓库（独立改造，本文档不做规划）
SharpFort.Net       ← 框架源码（不改造，作为 submodule 被引用）
```

### 1.2 模板分支约定（SharpFort.Template 仓库）

| 分支 | 用途 | 当前状态 |
|------|------|---------|
| `project` | 宿主项目模板 | 🆕 待创建 |
| `module` | DDD 模块模板 | 📛 由 `main` 重命名而来 |

> CLI 工具需将默认模板分支从 `main` 改为 `module`，并在 `new list` 中同时展示两个分支。

### 1.3 宿主项目标准结构

```
MyCrm/                              ← 项目根目录
├── MyCrm.sln                       ← 顶层解决方案
├── .gitmodules                     ← SharpFort.Net submodule 声明
├── global.json                     ← .NET SDK 版本锁定
├── Directory.Build.props           ← 统一编译属性
├── SharpFort.Net/                  ← git submodule（框架源码）
├── src/
│   └── MyCrm.Host/                 ← Web 启动项目
│       ├── MyCrm.Host.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       └── MyCrmHostModule.cs
└── modules/                        ← 业务模块目录
    ├── Order/                      ← 典型 DDD 五层模块
    │   ├── Order.Application/
    │   ├── Order.Application.Contracts/
    │   ├── Order.Domain/
    │   ├── Order.Domain.Shared/
    │   └── Order.SqlSugarCore/
    └── Product/
        └── ...
```

---

## 二、命令体系总览

### 2.1 改造后完整命令树

```
sharpfort
├── -h, -v                              [强化] 修复版本号
├── new                                  [重构]
│   ├── new project <name> [options]    🆕 创建宿主项目
│   ├── new module <name> [options]     ✏️ 创建业务模块（原 new 重构）
│   ├── new list [options]              ✏️ 列出模板（显示 project + module 分支）
│   └── new <name>                      ✏️ 快捷方式（自动判断上下文）
├── sync [version|branch]               🆕 同步 SharpFort.Net submodule
├── add-module <name> [options]         ✏️ 添加模块到解决方案
├── clone [options]                     ✏️ 克隆框架源码
├── init [options]                      ✏️ 首次设置向导
├── clear [options]                     ➖ 无变化
└── doctor                              ✏️ 增加 submodule 诊断
```

---

## 三、新增命令详细设计

### 3.1 `sharpfort new project <name>`

**目标**: 一键创建符合 Monorepo 规范的宿主项目骨架。

#### 3.1.1 命令签名

```bash
sharpfort new project <ProjectName> [options]

Options:
  -b, --branch <name>    框架分支/版本（默认 main，写入 .gitmodules）
  -o, --output <path>    输出路径（默认 ./）
  --no-git               跳过 git init 和 submodule 初始化
  --dry-run              预览模式，不实际创建文件
```

#### 3.1.2 执行流程

```
Step 1: 参数校验
  ├─ ProjectName 必须是合法的 C# 命名空间（字母开头，不含特殊字符）
  └─ output 路径不能已存在同名目录（除非 --force）

Step 2: 下载模板
  ├─ GET https://api.github.com/repos/SharpFort/SharpFort.Template/zipball/project
  ├─ 复用现有 ETag 缓存机制
  └─ 解压到 ~/.sharpfort/temp/{guid}/

Step 3: 模板变量替换（递归）
  ├─ 目录名:   SharpFort → {ProjectName}
  ├─ 文件名:   SharpFort → {ProjectName}
  ├─ 文件内容: SharpFort → {ProjectName}  (namespace / class name)
  ├─ 文件内容: sharpfort → {projectname}  (小写形式，用于路径)
  └─ ⚠️ 关键: 对 SharpFort.Net 的 ProjectReference 路径保持不变

Step 4: 生成项目文件
  ├─ 解压替换后的内容到 {output}/{ProjectName}/
  └─ 验证必要文件: .sln, src/*.Host/ 存在

Step 5: Git 初始化（除非 --no-git）
  ├─ cd {output}/{ProjectName}
  ├─ git init
  ├─ git submodule add https://github.com/SharpFort/SharpFort.Net.git SharpFort.Net
  ├─ git submodule update --init --recursive
  └─ git add . && git commit -m "chore: init project from SharpFort template"

Step 6: 验证
  ├─ dotnet restore {ProjectName}.sln
  └─ 报告创建结果
```

#### 3.1.3 模板变量替换规则（白名单 vs 黑名单）

这是最关键的设计点。模板中包含两类 `SharpFort` 字样：

| 类别 | 示例 | 处理 |
|------|------|------|
| **项目标识** | namespace、类名、.csproj 中 `<RootNamespace>` | ✅ 替换为 ProjectName |
| **框架引用** | `ProjectReference Include="..\..\SharpFort.Net\..."` | ❌ 保留 SharpFort |

**方案：基于路径的白名单过滤**

```
替换规则:
  对 src/    目录下所有文件 → 执行替换
  对 modules/ 目录下所有文件 → 执行替换
  对 SharpFort.Net/ 目录    → 完全跳过（这是 submodule 目录）

文件内容替换:
  替换 namespace SharpFort... → namespace {ProjectName}...
  替换 class SharpFort...     → class {ProjectName}...  
  保留 SharpFort.Net/ 路径引用（正则: (?<!\.Net[/\\])SharpFort 避免误伤）
```

> **建议：模板中使用独立占位符** `__ProjectName__` 避免与框架名 `SharpFort` 混淆。但考虑到与现有 module 模板（用 `SharpFort` 作占位符）的一致性，短期沿用 `SharpFort` + 白名单策略。

#### 3.1.4 输出示例

```
$ sharpfort new project MyCrm

╔══════════════════════════════════════════╗
║    SharpFort CLI - 创建宿主项目          ║
╚══════════════════════════════════════════╝

项目名称: MyCrm
输出路径: E:\Projects\tool-test\MyCrm
框架版本: main

[1/5] 下载项目模板...
      ✓ 模板下载完成 (project 分支)

[2/5] 生成项目结构...
      ✓ MyCrm.sln
      ✓ src/MyCrm.Host/
      ✓ modules/
      ✓ Directory.Build.props

[3/5] 初始化 Git...
      ✓ git init
      ✓ 添加 SharpFort.Net submodule

[4/5] 还原依赖...
      ✓ dotnet restore 成功

[5/5] 验证构建...
      ✓ 0 个错误

🎉 项目 MyCrm 创建成功！

下一步:
  cd MyCrm
  sharpfort new module Order    ← 创建第一个业务模块
```

---

### 3.2 `sharpfort new module <name>`

**目标**: 在宿主项目或独立目录中创建 DDD 五层模块。

#### 3.2.1 命令签名

```bash
sharpfort new module <ModuleName> [options]

Options:
  -b, --branch <name>    模板分支（默认 module）
  -p, --path <path>      输出路径（默认自动检测宿主项目的 modules/）
  --no-sln               不自动添加到解决方案
  -nc, --no-cache        跳过缓存强制下载
```

#### 3.2.2 上下文感知

```
1. 检测当前目录或父目录是否存在 *.sln 文件
   ├─ YES → 宿主项目模式
   │   ├─ 默认输出路径: {sln所在目录}/modules/{ModuleName}/
   │   └─ 自动调用 add-module 加入解决方案
   └─ NO  → 独立模式
       ├─ 默认输出路径: ./
       └─ 仅生成模块文件
```

#### 3.2.3 与现有 `sharpfort new` 的关系

| 现有 | 改造后 |
|------|--------|
| `sharpfort new` | → 交互式向导，优先判断上下文后调用 `new project` 或 `new module` |
| `sharpfort new <name>` | → 保留为快捷方式，行为同 `new module <name>`（向后兼容） |
| `sharpfort new <name> -s main` | → `-s` 废弃，改用 `-b`，默认值从 `main` 改为 `module` |

---

### 3.3 `sharpfort sync`

**目标**: 统一管理 SharpFort.Net submodule 的版本同步。

#### 3.3.1 命令签名

```bash
sharpfort sync [target] [options]

Arguments:
  target                 版本号/tag/分支名（省略则用 .gitmodules 中配置的版本）

Options:
  -b, --branch <name>    切换到指定分支并拉取最新
  --dry-run              仅显示将要执行的操作
```

#### 3.3.2 执行逻辑

```
sharpfort sync                    → git submodule update --init --recursive
                                     （使用 .gitmodules 中锁定的 commit）

sharpfort sync v2.0.0             → cd SharpFort.Net
                                     git fetch --tags
                                     git checkout v2.0.0
                                     cd ..
                                     git add SharpFort.Net
                                     git commit -m "chore: bump SharpFort.Net to v2.0.0"

sharpfort sync -b develop         → cd SharpFort.Net
                                     git fetch
                                     git checkout develop
                                     git pull origin develop
                                     cd ..
                                     git add SharpFort.Net
```

#### 3.3.3 前置校验

- 必须在宿主项目根目录（有 .gitmodules 且其中包含 SharpFort.Net）
- SharpFort.Net 目录必须是有效的 git submodule
- 工作区必须干净（无未提交变更），否则提示用户先提交

---

## 四、现有命令修改

### 4.1 `sharpfort -v` — 版本号修复

**Bug #4**: 输出 1.0.0.0，与 NuGet 包版本 0.9.1 不一致。

**修复方案**:
- 在 `SharpFort.Tool.csproj` 中统一版本源
- 从 `version.props` 或 `<Version>` 标签读取
- CI 构建时通过 `-p:Version=x.y.z` 注入
- `-v` 输出格式: `sharpfort v0.9.1 (assembly: 1.0.0.0)` 同时展示两者

---

### 4.2 `sharpfort new list` — 修复 + 增强

**Bug #2**: 分支列表 API 返回正常但解析为 0。

**修复方案**:
- 检查 `TemplateRepoManager` 中 JSON 反序列化代码
- 确认 C# 模型类属性与 GitHub API 字段对应（`name`, `commit.sha`, `protected`）
- 添加单元测试覆盖分支列表解析

**增强**:

```bash
$ sharpfort new list

全部模板:
──────────────────────────────────────────────
  分支        类型        说明
  ──────────  ──────────  ─────────────────────
  project     宿主项目    创建带框架引用的新项目
  module      业务模块    DDD 五层模块
──────────────────────────────────────────────
共 2 个模板
```

- `list -d` 增加分支类型标注
- `list -b <name>` 修复末尾异常（**Bug #3**: NewCommand.cs:42 Task.Wait）

---

### 4.3 `sharpfort add-module` — 增强

**当前问题**:
1. `-s` 参数需传目录路径而非 .sln 文件路径
2. 不传 `-s` 时仅在当前目录查找 .sln
3. 无上下文检测

**改造方案**:

```bash
sharpfort add-module <ModuleName> [options]

Options:
  -p, --modulePath <path>    模块路径（必填或自动检测）
  -s, --solution <path>      解决方案路径（支持 .sln 文件路径或目录路径）

行为:
  1. -s 未指定时:
     ├─ 当前目录查找 *.sln → 找到则使用
     └─ 向上递归查找（最多 3 层）
  
  2. -s 指定文件路径时:
     └─ 直接使用该 .sln 文件
  
  3. -s 指定目录路径时:
     └─ 在目录下查找 *.sln（保持现有行为）
  
  4. -p 未指定时:
     └─ 在当前目录下查找同名模块目录
```

---

### 4.4 `sharpfort init` — 模式选择

**改造**: 增加创建模式选择

```
╔══════════════════════════════════════════╗
║    SharpFort CLI 首次设置向导            ║
╚══════════════════════════════════════════╝

请选择使用模式:
  [1] 创建新项目 (sharpfort new project)
  [2] 创建独立模块 (sharpfort new module)
  [3] 仅配置环境

Step 1/4: 克隆 SharpFort.Net 框架 [Y/n]?
  → 模式 1: 建议克隆（用于 submodule）
  → 模式 2: 可选
```

---

### 4.5 `sharpfort doctor` — 新增 submodule 诊断

```
── Submodule 状态 ──
  检测 SharpFort.Net submodule...
  [OK] .gitmodules 已配置
  [OK] SharpFort.Net/ 存在
  [OK] 当前版本: main (commit a1b2c3d)
  [WARN] 落后远程 3 个 commit，建议执行 sharpfort sync
```

---

### 4.6 `sharpfort clone` — 调整

当前行为：克隆到当前目录下 `SharpFort.Net/`。

**调整**: 
- 增加 `--as-submodule` 选项，执行 `git submodule add` 而非 `git clone`
- 若检测到已在宿主项目中（有 .gitmodules），提示使用 `sharpfort sync` 代替

---

## 五、上下文检测机制

CLI 需要感知"当前处于什么环境"，这是多个命令的共同基础。

### 5.1 检测逻辑

```csharp
// SharpFortToolContext.cs (新增)

public enum ProjectContext
{
    Standalone,           // 独立目录，无 .sln
    HostProject,          // 宿主项目根目录（有 .sln + .gitmodules + SharpFort.Net/）
    HostProjectChild,     // 宿主项目的子目录
    FrameworkStandalone,  // SharpFort.Net 框架自身根目录
}

public class ProjectContextDetector
{
    public ProjectContext Detect(string currentPath)
    {
        // 1. 向上查找 .sln
        var sln = FindFileUpward(currentPath, "*.sln", maxDepth: 4);
        if (sln == null) return ProjectContext.Standalone;
        
        var slnDir = Path.GetDirectoryName(sln);
        
        // 2. 检查是否有 .gitmodules 且其中包含 SharpFort.Net
        var gitmodules = Path.Combine(slnDir, ".gitmodules");
        if (File.Exists(gitmodules))
        {
            var content = File.ReadAllText(gitmodules);
            if (content.Contains("SharpFort.Net"))
                return slnDir == currentPath 
                    ? ProjectContext.HostProject 
                    : ProjectContext.HostProjectChild;
        }
        
        // 3. 检查是否是 SharpFort.Net 框架自身
        if (Directory.Exists(Path.Combine(slnDir, "framework")) &&
            Directory.Exists(Path.Combine(slnDir, "module")))
            return ProjectContext.FrameworkStandalone;
        
        return ProjectContext.Standalone;
    }
}
```

### 5.2 各命令的上下文行为

| 命令 | Standalone | HostProject | FrameworkStandalone |
|------|-----------|-------------|---------------------|
| `new project` | 在当前目录创建 | 提示已在项目中 | 拒绝（框架内不创建宿主） |
| `new module` | 当前目录创建 | 默认创建到 modules/ | 创建到 module/ |
| `sync` | 报错：非宿主项目 | 正常执行 | 报错：框架自身无需同步 |
| `add-module` | 报错：无 .sln | 自动检测 .sln | 使用 Sf.Abp.sln |
| `clear` | 清理当前目录 | 清理所有 bin/obj | 清理所有 bin/obj |

---

## 六、配置文件变更

### 6.1 `~/.sharpfort/config.json` 新增字段

```json
{
  "Repo": {
    "Primary": { "...", "RepoName": "SharpFort.Template" },
    "Fallback": { "..." },
    "AccessToken": ""
  },
  "Tool": {
    "TempDirPath": "...",
    "CacheDirPath": "..."
  },
  "Clone": {
    "Primary": "https://github.com/SharpFort/SharpFort.Net",
    "Fallback": "..."
  },
  
  // ↓ 新增
  "Templates": {
    "DefaultModuleBranch": "module",
    "DefaultProjectBranch": "project",
    "AvailableBranches": {
      "module":  { "type": "module",  "description": "DDD 五层业务模块" },
      "project": { "type": "project", "description": "带框架引用的宿主项目" }
    }
  },
  
  "HostProject": {
    "SolutionFilePattern": "*.sln",
    "ModuleDirectory": "modules",
    "FrameworkSubmodulePath": "SharpFort.Net",
    "MaxSearchDepth": 4
  }
}
```

---

## 七、Bug 修复清单（从测试报告中提取）

| # | Bug | 定位 | 严重程度 |
|:--|------|------|:--:|
| 1 | `new` ZipFile 路径不匹配 | `TemplateGenManager.cs:132` | 🔴 P0 |
| 2 | `new list` 显示 0 模板 | 分支列表 JSON 反序列化 | 🐛 P1 |
| 3 | `new list -b` 末尾异常 | `NewCommand.cs:42` Task.Wait | ⚠️ P2 |
| 4 | `-v` 版本号不一致 | .csproj / CI 版本注入 | ⚠️ P2 |
| 5 | README 缺失命令 | 文档 | ⚠️ P3 |

---

## 八、实施路线建议

### Phase 1: 修 Bug（0.9.2）
- Bug #1, #2, #3, #4
- 无需改动模板

### Phase 2: 上下文检测 + add-module 增强（0.10.0）
- `ProjectContextDetector` 实现
- `add-module` 增强
- `doctor` 增加 submodule 诊断

### Phase 3: new project 命令（0.11.0）
- 依赖 SharpFort.Template 仓库 `project` 分支就绪
- `sharpfort new project` 实现

### Phase 4: sync 命令 + 文档完善（1.0.0）
- `sharpfort sync` 实现
- `init` 模式选择
- 完整 README + 使用文档

---

## 九、待讨论 / 开放问题

1. **模板占位符**: 继续用 `SharpFort` 还是引入 `__ProjectName__` / `__ModuleName__`？前者与现有模板一致但替换逻辑复杂，后者更清晰但需改造模板。

2. **框架版本锁定**: `.gitmodules` 锁定 commit SHA，是否需要 `sharpfort.tool.config.json` 记录"期望的框架版本号"以实现语义化版本管理？

3. **`new project` 是否应支持 `--framework-branch`**：允许用户选择 SharpFort.Net 的特定分支（如 `develop`）作为 submodule 的初始分支？

4. **多 .sln 场景**: 如果宿主项目有多个 .sln（如 `MyCrm.sln` + `MyCrm.Modules.sln`），`add-module` 应添加到哪个？是否需要配置或交互选择？

---

## 十、专家审查意见与决策落地建议（评审补充）

针对第九章提出的待讨论问题及整体方案架构，经深入分析与评估，现将明确的决策结论、改进建议及技术完善细节追加如下，供后续实施与最终审查参考。

### 10.1 待讨论问题（Section 九）最终决议

#### 1. 模板占位符策略：采用 `__ProjectName__` / `__ModuleName__` 显式占位符
*   **决策结果**：**采用后者（显式占位符）**。确认改造 `SharpFort.Template` 模板仓库。
*   **实施细节与优势**：
    *   将模板中的 `SharpFort` / `sharpfort` 关键字替换为 `__ProjectName__` / `__ModuleName__` 及 `__projectname__` / `__modulename__`。
    *   **彻底消除逻辑误伤**：原“基于路径白名单+正则负向零宽断言 (`(?<!\.Net[/\\])SharpFort`) ”的模糊替换方案过于脆弱，易因代码注释、命名空间变动或路径微调导致误替换。
    *   **确定性与性能提升**：CLI 替换逻辑变为全文本精准字符串替换（100% 确定性），无需复杂的白名单判断，显著提升模板渲染效率与稳定性。

#### 2. 框架版本锁定策略：动态标签解析与零额外配置文件
*   **决策结果**：**无需在宿主项目中增加 `sharpfort.tool.config.json` 额外文件**。
*   **实施细节与优势**：
    *   **维持 Git 规范原生性**：Git submodule 本身已通过 `.gitmodules` 和 Commit SHA 实现了强一致性的版本锁定，新增独立配置文件极易产生“配置与实际 Git Commit 状态不同步”的冗余与不一致风险。
    *   **动态读取与展示**：`sharpfort doctor` 和 `sharpfort sync` 在展示版本时，直接进入 `SharpFort.Net` 目录执行 `git describe --tags --always` 或读取框架自身的 `Directory.Build.props`，实时获取语义化版本号（如 `v1.2.0 (commit a1b2c3d)`）。

#### 3. 框架分支管理策略：锁定 `main` 单一主线分支
*   **决策结果**：**暂不引入多分支管理逻辑，全面锁定 `main` 分支**。
*   **实施细节与优势**：
    *   鉴于 `SharpFort.Net` 目前仅维护 `main` 分支，CLI 命令中省略或隐藏针对框架源码的 `--framework-branch` 等复杂参数。
    *   保持命令调用的极简性，避免向用户暴露不必要的复杂度。未来若有多分支需求，可在 `sync` 命令中平滑扩展。

#### 4. 解决方案文件规范：宿主根目录严格单 `.sln`/`.slnx` 约束
*   **决策结果**：**明确规范宿主项目根目录下仅允许存在一个顶层解决方案文件（排除 `SharpFort.Net/` 内部及 `modules/` 子目录）**。
*   **实施细节与优势**：
    *   **降低复杂性与歧义**：多 `.sln` 会导致 `add-module` 等命令无法自动感知目标，迫使交互提示或手动指定路径，违背一键自动化的初衷。
    *   **CLI 上下文检测机制**：在 `ProjectContextDetector` 中，检测算法仅检索宿主根目录下的 `.sln` 或 `.slnx` 文件。若检测到多个根 `.sln`，主动抛出明确的规范校验警告，提示用户保持单解决方案架构。

---

### 10.2 方案完善与关键细节补充

为了打造真正工业级、高可用的 CLI 工具，建议在原规划的基础上补充以下 5 个维度的技术细节：

#### 1. 现代化 `.slnx` 解决方案格式支持
*   **背景**：.NET 9+ (MSBuild 17.13+) 引入了基于 XML 的全新 `.slnx` 解决方案格式，未来逐步替代传统 `.sln`。
*   **完善方案**：
    *   `ProjectContextDetector` 和 `add-module` 命令需同时兼容 `*.sln` 与 `*.slnx` 文件。
    *   在执行 `dotnet sln add` 时，.NET CLI 已原生支持两种格式，CLI 工具需确保文件匹配正则为 `*.(sln|slnx)`。

#### 2. 中央包管理 (Central Package Management, CPM) 适配
*   **背景**：现代 .NET Monorepo 项目常采用 `Directory.Packages.props` 统一管理 NuGet 包版本。
*   **完善方案**：
    *   当 `sharpfort new module` 生成 DDD 模块项目文件（`.csproj`）时，若检测到宿主项目启用了 CPM（即包含 `Directory.Packages.props` 且 `ManagePackageVersionsCentrally=true`），生成的 `.csproj` 中 `<PackageReference>` 应自动去除 `Version` 属性。
    *   必要时，CLI 可自动在宿主根目录的 `Directory.Packages.props` 中追加新模块所需的特有依赖包版本声明。

#### 3. 解决方案文件夹 (Solution Folder) 自动分组
*   **背景**：执行 `sharpfort add-module` 时，若直接将 5 个层级的项目拍平加到 `.sln` 根部，会导致解决方案结构混乱。
*   **完善方案**：
    *   在调用 `dotnet sln add` 时，必须带有 `--solution-folder` 参数，例如：
        `dotnet sln MyCrm.sln add modules/Order/src/*/*.csproj --solution-folder modules/Order`
    *   确保在 Visual Studio / Rider 中自动呈现清晰的 `modules/ModuleName` 文件夹分组结构。

#### 4. 网络容灾与镜像回退（Fallback）机制落地方案
*   **背景**：在中国大陆或受限企业网络环境下，GitHub API (`api.github.com`) 及 Git 仓库克隆极易超时或失败。
*   **完善方案**：
    *   **模板下载容灾**：`sharpfort new project` / `module` 在从 GitHub 下载 Zip 失败时，自动回退至 `config.json` 中配置的镜像源（如 Gitee / GitCode）或本地离线模板缓存 (`~/.sharpfort/cache/`)。
    *   **Submodule 初始化容灾**：`git submodule add` 失败时，提供明晰的提示，引导用户配置 Git HTTP 代理或切换为国内镜像 URL。

#### 5. 命令执行失败的安全回滚（Rollback）保障
*   **背景**：`new project` 涉及模板下载、解压、变量替换、Git 初始化、Restore 等 6 个步骤，中途任何一步失败都可能残留脏文件。
*   **完善方案**：
    *   引入 Step 执行事务机制。若 Step 4 或 Step 5 失败，CLI 自动捕获异常并询问或提示清理已生成的 `{output}/{ProjectName}` 临时目录，避免产生半成品项目影响二次创建。

---

### 10.3 改造总结与专家评审关注点提示

修改后的 `SharpFort.Tool` 增强方案具备以下的核心优势，便于提供给后续专家审查：
1. **架构清晰，职责收敛**：命令体系严格控制在现有框架内，无冗余命令，聚焦于 `project` 与 `module` 的生命周期管理。
2. **极简且健壮的变量替换**：采用显式占位符 `__ProjectName__`，消除了正则白名单的不确定性。
3. **前瞻性与标准兼容**：全面融入了 .NET 9+ 的 `.slnx` 规范与 CPM (中央包管理) 最佳实践。
4. **强健的工业级体验**：补全了上下文感知、解决方案文件夹自动分组及网络容灾机制。


---

## 十一、二次审查补充意见（原方案作者对第十章的回审）

### 11.1 决策 #3（锁定 main）与 `sync -b` 命令的张力

第十章 10.1.3 决定"暂不引入多分支管理逻辑，锁定 main"，但前文 3.3 节已设计 `sharpfort sync -b <branch>`，两者存在矛盾。

**建议调和方案**：

- `sharpfort new project` 创建时，`.gitmodules` 的 `branch` 字段锁死为 `main`，**不接受** `--framework-branch` 参数（遵循决策 #3）
- `sharpfort sync` 默认使用 `.gitmodules` 中的 `branch`（即 `main`）
- `sharpfort sync -b <branch>` **保留**，但仅用于临时切换（如"我要试一下 develop 分支的最新特性"），并在执行时给出提示：

```
⚠️ 你正在将 SharpFort.Net 切换到非主线分支 'develop'。
   主线分支为 main，建议仅在临时调试时使用。
   是否继续? [y/N]
```

- `sharpfort sync`（无参数）始终回到 `.gitmodules` 锁定的 `main`

这样既保持默认路径极简，又不封死高级用户的灵活性。

---

### 11.2 决策 #1（显式占位符）对 Tool 替换引擎的影响

当前 `TemplateGenManager` 中替换逻辑为：

```
SharpFort → ModuleName（全文替换）
```

改为显式占位符后，需要**双占位符区分**：

| 命令 | 模板占位符 | 替换为 |
|------|-----------|--------|
| `new project <Name>` | `__ProjectName__` | `<Name>` |
| `new project <Name>` | `__projectname__` | `<name>` (小写) |
| `new module <Name>` | `__ModuleName__` | `<Name>` |
| `new module <Name>` | `__modulename__` | `<name>` (小写) |

**Tool 内部实现建议**：

```csharp
// 替换映射表（命令类型驱动）
var replacements = commandType switch
{
    CommandType.NewProject => new Dictionary<string, string>
    {
        ["__ProjectName__"] = projectName,
        ["__projectname__"] = projectName.ToLower(),
        // __ModuleName__ 不出现在 project 模板中，无需处理
    },
    CommandType.NewModule => new Dictionary<string, string>
    {
        ["__ModuleName__"] = moduleName,
        ["__modulename__"] = moduleName.ToLower(),
    }
};
```

> **优势**：即使 project 模板中意外残留 `__ModuleName__`，也不会被错误替换；反之亦然。两个占位符空间互不污染。

---

### 11.3 缓存迁移：`main` 分支重命名为 `module`

SharpFort.Template 仓库中 `main` → `module` 重命名后，已安装旧版 Tool 的用户在缓存目录 (`~/.sharpfort/cache/`) 中仍有 `main.zip` / `main.meta.json`。

**Tool 需要处理迁移**：

```csharp
// 首次运行 new list 时检测
if (cache has "main" metadata但远端已无 "main" 分支)
{
    // 方案 A: 自动清除旧缓存，重新下载
    File.Delete("~/.sharpfort/cache/main.zip");
    File.Delete("~/.sharpfort/cache/main.meta.json");
    Log("检测到模板分支已从 main 更名为 module，缓存已更新");
    
    // 方案 B: 就地重命名缓存文件
    File.Move("main.zip", "module.zip");
    File.Move("main.meta.json", "module.meta.json");
}
```

**推荐方案 A**（自动清除），因为 `main` 分支的旧 zip 内容与新 `module` 分支可能不同（模板内部也改了占位符）。

---

### 11.4 `.slnx` 升级 — 跨三个项目的联动改造

用户已明确三个项目（SharpFort.Template、SharpFort.Tool、SharpFort.Net）均要升级 `.sln` → `.slnx`。对 Tool 的影响：

#### 11.4.1 Tool 自身的 .sln 升级

```
SharpFort.Tool.sln → SharpFort.Tool.slnx
```

- 迁移命令：`dotnet sln SharpFort.Tool.sln upgrade`（.NET 9+ SDK 内置）
- 无代码改动

#### 11.4.2 模板中 .sln 升级

SharpFort.Template 的两个分支模板中的 `.sln` 文件需要替换为 `.slnx`。Tool 侧的适配：

| 位置 | 改动 |
|------|------|
| `ProjectContextDetector` | `FindFileUpward(..., "*.sln")` → `FindFileUpward(..., "*.slnx")`，同时兼容 `*.sln` |
| `add-module` | `dotnet sln add` 必须指向 `.slnx` 文件 |
| `new project` 模板验证 | 检查生成产物中 `.slnx` 文件存在 |
| 文件名替换 | `__ProjectName__.slnx` 作为占位文件名 |

**搜索优先级**：`.slnx` > `.sln`（优先新格式，但向下兼容旧项目）

```csharp
var patterns = new[] { "*.slnx", "*.sln" };
foreach (var pattern in patterns)
{
    var sln = FindFileUpward(currentPath, pattern, maxDepth);
    if (sln != null) return sln;
}
```

#### 11.4.3 SharpFort.Net 的 .sln 升级（非 Tool 改造范围但影响检测）

SharpFort.Net 的 `Sf.Abp.sln` → `Sf.Abp.slnx` 会影响 `ProjectContextDetector.FrameworkStandalone` 的判断——当前检测条件是 `framework/` 和 `module/` 目录存在，不依赖 .sln 文件名，所以**不受影响**。

---

### 11.5 CPM 检测与适配的细化

第十章 10.2.2 提到 CPM 适配，但未给出检测标准。Tool 需要明确的 CPM 判定逻辑：

```csharp
bool IsCpmEnabled(string slnDirectory)
{
    var packagesProps = Path.Combine(slnDirectory, "Directory.Packages.props");
    if (!File.Exists(packagesProps)) return false;
    
    var content = File.ReadAllText(packagesProps);
    return content.Contains("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>")
        || content.Contains("<ManagePackageVersionsCentrally>True</ManagePackageVersionsCentrally>");
}
```

检测到 CPM 后，`new module` 生成 `.csproj` 时的行为差异：

| 字段 | CPM 关闭（传统） | CPM 启用 |
|------|-----------------|---------|
| `<PackageReference>` | 含 `Version="x.y.z"` | 不含 `Version` |
| `Directory.Packages.props` | 不操作 | 自动追加模块独有依赖的版本声明 |

---

### 11.6 补充：项目名称校验规则

`new project <name>` 和 `new module <name>` 的 `<name>` 参数需要统一校验规则：

```csharp
static readonly Regex ValidProjectName = new Regex(
    @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
    RegexOptions.Compiled);

// 合法示例: MyCrm, MyCompany.Crm, MyCompany_Crm
// 非法示例: 123Crm, My-Crm, My Crm, My.Crm.
```

| 校验项 | 规则 |
|--------|------|
| 非空 | 必填 |
| 首字符 | 字母或下划线 |
| 后续字符 | 字母、数字、下划线 |
| 点分隔 | 支持命名空间风格 `Company.Project` |
| 长度上限 | 128 字符（文件系统路径限制） |
| 保留名 | 禁止 `SharpFort`, `Sf`, `Abp` 等框架保留字 |

---

### 11.7 对专家审查意见的总体评价

| 维度 | 评分 | 说明 |
|------|:--:|------|
| 决策明确性 | ⭐⭐⭐⭐⭐ | 4 个开放问题全部给出明确结论 |
| 前瞻性 | ⭐⭐⭐⭐⭐ | .slnx / CPM 适配切中 .NET 演进方向 |
| 工业级品质 | ⭐⭐⭐⭐⭐ | 回滚、容灾、解决方案文件夹分组 |
| 可执行性 | ⭐⭐⭐⭐☆ | 个别细节需补充（本节的 11.1 ~ 11.6） |

**结论**：第十章专家意见可直接作为实施依据。本章（第十一章）的 6 点补充建议与其保持一致，仅做细化落地。建议合并为最终版改造方案后进入 Phase 1 实施。

---

## 十二、终审裁决与完整实施任务清单（Development Tasklist）

针对第十一章（原方案作者对第十章的回审），经综合审查评估，该章补充意见**非常中肯、切中要害**。它补充了代码级正则校验、CPM 检测具体 XML 规则、旧缓存自动失效及 `.slnx` 优先检索链等极具工程价值的细节，避免了后续开发过程中的诸多“暗坑”。

基于您的两点核心诉求，现作出最终裁决并整理出完整的**开发实施任务清单（Tasklist）**。

### 12.1 终审裁决与意见调和

#### 1. 关于 `sharpfort sync -b <branch>` 的裁剪裁决
*   **裁决结论**：**同意您的意见，完全废弃 `-b <branch>` 参数**。
*   **落地处理**：
    *   `sharpfort sync` 保持绝对单一纯粹的职责：仅执行 `git submodule update --init --recursive`，确保本地 submodule 状态与宿主 `.gitmodules` 记录的 commit 保持严格同步。
    *   不增加任何分支切换重载选项。若开发者需要测试其他分支，由开发者通过原生 Git 命令在 `SharpFort.Net/` 目录内手动操作，CLI 不做多余封装。

#### 2. 关于项目与模块名称大写规范（PascalCase）的控制抉择
*   **裁决结论**：**采用“智能自动纠正（PascalCase Normalization）+ 友情提示”策略**。
*   **最佳实践设计**：
    *   **自动修正**：C# 类名、命名空间及项目文件名在 .NET 规范中强制要求使用 **PascalCase**（如 `MyCrm`, `Order`）。当用户输入全小写（如 `mycrm` 或 `order`）时，CLI 自动将首字母或分隔首字母转换为大写（`MyCrm`, `Order`）。
    *   **明确提示**：控制台输出提示：`ℹ️ 项目名称已自动规范化为 PascalCase: MyCrm`，既保障了生成的代码 100% 符合 C# 规范，又不会因格式问题直接报错卡住用户。
    *   **非法字符拦截**：若名称包含中划线 `my-crm`、空格或以数字开头等绝对无法作为 C# 标识符的字符，抛出明确错误并给出合法示例。
    *   **占位符映射**：
        *   `__ProjectName__` / `__ModuleName__` 绑定修正后的 PascalCase（如 `MyCrm`）。
        *   `__projectname__` / `__modulename__` 绑定全小写格式（如 `mycrm`），用于路径或路由。

---

### 12.2 改造实施任务清单（Phase 划分）

以下为合并全案后最终确定的开发实施任务清单，按 Phase 顺序推进：

#### Phase 1: 基础设施重构与 Bug 修复（目标版本：v0.9.2）
- [ ] **Task 1.1: 修复已知缺陷**
  - [ ] 修复 ZipFile 解压路径不匹配问题 (`TemplateGenManager.cs`)
  - [ ] 修复 `new list` 分支 JSON 反序列化解析为 0 的 Bug
  - [ ] 修复 `new list -b` 执行完毕后的 `Task.Wait` 异步阻塞异常 (`NewCommand.cs`)
  - [ ] 修复 `-v` 输出版本号与 NuGet 包版本号不一致问题 (`csproj` / AssemblyInfo 规范)
- [ ] **Task 1.2: 实现命名校验与规范化组件**
  - [ ] 新建 `NameNormalizer.cs`，实现首字母大写转换与全小写转换逻辑
  - [ ] 引入 `ValidProjectName` 正则表达式校验，拦截非法字符与 C# 关键字

#### Phase 2: 核心感知组件与 SLNX/CPM 适配（目标版本：v0.10.0）
- [ ] **Task 2.1: 实现上下文检测器 (`ProjectContextDetector.cs`)**
  - [ ] 实现向上递归检索 `.slnx` 与 `.sln` 文件（按 `*.slnx` > `*.sln` 优先级）
  - [ ] 实现宿主项目根目录检测（检查 `.gitmodules` 是否包含 `SharpFort.Net`）
  - [ ] 实现框架自身根目录识别
- [ ] **Task 2.2: 实现 CPM 中央包管理检测组件 (`CpmDetector.cs`)**
  - [ ] 实现 `Directory.Packages.props` 存在性及 `<ManagePackageVersionsCentrally>` 状态解析
- [ ] **Task 2.3: 实现旧模板缓存自动迁移逻辑 (`CacheManager.cs`)**
  - [ ] 当 `new list` 检测到本地存在旧 `main.zip` 缓存但远程模板分支已改为 `module` 时，自动清理旧缓存

#### Phase 3: `new project` 与 `new module` 命令重构（目标版本：v0.11.0）
- [ ] **Task 3.1: 改造模板变量替换引擎 (`TemplateGenManager.cs`)**
  - [ ] 废弃模糊的 `SharpFort` 替换逻辑，改为精准的双占位符替换映射（`__ProjectName__` / `__projectname__` 与 `__ModuleName__` / `__modulename__`）
- [ ] **Task 3.2: 实现 `sharpfort new project <name>` 命令**
  - [ ] 参数校验与 `NameNormalizer` 规范化
  - [ ] 下载 `project` 分支模板 zip 并解压
  - [ ] 递归执行占位符替换
  - [ ] 初始化 Git 仓库并执行 `git submodule add` 挂载 `SharpFort.Net` (锁定 `main` 分支)
  - [ ] 校验产物 `.slnx`/`.sln` 完整性并执行 `dotnet restore`
  - [ ] 引入 Step 事务回滚机制，若中途失败自动清理生成的临时目录
- [ ] **Task 3.3: 重构 `sharpfort new module <name>` 命令**
  - [ ] 利用 `ProjectContextDetector` 自动感知输出路径（宿主环境自动定向到 `modules/<ModuleName>`）
  - [ ] 结合 CPM 检测生成合规的 `.csproj`（启用 CPM 时自动剔除 `<PackageReference>` 中的 `Version`）
  - [ ] 自动调用解决方案挂载逻辑，带有 `--solution-folder modules/<ModuleName>` 分组参数

#### Phase 4: 同步命令、增强命令与文档交付（目标版本：v1.0.0）
- [ ] **Task 4.1: 实现 `sharpfort sync` 命令**
  - [ ] 前置检查当前目录是否为宿主根目录
  - [ ] 执行 `git submodule update --init --recursive`
  - [ ] 保持简洁，不提供 `-b` 分支切换选项
- [ ] **Task 4.2: 增强 `add-module` 与 `doctor` 命令**
  - [ ] `add-module` 支持向上递归检索 `.slnx`/`.sln` 文件并支持解决方案文件夹分组
  - [ ] `doctor` 命令新增 Submodule 状态诊断（检测 `.gitmodules` 完整性及落后 commit 提示）
- [ ] **Task 4.3: 交付测试与文档**
  - [ ] 编写全流程端到端自动化测试脚本
  - [ ] 更新 `README.md` 与 CLI 命令使用指南


---

## 十三、对终审任务清单的审查意见（原方案作者对第十二章的回审）

### 13.1 总体评价

第十二章的实施清单结构清晰、Phase 划分合理。两个终审裁决（废弃 `sync -b`、PascalCase 智能纠错）均切合实际，消除了前一章的设计张力。任务粒度适中，可直接作为开发任务指派。

以下逐 Phase 审查，标注遗漏项和建议调整。

---

### 13.2 Phase 1 — 已跳过 ✅

用户确认 Bug #1~#4 已修复。无需审查。

---

### 13.3 Phase 2 — 核心感知组件与 SLNX/CPM 适配（目标：v0.10.0）

#### 审查结论：✅ 基本完整，建议 1 处补充

| 任务 | 状态 | 备注 |
|------|:--:|------|
| Task 2.1 上下文检测器 | ✅ | `.slnx` > `.sln` 优先级正确 |
| Task 2.2 CPM 检测 | ✅ | 需与第十一章 11.5 的 `IsCpmEnabled()` 伪代码对齐 |
| Task 2.3 旧缓存迁移 | ✅ | 推荐方案 A（自动清除） |

**🔧 补充建议：Task 2.4 — config.json 模板配置自动迁移**

sharpfort 首次启动时，若检测到 `config.json` 中缺少第六章定义的 `Templates` 和 `HostProject` 字段，应自动补全默认值。避免老用户升级后因缺少配置字段而报错。

```csharp
// 伪代码：ConfigMigrator.Migrate()
if (config.Templates == null)
{
    config.Templates = new TemplatesConfig
    {
        DefaultModuleBranch = "module",
        DefaultProjectBranch = "project",
        AvailableBranches = { ... }
    };
}
if (config.HostProject == null)
{
    config.HostProject = new HostProjectConfig { ... };
}
configManager.Save(config);
```

---

### 13.4 Phase 3 — new project / new module 命令重构（目标：v0.11.0）

#### 审查结论：⚠️ 基本完整，但存在 3 处遗漏和 1 处依赖顺序问题

**遗漏 #1：缺少 `new list` 增强任务**

第四章 4.2 节设计的增强版 `new list`（显示 project / module 类型标注）在 Phase 3 中未见。这是 `new project` / `new module` 的配套发现能力，应在本 Phase 同步完成。

```
补充任务 — Task 3.4: 增强 sharpfort new list
  - [ ] list 输出增加"类型"列（project / module）
  - [ ] list -d 增加分支描述
  - [ ] list -b <name> 增加模板类型标注
  - [ ] 默认 Tab 补全仅显示与当前上下文相关的模板类型
```

**遗漏 #2：Task 3.3 依赖 Phase 4 的 add-module 功能**

Task 3.3 描述中提到"自动调用解决方案挂载逻辑，带有 `--solution-folder` 分组参数"。但 `--solution-folder` 分组能力属于 Phase 4 的 Task 4.2。这导致 Phase 3 交付的新模块创建无法在 IDE 中获得正确的解决方案文件夹分组。

**调整方案**：将 `add-module` 的 `--solution-folder` 能力**上移到 Phase 3**，作为 Task 3.3 的前置子任务。Phase 4 仅保留 `doctor` submodule 诊断增强，`add-module` 的剩余增强（向上递归搜索 `.slnx`/`.sln`）已在 Phase 2 由上下文检测器覆盖。

```
调整后:
  Phase 3 Task 3.3a: 增强 add-module（解决方案文件夹分组 + .slnx 支持）
  Phase 3 Task 3.3b: 重构 new module（整合上下文感知 + CPM + solution-folder）
  
  Phase 4 Task 4.2: 仅保留 doctor submodule 诊断增强
```

**遗漏 #3：缺少 `init` 命令模式选择**

第四章 4.4 节设计的 `init` 模式选择（project / module / config-only）在任务清单中完全缺失。应在 Phase 3 末尾或 Phase 4 增加。

```
补充任务 — Task 4.2b: 增强 sharpfort init（模式选择）
  - [ ] 交互式询问: 创建项目 / 创建模块 / 仅配置环境
  - [ ] 根据选择自动引导下一步操作
```

**🔴 关键依赖提示：Phase 3 的全部 `new` 类任务依赖 SharpFort.Template 仓库的 `project` 和 `module` 分支就绪，且模板内部已使用 `__ProjectName__` / `__ModuleName__` 显式占位符。这是一个跨项目阻塞项，建议在 Task 3.1 前增加前置校验：若远端 `project` 分支不存在，给出明确提示"请先创建 SharpFort.Template 的 project 分支"。**

---

### 13.5 Phase 4 — 同步命令、增强命令与文档交付（目标：v1.0.0）

#### 审查结论：⚠️ 基本完整，存在 2 处遗漏

**遗漏 #1：缺少 `clone` 命令调整**

第四章 4.6 节设计的 `clone --as-submodule` 选项未出现在任务清单中。虽然这是一个低优先级功能，但既然设计了就应列入。

```
补充任务 — Task 4.2c: 增强 sharpfort clone
  - [ ] 增加 --as-submodule 选项
  - [ ] 检测到已在宿主项目中时，提示使用 sharpfort sync 代替
```

**遗漏 #2：缺少 `-v` 版本号格式强化验证**

Phase 1 修复了版本号不一致，但未在后续 Phase 中列入验证任务。建议在 Phase 4 交付前增加一个"端到端验证"子任务，确认 `sharpfort -v` 输出格式为 `sharpfort v1.0.0` 且与 NuGet 包版本一致。

```
补充任务 — Task 4.3a: 端到端回归验证
  - [ ] sharpfort -v 版本号与 NuGet 一致
  - [ ] sharpfort new project → 创建 → dotnet build 通过
  - [ ] sharpfort new module → 创建 → 自动加入 .slnx → dotnet build 通过
  - [ ] sharpfort sync → submodule 同步成功
  - [ ] sharpfort doctor → 所有诊断项通过
```

---

### 13.6 跨 Phase 的全局遗漏

| 遗漏项 | 原始章节 | 建议归属 |
|--------|---------|---------|
| `new list` 增强（类型标注） | 4.2 | Phase 3 |
| `init` 模式选择 | 4.4 | Phase 4 |
| `clone --as-submodule` | 4.6 | Phase 4 |
| `config.json` 自动迁移 | 6.1 | Phase 2 |
| 端到端回归验证 | — | Phase 4 |
| `-v` 版本号格式验证 | 4.1 | Phase 4 |

---

### 13.7 修正后的完整 Phase 规划

```
Phase 1: Bug 修复（v0.9.2）              ← 已跳过（用户确认修复完成）
  └─ (已修复，不列入开发任务)

Phase 2: 核心感知 + 适配（v0.10.0）
  ├─ 2.1 ProjectContextDetector.cs
  ├─ 2.2 CpmDetector.cs
  ├─ 2.3 旧模板缓存自动迁移
  └─ 2.4 config.json 自动迁移（🆕 补充）

Phase 3: 创建命令重构（v0.11.0）
  ├─ 3.1 双占位符替换引擎（__ProjectName__ / __ModuleName__）
  ├─ 3.2 sharpfort new project <name>
  ├─ 3.3 增强 add-module（solution-folder + .slnx）← 从 Phase 4 上移
  ├─ 3.4 重构 sharpfort new module（整合上下文 + CPM + solution-folder）
  └─ 3.5 增强 sharpfort new list（类型标注）（🆕 补充）

Phase 4: 同步/增强/文档（v1.0.0）
  ├─ 4.1 sharpfort sync（纯 submodule update）
  ├─ 4.2 增强 doctor（submodule 诊断）
  ├─ 4.3 增强 init（模式选择）（🆕 补充）
  ├─ 4.4 增强 clone（--as-submodule）（🆕 补充）
  ├─ 4.5 端到端回归验证（🆕 补充）
  └─ 4.6 README + 使用文档更新
```

---

### 13.8 最终审查结论

| 维度 | 评估 |
|------|------|
| Phase 划分合理性 | ✅ 正确（新版 Phase 3/4 边界调整后更清晰） |
| 任务粒度 | ✅ 适中，每项 2~5 分钟可实现 |
| 依赖顺序 | ⚠️ 已修正（add-module 上移到 Phase 3） |
| 遗漏覆盖 | ✅ 6 项遗漏全部补充 |
| 可进入开发 | ✅ **是，修正后即可开始** |

**给开发者的话**：Phase 2 是整个改造的地基，`ProjectContextDetector` 和 `CpmDetector` 的接口设计务必稳定，后续 Phase 3/4 的所有命令都依赖它们。建议先在单元测试中覆盖四种上下文的检测 + CPM 开启/关闭两种状态，再进入 Phase 3 开发。

---

## 十四、吸收最新审查意见后的总结与最终文件归档说明

基于第十三章专家的回审意见，我们已完成全案的最终融合与任务依赖重构（包括上移 `add-module` 的 `--solution-folder` 能力至 Phase 3、补全 `config.json` 迁移、`new list` 增强、`init` 模式选择、`clone` 选项及端到端回归验证等 6 项遗漏）。

为了便于开发人员直接对照 Checkbox 进行开发与验收，**最终版本的“完整实施任务清单”已正式创建并保存于项目的根目录中**：

📄 **最终任务清单路径**: `sharpfort-tool-final-implementation-tasklist.md` (项目根目录)

至此，全部规划与审查工作圆满完成，可直接依据根目录下的最终任务清单开启 Phase 2 的代码开发！

