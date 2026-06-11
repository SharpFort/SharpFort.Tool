using Microsoft.Extensions.DependencyInjection;
using SharpFort.Tool.Application;

namespace SharpFort.Tool
{
    [DependsOn(typeof(SharpFortToolApplicationModule))]
    public class SharpFortToolModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            // 注册 HttpClient 工厂（TemplateRepoManager 依赖）
            context.Services.AddHttpClient();
        }
    }
}
