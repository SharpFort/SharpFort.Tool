# SharpFort.Tool v0.9.1 — Bug 详细报告

> 测试环境：Windows 10, .NET SDK 10.0.301, sharpfort.tool NuGet v0.9.1
> 测试日期：2026-06-28
> 模板仓库：https://github.com/SharpFort/SharpFort.Template (main 分支, commit 089fab3)

---

## Bug #1 🔴 阻断 — `sharpfort new` 创建模块失败（ZipFile 路径不匹配）

**严重程度：** 🔴 阻断（核心功能不可用）

**影响命令：**
- `sharpfort new TestMod`
- `sharpfort new TestMod -csf`
- `sharpfort new TestMod -nc`

**现象：** 模板下载、解压、字符串替换均成功执行，模块内容已在临时目录正确生成，但最终打包到目标路径时抛出异常。

**完整堆栈：**
```
Could not find a part of the path 'C:\Users\Administrator\.sharpfort\temp\{guid}\SharpFort-SharpFort.Template-089fab3'.
   at System.IO.Enumeration.FileSystemEnumerator`1.CreateDirectoryHandle(...)
   at System.IO.Compression.ZipFile.CreateFromDirectory(String sourceDirectoryName, String destinationArchiveFileName)
   at SharpFort.Tool.Domain.TemplateGenManager.CreateTemplateAsync(TemplateGenCreateDto input)
        in /home/runner/work/SharpFort.Tool/SharpFort.Tool/SharpFort.Tool.Domain/TemplateGenManager.cs:line 132
   at SharpFort.Tool.Application.TemplateGenService.CreateModuleAsync(TemplateGenCreateInputDto moduleCreateInputDto)
        in /home/runner/work/SharpFort.Tool/SharpFort.Tool/SharpFort.Tool.Application/TemplateGenService.cs:line 36
   at SharpFort.Tool.Commands.NewCommand.GenerateModule(...)
        in /home/runner/work/SharpFort.Tool/SharpFort.Tool/SharpFort.Tool/Commands/NewCommand.cs:line 135
```

**根因分析：**

工具的处理流程为：
1. 下载 GitHub zipball → 内层目录名为 `SharpFort-SharpFort.Template-089fab3/`
2. 解压到 `~/.sharpfort/temp/{guid}/`
3. 递归替换 `SharpFort` → `TestMod`（目录名 + 文件名 + 文件内容）
4. 替换后目录变为 `TestMod-TestMod.Template-089fab3/`
5. **Bug 在此**：`ZipFile.CreateFromDirectory()` 仍然引用旧路径 `SharpFort-SharpFort.Template-089fab3`，该路径已不存在

**实际验证：** 临时目录中存在正确的 `TestMod-TestMod.Template-089fab3/`，内含 14 个文件、DDD 五层结构、内容全部替换正确。

**修复方向：** `TemplateGenManager.cs:132`，在调用 `ZipFile.CreateFromDirectory()` 前，源路径需更新为替换后的目录名。

**复现步骤：**
```bash
sharpfort new list --clear
sharpfort new TestMod
# 100% 复现
```

---

## Bug #2 🐛 — `sharpfort new list` 显示 0 个模板

**严重程度：** 🐛 中等（不影响直接指定分支下载）

**影响命令：**
- `sharpfort new list`
- `sharpfort new list -d`
- `sharpfort new list --refresh`

**现象：** 输出"共 0 个模板"。

**验证：** 直接调用 GitHub API 确认：
```json
GET https://api.github.com/repos/SharpFort/SharpFort.Template/branches
→ [{"name": "main", "commit": {"sha": "089fab3..."}, "protected": false}]
```
API 返回 1 个分支（main），工具解析为 0。

**根因分析：** 分支列表 JSON 反序列化逻辑有误。可能原因：
- C# 类属性名与 API 返回的 JSON 字段名不匹配
- 数组反序列化时期望的包装类型不对
- 分页处理遗漏了单页结果

**修复方向：** 检查 `TemplateRepoManager` 中分支列表获取与反序列化代码，对照 GitHub Branches API v3 返回格式。

---

## Bug #3 ⚠️ — `sharpfort new list -b main` 末尾异常

**严重程度：** ⚠️ 低（模板预览正常，仅结束后报错）

**现象：** 成功下载并预览 14 个文件，末尾抛出异常：
```
One or more errors occurred. (This operation is not supported.)
   at System.Threading.Tasks.Task.Wait(...)
   at SharpFort.Tool.Commands.NewCommand.<>c__DisplayClass6_1.<CommandLineApplication>b__2()
        in NewCommand.cs:line 42
```

**根因分析：** 预览完成后有一个展示步骤调用了当前平台不支持的操作，可能与控制台 ANSI 编码或颜色输出有关。

**修复方向：** 检查 `NewCommand.cs:42` 处对预览结果的后处理逻辑。

---

## Bug #4 ⚠️ — `sharpfort -v` 版本号不一致

**严重程度：** ⚠️ 低

**现象：**
```bash
sharpfort -v          → 1.0.0.0
dotnet tool list -g   → sharpfort.tool  0.9.1
```

**根因分析：** 程序集版本与 NuGet 打包版本不同步。CI 构建时版本号注入可能未生效。

**修复方向：**
- 检查 `SharpFort.Tool.csproj` 中 `<Version>` 标签
- 检查 CI（GitHub Actions）版本号传递
- 建议从 `version.props` 统一读取

---

## Bug #5 ⚠️ — README 文档缺失 `doctor` / `init` 命令

**严重程度：** ⚠️ 文档问题

**现象：** 工具实际支持 7 个子命令，README 仅列出 5 个。缺失 `doctor` 和 `init`。

---

## 优先级总结

| 优先级 | Bug | 影响 |
|:--:|------|------|
| 🔴 P0 | **#1** — new 创建失败 | 核心功能完全不可用 |
| 🐛 P1 | **#2** — list 显示 0 模板 | 用户无法发现模板 |
| ⚠️ P2 | **#3** — list -b 末尾异常 | 功能可用但有报错 |
| ⚠️ P2 | **#4** — 版本号不一致 | 用户困惑 |
| ⚠️ P3 | **#5** — README 缺失 | 文档不完整 |

---

## 额外发现（非 Bug）

### add-module 路径解析
`-s` 参数期望**目录路径**而非 .sln 文件路径。建议支持直接传入 .sln 文件路径。

### SharpFort.Template 仓库
仅 `main` 一个分支，建议新增 `project` 分支存放宿主项目模板。
