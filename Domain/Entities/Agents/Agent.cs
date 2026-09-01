using Domain.Common;
using Domain.Entities.Agents.Parameters;
using Domain.Entities.Skills;
using Domain.Enums;

namespace Domain.Entities.Agents;

/// <summary>
/// Агент, собранный пользователем в конструкторе: промпт, набор скиллов и параметры генерации.
/// Встроенного агента здесь нет — он живёт в конфигурации приложения и не редактируется,
/// а чат без явно выбранного агента отвечает именно им.
/// </summary>
public sealed class Agent : Entity
{
    /// <summary>Ограничения на длину — чтобы форма конструктора не уехала в БД мегабайтами.</summary>
    public const int MaxNameLength = 60;

    public const int MaxDescriptionLength = 300;

    public const int MaxIconLength = 8;

    public const int MaxInstructionsLength = 20_000;

    private readonly List<Skill> _skills = [];

    public Guid UserId { get; private init; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }

    /// <summary>Системный промпт в Markdown. Пусто — агент работает на встроенном промпте.</summary>
    public string? Instructions { get; private set; }

    public float Temperature { get; private set; }
    public float TopP { get; private set; }
    public int TopK { get; private set; }
    public int MaxOutputTokens { get; private set; }
    public float FrequencyPenalty { get; private set; }
    public float PresencePenalty { get; private set; }
    public ReasoningEffortEnum ReasoningEffortEnum { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>
    /// Меняется при любой правке. Слой ассистента по этой метке понимает, что собранный
    /// в память агент устарел, и пересобирает его.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Скиллы, доступные агенту. Связь многие-ко-многим: один скилл живёт у нескольких
    /// агентов, копировать его на каждого незачем.
    /// </summary>
    public IReadOnlyCollection<Skill> Skills => _skills;

    private Agent()
    {
        Name = null!;
    }

    private Agent(Guid id, Guid userId, DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        Name = null!;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Agent Create(CreateAgentParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (parameter.UserId == Guid.Empty)
            throw new ArgumentException("Agent must belong to a user.", nameof(parameter));

        var agent = new Agent(Guid.CreateVersion7(), parameter.UserId, DateTimeOffset.UtcNow);
        agent.Apply(parameter);

        return agent;
    }

    /// <summary>Полностью переписывает настройки агента — конструктор всегда шлёт форму целиком.</summary>
    public void Update(CreateAgentParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Apply(parameter);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Apply(CreateAgentParameter parameter)
    {
        Name = NormalizeName(parameter.Name);
        Description = Trim(parameter.Description, MaxDescriptionLength);
        Icon = Trim(parameter.Icon, MaxIconLength);
        Instructions = Trim(parameter.Instructions, MaxInstructionsLength);

        // Скиллы приезжают сюда уже загруженными сущностями: проверить, что они существуют
        // и принадлежат этому пользователю, домен не может — это работа обработчика команды.
        _skills.Clear();
        _skills.AddRange(parameter.Skills.DistinctBy(skill => skill.Id));

        var generation = parameter.Generation;

        // Границы намеренно широкие: это не бизнес-правило, а защита от значений,
        // на которых сервер модели просто вернёт ошибку.
        Temperature = Clamp(generation.Temperature, 0f, 2f);
        TopP = Clamp(generation.TopP, 0f, 1f);
        TopK = Math.Clamp(generation.TopK, 0, 500);
        MaxOutputTokens = Math.Clamp(generation.MaxOutputTokens, 64, 200_000);
        FrequencyPenalty = Clamp(generation.FrequencyPenalty, -2f, 2f);
        PresencePenalty = Clamp(generation.PresencePenalty, -2f, 2f);
        ReasoningEffortEnum = generation.ReasoningEffortEnum;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name cannot be empty.", nameof(name));

        var trimmed = name.Trim();

        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static float Clamp(float value, float min, float max) =>
        float.IsNaN(value) ? min : Math.Clamp(value, min, max);
}
