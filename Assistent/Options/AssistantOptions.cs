namespace Assistant.Options;

public sealed class AssistantOptions
{
    /// <summary>
    /// Адрес сервера без /v1 на конце. Для локальной LM Studio — http://192.168.0.12:1234,
    /// для Yandex AI Studio — https://ai.api.cloud.yandex.net.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Идентификатор модели. У локального сервера это просто имя (google/gemma-4-12b-qat),
    /// у Yandex — URI вида gpt://{FolderId}/qwen3-235b-a22b-fp8/latest.
    /// </summary>
    public required string Model { get; init; }
}
