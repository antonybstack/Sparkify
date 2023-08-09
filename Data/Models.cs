using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Raven.Client.Documents.Changes;

namespace Data;

public class User : IEntity
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DefaultValue(null)]
    public string Id { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
}

public record PaymentEvent : IEvent
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [DefaultValue(null)]
    public string Id { get; init; }
    [DefaultValue(1000)]
    public int Amount { get; init; }
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
    public void OnCompleted()
    {
        Debug.WriteLine("All changes have been processed.");
    }

    public void OnError(Exception error)
    {
        // Handle any errors.
    }

    public void OnNext(IndexChange change)
    {
        Debug.WriteLine($"Index {change.Name} has changed. Type of change: {change.Type}");
    }
}
