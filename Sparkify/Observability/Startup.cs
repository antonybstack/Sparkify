// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Hosting.Server;

namespace Sparkify.Observability;

public static class Startup
{
    public static void LogStartupInfo(this WebApplication app, WebApplicationBuilder builder)
    {
        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
        var isDevelopment = app.Environment.IsDevelopment();
        IServer server = app.Services.GetRequiredService<IServer>();
        logger.LogInformation("Application Name: {ApplicationName}", builder.Environment.ApplicationName);
        logger.LogInformation("Environment Name: {EnvironmentName}", builder.Environment.EnvironmentName);
        logger.LogInformation("ContentRoot Path: {ContentRootPath}", builder.Environment.ContentRootPath);
        logger.LogInformation("WebRootPath: {WebRootPath}", builder.Environment.WebRootPath);
        logger.LogInformation("IsDevelopment: {IsDevelopment}", isDevelopment);
        logger.LogInformation("Web server: {WebServer}", server.GetType().Name);
    }
}
