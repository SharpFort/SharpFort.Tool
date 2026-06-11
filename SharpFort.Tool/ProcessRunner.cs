using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpFort.Tool
{
    /// <summary>
    /// 共享进程执行工具 — 跨平台命令执行，统一输出与错误捕获
    /// </summary>
    public static class ProcessRunner
    {
        /// <summary>
        /// 执行一条或多条命令，输出标准输出和标准错误
        /// </summary>
        /// <param name="commands">要执行的命令列表</param>
        /// <returns>进程退出码 (0=成功)</returns>
        public static int Run(params string[] commands)
        {
            var psi = new ProcessStartInfo
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
                psi.Arguments = $"/c chcp 65001&{string.Join("&", commands)}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                psi.FileName = "/bin/bash";
                psi.Arguments = $"-c \"{string.Join("; ", commands)}\"";
            }
            else
            {
                throw new PlatformNotSupportedException($"不支持的操作系统平台");
            }

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            // 异步读取避免死锁：stdout 和 stderr 必须同时读取
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            proc.WaitForExit();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (!string.IsNullOrWhiteSpace(output))
                Console.WriteLine(output);

            if (!string.IsNullOrWhiteSpace(error))
                Console.Error.WriteLine(error);

            return proc.ExitCode;
        }
    }
}
