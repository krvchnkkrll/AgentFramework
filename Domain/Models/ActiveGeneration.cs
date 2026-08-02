using System.Collections.Concurrent;
using System.Text;

namespace Domain.Models;

public sealed class ActiveGeneration(Guid conversationId, Guid userId)
{
    /// <summary>
    /// Вызовы инструментов текущей генерации, разложенные по идентификатору вызова от модели.
    /// Словарь, а не список: результат инструмента приходит отдельным событием и его надо
    /// сопоставить с ранее начатым вызовом.
    /// </summary>
    private readonly ConcurrentDictionary<string, PipelineCallingInformation> _toolCalls =
        new(StringComparer.Ordinal);

    public Guid ConversationId { get; private init; } = conversationId;
    public Guid UserId { get; private init; } = userId;
    public StringBuilder Message { get; private init; } = new();
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <summary>Все вызовы инструментов, случившиеся за эту генерацию.</summary>
    public IReadOnlyCollection<PipelineCallingInformation> ToolCalls => _toolCalls.Values.ToArray();

    public void AddStreamMessageChunk(string chunk)
    {
        Message.Append(chunk);
    }

    /// <summary>
    /// Запоминает начатый вызов инструмента. Повторный вызов с тем же CallId ничего не меняет:
    /// модель может прислать вызов кусками, и дублировать событие в UI не нужно.
    /// </summary>
    public bool TryAddToolCall(PipelineCallingInformation callingInformation)
    {
        return _toolCalls.TryAdd(callingInformation.CallId, callingInformation);
    }

    /// <summary>Находит ранее начатый вызов по идентификатору от модели.</summary>
    public PipelineCallingInformation? FindToolCall(string callId)
    {
        return _toolCalls.GetValueOrDefault(callId);
    }
}
