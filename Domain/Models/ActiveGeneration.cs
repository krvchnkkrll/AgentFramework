using System.Collections.Concurrent;
using System.Text;

namespace Domain.Models;

public sealed class ActiveGeneration(Guid conversationId, Guid userId)
{
    public Guid ConversationId { get; private init; } = conversationId;
    public Guid UserId { get; private init; } = userId;
    public StringBuilder Message { get; private init; } = new();
    public readonly ConcurrentBag<PipelineCallingInformation> ToolMessages = [];
    public CancellationTokenSource CancellationTokenSource { get; } = new ();

    public void AddStreamMessageChunk(string chunk)
    {
        Message.Append(chunk);
    }
    
    public void AddToolMessage(PipelineCallingInformation callingInformation)
    {
        ToolMessages.Add(callingInformation);
    }
}