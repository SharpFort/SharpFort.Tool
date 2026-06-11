using System.IO.Compression;
using Microsoft.Extensions.CommandLineUtils;
using SharpFort.Tool.Application.Contracts;
using SharpFort.Tool.Application.Contracts.Dtos;

namespace SharpFort.Tool.Commands
{
    public class NewCommand : ICommand
    {
        private readonly ITemplateGenService _templateGenService;

        public NewCommand(ITemplateGenService templateGenService)
        {
            _templateGenService = templateGenService;
        }

        public string Command => "new";
        public string? Description => "创建模块模板 + 模板预览 | sharpfort new <name> [options]";

        public void CommandLineApplication(CommandLineApplication application)
        {
            application.HelpOption("-h|--help");

            var pathOption = application.Option("-p|--path", "创建路径", CommandOptionType.SingleValue);
            var csfOption = application.Option("-csf", "是否创建解决方案文件夹", CommandOptionType.NoValue);
            var soureOption = application.Option("-s|--soure", "模板分支名称，默认值 default", CommandOptionType.SingleValue);
            var noCacheOption = application.Option("-nc|--no-cache", "跳过缓存，强制重新下载", CommandOptionType.NoValue);
            var moduleNameArgument = application.Argument("moduleName", "模块名");

            // ================ 子命令: new list ================
            application.Command("list", listApp =>
            {
                var detailOption = listApp.Option("-d|--detail", "显示模板详细结构", CommandOptionType.NoValue);
                var branchOption = listApp.Option("-b|--branch", "查看指定分支详情", CommandOptionType.SingleValue);

                listApp.OnExecute(() =>
                {
                    if (branchOption.HasValue())
                    {
                        // 预览指定分支
                        PreviewTemplate(branchOption.Value()).Wait();
                        return 0;
                    }

                    Console.WriteLine("正在获取模板列表...");
                    var list = _templateGenService.GetAllTemplatesAsync().Result;

                    if (detailOption.HasValue())
                    {
                        Console.WriteLine("\n模板名称           说明");
                        Console.WriteLine("------------------  ---------------------");
                        foreach (var name in list)
                        {
                            var desc = name switch
                            {
                                "default" => "基础模块模板",
                                _ => "自定义模板"
                            };
                            Console.WriteLine($"{name,-18}  {desc}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\n全部模板:");
                        Console.WriteLine("----------------");
                        Console.WriteLine(list.JoinAsString("\n"));
                    }
                    Console.WriteLine($"\n共 {list.Count} 个模板");
                    Console.WriteLine("使用 sharpfort new list -d 查看详细信息");
                    Console.WriteLine("使用 sharpfort new list -b <分支名> 预览模板结构");
                    return 0;
                });
            });

            // ================ 主命令: new <name> ================
            application.OnExecute(() =>
            {
                var path = string.Empty;
                if (pathOption.HasValue())
                {
                    path = pathOption.Value();
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                }

                var soure = soureOption.HasValue() ? soureOption.Value() : "default";
                var noCache = noCacheOption.HasValue();

                byte[] fileByteArray = _templateGenService.CreateModuleAsync(
                    new TemplateGenCreateInputDto
                    {
                        Name = moduleNameArgument.Value,
                        ModuleSoure = soure,
                        NoCache = noCache
                    }).Result;

                var id = Guid.NewGuid().ToString("N");
                var zipPath = Path.Combine(path, $"{id}.zip");
                File.WriteAllBytes(zipPath, fileByteArray);

                // 解压
                var unzipDirPath = "./";
                if (csfOption.HasValue())
                {
                    var moduleName = moduleNameArgument.Value.ToLower().Replace(".", "-");
                    unzipDirPath = Path.Combine(path, moduleName);
                    if (Directory.Exists(unzipDirPath))
                        throw new UserFriendlyException($"文件夹[{unzipDirPath}]已存在，请删除后重试");
                    Directory.CreateDirectory(unzipDirPath);
                }

                ZipFile.ExtractToDirectory(zipPath, unzipDirPath);
                File.Delete(zipPath);

                Console.WriteLine("恭喜~模块已生成！");
                return 0;
            });
        }

        // ================ 模板预览 ================
        private async Task PreviewTemplate(string branch)
        {
            Console.WriteLine($"正在获取模板 [{branch}] 的结构...\n");
            var stream = await _templateGenService.PreviewTemplateAsync(branch);

            using var archive = new ZipArchive(stream);
            var entries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .OrderBy(e => e.FullName)
                .ToList();

            // 找到根目录前缀（zip 内多一层）
            var root = entries.FirstOrDefault()?.FullName.Split('/')[0] ?? "";

            Console.WriteLine($"模板: {branch}");
            Console.WriteLine($"文件数: {entries.Count}");
            Console.WriteLine(new string('-', 50));

            // 树形输出
            var prevDirs = new HashSet<string>();
            foreach (var entry in entries)
            {
                var relative = entry.FullName;
                if (relative.StartsWith(root + "/"))
                    relative = relative.Substring(root.Length + 1);
                if (string.IsNullOrEmpty(relative)) continue;

                var depth = relative.Count(c => c == '/');
                var name = relative.Split('/').Last();
                var indent = new string(' ', depth * 2);
                var isDir = string.IsNullOrEmpty(entry.Name);
                var prefix = isDir ? "📁 " : "📄 ";

                Console.WriteLine($"{indent}{prefix}{name}");
            }

            // 尝试显示 README
            var readme = entries.FirstOrDefault(e =>
                e.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase) ||
                e.Name.Equals("readme.md", StringComparison.OrdinalIgnoreCase));
            if (readme != null)
            {
                Console.WriteLine(new string('-', 50));
                Console.WriteLine("README.md 内容:");
                using var reader = new StreamReader(readme.Open());
                var content = reader.ReadToEnd();
                // 只显示前 20 行
                var lines = content.Split('\n').Take(20);
                Console.WriteLine(string.Join("\n", lines));
                if (content.Split('\n').Length > 20)
                    Console.WriteLine("...(更多内容请在模板仓库查看)");
            }

            Console.WriteLine(new string('-', 50));
            Console.WriteLine($"使用 sharpfort new <模块名> -s {branch} 创建此模板");
        }
    }
}
