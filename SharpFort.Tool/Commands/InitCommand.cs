using Microsoft.Extensions.CommandLineUtils;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Commands
{
    /// <summary>
    /// init 命令 — 首次引导向导
    /// 引导用户克隆 SharpFort.Net 框架、配置模板仓库和 Token
    /// </summary>
    public class InitCommand : ICommand
    {
        private readonly ConfigManager _configManager;

        public InitCommand(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        public string Command => "init";
        public string? Description => "首次设置向导：克隆框架、配置模板源和 Token";

        public void CommandLineApplication(CommandLineApplication app)
        {
            app.HelpOption("-h|--help");
            var skipCloneOption = app.Option("--skip-clone", "跳过框架克隆", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                Console.WriteLine();
                Console.WriteLine("╔══════════════════════════════════════════╗");
                Console.WriteLine("║    SharpFort CLI 首次设置向导            ║");
                Console.WriteLine("╚══════════════════════════════════════════╝");
                Console.WriteLine();

                var config = _configManager.GetConfig();

                // ===== Step 1: 克隆框架 =====
                if (!skipCloneOption.HasValue())
                {
                    Console.WriteLine("Step 1/4: 克隆 SharpFort.Net 框架");
                    Console.WriteLine("  框架包含: RBAC / 审计日志 / 限流 / 多租户 / 定时任务");
                    Console.WriteLine();

                    Console.Write($"克隆地址 [{config.Clone.Primary}]: ");
                    var cloneInput = Console.ReadLine()?.Trim();
                    var cloneUrl = string.IsNullOrEmpty(cloneInput) ? config.Clone.Primary : cloneInput;

                    Console.Write("是否执行克隆? [Y/n]: ");
                    var cloneConfirm = Console.ReadLine()?.Trim().ToLower();
                    if (cloneConfirm != "n" && cloneConfirm != "no")
                    {
                        Console.WriteLine($"正在克隆 {cloneUrl}，请耐心等待...");
                        var exitCode = ProcessRunner.Run($"git clone {cloneUrl}");
                        if (exitCode != 0)
                        {
                            Console.WriteLine($"  主源克隆失败，尝试备用源: {config.Clone.Fallback}");
                            exitCode = ProcessRunner.Run($"git clone {config.Clone.Fallback}");
                            if (exitCode != 0)
                            {
                                Console.WriteLine("  克隆失败，请手动执行: git clone <地址>");
                            }
                            else
                            {
                                Console.WriteLine("  克隆完成！（来自备用源）");
                            }
                        }
                        else
                        {
                            Console.WriteLine("  克隆完成！");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  已跳过克隆");
                    }
                    Console.WriteLine();
                }

                // ===== Step 2: 模板仓库 Owner =====
                Console.WriteLine("Step 2/4: 模板仓库配置");
                Console.Write($"模板仓库 Owner [{config.Repo.Primary.Owner}]: ");
                var ownerInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(ownerInput))
                {
                    config.Repo.Primary.Owner = ownerInput;
                    config.Repo.Fallback.Owner = ownerInput;
                }

                Console.Write($"模板仓库名称 [{config.Repo.Primary.RepoName}]: ");
                var repoInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(repoInput))
                {
                    config.Repo.Primary.RepoName = repoInput;
                    config.Repo.Fallback.RepoName = repoInput;
                }
                Console.WriteLine();

                // ===== Step 3: GitHub Token =====
                Console.WriteLine("Step 3/4: 访问令牌 (可选)");
                Console.WriteLine("  免认证: GitHub 60次/小时, Gitee 60次/小时");
                Console.WriteLine("  认证后: 5000次/小时");
                Console.Write("GitHub/Gitee Token (留空跳过): ");
                var tokenInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(tokenInput))
                {
                    config.Repo.AccessToken = tokenInput;
                    Console.WriteLine("  Token 已配置");
                }
                else
                {
                    Console.WriteLine("  未配置 Token（使用免认证模式）");
                }
                Console.WriteLine();

                // ===== Step 4: 确认并保存 =====
                Console.WriteLine("Step 4/4: 确认配置");
                Console.WriteLine($"  主源:     {config.Repo.Primary.GetSourceName()} ({config.Repo.Primary.Host})");
                Console.WriteLine($"  备用源:   {config.Repo.Fallback.GetSourceName()} ({config.Repo.Fallback.Host})");
                Console.WriteLine($"  仓库:     {config.Repo.Primary.Owner}/{config.Repo.Primary.RepoName}");
                Console.WriteLine($"  Token:    {(string.IsNullOrEmpty(config.Repo.AccessToken) ? "未配置" : "已配置")}");
                Console.WriteLine($"  克隆地址: {config.Clone.Primary}");
                Console.WriteLine($"  配置文件: {ConfigManager.GetConfigPath()}");
                Console.WriteLine();

                Console.Write("确认保存? [Y/n]: ");
                var confirm = Console.ReadLine()?.Trim().ToLower();
                if (confirm == "n" || confirm == "no")
                {
                    Console.WriteLine("已取消，配置未保存");
                    return 1;
                }

                _configManager.SaveConfig(config);
                Console.WriteLine();
                Console.WriteLine("配置已保存！你可以:");
                Console.WriteLine("  sharpfort new              — 交互式创建模块");
                Console.WriteLine("  sharpfort new <name>       — 快速创建模块");
                Console.WriteLine("  sharpfort doctor           — 环境诊断");
                Console.WriteLine("  sharpfort -h               — 查看所有命令");

                return 0;
            });
        }
    }
}
