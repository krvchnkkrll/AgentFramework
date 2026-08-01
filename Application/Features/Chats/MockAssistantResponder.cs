using Application.Contracts.Features.Chats;
using Domain.Entities.Conversations.Parameters;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Persistence.Contracts.Repositories;

namespace Application.Features.Chats;

/// <summary>
/// Stands in for a real agent: after a user message is saved, streams a canned reply
/// over <see cref="IChatNotifier"/> and persists it once "done". Runs detached from the
/// HTTP request that triggered it, so it gets its own DI scope instead of reusing one
/// whose DbContext may already be disposed by the time it runs.
/// </summary>
public sealed class MockAssistantResponder(IServiceScopeFactory scopeFactory, ILogger<MockAssistantResponder> logger)
{
    /// <summary>Fire-and-forget: kicks off the reply and returns immediately.</summary>
    public void Start(Guid chatId, string userText)
    {
        _ = RunAsync(chatId, userText);
    }

    private async Task RunAsync(Guid chatId, string userText)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var conversationRepository = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
            var notifier = scope.ServiceProvider.GetRequiredService<IChatNotifier>();

            var conversation = await conversationRepository.GetByIdAsync(chatId, CancellationToken.None);
            if (conversation is null)
                return;

            var isFirstExchange = conversation.Messages.Count == 1;
            var assistantMessageId = Guid.CreateVersion7();

            await notifier.MessageStartedAsync(chatId, assistantMessageId);
            await Task.Delay(TimeSpan.FromMilliseconds(400));

            var replyText = BuildReply(userText);

            foreach (var chunk in SplitIntoChunks(replyText))
            {
                await notifier.MessageDeltaAsync(chatId, assistantMessageId, chunk);
                await Task.Delay(TimeSpan.FromMilliseconds(35));
            }

            var message = conversation.AddMessage(new AddMessageParameter
            {
                Id = assistantMessageId,
                RoleEnum = MessageRoleEnum.Assistant,
                Text = replyText,
            });

            await conversationRepository.SaveChangesAsync(CancellationToken.None);
            await notifier.MessageCompletedAsync(chatId, message.ToResponse());

            if (isFirstExchange)
            {
                var title = BuildTitle(userText);
                conversation.Rename(title);
                await conversationRepository.SaveChangesAsync(CancellationToken.None);
                await notifier.ChatRenamedAsync(chatId, title);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Mock assistant reply failed for chat {ChatId}.", chatId);
        }
    }

    private static string BuildReply(string userText) =>
        $"Это заглушка ответа модели — настоящей генерации пока нет. " +
        $"Вы написали: «{userText.Trim()}».";

    private static IEnumerable<string> SplitIntoChunks(string text)
    {
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
            yield return i == 0 ? words[i] : " " + words[i];
    }

    private static string BuildTitle(string userText)
    {
        var trimmed = userText.Trim();
        return trimmed.Length <= 40 ? trimmed : trimmed[..40].TrimEnd() + "…";
    }
}
