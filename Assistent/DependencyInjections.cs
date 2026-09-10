using System.ClientModel;
using System.Net.Http.Headers;
using System.Text;
using Assistant.Agents;
using Assistant.Contracts;
using Assistant.Contracts.Documents;
using Assistant.Documents;
using Assistant.Options;
using Assistant.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Assistant;

public static class DependencyInjections
{
    public static IHostApplicationBuilder AddAssistant(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AssistantOptions>().BindConfiguration("Assistant").ValidateOnStart();

        builder.Services.AddSingleton<OpenAIClient>(sp =>
        {
            var assistantOptions = sp.GetRequiredService<IOptions<AssistantOptions>>().Value;

            return new OpenAIClient(
                new ApiKeyCredential("RandomKey"),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(assistantOptions.Url + "/v1"),
                }
            );
        });

        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var openAiClient = sp.GetRequiredService<OpenAIClient>();
            var assistantOptions = sp.GetRequiredService<IOptions<AssistantOptions>>().Value;
            return openAiClient.GetChatClient(assistantOptions.Model).AsIChatClient();
        });

        builder.AddOpenSearch();

        // IFileService, из которого качаются тексты скиллов, регистрирует проект FileService
        // (AddFileService). Без него агенты просто работают без скиллов.

        // Документы живут в памяти процесса — это временно, под эксперимент.
        builder.Services.AddSingleton<InMemoryDocumentStore>();
        builder.Services.AddSingleton<IDocumentStore>(sp => sp.GetRequiredService<InMemoryDocumentStore>());
        builder.Services.AddSingleton<DefaultAgent>();
        builder.Services.AddSingleton<IAssistantAgent>(sp => sp.GetRequiredService<DefaultAgent>());

        return builder;
    }

    /// <summary>
    /// Регистрирует клиент OpenSearch, только если поиск включён — иначе агент получит null
    /// и просто не станет добавлять провайдер поиска.
    /// </summary>
    private static void AddOpenSearch(this IHostApplicationBuilder builder)
    {
        var searchOptions = builder.Configuration.GetSection("Assistant:Search").Get<SearchOptions>();

        if (searchOptions is not { Enabled: true } || string.IsNullOrWhiteSpace(searchOptions.Url))
            return;

        builder.Services
            .AddHttpClient<OpenSearchTextSearchClient>(OpenSearchTextSearchClient.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(searchOptions.Url);
                client.Timeout = TimeSpan.FromSeconds(searchOptions.TimeoutSeconds);

                if (string.IsNullOrWhiteSpace(searchOptions.Username))
                    return;

                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{searchOptions.Username}:{searchOptions.Password}"));

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // В локальном контуре OpenSearch обычно поднят с self-signed сертификатом.
                ServerCertificateCustomValidationCallback = searchOptions.AllowInvalidCertificate
                    ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    : null,
            });
    }
}
