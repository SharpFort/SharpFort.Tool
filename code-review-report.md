# SharpFort.Tool 代码审查报告

> 审查时间: 2026-06-11
> 审查依据: dotnet-skills (csharp-coding-standards, project-structure, csharp-api-design), sharpfort-performance-optimizer

---

## 严重问题

| # | 文件 | 行号 | 问题 | 说明 |
|---|------|:--:|------|------|
| 1 | `Application/TemplateGenService.cs` | L54-L65 | 语法错误/重复代码 | `PreviewTemplateAsync` 和 `GetAllTemplatesAsync` 代码块互相交错重复，第55行有多余 `n` 字符，文件无法编译 |
| 2 | `Commands/NewCommand.cs` | L73,L132,L162 | 同步阻塞 `.Result` | 多处使用 `.Result` 阻塞异步调用，在 CLI 场景可能导致死锁和资源浪费 |
| 3 | `CommandInvoker.cs` | L42-L45 | 伪异步 | `InvokerAsync` 标记为 `async` 但内部 `Application.Execute(args)` 是同步调用，且 `async` 无 `await` |
| 4 | `Domain/TemplateGenManager.cs` | L27 | DI 绕过 | `GetCacheDir()` 中 `new ConfigManager()` 直接实例化，绕过依赖注入容器，导致单例失效且无法测试 |
| 5 | `Tests/SharpFortConfigTests.cs` | L19 | 测试断言不匹配 | 测试期望 `"default"` 但实际默认值是 `"main"`，测试必然失败 |
| 6 | `CommandInvoker.cs` | L9 | 无关引用 | `using static System.Runtime.InteropServices.JavaScript.JSType` 完全无关 |

## 一般问题

| # | 文件 | 问题 | 说明 |
|---|------|------|------|
| 7 | `CloneCommand.cs` + `AddModuleCommand.cs` | 代码重复 | 两个命令中有几乎相同的 `StartCmd` 方法（进程调用逻辑），应抽取为共享工具类 |
| 8 | `Commands/AddModuleCommand.cs` L108 | 错误流丢失 | `StartCmd` 只读 `StandardOutput`，不读 `StandardError`，命令失败时无任何错误信息 |
| 9 | `Commands/ClearCommand.cs` L12 | 多余属性 | `CommandStrs` 属性不在 `ICommand` 接口中，属于冗余代码 |
| 10 | `Domain/TemplateRepoManager.cs` L43 | 命名拼写 | `IsExsitBranchAsync` 应为 `BranchExistsAsync` |
| 11 | 多个异步方法 | 缺少 CancellationToken | 所有异步方法均未接受 `CancellationToken` 参数 |
| 12 | 项目文件 | 外部依赖缺失 | 引用 `..\..\framework\` 和 `..\..\common.props`，仓库中不存在，无法独立编译 |
| 13 | `Domain/TemplateRepoManager.cs` L4 | 混合 JSON 库 | 同时使用 `Newtonsoft.Json.Linq` 和 `System.Text.Json`，应统一 |

---

## 修复方案

### Fix 1: TemplateGenService.cs — 修复语法错误/重复代码
重写文件，移除重复的代码块，确保 `GetAllTemplatesAsync` 和 `PreviewTemplateAsync` 方法完整且独立。

### Fix 2: NewCommand.cs — 修复 .Result 同步阻塞
将 `OnExecute` 回调中的 `.Result` 调用替换为 `GetAwaiter().GetResult()` 或重构为异步模式。

### Fix 3: CommandInvoker.cs — 修复伪异步 + 移除无关引用
移除 `async` 关键字（因为内部是同步调用），移除 `using static System.Runtime.InteropServices.JavaScript.JSType`。

### Fix 4: TemplateGenManager.cs — 修复 DI 绕过
将 `ConfigManager` 注入构造函数，替代 `new ConfigManager()` 直接实例化。

### Fix 5: SharpFortConfigTests.cs — 修复断言
将测试期望从 `"default"` 修改为 `"main"` 以匹配实际默认值。

### Fix 6: 创建共享 ProcessRunner 工具类
抽取 `StartCmd` 逻辑为 `SharpFort.Tool\ProcessRunner.cs`，统一处理跨平台命令执行、标准输出和标准错误捕获。

### Fix 7: CloneCommand.cs — 使用 ProcessRunner
移除内联 `StartCmd`，使用共享的 `ProcessRunner`。

### Fix 8: AddModuleCommand.cs — 使用 ProcessRunner + 捕获错误流
移除内联 `StartCmd`，使用共享的 `ProcessRunner`，同时捕获 `StandardError`。

### Fix 9: ClearCommand.cs — 移除冗余属性
移除 `CommandStrs` 属性。

### Fix 10: TemplateRepoManager.cs — 修复命名 + 统一 JSON 库
- `IsExsitBranchAsync` → `BranchExistsAsync`
- 替换 `Newtonsoft.Json.Linq` 为 `System.Text.Json`
- 同步更新调用方 (TemplateGenManager)

---

## 扩展命令规划（待实施）

按推荐顺序：
1. **双源自动切换 (GitHub + Gitee)** — 重构 `RepoConfig` 为 Primary/Fallback 结构
2. **init 命令** — 首次设置向导
3. **doctor 命令** — 环境诊断
4. **list --refresh / --clear** — 缓存管理

详见: `扩展命令规划.md`
