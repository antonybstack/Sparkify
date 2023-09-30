using System.Diagnostics;
using System.Text.Json.Serialization;
using Raven.Client.Documents.Changes;

namespace Sparkify;

public sealed class Blog : IEntity
{
    public string? Company { get; set; }
    public string? Link { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; init; }
}

public sealed record RssBlogFeed : IEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; init; }
    public string BlogId { get; set; }
}

public sealed record Article : IEntity
{
    public string Id { get; init; }
    public string BlogId { get; init; }
    public string Link { get; set; }
    public DateTimeOffset Date { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    // public string? HtmlContent { get; set; }
    public ICollection<string>? Authors { get; set; }
    public ICollection<string>? Categories { get; set; }
    public string Uid { get; set; }
}

public sealed record Payload<T>
{
    public T? Data { get; init; }
    public RequestStatistics Stats { get; init; }
}

public sealed record RequestStatistics
{
    public long DurationInMs { get; init; }
    public long TotalResults { get; init; }
}

public interface IEntity
{
    public string Id { get; init; }
}

public class IndexChangeObserver : IObserver<IndexChange>
{
    public void OnCompleted() =>
        Debug.WriteLine("All changes have been processed.");

    public void OnError(Exception error) =>
        Debug.WriteLine($"Error: {error.Message}");

    public void OnNext(IndexChange change) =>
        Debug.WriteLine($"Index {change.Name} has changed. Type of change: {change.Type}");
}
