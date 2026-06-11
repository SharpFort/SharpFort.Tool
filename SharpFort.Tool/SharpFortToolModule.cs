using SharpFort.Tool.Application;

namespace SharpFort.Tool
{
    [DependsOn(typeof(SharpFortToolApplicationModule))]
    public class SharpFortToolModule : AbpModule
    {
        // 自包含模式：不再需要远程服务配置
        // PostConfigureServices 已移除
    }
}
