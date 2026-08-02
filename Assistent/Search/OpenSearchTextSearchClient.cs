using System.Net.Http.Json;
using System.Text.Json;
using Assistant.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assistant.Search;

/// <summary>
/// Полнотекстовый поиск по OpenSearch для <see cref="TextSearchProvider"/>.
///
/// Почему руками через HTTP, а не готовым коннектором: официальный коннектор в семействе
/// Microsoft.Extensions.VectorData есть для Elasticsearch, но начиная с 8.x клиент Elastic
/// проверяет заголовок ответа x-elastic-product и на OpenSearch отваливается — OpenSearch
/// форкнулся от Elasticsearch 7.10 и этот заголовок не отдаёт. Поисковый REST-контракт при этом
/// у них совместим, поэтому достаточно одного POST в /{index}/_search.
///
/// Здесь чистый BM25 (multi_match). Если в индексе есть векторное поле и включён плагин k-NN,
/// сюда же добавляется knn-запрос — точка расширения одна, метод <see cref="BuildQuery"/>.
/// </summary>
public sealed class OpenSearchTextSearchClient(
    HttpClient httpClient,
    IOptions<AssistantOptions> options,
    ILogger<OpenSearchTextSearchClient> logger)
{
    /// <summary>Имя HTTP-клиента в IHttpClientFactory.</summary>
    public const string HttpClientName = "opensearch";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private SearchOptions Options => options.Value.Search;

    /// <summary>
    /// Делегат, который ждёт <see cref="TextSearchProvider"/>. Никогда не кидает:
    /// упавший поиск не должен ронять ответ ассистента, поэтому в худшем случае вернётся пусто.
    /// </summary>
    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                $"/{Uri.EscapeDataString(Options.Index)}/_search",
                BuildQuery(query),
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                logger.LogWarning(
                    "OpenSearch ответил {StatusCode} на поиск по индексу {Index}: {Body}.",
                    (int)response.StatusCode,
                    Options.Index,
                    body);

                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var results = ReadHits(document.RootElement);

            logger.LogInformation(
                "OpenSearch: по запросу длиной {QueryLength} символов найдено {Count} документов.",
                query.Length,
                results.Count);

            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Поиск в OpenSearch не удался, отвечаем без найденных документов.");
            return [];
        }
    }

    /// <summary>Тело запроса в _search. Здесь же добавляется knn, если появится векторное поле.</summary>
    private object BuildQuery(string query) => new
    {
        size = Options.MaxResults,
        query = new
        {
            multi_match = new
            {
                query,
                fields = Options.Fields,
                type = "best_fields",
            },
        },
    };

    private List<TextSearchProvider.TextSearchResult> ReadHits(JsonElement root)
    {
        var results = new List<TextSearchProvider.TextSearchResult>();

        if (!root.TryGetProperty("hits", out var hitsRoot)
            || !hitsRoot.TryGetProperty("hits", out var hits)
            || hits.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var hit in hits.EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out var source))
                continue;

            var text = ReadString(source, Options.TextField);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            results.Add(new TextSearchProvider.TextSearchResult
            {
                Text = text,
                SourceName = Options.TitleField is null ? null : ReadString(source, Options.TitleField),
                SourceLink = Options.LinkField is null ? null : ReadString(source, Options.LinkField),
                RawRepresentation = hit.Clone(),
            });
        }

        return results;
    }

    /// <summary>Достаёт строковое поле, понимая точечную нотацию вида "meta.title".</summary>
    private static string? ReadString(JsonElement source, string path)
    {
        var current = source;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.ToString(),
            _ => null,
        };
    }
}
