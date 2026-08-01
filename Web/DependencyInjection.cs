using System.Text.Json.Serialization;
using Application.Contracts.Features.Chats;
using Web.Hubs;

namespace Web;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddWeb(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddOpenApi();

        builder.Services.AddSignalR();
        builder.Services.AddScoped<IChatNotifier, SignalRChatNotifier>();

        return builder;
    }
}
