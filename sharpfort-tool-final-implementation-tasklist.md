# SharpFort.Tool 增强改造最终实施任务清单 (Final Implementation Tasklist)

> **当前版本**: v1.0 Final  
> **生成日期**: 2026-06-29  
> **状态**: 终审通过（吸收第十三阶段专家回审意见）  
> **关联规划文档**: `sharpfort-tool-enhancement-plan.md`  

---

## 一、改造架构共识与终审决议汇总

在进入具体代码开发前，需严格遵守以下经过多轮专家评审收敛的核心架构决议：

1. **宿主仓库关系**：基于 Monorepo + 子目录模式，`SharpFort.Net` 作为 Git Submodule 挂载在宿主项目根目录。
2. **模板占位符**：`SharpFort.Template` 仓库全面采用显式双占位符：
   - 项目/模块名称（PascalCase）：`__ProjectName__` / `__ModuleName__`（例：`MyCrm` / `Order`）
   - 路径/路由名称（Lowercased）：`__projectname__` / `__modulename__`（例：`mycrm` / `order`）
3. **框架版本与分支**：全面锁定 `main` 分支。`sharpfort sync` 不接受 `-b` 分支切换参数，仅执行原生的 `git submodule update --init --recursive`。
4. **解决方案文件规范**：宿主项目根目录严格限制为单个顶层解决方案文件，优先支持并检索 `.slnx` 格式，兼容 `.sln`（检索优先级：`*.slnx` > `*.sln`）。
5. **名称规范化策略**：实行“智能 PascalCase 自动纠正 + 友情提示”。全小写输入自动转为首字母大写（例：`mycrm` → `MyCrm`），非法标识符字符直接拦截。
6. **模板仓库依赖**：Phase 3 执行前，须确保远程 `SharpFort.Template` 仓库已建立 `project` 与 `module` 分支并完成占位符重构。

---

## 二、Phase 阶段开发实施任务清单

---

### Phase 1: 基础 Bug 修复与命名组件（v0.9.2）
> **注意**：经确认，已知 Bug #1 ~ #4 已在前期完成修复。本阶段仅保留规范化组件开发任务。

- [x] **Task 1.1: 修复已知缺陷 (已完成)**
  - [x] 修复 ZipFile 解压路径不匹配问题 (`TemplateGenManager.cs`)
  - [x] 修复 `new list` 分支 JSON 反序列化解析为 0 的 Bug
  - [x] 修复 `new list -b` 执行完毕后的 `Task.Wait` 异步阻塞异常 (`NewCommand.cs`)
  - [x] 修复 `-v` 输出版本号与 NuGet 包版本号不一致问题 (`csproj` / AssemblyInfo 规范)
- [ ] **Task 1.2: 实现命名校验与规范化组件**
  - [ ] 新建 `NameNormalizer.cs`，实现智能 PascalCase 自动转换（例：`mycrm` → `MyCrm`）及全小写映射逻辑
  - [ ] 引入 `ValidProjectName` 正则表达式 (`^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$`)，拦截非法字符与 C# 关键字

---

### Phase 2: 核心感知与环境适配（目标版本：v0.10.0）
> **目标**：筑牢底层基础设施，提供稳定的上下文感知、CPM 检测及老旧配置/缓存迁移能力。

- [ ] **Task 2.1: 实现上下文检测器 (`ProjectContextDetector.cs`)**
  - [ ] 实现向上递归检索 `.slnx` 与 `.sln` 文件（按 `*.slnx` > `*.sln` 优先级，最大深度 4 层）
  - [ ] 实现宿主项目根目录检测（识别是否存在顶层 `.slnx`/`.sln` 及 `.gitmodules` 中的 `SharpFort.Net` 声明）
  - [ ] 实现宿主项目子目录与框架自身根目录识别
  - [ ] 编写 `ProjectContextDetectorTests` 单元测试，覆盖四种上下文场景
- [ ] **Task 2.2: 实现 CPM 中央包管理检测组件 (`CpmDetector.cs`)**
  - [ ] 实现 `Directory.Packages.props` 存在性及 `<ManagePackageVersionsCentrally>` 状态解析
  - [ ] 编写 `CpmDetectorTests` 单元测试，验证 CPM 开启与关闭状态
- [ ] **Task 2.3: 实现旧模板缓存自动清理机制 (`CacheManager.cs`)**
  - [ ] 当执行模板相关操作时，检测本地是否存在旧 `main.zip` 缓存。若远程主分支已改为 `module`，自动清理旧缓存并友情提示
- [ ] **Task 2.4: 实现 config.json 配置文件自动迁移 (`ConfigMigrator.cs`)**
  - [ ] 启动时检测 `~/.sharpfort/config.json`，缺失 `Templates` 或 `HostProject` 字段时自动补全默认值并保存

