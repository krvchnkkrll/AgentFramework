using System.Text.Json.Serialization;

namespace Web;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddWeb(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddOpenApi();
        return builder;
    }
}
