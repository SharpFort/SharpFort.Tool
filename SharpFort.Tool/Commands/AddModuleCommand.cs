using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.CommandLineUtils;

namespace SharpFort.Tool.Commands
{
    public class AddModuleCommand : ICommand
    {
        public string Command => "add-module";
        public string? Description => "将内容添加到当前解决方案` sharpfort add-module <moduleName> [-p <path>] [-s <solution>] ";
        public void CommandLineApplication(CommandLineApplication application)
        {
            application.HelpOption("-h|--help");
            var modulePathOption=  application.Option("-p|--modulePath", "模块路径",CommandOptionType.SingleValue);
            var solutionOption=  application.Option("-s|--solution", "解决方案路径",CommandOptionType.SingleValue);
            var moduleNameArgument = application.Argument("moduleName", "模块名", (_) => { });
            application.OnExecute(() =>
            {
                var moduleName = moduleNameArgument.Value;
  
                //模块路径默认按小写规则，默认在模块路径下一层
                var modulePath = moduleName.ToLower().Replace(".", "-");
                if (modulePathOption.HasValue())
                {
                    modulePath = modulePathOption.Value();
                }
                
                
                //解决方案默认在模块文件夹上一级，也可以通过s进行指定
                var slnPath = "../";
                
                if (solutionOption.HasValue())
                {
                    slnPath = solutionOption.Value();
                }
                
                slnPath = CheckFirstSlnPath(slnPath);
                var dotnetSlnCommandPart = new List<string>() { "Application", "Application.Contracts", "Domain", "Domain.Shared", "SqlSugarCore" };
                var paths = dotnetSlnCommandPart.Select(x => Path.Combine(modulePath, $"{moduleName}.{x}")).ToArray();
                CheckPathExist(paths);
                /*
                 * 修复: 预检 .csproj 是否依赖外部框架配置文件（如 common.props）。
                 * 模板生成的模块引用了框架级文件，在独立环境中 MSBuild 导入失败会给出令人困惑的错误。
                 * 提前扫描并给出清晰的错误指引。
                 */
                var missingImports = CheckCommonPropsAvailable(paths);
                if (missingImports.Count > 0)
                {
                    var sb_err = new System.Text.StringBuilder();
                    sb_err.AppendLine("模块 .csproj 文件引用了框架级配置文件，但当前目录未找到：");
                    sb_err.AppendLine();
                    foreach (var entry in missingImports)
                        sb_err.AppendLine("  " + entry.csproj + " 缺少: " + entry.missingImport);
                    sb_err.AppendLine();
                    sb_err.AppendLine("请确保在 SharpFort.Net 框架源码目录下执行此命令：");
                    sb_err.AppendLine("  1. 运行 sharpfort clone 获取完整框架源码");
                    sb_err.AppendLine("  2. 将模块放入框架的 module/ 目录，再从框架根目录执行 add-module");
                    sb_err.AppendLine("  3. 手动复制 common.props/version.props/usings.props 到上级目录");
                    throw new UserFriendlyException(sb_err.ToString().TrimEnd());
                }

                var cmdCommands = dotnetSlnCommandPart.Select(x => $"dotnet sln \"{slnPath}\" add \"{Path.Combine(modulePath, $"{moduleName}.{x}")}\"").ToArray();
                var exitCode = ProcessRunner.Run(cmdCommands);
                if (exitCode != 0)
                    throw new UserFriendlyException($"添加模块到解决方案失败，退出码: {exitCode}");
                
                Console.WriteLine("恭喜~模块添加成功！");
                return 0;
            });
            
        }
        
        /// <summary>
        /// 获取一个sln解决方案，多个将报错
        /// </summary>
        private string CheckFirstSlnPath(string slnPath)
        {
            if (File.Exists(slnPath) && (slnPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || slnPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
            {
                return slnPath;
            }
            if (!Directory.Exists(slnPath))
            {
                throw new UserFriendlyException($"解决方案路径不存在：{slnPath}");
            }
                        string[] slnFiles = Directory.GetFiles(slnPath, "*.sln");
            string[] slnxFiles = Directory.GetFiles(slnPath, "*.slnx");
            var allSolutionFiles = slnFiles.Concat(slnxFiles).ToArray();
            if (allSolutionFiles.Length > 1)
            {
                throw new UserFriendlyException("当前目录包含多个解决方案文件(.sln/.slnx)，请只保留一个或使用 -s 指定确切的文件");
            }
            if (allSolutionFiles.Length == 0)
            {
                throw new UserFriendlyException("当前目录未找到 .sln 或 .slnx 解决方案文件。" + Environment.NewLine + "提示: .NET 10 默认创建 .slnx 格式，可使用 dotnet new sln 创建。");
            }

            return allSolutionFiles[0];
        }

        /// <summary>
        /// 检查路径
        /// </summary>

        /// <summary>
        /// 修复: 预检 .csproj 是否有无法解析的外部导入（如 common.props）。
        /// 在独立测试环境中，模板模块的 .csproj 引用了框架级配置文件，
        /// 这些文件只在完整的 SharpFort.Net 源码树中存在。
        /// </summary>
        private System.Collections.Generic.List<(string csproj, string missingImport)> CheckCommonPropsAvailable(
            string[] moduleProjectDirs)
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            foreach (var dir in moduleProjectDirs)
            {
                if (!Directory.Exists(dir)) continue;
                var csprojFiles = Directory.GetFiles(dir, "*.csproj");
                foreach (var csprojFile in csprojFiles)
                {
                    var lines = File.ReadAllLines(csprojFile);
                    foreach (var line in lines)
                    {
                        if (!line.Trim().StartsWith("<Import")) continue;
                        var match = System.Text.RegularExpressions.Regex.Match(
                            line, "Project=\\\"([^\\\"]+)\\\"");
                        if (!match.Success) continue;
                        var importPath = match.Groups[1].Value;
                        var csprojDir = Path.GetDirectoryName(csprojFile);
                        var absoluteImport = Path.GetFullPath(
                            Path.Combine(csprojDir, importPath));
                        if (!File.Exists(absoluteImport))
                            result.Add((Path.GetFileName(csprojFile), importPath));
                    }
                }
            }
            return result;
        }
        private void CheckPathExist(string[] paths)
        {
            foreach (string path in paths)
            {
                if (!Directory.Exists(path))
                {
                    throw new UserFriendlyException($"路径错误，请检查你的路径，找不到：{path}");
                }
            }
        }
    }
}
