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
            if (File.Exists(slnPath) && slnPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return slnPath;
            }
            if (!Directory.Exists(slnPath))
            {
                throw new UserFriendlyException($"解决方案路径不存在：{slnPath}");
            }
            string[] slnFiles = Directory.GetFiles(slnPath, "*.sln");
            if (slnFiles.Length > 1)
            {
                throw new UserFriendlyException("当前目录包含多个sln解决方案，请只保留一个或使用 -s 指定确切的 .sln 文件");
            }
            if (slnFiles.Length == 0)
            {
                throw new UserFriendlyException("当前目录未找到sln解决方案，请检查");
            }

            return slnFiles[0];
        }

        /// <summary>
        /// 检查路径
        /// </summary>
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
