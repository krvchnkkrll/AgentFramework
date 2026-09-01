using Microsoft.Agents.AI;

namespace Assistant.Agents;

/// <summary>
/// Собранный и готовый к работе агент: сам <see cref="AIAgent"/> плюс всё, что под него
/// построено. Сборка не бесплатная (сканирование папок со скиллами, создание провайдеров),
/// поэтому рантаймы кэшируются в <see cref="AgentRuntimeFactory"/> и переиспользуются
/// между запросами.
/// </summary>
internal sealed class AgentRuntime(
    AIAgent agent,
    AgentProviderSet providers,
    IReadOnlyList<string> toolNames,
    DateTimeOffset version,
    string skillsSignature) : IDisposable
{
    public AIAgent Agent { get; } = agent;

    public IReadOnlyList<string> ToolNames { get; } = toolNames;

    /// <summary>
    /// Отметка версии настроек, из которых собран агент. Если в БД она стала новее —
    /// пользователь поправил агента в конструкторе, и рантайм пора пересобрать.
    /// </summary>
    public DateTimeOffset Version { get; } = version;

    /// <summary>
    /// Отпечаток набора скиллов: идентификаторы вместе с хэшами содержимого.
    ///
    /// Отдельно от <see cref="Version"/>, потому что содержимое скилла меняется не через
    /// конструктор агента: загрузили новую версию скилла — UpdatedAt агента остался прежним,
    /// а работать он должен уже с новым текстом.
    /// </summary>
    public string SkillsSignature { get; } = skillsSignature;

    public IReadOnlyList<string> Descriptions => providers.Descriptions;

    public void Dispose()
    {
        foreach (var provider in providers.Providers.OfType<IDisposable>())
            provider.Dispose();
    }
}
