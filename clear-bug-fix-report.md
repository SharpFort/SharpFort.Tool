# SharpFort.Tool `clear` 命令 Bug 分析与修复

> 修复时间: 2026-06-11

---

## 问题现象

在项目目录下运行 `sharpfort clear` 时输出：

```
无法删除文件夹：./，错误信息: Access to the path 'Autofac.dll' is denied.
```

---

## 根本原因

`sharpfort clear` 的设计意图是递归扫描指定目录，删除所有名为 `bin` 和 `obj` 的子目录（.NET 编译产物）。

**触发链路：**

1. 用户通过 `dotnet run` 在 `SharpFort.Tool\SharpFort.Tool` 目录下启动程序
2. `dotnet run` 先编译，将输出写入 `bin\Debug\net10.0\`，再从此目录加载程序集运行
3. `clear` 命令默认扫描路径为 `./`（当前目录）
4. 递归遍历到 `bin\` 目录时，调用 `Directory.Delete(subDir, recursive: true)`
5. 此时 `bin\Debug\net10.0\` 内的 DLL（如 `Autofac.dll`）正被当前进程锁定
6. Windows 拒绝访问 → 抛出 `UnauthorizedAccessException`，操作失败

**本质：工具试图删除自己赖以运行的文件，是一个"自杀式"Bug。**

---

## 原代码问题清单

| # | 问题 | 原代码 |
|---|------|--------|
| 1 | **自删除崩溃** | 无进程目录检测，直接删除所有 `bin`/`obj` |
| 2 | **无确认提示** | 找到即删，无二次确认，误操作风险高 |
| 3 | **无预览模式** | 无法在删除前查看哪些目录会被清理 |
| 4 | **无汇总输出** | 操作完成后无统计信息 |
| 5 | **变量命名误导** | `delDirBlacklist` 实际存储的是"要删除的目录名"，语义应为白名单 |
| 6 | **默认 `./` 范围过大** | 从当前目录无差别递归，可能误删非目标项目文件 |
| 7 | **异常处理粗糙** | catch 后只打印一行，子目录遍历就此中断 |
| 8 | **大小写敏感匹配** | `Contains("obj")` 无法匹配 `Obj`、`OBJ`（Linux 文件系统区分大小写） |

---

## 修复方案

### 新增功能

| 功能 | 说明 |
|------|------|
| **进程自保护** | 通过 `Process.GetCurrentProcess().MainModule.FileName` 获取运行目录，跳过包含当前进程的 `bin` 目录 |
| **`--dry-run` 预览模式** | 列出所有待删目录但不执行删除 |
| **`-y` 跳过确认** | 默认需手动确认，`-y` 参数可跳过（适合 CI/脚本场景） |
| **`-path` 指定路径** | 明确指定扫描根目录，不依赖当前工作目录 |
| **大小写不敏感匹配** | `HashSet<string>(StringComparer.OrdinalIgnoreCase)` |
| **目录大小统计** | 扫描后显示总大小 |
| **结果汇总** | 成功 N 个 / 跳过 N 个，彩色输出 |
| **路径存在性校验** | 路径不存在时给出明确错误提示 |

### 进程自保护核心逻辑

```csharp
// 获取当前进程所在目录
var processDir = Path.GetDirectoryName(
    Process.GetCurrentProcess().MainModule?.FileName) ?? "";

// 收集时判断：进程目录是否"包含于"待删目录内
if (!string.IsNullOrEmpty(processDir) &&
    processDir.StartsWith(subDir, StringComparison.OrdinalIgnoreCase))
{
    // subDir 是当前进程 bin 目录的祖先，跳过
    continue;
}
```

> **注意**：判断方向是 `processDir.StartsWith(subDir)` 而非 `subDir.StartsWith(processDir)`，
> 因为进程运行在 `bin\Debug\net10.0\`（子目录），而待删的是 `bin\`（父目录）。

---

## 修复前后对比

### 修复前

```
$ sharpfort clear
无法删除文件夹：./，错误信息: Access to the path 'Autofac.dll' is denied.
```

### 修复后 — 预览模式

```
$ sharpfort clear --dry-run
找到 9 个目录待清理，共 2.3 MB：

  SharpFort.Tool\obj
  SharpFort.Tool.Application\bin
  SharpFort.Tool.Application\obj
  ...

[预览模式] 以上目录未被删除。去掉 --dry-run 参数执行实际清理。
```

### 修复后 — 实际执行（从外部目录）

```
$ sharpfort clear -path "E:\Projects\SharpFort.Tool" -y
找到 9 个目录待清理，共 2.3 MB：

  SharpFort.Tool\obj
  SharpFort.Tool.Application\bin
  ...

  ✓ 已删除：SharpFort.Tool\obj
  ✓ 已删除：SharpFort.Tool.Application\bin
  ...
  ✗ 跳过（被占用）：SharpFort.Tool\bin
    原因：Access to the path '...' is denied.

清理完成：成功 8 个，跳过 1 个。
```

---

## 修改文件

| 文件 | 变更 |
|------|------|
| `SharpFort.Tool\Commands\ClearCommand.cs` | 完全重写，新增自保护、dry-run、确认、汇总等功能 |
