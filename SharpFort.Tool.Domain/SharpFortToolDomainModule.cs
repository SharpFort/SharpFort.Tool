using Microsoft.Extensions.DependencyInjection;
using SharpFort.Tool.Domain.Shared;
using SharpFort.Tool.Domain.Shared.Options;

namespace SharpFort.Tool.Domain
{
    [DependsOn(typeof(SharpFortToolDomainSharedModule))]
    public class SharpFortToolDomainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // 从统一配置文件读取 ToolOptions
            var configManager = new ConfigManager();
            var config = configManager.GetConfig();

            Configure<ToolOptions>(options =>
            {
                options.TempDirPath = config.Tool.TempDirPath;
            });

            // 确保目录存在
            if (!Directory.Exists(config.Tool.TempDirPath))
                Directory.CreateDirectory(config.Tool.TempDirPath);
            if (!Directory.Exists(config.Tool.CacheDirPath))
                Directory.CreateDirectory(config.Tool.CacheDirPath);
        }
    }
}
