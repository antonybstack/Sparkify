namespace Data;

public class Account : IEntity
{
    public string Name { get; init; }
    public int Balance { get; init; }
    public string Id { get; init; }

    // public void Apply(PaymentEvent @event) => Balance += @event.Amount;
}

public class User : IEntity
{
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string Id { get; init; }
}

public record PaymentEvent : IEvent

{
    public int Amount { get; init; }
    public string Id { get; init; }
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
