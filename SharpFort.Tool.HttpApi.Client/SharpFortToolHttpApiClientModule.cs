using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.Http.Client;
using SharpFort.Tool.Application.Contracts;

namespace SharpFort.Tool.HttpApi.Client
{
    [DependsOn(typeof(AbpHttpClientModule),
            typeof(AbpAutofacModule),
            typeof(SharpFortToolApplicationContractsModule))]
    public class SharpFortToolHttpApiClientModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            //创建动态客户端代理
            context.Services.AddHttpClientProxies(
                typeof(SharpFortToolApplicationContractsModule).Assembly

            );
            Configure<AbpRemoteServiceOptions>(options =>
            {
                options.RemoteServices.Default =
                    new RemoteServiceConfiguration("http://localhost:19002");
            });
        }
    }
}
