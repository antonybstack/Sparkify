using Client;
using Data;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Raven.Client.Documents;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.TryAddSingleton(DbManager.Store);
builder.Services.TryAddTransient(provider => provider.GetRequiredService<IDocumentStore>().OpenAsyncSession());

builder.Services.AddSingleton<AccountDataStore>();
// builder.Services.AddHostedService<UserProjectionBackgroundService>();
builder.Services.AddHostedService<ConsoleService>();

IHost host = builder.Build();
host.Run();
