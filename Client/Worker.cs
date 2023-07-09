using System.Collections.Concurrent;
using System.Text.Json;
using EventStore.Client;

namespace Client
{
    public class UserProjectionBackgroundService : BackgroundService
    {
        private readonly ILogger<UserProjectionBackgroundService> _logger;
        private readonly EventStoreClient Client;
        private readonly EventStoreProjectionManagementClient ManagementClient;
        private readonly UserDataStore _userDataStore;
        private readonly Uuid eventId = Uuid.NewUuid();
        private readonly ConcurrentDictionary<string, User> _users = new ConcurrentDictionary<string, User>();
        public UserProjectionBackgroundService(ILogger<UserProjectionBackgroundService> logger,
            UserDataStore userDataStore)
        {
            _logger = logger;
            _userDataStore = userDataStore;
            var eventStoreClientSettings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
            Client = new EventStoreClient(eventStoreClientSettings);
            eventStoreClientSettings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
            eventStoreClientSettings.ConnectionName = "Projection management client";
            eventStoreClientSettings.DefaultCredentials = new UserCredentials("admin", "changeit");
            ManagementClient = new EventStoreProjectionManagementClient(eventStoreClientSettings);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var filter = new SubscriptionFilterOptions(
                EventTypeFilter.ExcludeSystemEvents(),
                checkpointReached: (s, p, c) =>
                {
                    Console.WriteLine($"Checkpoint taken at {p.PreparePosition}");
                    return Task.CompletedTask;
                });
            Task.FromResult(new StreamPosition?());

            await Client.SubscribeToAllAsync(
                FromAll.Start,
                eventAppeared: EventAppeared,
                resolveLinkTos: true,
                subscriptionDropped: SubscriptionDropped
            );

            await Client.SubscribeToStreamAsync(
                streamName: "$ce-payments",
                start: FromStream.Start,
                eventAppeared: EventAppeared,
                resolveLinkTos: true,
                subscriptionDropped: SubscriptionDropped
            );
        }

        private void SubscriptionDropped(StreamSubscription subscription, SubscriptionDroppedReason reason, Exception? exception)
        {
            Console.WriteLine($"Subscription was dropped due to {reason}. {exception}");
            if (reason != SubscriptionDroppedReason.Disposed)
            {
                // Resubscribe if the client didn't stop the subscription
            }
        }

        private async Task EventAppeared(StreamSubscription subscription, ResolvedEvent resolvedEvent, CancellationToken token)
        {
            var deserializedEvent = DeserializeEvent(resolvedEvent.Event);
            if (deserializedEvent == null)
            {
                return;
            }

            var userId = resolvedEvent.Event.EventStreamId.Split('-')[1];

            var user = _userDataStore.Users.GetValueOrDefault(userId) ?? new User();

            // Project the event onto the User object
            switch (resolvedEvent.Event.EventType)
            {
                case "payment":
                    user.Apply(deserializedEvent);
                    _userDataStore.AddOrUpdateUser(userId, user);
                    break;
            }
        }

        private Payment DeserializeEvent(EventRecord record)
        {
            Console.WriteLine($"EventType: {record.EventType}");
            Console.WriteLine($"EventStreamId: {record.EventStreamId}");
            switch (record.EventType)
            {
                case "payment":
                    return JsonSerializer.Deserialize<Payment>(record.Data.Span);
                default:
                    return null;
            }
        }
    }
}
