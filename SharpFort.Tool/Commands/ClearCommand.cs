using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;

namespace SharpFort.Tool.Commands
{
    public class ClearCommand : ICommand
    {
        public string Command => "clear";
        public string? Description => "清除项目目录下的 obj、bin 编译产物文件夹 | sharpfort clear [options]";

        public void CommandLineApplication(CommandLineApplication application)
        {
            application.HelpOption("-h|--help");
            var pathOption = application.Option("-path", "目标路径（默认当前目录）", CommandOptionType.SingleValue);
            var dryRunOption = application.Option("--dry-run", "预览模式，不实际删除", CommandOptionType.NoValue);
            var yesOption = application.Option("-y|--yes", "跳过确认提示", CommandOptionType.NoValue);

            // 要清理的目录名（编译产物）
            HashSet<string> targetDirNames = new(StringComparer.OrdinalIgnoreCase) { "obj", "bin" };

            application.OnExecute(() =>
            {
                var rootPath = pathOption.HasValue() ? pathOption.Value()! : Directory.GetCurrentDirectory();

                if (!Directory.Exists(rootPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"错误：路径不存在 - {rootPath}");
                    Console.ResetColor();
                    return 1;
                }

                rootPath = Path.GetFullPath(rootPath);

                // 获取当前进程所在目录，避免自删除
                var processDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? "";

                // 扫描目标目录
                var toDelete = new List<string>();
                CollectTargetDirs(rootPath, targetDirNames, processDir, toDelete);

                if (toDelete.Count == 0)
                {
                    Console.WriteLine("未找到需要清理的 bin/obj 目录。");
                    return 0;
                }

                // 计算总大小
                long totalSize = 0;
                foreach (var dir in toDelete)
                {
                    try { totalSize += GetDirSize(dir); } catch { /* 忽略 */ }
                }

                Console.WriteLine($"找到 {toDelete.Count} 个目录待清理，共 {FormatSize(totalSize)}：");
                Console.WriteLine();
                foreach (var dir in toDelete)
                {
                    Console.WriteLine($"  {GetRelativePath(rootPath, dir)}");
                }
                Console.WriteLine();

                // 预览模式
                if (dryRunOption.HasValue())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[预览模式] 以上目录未被删除。去掉 --dry-run 参数执行实际清理。");
                    Console.ResetColor();
                    return 0;
                }

                // 确认
                if (!yesOption.HasValue())
                {
                    Console.Write("确认删除以上目录？(y/N) ");
                    var input = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (input != "y" && input != "yes")
                    {
                        Console.WriteLine("已取消。");
                        return 0;
                    }
                }

                // 执行删除
                int success = 0, failed = 0;
                foreach (var dir in toDelete)
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  ✓ 已删除：{GetRelativePath(rootPath, dir)}");
                        Console.ResetColor();
                        success++;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ✗ 跳过（被占用）：{GetRelativePath(rootPath, dir)}");
                        Console.WriteLine($"    原因：{ex.Message}");
                        Console.ResetColor();
                        failed++;
                    }
                }

                // 汇总
                Console.WriteLine();
                Console.WriteLine($"清理完成：成功 {success} 个，跳过 {failed} 个。");
                return 0;
            });
        }

        /// <summary>
        /// 递归收集待删除的 bin/obj 目录（排除当前进程自身所在目录）
        /// </summary>
        private static void CollectTargetDirs(string directory, HashSet<string> targetNames,
            string processDir, List<string> result)
        {
            try
            {
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (targetNames.Contains(dirName))
                    {
                        // 排除当前进程自身所在目录链（进程运行于 bin/Debug/netX 内）
                        if (!string.IsNullOrEmpty(processDir) &&
                            processDir.StartsWith(subDir, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        result.Add(subDir);
                    }
                    else
                    {
                        CollectTargetDirs(subDir, targetNames, processDir, result);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 无权限目录，跳过
            }
            catch (IOException)
            {
                // IO 错误，跳过
            }
        }

        private static long GetDirSize(string path)
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { size += new FileInfo(file).Length; } catch { /* 忽略 */ }
                }
            }
            catch { /* 忽略 */ }
            return size;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            try { return Path.GetRelativePath(basePath, fullPath); }
            catch { return fullPath; }
        }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB"];
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
            return $"{size:F1} {units[unit]}";
        }
    }
}
