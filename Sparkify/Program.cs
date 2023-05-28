using Microsoft.EntityFrameworkCore;
using Sparkify.Message;

var builder = WebApplication.CreateBuilder(args);
// adds the database context to the dependency injection container
builder.Services.AddDbContext<Models>(opt => opt.UseInMemoryDatabase("Messages"));
// enables displaying database-related exceptions:
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
var app = builder.Build();

app.MapGroup("/messages").MapMessagesApi();

app.Run();