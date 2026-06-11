using SharpFort.Tool.Application.Contracts;
using SharpFort.Tool.Domain;

namespace SharpFort.Tool.Application
{
    [DependsOn(typeof(SharpFortToolApplicationContractsModule),
        typeof(SharpFortToolDomainModule))]
    public class SharpFortToolApplicationModule:AbpModule
    {

    }
}
