using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpFort.Tool;
class Program
{
    static async Task Main(string[] args)
    {

#if DEBUG
        
        //帮助
        //args = ["-h"];
        
        //版本
        //args = ["-v"];
        
        //清理
        //args = ["clear"];
        
        //创建模块
        //args = ["new","oooo", "-p","D:\\temp","-csf"];
        
        //查看模板列表
        //args = ["new","list"];
        //查看模板详细信息
        //args = ["new","list","-d"];
        //预览模板结构
        //args = ["new","list","-b","main"];
        //刷新缓存
        //args = ["new","list","--refresh"];
        //清空缓存
        //args = ["new","list","--clear"];

        //添加模块
        //args = ["add-module", "kkk"];

        //首次设置向导
        //args = ["init"];
        //跳过克隆的初始化
        //args = ["init","--skip-clone"];

        //环境诊断
        //args = ["doctor"];

        //克隆框架 (双源自动切换)
        //args = ["clone"];
#endif
        try
        {
            IHost host = Host.CreateDefaultBuilder()
                .ConfigureServices(async (host, service) =>
                {
                    await service.AddApplicationAsync<SharpFortToolModule>();
                })
                //})
                .UseAutofac()
                .Build();
            var commandSelector = host.Services.GetRequiredService<CommandInvoker>();
            await commandSelector.InvokerAsync(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }

}