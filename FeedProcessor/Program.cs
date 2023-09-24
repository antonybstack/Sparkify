using Sparkify.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(static options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
