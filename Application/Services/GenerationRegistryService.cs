using System.Collections.Concurrent;
using Application.Contracts.Models;
using Application.Contracts.Services;
using Domain.Entities.Conversations;
using Domain.Entities.Conversations.Parameters;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Contracts;

namespace Application.Services;

internal sealed class GenerationRegistryService(
    IDbContextFactory dbFactory,
    ILogger<GenerationRegistryService> logger) : IGenerationRegistryService
{
    private readonly ConcurrentDictionary<Guid, ActiveGeneration> _generations = new();

    public ActiveGeneration? GetGeneration(Guid conversationId)
    {
        return _generations.GetValueOrDefault(conversationId);
    }

    public bool IsGenerationActive(Guid conversationId)
    {
        return _generations.ContainsKey(conversationId);
    }

    public async Task StartGeneration(StartGenerationParameters parameters)
    {
        if (!AddGeneration(parameters))
            return;

        var activeGeneration = GetGeneration(parameters.ConversationId)!;

        try
        {
            var context = await dbFactory.CreateDbContextAsync();
            var conversation = await GetConversationAsync(parameters.ConversationId, context, CancellationToken.None);

            // TODO: сама генерация — вызов агента, чанки в activeGeneration.Message /
            // activeGeneration.AddStreamMessageChunk(...)

            conversation.AddMessage(new AddMessageParameter
            {
                RoleEnum = MessageRoleEnum.Assistant,
                Text = activeGeneration.Message.ToString(),
            });

            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Произошла ошибка при генерации сообщения для чата {ConversationId}.",
                parameters.ConversationId);
        }
        finally
        {
            RemoveGeneration(parameters.ConversationId);
        }
    }

    private bool AddGeneration(StartGenerationParameters parameters)
    {
        return _generations.TryAdd(parameters.ConversationId,
            new ActiveGeneration(parameters.ConversationId, parameters.UserId));
    }

    private bool RemoveGeneration(Guid conversationId)
    {
        return _generations.TryRemove(conversationId, out _);
    }

    private static async Task<Conversation> GetConversationAsync(Guid conversationId, IDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.Conversations
            .Where(conversation => conversation.Id == conversationId)
            .SingleAsync(cancellationToken);
    }
}
