using Microsoft.Extensions.CommandLineUtils;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Commands
{
    /// <summary>
    /// doctor 命令 — 环境诊断 + 双源连通性检测
    /// </summary>
    public class DoctorCommand : ICommand
    {
        private readonly ConfigManager _configManager;
        private readonly TemplateRepoManager _repoManager;

        public DoctorCommand(ConfigManager configManager, TemplateRepoManager repoManager)
        {
            _configManager = configManager;
            _repoManager = repoManager;
        }

        public string Command => "doctor";
        public string? Description => "环境诊断：检测配置文件、缓存目录、主备源连通性";

        public void CommandLineApplication(CommandLineApplication app)
        {
            app.HelpOption("-h|--help");

            app.OnExecute(async () =>
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════════╗");
                Console.WriteLine("║    SharpFort CLI 环境诊断                ║");
                Console.WriteLine("╚══════════════════════════════════════════╝");
                Console.WriteLine();

                var config = _configManager.GetConfig();
                var passCount = 0;
                var warnCount = 0;
                var failCount = 0;

                // ===== 1. 配置文件 =====
                Console.WriteLine("── 配置文件 ──");
                var configPath = ConfigManager.GetConfigPath();
                if (File.Exists(configPath))
                {
                    Console.WriteLine($"  [OK] 配置文件: {configPath}");
                    passCount++;

                    // JSON 格式验证
                    try
                    {
                        var json = File.ReadAllText(configPath);
                        System.Text.Json.JsonDocument.Parse(json);
                        Console.WriteLine($"  [OK] JSON 格式正确");
                        passCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [FAIL] JSON 格式错误: {ex.Message}");
                        failCount++;
                    }
                }
                else
                {
                    Console.WriteLine($"  [FAIL] 配置文件不存在: {configPath}");
                    Console.WriteLine($"         运行 `sharpfort init` 进行首次配置");
                    failCount++;
                }
                Console.WriteLine();

                // ===== 2. 缓存目录 =====
                Console.WriteLine("── 缓存目录 ──");
                var cacheDir = config.Tool.CacheDirPath;
                if (!string.IsNullOrEmpty(cacheDir) && Directory.Exists(cacheDir))
                {
                    var cacheFiles = Directory.GetFiles(cacheDir, "*.zip");
                    var metaFiles = Directory.GetFiles(cacheDir, "*.meta.json");
                    var totalSize = cacheFiles.Sum(f => new FileInfo(f).Length);
                    var sizeMb = totalSize / (1024.0 * 1024.0);

                    Console.WriteLine($"  [OK] 缓存目录: {cacheDir}");
                    Console.WriteLine($"  [OK] 模板缓存: {cacheFiles.Length} 个 ({sizeMb:F1} MB)");
                    Console.WriteLine($"  [OK] 元数据:   {metaFiles.Length} 个");
                    passCount += 3;
                }
                else
                {
                    Console.WriteLine($"  [WARN] 缓存目录不存在: {cacheDir}");
                    Console.WriteLine($"         首次使用 `sharpfort new` 时将自动创建");
                    warnCount++;
                }

                var tempDir = config.Tool.TempDirPath;
                if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                {
                    Console.WriteLine($"  [OK] 临时目录: {tempDir}");
                    passCount++;
                }
                else
                {
                    Console.WriteLine($"  [WARN] 临时目录不存在: {tempDir}");
                    warnCount++;
                }
                Console.WriteLine();

                // ===== 3. 主源 GitHub =====
                Console.WriteLine($"── 主源: {config.Repo.Primary.GetSourceName()} ──");
                Console.Write($"  检测中 {config.Repo.Primary.Host} ...");
                var primaryResult = await _repoManager.TestSourceAsync(config.Repo.Primary);
                if (primaryResult.success)
                {
                    Console.WriteLine($" [OK] {primaryResult.message}");
                    passCount++;

                    // 分支可用性
                    Console.Write($"  检测分支 [{config.DefaultTemplateBranch}] ...");
                    var branchResult = await _repoManager.TestBranchAsync(
                        config.Repo.Primary, config.DefaultTemplateBranch);
                    if (branchResult.success)
                    {
                        Console.WriteLine($" [OK] {branchResult.message}");
                        passCount++;
                    }
                    else
                    {
                        Console.WriteLine($" [WARN] {branchResult.message}");
                        warnCount++;
                    }
                }
                else
                {
                    Console.WriteLine($" [FAIL] {primaryResult.message}");
                    failCount++;
                }
                Console.WriteLine();

                // ===== 4. 备用源 Gitee =====
                Console.WriteLine($"── 备用源: {config.Repo.Fallback.GetSourceName()} ──");
                Console.Write($"  检测中 {config.Repo.Fallback.Host} ...");
                var fallbackResult = await _repoManager.TestSourceAsync(config.Repo.Fallback);
                if (fallbackResult.success)
                {
                    Console.WriteLine($" [OK] {fallbackResult.message}");
                    passCount++;

                    Console.Write($"  检测分支 [{config.DefaultTemplateBranch}] ...");
                    var branchResult = await _repoManager.TestBranchAsync(
                        config.Repo.Fallback, config.DefaultTemplateBranch);
                    if (branchResult.success)
                    {
                        Console.WriteLine($" [OK] {branchResult.message}");
                        passCount++;
                    }
                    else
                    {
                        Console.WriteLine($" [WARN] {branchResult.message}");
                        warnCount++;
                    }
                }
                else
                {
                    Console.WriteLine($" [WARN] {fallbackResult.message}");
                    warnCount++;
                }
                Console.WriteLine();

                // ===== 5. 认证状态 =====
                Console.WriteLine("── 认证状态 ──");
                if (!string.IsNullOrEmpty(config.Repo.AccessToken))
                {
                    Console.WriteLine($"  [OK] Token 已配置 (认证模式: 5000次/小时)");
                    passCount++;
                }
                else
                {
                    Console.WriteLine($"  [WARN] 未配置 Token (免认证模式: 60次/小时)");
                    Console.WriteLine($"         运行 `sharpfort init` 配置 Token");
                    warnCount++;
                }
                Console.WriteLine();

                // ===== 6. Git 可用性 =====
                Console.WriteLine("── 工具依赖 ──");
                try
                {
                    var exitCode = ProcessRunner.Run("git --version");
                    if (exitCode == 0)
                    {
                        passCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  [FAIL] git 不可用 (退出码: {exitCode})");
                        failCount++;
                    }
                }
                catch
                {
                    Console.WriteLine($"  [FAIL] git 未安装或不在 PATH 中");
                    failCount++;
                }
                Console.WriteLine();

                // ===== 汇总 =====
                Console.WriteLine("══════════════════════════════════════════");
                Console.WriteLine($"  通过: {passCount}  警告: {warnCount}  失败: {failCount}");

                if (failCount == 0 && warnCount == 0)
                {
                    Console.WriteLine("  状态: 一切正常！");
                }
                else if (failCount == 0)
                {
                    Console.WriteLine("  状态: 基本正常，部分项有警告");
                }
                else
                {
                    Console.WriteLine("  状态: 存在问题，请根据上方提示修复");
                }
                Console.WriteLine("══════════════════════════════════════════");

                return failCount > 0 ? 1 : 0;
            });
        }
    }
}
