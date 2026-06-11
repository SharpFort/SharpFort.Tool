# SharpFort.Tool

SharpFort 框架配套 CLI 脚手架工具。基于 .NET 8 + ABP Framework，从 GitHub 模板仓库一键生成 DDD 分层模块。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-blue)](./LICENSE)

---

## 安装

```bash
dotnet tool install -g sharpfort
```

## 快速开始

```bash
# 交互式向导（推荐）
sharpfort new

# 命令行创建
sharpfort new MyCompany.Crm -csf

# 查看可用模板
sharpfort new list
```

---

## 命令大全

### 创建模块

| 命令 | 说明 |
|------|------|
| `sharpfort new` | 交互式向导，逐步选择模板、输入名称、配置选项 |
| `sharpfort new <name>` | 从 `main` 分支创建模块 |
| `sharpfort new <name> -s <分支>` | 指定模板分支 |
| `sharpfort new <name> -p <路径>` | 指定输出路径 |
| `sharpfort new <name> -csf` | 创建独立解决方案文件夹 |
| `sharpfort new <name> -nc` | 跳过缓存，强制下载 |

### 查看模板

| 命令 | 说明 |
|------|------|
| `sharpfort new list` | 列出所有可用模板分支 |
| `sharpfort new list -d` | 列出模板 + 详细描述 |
| `sharpfort new list -b <分支>` | 预览模板结构（文件树 + README 片段） |

### 其他命令

| 命令 | 说明 | 示例 |
|------|------|------|
| `sharpfort add-module <name>` | 添加已有模块到 .sln 解决方案 | `sharpfort add-module MyMod -s ../` |
| `sharpfort clone` | 克隆 SharpFort 框架源码 | `sharpfort clone` |
| `sharpfort clear` | 递归清理 bin/obj 目录 | `sharpfort clear` |
| `sharpfort -h` | 查看帮助 | `sharpfort -h` |
| `sharpfort -v` | 查看版本 | `sharpfort -v` |

### 选项参考

| 选项 | 全名 | 适用命令 | 说明 | 默认值 |
|------|------|:--:|------|:--:|
| `-s` | `--soure` | `new` | 模板分支名称 | `main` |
| `-p` | `--path` | `new` | 创建路径 | `./` |
| `-csf` | — | `new` | 创建解决方案文件夹 | 否 |
| `-nc` | `--no-cache` | `new` | 跳过缓存 | 否 |
| `-p` | `--modulePath` | `add-module` | 模块路径 | 模块名小写 |
| `-s` | `--solution` | `add-module` | 解决方案路径 | `../` |

---

## 缓存机制

首次创建模块时，模板 zip 缓存到 `~/.sharpfort/cache/{branch}.zip`。后续使用 **ETag** 条件请求：

```
sharpfort new MyMod
  → 读缓存元数据 (meta.json)
  → 带 If-None-Match 请求 GitHub API
    → 304 Not Modified → 直接用缓存（毫秒级）
    → 200 OK          → 更新缓存
```

`-nc / --no-cache` 可强制跳过缓存重新下载。

---

## 配置

首次运行自动生成 `~/.sharpfort/config.json`：

```json
{
  "Repo": {
    "Host": "https://api.github.com",
    "Owner": "SharpFort",
    "RepoName": "SharpFort.Template",
    "AccessToken": ""
  },
  "Tool": {
    "TempDirPath": "~/.sharpfort/temp",
    "CacheDirPath": "~/.sharpfort/cache"
  },
  "CloneAddress": "https://github.com/SharpFort/SharpFort.Tool",
  "DefaultTemplateBranch": "main"
}
```

> 公开模板仓库无需 `AccessToken`。如需更高 API 频率，配置 GitHub Personal Access Token。

---

## 架构

### 自包含设计

```
sharpfort CLI
  ├── 命令解析 (CommandLineUtils)
  ├── 模板获取 (GitHub API v3)
  ├── ETag 缓存
  ├── 内容替换 (递归: 目录名/文件名/文件内容)
  └── Zip 解压
```

无需远程服务器，CLI 直接调用 GitHub API 获取模板。

### 项目分层

```
SharpFort.Tool/                    ← CLI 入口 + 命令
├── SharpFort.Tool.Application/    ← 应用服务
├── SharpFort.Tool.Application.Contracts/ ← 接口 + DTO
├── SharpFort.Tool.Domain/         ← 核心逻辑
│   ├── ConfigManager.cs           ← 统一配置
│   ├── TemplateRepoManager.cs     ← GitHub API
│   └── TemplateGenManager.cs      ← 模板生成 + 缓存
├── SharpFort.Tool.Domain.Shared/  ← 枚举 + 选项
└── SharpFort.Tool.Tests/          ← xUnit 测试
```

### 数据流

```
用户执行: sharpfort new MyModule -s main
    │
    ├── 1. NewCommand 解析参数
    ├── 2. TemplateRepoManager → GitHub API
    │       GET /repos/SharpFort/SharpFort.Template/zipball/main
    │       带 ETag 条件请求 → 304 用缓存 / 200 下载
    ├── 3. TemplateGenManager
    │       解压 zip → 递归替换 "SharpFort" → "MyModule"
    │       目录名 / 文件名 / 文件内容
    ├── 4. 重新打包 → 返回 byte[]
    └── 5. 解压到目标目录
```

---

## 开发

```bash
# 克隆
git clone https://github.com/SharpFort/SharpFort.Tool.git
cd SharpFort.Tool

# 构建
dotnet build SharpFort.Tool

# 运行测试
dotnet test SharpFort.Tool.Tests

# 本地调试
dotnet run --project SharpFort.Tool -- new list
```

---

## 相关项目

| 项目 | 说明 |
|------|------|
| [SharpFort.Template](https://github.com/SharpFort/SharpFort.Template) | 模块模板仓库 |

---

## 技术栈

| 技术 | 用途 |
|------|------|
| .NET 8 | 运行时 |
| ABP Framework | 模块化 + DI |
| Autofac | IoC 容器 |
| CommandLineUtils | CLI 解析 |
| xUnit | 单元测试 |
| GitHub API v3 | 模板获取 |

---

## License

MIT
