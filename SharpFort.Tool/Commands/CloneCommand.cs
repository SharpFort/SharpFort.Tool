using Microsoft.Extensions.CommandLineUtils;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Commands
{
    /// <summary>
    /// clone 命令 — 克隆 SharpFort.Net 框架，支持 GitHub/Gitee 双源自动切换
    /// </summary>
    public class CloneCommand : ICommand
    {
        private readonly ConfigManager _configManager;

        public CloneCommand(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        public string Command => "clone";
        public string? Description => "克隆 SharpFort.Net 框架源代码，支持 GitHub/Gitee 双源自动切换";

        public void CommandLineApplication(CommandLineApplication application)
        {
            application.HelpOption("-h|--help");

            application.OnExecute(() =>
            {
                var cloneConfig = _configManager.GetConfig().Clone;

                Console.WriteLine($"正在克隆 {cloneConfig.Primary}，请耐心等待...");
                var exitCode = ProcessRunner.Run($"git clone {cloneConfig.Primary}");

                if (exitCode != 0)
                {
                    Console.WriteLine($"  主源克隆失败，尝试备用源: {cloneConfig.Fallback}");
                    exitCode = ProcessRunner.Run($"git clone {cloneConfig.Fallback}");

                    if (exitCode != 0)
                    {
                        Console.WriteLine("  备用源也失败，请检查网络或手动执行:");
                        Console.WriteLine($"    git clone {cloneConfig.Primary}");
                        Console.WriteLine($"    git clone {cloneConfig.Fallback}");
                        throw new UserFriendlyException("所有源均克隆失败");
                    }

                    Console.WriteLine("克隆完成！（来自备用源）");
                }
                else
                {
                    Console.WriteLine("克隆完成！");
                }

                return 0;
            });
        }
    }
}
