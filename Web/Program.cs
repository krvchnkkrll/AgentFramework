using Application;
using Persistence;
using Sso;
using Web;
using Web.ExceptionHandling;
using Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.AddWeb()
    .AddSso()
    .AddApplication()
    .AddPersistence();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
