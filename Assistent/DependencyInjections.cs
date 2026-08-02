using System.ClientModel;
using Assistant.Agents;
using Assistant.Contracts;
using Assistant.Options;
using Microsoft.Extensions.AI;
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
                    Endpoint = new Uri(assistantOptions.Url + "/v1")
                }
            );
        });
        
        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var openAiClient = sp.GetRequiredService<OpenAIClient>();
            var assistantOptions = sp.GetRequiredService<IOptions<AssistantOptions>>().Value;
            return openAiClient.GetChatClient(assistantOptions.Model).AsIChatClient();
        });

        builder.Services.AddSingleton<DefaultAgent>();
        builder.Services.AddSingleton<IAssistantAgent>(sp => sp.GetRequiredService<DefaultAgent>());

        return builder;
    }
    
}