---

### Phase 3: 创建与模块管理命令重构（目标版本：v0.11.0）
> **前置阻塞校验**：执行 Task 3.1 前，校验远端 `SharpFort.Template` 的 `project` 和 `module` 分支是否存在。若不存在，给出明确提示。

- [ ] **Task 3.1: 改造模板变量替换引擎 (`TemplateGenManager.cs`)**
  - [ ] 废弃基于路径的 `SharpFort` 模糊替换逻辑
  - [ ] 实现精准的双占位符替换映射（`__ProjectName__` / `__projectname__` 与 `__ModuleName__` / `__modulename__`）
- [ ] **Task 3.2: 增强 `add-module` 命令能力 (前置移入)**
  - [ ] 重构 `add-module`，使其利用 `ProjectContextDetector` 自动定位顶层 `.slnx`/`.sln`
  - [ ] 调用 `dotnet sln add` 时带上 `--solution-folder modules/<ModuleName>` 分组参数
- [ ] **Task 3.3: 实现 `sharpfort new project <name>` 命令**
  - [ ] 校验参数并调用 `NameNormalizer`（全小写输入时输出 `ℹ️ 已自动规范化为 PascalCase: MyCrm`）
  - [ ] 下载 `project` 分支模板 zip 并解压到临时目录
  - [ ] 递归执行双占位符替换
  - [ ] 初始化 Git 仓库并执行 `git submodule add` 挂载 `SharpFort.Net` (锁定 `main` 分支)
  - [ ] 校验产物 `.slnx`/`.sln` 完整性并自动执行 `dotnet restore`
  - [ ] 引入 Step 事务机制：若解压或 Git 初始化失败，自动清理创建的临时目录
- [ ] **Task 3.4: 重构 `sharpfort new module <name>` 命令**
  - [ ] 利用 `ProjectContextDetector` 感知输出路径（宿主环境自动定向到 `modules/<ModuleName>`）
  - [ ] 结合 `CpmDetector` 生成合规 `.csproj`（启用了 CPM 时自动去除 `<PackageReference>` 中的 `Version` 属性）
  - [ ] 创建完成后自动调用 Task 3.2 的挂载逻辑，带有解决方案文件夹分组
- [ ] **Task 3.5: 增强 `sharpfort new list` 模板列表视图**
  - [ ] 终端表格增加“类型”列，清晰标明 `project` (宿主项目) 与 `module` (DDD 业务模块)
  - [ ] `list -d` 增加详细分支描述，`list -b <name>` 增加模板类型标注

---

### Phase 4: 同步/扩展命令与端到端交付（目标版本：v1.0.0）
> **目标**：补齐辅助运维命令，完成端到端回归验证与文档交付。

- [ ] **Task 4.1: 实现 `sharpfort sync` 命令**
  - [ ] 前置校验当前是否处于宿主项目根目录（存在 `.gitmodules`）
  - [ ] 执行 `git submodule update --init --recursive` 保持纯粹同步（无 `-b` 参数）
- [ ] **Task 4.2: 增强 `sharpfort doctor` 诊断命令**
  - [ ] 新增 Submodule 诊断项：检测 `.gitmodules` 配置完整性、`SharpFort.Net/` 目录状态及落后 commit 提示
- [ ] **Task 4.3: 增强 `sharpfort init` 交互式向导**
  - [ ] 新增模式选择引导：[1] 创建新项目 [2] 创建业务模块 [3] 仅配置环境，并根据选择自动引导下一步
- [ ] **Task 4.4: 增强 `sharpfort clone` 命令**
  - [ ] 增加 `--as-submodule` 选项；若检测到已在宿主项目中，友情提示用户改用 `sharpfort sync`
- [ ] **Task 4.5: 端到端自动化回归验证**
  - [ ] 验证 `sharpfort -v` 输出格式为 `sharpfort v1.0.0` 且与 NuGet 包版本一致
  - [ ] 执行端到端集成测试：`new project` → `new module` → `sync` → `doctor` 全流程校验并通过 `dotnet build`
- [ ] **Task 4.6: 交付 README 与使用文档**
  - [ ] 更新项目根目录 `README.md` 与在线 CLI 使用手册

---

## 三、开发执行指导与质量要求

1. **单步验证**：开发过程中，每个 Phase 对应的单元测试与集成测试必须全部通过，方可标记任务并推进到下一 Phase。
2. **日志与输出规范**：CLI 控制台输出统一采用带颜色的格式化图标（例：`✓` 成功，`ℹ️` 提示，`⚠️` 警告，`✖` 错误）。
3. **代码变更防护**：涉及核心改动时，随时使用单元测试验证逻辑，严禁随意破坏向后兼容性。
