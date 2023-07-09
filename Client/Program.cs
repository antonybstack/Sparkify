using Client;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<UserDataStore>();
builder.Services.AddHostedService<UserProjectionBackgroundService>();
builder.Services.AddHostedService<ConsoleService>();

var host = builder.Build();
host.Run();
