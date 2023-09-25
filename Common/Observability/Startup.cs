// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Common.Observability;

public static class Startup
{
    public static void LogStartupInfo(this WebApplication app, WebApplicationBuilder builder)
    {
        var isDevelopment = app.Environment.IsDevelopment();
        var server = app.Services.GetRequiredService<IServer>();
        app.Logger.LogInformation("Application Name: {ApplicationName}", builder.Environment.ApplicationName);
        app.Logger.LogInformation("Environment Name: {EnvironmentName}", builder.Environment.EnvironmentName);
        app.Logger.LogInformation("ContentRoot Path: {ContentRootPath}", builder.Environment.ContentRootPath);
        app.Logger.LogInformation("WebRootPath: {WebRootPath}", builder.Environment.WebRootPath);
        app.Logger.LogInformation("IsDevelopment: {IsDevelopment}", isDevelopment);
        app.Logger.LogInformation("Web server: {WebServer}", server.GetType().Name);
    }
}
