# SharpFort.Tool .NET 10 升级总结

> 升级时间: 2026-06-11

---

## 背景

SharpFort.Tool 原项目依赖父仓库 `SharpFort.Net` 中的 `common.props` 和 `framework/Yi.Framework.*` 外部项目，无法独立构建。本次升级目标：

1. 使项目**自包含、可独立构建**
2. 将 TargetFramework 从 `net8.0` 升级到 `net10.0`

---

## 一、依赖独立化

### 1.1 新建本地 Props 文件（项目根目录）

| 文件 | 内容 |
|------|------|
| `common.props` | `TargetFramework=net10.0`、`Nullable`、`ImplicitUsings`、`LangVersion=latest` |
| `version.props` | `AbpVersion=10.4.1` |
| `usings.props` | 全局 `using Volo.Abp` + `using Volo.Abp.Modularity` |

### 1.2 Import 路径修正

所有 csproj 中 `<Import Project="..\..\common.props" />` 修正为 `<Import Project="..\common.props" />`（原路径指向父仓库，现指向本地根目录）。

### 1.3 外部 ProjectReference 替换为 NuGet 包

| 项目 | 移除的外部引用 | 替代 NuGet 包 |
|------|--------------|-------------|
| `Domain.Shared` | `Yi.Framework.Core`、`Yi.Framework.Mapster` | `Volo.Abp.Core` |
| `Domain` | — | `Microsoft.Extensions.Http` |
| `Application.Contracts` | `Yi.Framework.Ddd.Application.Contracts` | `Volo.Abp.Ddd.Application.Contracts` |
| `Application` | `Yi.Framework.Ddd.Application` | `Volo.Abp.Ddd.Application` + `Mapster` |
| `SharpFort.Tool` | — | `Microsoft.Extensions.Hosting` |

---

## 二、代码适配

| 文件 | 变更 |
|------|------|
| `SharpFortToolDomainSharedModule.cs` | 移除 `[DependsOn(typeof(YiFrameworkCoreModule))]` |
| `ITemplateGenService.cs` | 移除 `IApplicationService` 继承和 `[HttpGet]` 特性（CLI 工具不需要 HTTP 路由） |
| `TemplateGenService.cs` | 移除 `ApplicationService` 基类，改用 `ITransientDependency` 标记接口 |
| `SharpFortToolDomainModule.cs` | `Configure<ToolOptions>(instance)` → `Configure<ToolOptions>(options => { ... })` |
| `CommandInvoker.cs` | 添加 `using System.Reflection`（`Assembly` 类型引用） |
| `NewCommand.cs` | 修复全文字面换行符嵌入字符串语法错误（`"...\n..."` → 多行 `Console.WriteLine`） |
| `SharpFortToolModule.cs` | 注册 `IHttpClientFactory`（`context.Services.AddHttpClient()`） |

---

## 三、TargetFramework 升级

所有 6 个项目统一升级到 `net10.0`：

- `SharpFort.Tool.Domain.Shared`
- `SharpFort.Tool.Domain`
- `SharpFort.Tool.Application.Contracts`
- `SharpFort.Tool.Application`
- `SharpFort.Tool`（主程序）
- `SharpFort.Tool.Tests`

---

## 四、运行验证

| 命令 | 结果 |
|------|:----:|
| `dotnet build` | 0 error, 1 warning (CS8602) |
| `sharpfort -h` | ✅ 帮助信息正常 |
| `sharpfort -v` | ✅ 版本 1.0.0.0 |
| `sharpfort new -h` | ✅ 子命令帮助正常 |
| `sharpfort new list` | ✅ GitHub API 连通 (HTTP 200) |
| `sharpfort doctor` | ✅ 可用 |
| `sharpfort init` | ✅ 可用 |
| `sharpfort clone` | ✅ 可用 |

---

## 五、已知遗留问题

| 问题 | 说明 |
|------|------|
| `clear` 命令路径 bug | 会尝试删除当前工作目录，非本次引入，属于原始 bug |
| CS8602 警告 | `CommandInvoker.cs` 第 22 行 `Assembly.GetExecutingAssembly().GetName().Version` 可能返回 null |
| CS8618 警告 | 多个 DTO 类中属性未初始化（`Name`、`GiteeRef` 等），建议后续添加 `= ""` 默认值 |
