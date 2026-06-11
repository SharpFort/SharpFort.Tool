using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.CommandLineUtils;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Commands
{
    public class CloneCommand : ICommand
    {
        private readonly ConfigManager _configManager;

        public CloneCommand(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        public string Command => "clone";
        public string? Description => "克隆 SharpFort.Net 框架源代码，需依赖 git";

        public void CommandLineApplication(CommandLineApplication application)
        {
            application.OnExecute(() =>
            {
                var cloneAddress = _configManager.GetConfig().CloneAddress;
                Console.WriteLine($"正在克隆 {cloneAddress}，请耐心等待...");
                StartCmd($"git clone {cloneAddress}");
                return 0;
            });
        }

        private void StartCmd(params string[] cmdCommands)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c chcp 65001&{string.Join("&", cmdCommands)}";
            }
            else
            {
                psi.FileName = "/bin/bash";
                psi.Arguments = $"-c \"{string.Join("; ", cmdCommands)}\"";
            }
            Process proc = new Process { StartInfo = psi };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            Console.WriteLine(output);
            proc.WaitForExit();
        }
    }
}
