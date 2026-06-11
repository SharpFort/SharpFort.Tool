# SharpFort.Tool 开发计划 (Phase 4-9)

## 配置项全景扫描

### 当前所有配置来源

| 配置项 | 当前来源 | 建议 |
|--------|----------|------|
| `ToolOptions.TempDirPath` | `appsettings.json` | → `config.json` |
| `GiteeAccession` | 环境变量 `SHARPFORT_GITEE_TOKEN` | → `config.json` |
| `GiteeHost` | 硬编码 `"https://gitee.com/api/v5"` | → `config.json` |
| `Owner` | 硬编码 `"sunshang-hl"` | → `config.json` |
| `Repo` | 硬编码 `"yi-template"` | → `config.json` |
| `CloneAddress` | 硬编码 | → `config.json` |

### 统一配置文件结构

```
~/.sharpfort/config.json:

{
  "Gitee": {
    "Host": "https://gitee.com/api/v5",
    "Owner": "sunshang-hl",
    "Repo": "yi-template",
    "AccessToken": ""
  },
  "Tool": {
    "TempDirPath": "~/.sharpfort/temp",
    "CacheDirPath": "~/.sharpfort/cache"
  },
  "CloneAddress": "https://github.com/SharpFort/SharpFort.Tool",
  "DefaultTemplateBranch": "default"
}
```

---

## Phase 4: 配置文件外置 (P0)

**目标:** 所有配置统一存到 `~/.sharpfort/config.json`，移除 `appsettings.json`

**涉及文件:**
- 新建: `ConfigManager.cs` — 配置读取/写入/默认值
- 修改: `GiteeManager.cs` — 从 ConfigManager 读取
- 修改: `SharpFortToolDomainModule.cs` — 从 ConfigManager 读取 ToolOptions
- 修改: `CloneCommand.cs` — 从 ConfigManager 读取
- 修改: `Program.cs` — 首次运行初始化
- 删除: `appsettings.json`

---

## Phase 5: 本地模板缓存 (P0)

**目标:** 首次下载后缓存模板 zip，后续离线使用

**涉及文件:**
- 新建: `TemplateCacheManager.cs`
- 修改: `TemplateGenManager.cs` — 添加缓存检查

**缓存策略:**
- 缓存路径: `~/.sharpfort/cache/{branch}.zip`
- `new` 命令默认优先读缓存
- `new --no-cache` 强制重新下载
- `new --cache-ttl <hours>` 设置缓存有效期

---

## Phase 6: dbms 选项集成 (P1)

**目标:** `-dbms` 选项真正影响模板内容替换

**涉及文件:**
- 修改: `NewCommand.cs` — 传递 DbmsEnum
- 修改: `TemplateGenManager.cs` — 根据 Dbms 追加替换规则
- 模板仓库配合: fork 后添加数据库条件占位符

---

## Phase 7: 模板预览 (P1)

**目标:** `new list --detail` 显示模板结构和简介

**涉及文件:**
- 修改: `NewCommand.cs` — 添加 --detail 选项
- 修改: `TemplateGenManager.cs` — 获取模板简介

---

## Phase 8: 交互式模式 (P2)

**目标:** 无参数运行 `sharpfort new` 时进入交互选择

**涉及文件:**
- 新建: 交互式输入处理
- 修改: `NewCommand.cs`

---

## Phase 9: 清理与测试 (P2)

- 删除 `HelpCommand.cs`
- 添加强制刷新缓存选项 (`--no-cache`)
- 添加 xUnit 测试项目

---

**文档创建时间:** 2026-06-11
