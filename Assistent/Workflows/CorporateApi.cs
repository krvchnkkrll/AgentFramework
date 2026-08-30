using Assistant.Options;
using Microsoft.Extensions.Options;

namespace Assistant.Workflows;

/// <summary>
/// Два «внешних сервиса» стенда: оргструктура и ЭДО. Данные захардкожены, а вместо сети —
/// задержка из <see cref="WorkflowOptions.ApiDelaySeconds"/>.
///
/// Задержка здесь не для красоты: без неё оба обращения отрабатывают за микросекунды,
/// и разница между «позвали по очереди» и «позвали параллельно» тонет в шуме. Пять секунд —
/// правдоподобное время ответа корпоративного сервиса, и на нём разница видна невооружённым глазом.
///
/// Задержка слушает токен отмены, иначе отменённый прогон всё равно досиживал бы её до конца.
/// </summary>
public sealed class CorporateApi(IOptions<AssistantOptions> options)
{
    private static readonly Employee[] Directory =
    [
        new("Иванов Иван Иванович", "Директор по развитию", "Департамент развития", "ivanov@company.ru", "100234"),
        new("Петрова Мария Сергеевна", "Начальник юридического отдела", "Юридический департамент", "petrova@company.ru", "100518"),
        new("Сидоров Алексей Петрович", "Главный бухгалтер", "Финансовый департамент", "sidorov@company.ru", "100077"),
        new("Кузнецова Ольга Викторовна", "Руководитель отдела кадров", "Департамент персонала", "kuznetsova@company.ru", "100341"),
        new("Морозов Дмитрий Андреевич", "Технический директор", "Департамент разработки", "morozov@company.ru", "100902"),
    ];

    private static readonly DocumentTemplate[] Templates =
    [
        new(
            "служебная записка",
            "СЗ-2024-07",
            """
            «Шапка»: кому (должность, ФИО в дательном падеже), от кого (должность, ФИО в родительном),
            дата, регистрационный номер.
            Заголовок: СЛУЖЕБНАЯ ЗАПИСКА.
            Тело: 1) обстоятельства, 2) обоснование, 3) просьба или предложение.
            Подпись: должность, личная подпись, расшифровка.
            Лист согласования: по одной строке на согласующего — должность, ФИО, дата, отметка о визе.
            """),
        new(
            "приказ",
            "ПР-2024-02",
            """
            «Шапка»: полное наименование организации, вид документа, дата, номер, место составления.
            Заголовок: краткая формулировка «О ...».
            Преамбула: основание издания приказа.
            Распорядительная часть: ПРИКАЗЫВАЮ, далее нумерованные пункты с исполнителями и сроками.
            Последний пункт — на кого возложен контроль исполнения.
            Подпись: руководитель. Ниже — лист ознакомления.
            """),
        new(
            "доверенность",
            "ДВ-2024-11",
            """
            «Шапка»: город, дата прописью.
            Тело: доверитель (реквизиты организации, представитель, основание полномочий),
            поверенный (ФИО, паспорт), перечень передаваемых полномочий, срок действия,
            указание на право или запрет передоверия.
            Подписи: доверитель, образец подписи поверенного, печать.
            """),
        new(
            "договор",
            "ДГ-2024-05",
            """
            Преамбула: стороны, их представители и основания полномочий.
            Разделы: 1) предмет, 2) права и обязанности, 3) цена и порядок расчётов, 4) ответственность,
            5) форс-мажор, 6) порядок разрешения споров, 7) срок действия, 8) реквизиты и подписи сторон.
            Приложения перечисляются отдельным пунктом.
            """),
    ];

    private TimeSpan Delay => TimeSpan.FromSeconds(options.Value.Workflow.ApiDelaySeconds);

    /// <summary>Ищет сотрудников по фамилии или части ФИО. Ненайденные перечисляются отдельно.</summary>
    public async Task<string> FindEmployeesAsync(
        IReadOnlyList<string> names,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);

        if (names.Count == 0)
            return "Имена не переданы, искать нечего.";

        var found = new List<string>();
        var missing = new List<string>();

        foreach (var name in names.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var match = Directory.FirstOrDefault(employee =>
                employee.FullName.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match is null)
                missing.Add(name.Trim());
            else
                found.Add($"{match.FullName} — {match.Position}, {match.Department}, {match.Email}, таб. № {match.PersonnelNumber}");
        }

        var lines = new List<string>();

        if (found.Count > 0)
            lines.Add("Найдены в оргструктуре:\n" + string.Join("\n", found.Select(line => "- " + line)));

        if (missing.Count > 0)
            lines.Add("Не найдены: " + string.Join(", ", missing));

        return string.Join("\n\n", lines);
    }

    /// <summary>Отдаёт структуру шаблона по типу документа. Неизвестный тип — список доступных.</summary>
    public async Task<string> GetTemplateAsync(
        string documentType,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);

        var normalized = (documentType ?? string.Empty).Trim();

        var template = Templates.FirstOrDefault(candidate =>
            normalized.Contains(candidate.DocumentType, StringComparison.OrdinalIgnoreCase)
            || candidate.DocumentType.Contains(normalized, StringComparison.OrdinalIgnoreCase));

        if (template is null)
        {
            return $"Шаблон для типа «{normalized}» в ЭДО не зарегистрирован. "
                + $"Доступные типы: {string.Join(", ", Templates.Select(item => item.DocumentType))}.";
        }

        return $"Шаблон {template.Code} ({template.DocumentType}). Структура:\n{template.Structure}";
    }

    private Task DelayAsync(CancellationToken cancellationToken) =>
        Delay > TimeSpan.Zero ? Task.Delay(Delay, cancellationToken) : Task.CompletedTask;

    private sealed record Employee(
        string FullName,
        string Position,
        string Department,
        string Email,
        string PersonnelNumber);

    private sealed record DocumentTemplate(string DocumentType, string Code, string Structure);
}
