using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Sparkify;
using Sparkify.Features.Message;
using Sparkify.Hubs;

// configure use web root
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client/wwwroot"
});
var isDevelopment = builder.Environment.IsDevelopment();
Debug.WriteLine($"ContentRoot Path: {builder.Environment.ContentRootPath}");
Debug.WriteLine($"WebRootPath: {builder.Environment.WebRootPath}");
Debug.WriteLine($"IsDevelopment: {isDevelopment}");
 
// adds the database context to the dependency injection container
builder.Services.AddDbContext<Models>(opt => opt.UseInMemoryDatabase("Messages"));
// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddSignalR();
var app = builder.Build();

if (isDevelopment)
    app.UseDeveloperExceptionPage();

/* The preceding code allows the server to locate and serve the index.html file.
 * The file is served whether the user enters its full URL or the root URL of the web app. */
app.UseDefaultFiles(); // Enables default file mapping on the current path
app.UseStaticFiles(); // Enables static file serving for the current request path

app.MapGroup("/messages").MapMessagesApi();
app.MapHub<MessageHub>("/hub");

app.Run();