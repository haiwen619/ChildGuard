using Haidou.ChildGuard.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// 配置为 Windows 原生系统服务
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "HaidouChildGuardService";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
