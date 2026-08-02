using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Assistant.Skills;

/// <summary>
/// Запускает скрипты скиллов как обычные процессы.
///
/// Что здесь важно понимать про безопасность: скрипты берутся из папок со скиллами, то есть их
/// кладёт туда администратор, а не модель. Модель может лишь выбрать, какой из уже лежащих скриптов
/// запустить и с какими аргументами. Тем не менее это выполнение произвольного кода на сервере,
/// поэтому скрипты выключены по умолчанию (Assistant:Skills:AllowScripts) и имеют жёсткий таймаут.
///
/// Аргументы приходят от модели в виде JSON-массива строк — ровно так описан контракт в
/// <see cref="AgentFileSkillScript.ParametersSchema"/>. Они передаются процессу как argv,
/// без участия шелла, поэтому подставить в них «; rm -rf /» бесполезно.
/// </summary>
public sealed class ProcessSkillScriptRunner(ILogger<ProcessSkillScriptRunner> logger)
{
    private const int TimeoutSeconds = 60;
    private const int MaxOutputChars = 16_000;

    /// <summary>Сопоставление расширения скрипта и интерпретатора.</summary>
    private static readonly Dictionary<string, string> Interpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = "python3",
        [".js"] = "node",
        [".sh"] = "bash",
        [".ps1"] = "pwsh",
    };

    /// <summary>Делегат в том виде, в котором его ждёт AgentFileSkillsSource.</summary>
    public AgentFileSkillScriptRunner AsRunner() => RunAsync;

    /// <summary>
    /// Раннер-заглушка на случай, когда скрипты выключены.
    ///
    /// Нужен потому, что AgentSkillsProviderBuilder требует раннер всегда — даже если ни один
    /// скилл скриптов не содержит (в отличие от конструктора AgentSkillsProvider, где он
    /// необязательный). Сюда управление в норме не попадает: при выключенных скриптах
    /// источник скиллов вообще их не находит, см. AgentContextProviderFactory.CreateFileOptions.
    /// </summary>
    public static AgentFileSkillScriptRunner Disabled { get; } =
        (_, script, _, _, _) => Task.FromResult<object?>(
            $"Запуск скриптов выключен, скрипт '{script.Name}' выполнить нельзя.");

    private async Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        JsonElement? arguments,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(script.FullPath);

        if (!Interpreters.TryGetValue(extension, out var interpreter))
            return $"Скрипты с расширением '{extension}' запускать нельзя.";

        var startInfo = new ProcessStartInfo
        {
            FileName = interpreter,
            WorkingDirectory = Path.GetDirectoryName(script.FullPath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(script.FullPath);

        foreach (var argument in ReadArguments(arguments))
            startInfo.ArgumentList.Add(argument);

        logger.LogInformation(
            "Скилл {SkillName}: запускаем скрипт {ScriptName} через {Interpreter}.",
            skill.Frontmatter.Name,
            script.Name,
            interpreter);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
                return $"Не удалось запустить '{interpreter}'.";

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

            await process.WaitForExitAsync(timeout.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return Format(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Скрипт {ScriptName} скилла {SkillName} не уложился в {TimeoutSeconds} с и был прерван.",
                script.Name,
                skill.Frontmatter.Name,
                TimeoutSeconds);

            return $"Скрипт не завершился за {TimeoutSeconds} с и был прерван.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Скрипт {ScriptName} упал при запуске.", script.Name);
            return $"Не удалось выполнить скрипт: {exception.Message}";
        }
    }

    private static IEnumerable<string> ReadArguments(JsonElement? arguments)
    {
        if (arguments is not { ValueKind: JsonValueKind.Array } array)
            yield break;

        foreach (var item in array.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();

            if (!string.IsNullOrEmpty(value))
                yield return value;
        }
    }

    private static string Format(int exitCode, string stdout, string stderr)
    {
        var builder = new StringBuilder();

        builder.Append("exit_code: ").Append(exitCode);

        if (!string.IsNullOrWhiteSpace(stdout))
            builder.AppendLine().AppendLine("stdout:").Append(Trim(stdout));

        if (!string.IsNullOrWhiteSpace(stderr))
            builder.AppendLine().AppendLine("stderr:").Append(Trim(stderr));

        return builder.ToString();
    }

    private static string Trim(string value) => value.Length <= MaxOutputChars
        ? value
        : string.Concat(value.AsSpan(0, MaxOutputChars), "\n… вывод обрезан");
}
