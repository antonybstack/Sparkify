using System.ComponentModel;
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

public record Article : IEntity
{
    public string BlogId { get; init; }
    public string Link { get; set; }
    public ICollection<string>? Authors { get; set; }
    public string? Title { get; set; }
    public DateTime? Date { get; set; }
    public string Uid { get; set; }
    public ICollection<string>? Categories { get; set; }
    public string? Content { get; set; }
    // [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; init; }
}

public record ArticleDto : Article
{
    public string Company { get; set; }
    public string Logo { get; set; }
}

public record Payload<T>
{
    public T? Data { get; init; }
    public RequestStatistics stats { get; init; }
}

public record RequestStatistics
{
    public long DurationInMs { get; init; }
    public long TotalResults { get; init; }
}

public class User : IEntity
{
    public string FirstName { get; init; }
    public string LastName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), DefaultValue(null)]
    public string Id { get; init; }
}

public record PaymentEvent : IEvent
{
    [DefaultValue(1000)]
    public int Amount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), DefaultValue(null)]
    public string Id { get; init; }
    [DefaultValue(nameof(EventType.PaymentRequested))]
    public EventType EventType { get; init; }
    public string ReferenceId { get; init; }
}

public interface IEvent : IEntity
{
    public EventType EventType { get; init; }
    public string ReferenceId { get; init; } // database ID of the aggregate that the event belongs to
}

public interface IEntity
{
    public string Id { get; init; }
}

public enum EventType
{
    Invalid,
    PaymentRequested,
    PaymentCompleted,
    PaymentFailed,
    PaymentCancelled,
    PaymentRefunded,
    PaymentExpired,
    PaymentDeclined,
    PaymentReversed,
    PaymentSettled,
    PaymentCaptured,
    PaymentAuthorized,
    PaymentVoided,
    PaymentChargedBack,
    PaymentChargebackReversed,
    PaymentChargebackDeclined,
    PaymentChargebackSettled,
    PaymentChargebackExpired,
    PaymentChargebackCancelled,
    PaymentChargebackFailed,
    PaymentChargebackRequested,
    PaymentChargebackReversedRequested,
    PaymentChargebackSettledRequested,
    PaymentChargebackExpiredRequested,
    PaymentChargebackCancelledRequested,
    PaymentChargebackFailedRequested
}

public class IndexChangeObserver : IObserver<IndexChange>
{
    public void OnCompleted() =>
        Debug.WriteLine("All changes have been processed.");

    public void OnError(Exception error)
    {
        // Handle any errors.
    }

    public void OnNext(IndexChange change) =>
        Debug.WriteLine($"Index {change.Name} has changed. Type of change: {change.Type}");
}
