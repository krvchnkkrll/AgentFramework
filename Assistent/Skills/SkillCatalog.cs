using Assistant.Agents;
using Assistant.Contracts.Models;
using Assistant.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Assistant.Skills;

/// <summary>
/// Список скиллов, лежащих в папках со скиллами. Нужен конструктору агентов, чтобы показать
/// пользователю, из чего выбирать.
///
/// Читает те же файлы тем же парсером, что и агент во время работы, — иначе в конструкторе
/// показывался бы один набор скиллов, а агенту доставался другой.
/// </summary>
internal sealed class SkillCatalog : IDisposable
{
    private readonly AgentFileSkillsSource? _source;
    private readonly ILogger<SkillCatalog> _logger;

    public SkillCatalog(AssistantOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _logger = loggerFactory.CreateLogger<SkillCatalog>();

        if (!options.Skills.Enabled)
            return;

        var directories = AgentContextProviderFactory.ResolveSkillDirectories(options);

        if (directories.Length == 0)
            return;

        _source = new AgentFileSkillsSource(
            directories,
            scriptRunner: null,
            AgentContextProviderFactory.CreateFileOptions(options),
            loggerFactory);
    }

    /// <param name="agent">
    /// Нужен только для того, чтобы собрать контекст, который требует источник скиллов.
    /// На результат не влияет — скиллы читаются с диска, а не из состояния агента.
    /// </param>
    public async Task<IReadOnlyList<AssistantSkillInfo>> GetAsync(
        AIAgent agent,
        CancellationToken cancellationToken = default)
    {
        if (_source is null)
            return [];

        try
        {
            var session = await agent.CreateSessionAsync(cancellationToken);
            var skills = await _source.GetSkillsAsync(new AgentSkillsSourceContext(agent, session), cancellationToken);

            return
            [
                .. skills
                    .Select(skill => new AssistantSkillInfo
                    {
                        Name = skill.Frontmatter.Name,
                        Description = skill.Frontmatter.Description,
                    })
                    .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Битый SKILL.md не должен ронять конструктор агентов — покажем пустой список.
            _logger.LogWarning(exception, "Не удалось прочитать список скиллов.");
            return [];
        }
    }

    public void Dispose() => _source?.Dispose();
}
