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
        public string? Description => "创建模块 | sharpfort new [name] [options] (无参数进入交互模式)";

        public void CommandLineApplication(CommandLineApplication app)
        {
            app.HelpOption("-h|--help");
            var pathOption = app.Option("-p|--path", "创建路径", CommandOptionType.SingleValue);
            var csfOption = app.Option("-csf", "创建解决方案文件夹", CommandOptionType.NoValue);
            var soureOption = app.Option("-s|--soure", "模板分支名称", CommandOptionType.SingleValue);
            var noCacheOption = app.Option("-nc|--no-cache", "强制重新下载", CommandOptionType.NoValue);
            var nameArg = app.Argument("moduleName", "模块名（留空进入交互模式）");

            // list 子命令
            app.Command("list", listApp =>
            {
                listApp.HelpOption("-h|--help");
                var detailOpt = listApp.Option("-d|--detail", "详细信息", CommandOptionType.NoValue);
                var branchOpt = listApp.Option("-b|--branch", "预览指定分支", CommandOptionType.SingleValue);
                var refreshOpt = listApp.Option("--refresh", "强制刷新所有缓存（重新下载）", CommandOptionType.NoValue);
                var clearOpt = listApp.Option("--clear", "清空所有缓存", CommandOptionType.NoValue);

                listApp.OnExecute(() =>
                {
                    if (refreshOpt.HasValue()) { RefreshCache(); return 0; }
                    if (clearOpt.HasValue()) { ClearCache(); return 0; }
                    if (branchOpt.HasValue()) { PreviewTemplate(branchOpt.Value()).GetAwaiter().GetResult(); return 0; }
                    ListTemplates(detailOpt.HasValue());
                    return 0;
                });
            });

            // 主命令
            app.OnExecute(() =>
            {
                string moduleName, soure, path;
                bool csf, noCache;

                if (string.IsNullOrEmpty(nameArg.Value))
                {
                    var interactive = RunInteractive();
                    if (interactive == null) return 1;
                    (moduleName, soure, path, csf, noCache) = interactive.Value;
                }
                else
                {
                    moduleName = nameArg.Value;
                    soure = soureOption.HasValue() ? soureOption.Value() : "main";
                    path = pathOption.HasValue() ? pathOption.Value() : "./";
                    csf = csfOption.HasValue();
                    noCache = noCacheOption.HasValue();
                }
                return GenerateModule(moduleName, soure, path, csf, noCache);
            });
        }

        // ===== 交互模式 =====
        private (string name, string soure, string path, bool csf, bool noCache)? RunInteractive()
        {
            Console.WriteLine();
            Console.WriteLine("=== SharpFort 模块生成向导 ===");
            Console.WriteLine();

            var templates = _templateGenService.GetAllTemplatesAsync().GetAwaiter().GetResult();
            if (templates.Count == 0)
            {
                Console.WriteLine("未找到可用模板，请先 fork 模板仓库并配置 ~/.sharpfort/config.json");
                return null;
            }

            Console.WriteLine("请选择模板:");
            for (int i = 0; i < templates.Count; i++)
                Console.WriteLine($"  {i + 1}. {templates[i]} {(templates[i] == "main" ? "(基础模块)" : "")}");

            string? soure = null;
            while (soure == null)
            {
                Console.Write($"输入序号 [1-{templates.Count}] (默认 1): ");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) soure = templates[0];
                else if (int.TryParse(input, out int idx) && idx >= 1 && idx <= templates.Count)
                    soure = templates[idx - 1];
                else Console.WriteLine("无效输入，请重试");
            }

            string? moduleName = null;
            while (string.IsNullOrEmpty(moduleName))
            {
                Console.Write("模块名称 (如 SharpFort.Crm): ");
                moduleName = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(moduleName)) Console.WriteLine("模块名称不能为空");
            }

            Console.Write("创建路径 [./]: ");
            var path = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(path)) path = "./";

            Console.Write("创建解决方案文件夹? [y/N]: ");
            var csfInput = Console.ReadLine()?.Trim().ToLower();
            var csf = csfInput == "y" || csfInput == "yes";

            Console.WriteLine();
            Console.WriteLine($"即将创建:");
            Console.WriteLine($"  模板:   {soure}");
            Console.WriteLine($"  名称:   {moduleName}");
            Console.WriteLine($"  路径:   {path}");
            Console.WriteLine($"  文件夹: {(csf ? "是" : "否")}");
            Console.Write("确认? [Y/n]: ");
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm == "n" || confirm == "no") { Console.WriteLine("已取消"); return null; }

            return (moduleName, soure, path, csf, false);
        }

        // ===== 生成模块 =====
        private int GenerateModule(string moduleName, string soure, string path, bool csf, bool noCache)
        {
            Console.WriteLine($"正在从 [{soure}] 模板生成 [{moduleName}]...");

            byte[] fileByteArray = _templateGenService.CreateModuleAsync(
                new TemplateGenCreateInputDto { Name = moduleName, ModuleSoure = soure, NoCache = noCache }
            ).GetAwaiter().GetResult();

            var id = Guid.NewGuid().ToString("N");
            var zipPath = Path.Combine(path, $"{id}.zip");
            File.WriteAllBytes(zipPath, fileByteArray);

            var unzipDirPath = "./";
            if (csf)
            {
                var dirName = moduleName.ToLower().Replace(".", "-");
                unzipDirPath = Path.Combine(path, dirName);
                if (Directory.Exists(unzipDirPath))
                    throw new UserFriendlyException($"文件夹[{unzipDirPath}]已存在，请删除后重试");
                Directory.CreateDirectory(unzipDirPath);
            }

            ZipFile.ExtractToDirectory(zipPath, unzipDirPath);
            File.Delete(zipPath);

            Console.WriteLine($"模块 [{moduleName}] 已生成到 {Path.GetFullPath(unzipDirPath)}");
            Console.WriteLine($"  下一步: cd {unzipDirPath} && dotnet restore");
            return 0;
        }

        // ===== 模板列表 =====
        private void ListTemplates(bool detail)
        {
            Console.WriteLine("正在获取模板列表...");
            var list = _templateGenService.GetAllTemplatesAsync().GetAwaiter().GetResult();

            if (detail)
            {
                Console.WriteLine();
                Console.WriteLine("  模板名称           说明");
                Console.WriteLine("  ------------------  ---------------------");
                foreach (var name in list)
                {
                    var desc = name switch { "main" => "基础模块模板", _ => "自定义模板" };
                    Console.WriteLine($"  {name,-18}  {desc}");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("全部模板:");
                Console.WriteLine("----------------");
                Console.WriteLine(string.Join(Environment.NewLine, list));
            }
            Console.WriteLine();
            Console.WriteLine($"共 {list.Count} 个模板");

            // 显示缓存统计
            var (zipCount, totalBytes) = _templateGenService.GetCacheStats();
            if (zipCount > 0)
            {
                var sizeMb = totalBytes / (1024.0 * 1024.0);
                Console.WriteLine($"  缓存: {zipCount} 个模板 ({sizeMb:F1} MB)");
            }
            Console.WriteLine();
            Console.WriteLine("  sharpfort new                  — 交互式创建");
            Console.WriteLine("  sharpfort new list -d          — 详细信息");
            Console.WriteLine("  sharpfort new list -b <分支名> — 预览模板结构");
            Console.WriteLine("  sharpfort new list --refresh   — 刷新缓存");
            Console.WriteLine("  sharpfort new list --clear     — 清空缓存");
        }

        // ===== 缓存刷新 =====
        private void RefreshCache()
        {
            Console.WriteLine("正在刷新所有模板缓存...");
            Console.WriteLine();
            var count = _templateGenService.RefreshCacheAsync().GetAwaiter().GetResult();
            Console.WriteLine();
            Console.WriteLine($"刷新完成，共 {count} 个模板已更新");
        }

        // ===== 缓存清空 =====
        private void ClearCache()
        {
            var (zipCount, metaCount, totalBytes) = _templateGenService.ClearCache();
            if (zipCount == 0 && metaCount == 0)
            {
                Console.WriteLine("缓存目录为空，无需清理");
                return;
            }

            var sizeMb = totalBytes / (1024.0 * 1024.0);
            Console.WriteLine($"已清空缓存: {zipCount} 个模板 + {metaCount} 个元数据 ({sizeMb:F1} MB)");
            Console.WriteLine("下次使用 `sharpfort new` 时将自动重新下载");
        }

        // ===== 模板预览 =====
        private async Task PreviewTemplate(string branch)
        {
            Console.WriteLine($"正在获取模板 [{branch}] 的结构...");
            Console.WriteLine();
            var stream = await _templateGenService.PreviewTemplateAsync(branch);

            using var archive = new ZipArchive(stream);
            var entries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .OrderBy(e => e.FullName).ToList();

            var root = entries.FirstOrDefault()?.FullName.Split('/')[0] ?? "";

            Console.WriteLine($"模板: {branch}");
            Console.WriteLine($"文件数: {entries.Count}");
            Console.WriteLine(new string('-', 50));

            foreach (var entry in entries)
            {
                var relative = entry.FullName;
                if (relative.StartsWith(root + "/")) relative = relative.Substring(root.Length + 1);
                if (string.IsNullOrEmpty(relative)) continue;
                var depth = relative.Count(c => c == '/');
                var name = relative.Split('/').Last();
                var isDir = string.IsNullOrEmpty(entry.Name);
                Console.WriteLine($"{new string(' ', depth * 2)}{(isDir ? "+" : " ")} {name}");
            }

            var readme = entries.FirstOrDefault(e => e.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase));
            if (readme != null)
            {
                Console.WriteLine(new string('-', 50));
                using var reader = new StreamReader(readme.Open());
                var content = reader.ReadToEnd();
                var allLines = content.Split('\n');
                var lines = allLines.Take(20);
                Console.WriteLine(string.Join(Environment.NewLine, lines));
                if (allLines.Length > 20)
                    Console.WriteLine("...(更多内容请在模板仓库查看)");
            }
        }
    }
}
