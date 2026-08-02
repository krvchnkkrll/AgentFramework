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
        // Синглтон, а не scoped: нотификатором пользуется фоновая генерация (тоже синглтон),
        // живущая вне HTTP-запроса. Зависит только от IHubContext, который и сам синглтон.
        builder.Services.AddSingleton<IChatNotifier, SignalRChatNotifier>();

        return builder;
    }
}
