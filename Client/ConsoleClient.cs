using System.Text.Json;
using EventStore.Client;
using Spectre.Console;
using System.Security.Cryptography;
using Spectre.Console.Json;
using System.Text;
using System.Diagnostics;

namespace Client
{
    // See Spectre.Console docs for more info: https://spectreconsole.net/prompts/text
    // TODO: refactor to use command pattern: https://spectreconsole.net/cli/commands
    public class ConsoleService : BackgroundService
    {
        private readonly ILogger<ConsoleService> _logger;
        private readonly EventStoreClient Client;
        private readonly EventStoreProjectionManagementClient ManagementClient;
        private readonly Uuid eventId = Uuid.NewUuid();

        public ConsoleService(ILogger<ConsoleService> logger)
        {
            _logger = logger;
            var eventStoreClientSettings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
            Client = new EventStoreClient(eventStoreClientSettings);
            eventStoreClientSettings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
            eventStoreClientSettings.ConnectionName = "Projection management client";
            eventStoreClientSettings.DefaultCredentials = new UserCredentials("admin", "changeit");
            ManagementClient = new EventStoreProjectionManagementClient(eventStoreClientSettings);
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCommandLoop(stoppingToken);
            }
        }

        public async Task RunCommandLoop(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var command = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Choose one of the following [green]commands[/]")
                        .PageSize(10)
                        .MoreChoicesText("[grey](Use up and down arrow keys to cycle)[/]")
                        .AddChoices(
                            Enum.GetValues(typeof(Command))
                                           .Cast<Command>()
                                           .Select(c => c.ToString())
                                           .ToList()
                        ));
                Enum.TryParse<Command>(command, out var commandEnum);

                AnsiConsole.WriteLine();
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Aesthetic)
                    .Start("Processing...", async ctx => await CommandHandler(commandEnum));


                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule($"[yellow]complete.[/]").RuleStyle("grey").LeftJustified());
                AnsiConsole.WriteLine();
            }
        }

        private async Task CommandHandler(Command commandEnum)
        {
            await (commandEnum switch
            {
                Command.Command1 => ExecuteCommand1(),
                Command.Command2 => ExecuteCommand2(),
                Command.Command3 => ExecuteCommand3(),
                _ => throw new ArgumentOutOfRangeException(nameof(commandEnum))
            });
        }

        private async Task ExecuteCommand1()
        {
            var eventData = new EventData(
                eventId: Uuid.NewUuid(),
                type: "payment",
                data: JsonSerializer.SerializeToUtf8Bytes(
                    new Payment { Amount = RandomNumberGenerator.GetInt32(100, 10000) }),
                metadata: JsonSerializer.SerializeToUtf8Bytes(
                    new { correlationId = Uuid.NewUuid() })
            );

            await Client.AppendToStreamAsync(
                "payments-user1",
                StreamState.Any,
                new[] { eventData }
            );
        }

        private async Task ExecuteCommand2()
        {

            EventStoreClient.ReadStreamResult result = Client.ReadStreamAsync(
                Direction.Forwards,
                "payments-user1",
                StreamPosition.Start,
                resolveLinkTos: true);

            List<ResolvedEvent> events = await result.ToListAsync();
            // foreach event source db event, deserialize the data and add to a list
            var list = new List<object>();
            foreach (ResolvedEvent @event in events)
            {
                object data = JsonSerializer.Deserialize<object>(@event.Event.Data.Span);
                list.Add(data);
            }
            // convert the list to json string
            string json = JsonSerializer.Serialize(list,
                new JsonSerializerOptions { WriteIndented = true });

            var panel = new Panel(new JsonText(json)
                .BracesColor(Color.Red)
                .BracketColor(Color.Green)
                .ColonColor(Color.Blue)
                .CommaColor(Color.Red)
                .StringColor(Color.Green)
                .NumberColor(Color.Blue)
                .BooleanColor(Color.Red)
                .NullColor(Color.Green));
            panel.Header = new PanelHeader("[blue]payments-stream[/]", Justify.Center);
            panel.Border = BoxBorder.Rounded;
            panel.Padding = new Padding(1, 1, 1, 1);
            panel.Expand();
            AnsiConsole.Write(panel);
        }

        private async Task ExecuteCommand3()
        {
            int clientsCnt = 1;
            long requestsCnt = 5000;
            int streamsCnt = 1;
            int size = 100;
            int batchSize = 1;
            string streamNamePrefix = "TestLocal";

            try
            {
                await WriteFlood(clientsCnt, requestsCnt, streamsCnt, size, batchSize, streamNamePrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during WriteFlood");
            }
        }

        private async Task WriteFlood(int clientsCnt, long requestsCnt, int streamsCnt,
            int size, int batchSize, string streamNamePrefix)
        {
            byte[] data = Encoding.UTF8.GetBytes("{ \"DATA\" : \"" + new string('*', size) + "\"}");
            byte[] metadata = Encoding.UTF8.GetBytes("{ \"METADATA\" : \"" + new string('$', 100) + "\"}");

            var streams = Enumerable.Range(0, streamsCnt).Select(x =>
                string.IsNullOrWhiteSpace(streamNamePrefix)
                    ? Guid.NewGuid().ToString()
                    : $"{streamNamePrefix}-{x}").ToArray();

            _logger.LogInformation("Writing streams randomly between {first} and {last}",
                streams.FirstOrDefault(),
                streams.LastOrDefault());

            var start = new TaskCompletionSource();
            var utcNow = DateTime.UtcNow;
            var sw2 = new Stopwatch();
            var capacity = 2000 / clientsCnt;
            var clientTasks = new List<Task>();
            int success = 0;
            int fail = 0;
            for (int i = 0; i < clientsCnt; i++)
            {
                var count = requestsCnt / clientsCnt + ((i == clientsCnt - 1) ? requestsCnt % clientsCnt : 0);
                var eventStoreClientSettings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
                var client = new EventStoreClient(eventStoreClientSettings);
                clientTasks.Add(RunClient(client, count));
            }

            var sw = Stopwatch.StartNew();
            sw2.Start();
            start.SetResult();
            await Task.WhenAll(clientTasks);
            sw.Stop();
            var elapsed = sw2.Elapsed;
            var rate = 1000.0 * (requestsCnt) / elapsed.TotalMilliseconds;
            AnsiConsole.MarkupLine(
                "DONE TOTAL {0} WRITES IN {1} ({2:0.0}/s).",
                requestsCnt, elapsed.TotalMilliseconds, rate);
            AnsiConsole.MarkupLine("[green]Successes: {0}, failures: {1}[/]", success, fail);

            async Task RunClient(EventStoreClient client, long count)
            {
                var rnd = new Random();
                List<Task> pending = new List<Task>(capacity);
                await start.Task;
                var events = new EventData[batchSize];

                for (int j = 0; j < count; ++j)
                {
                    for (int q = 0; q < batchSize; q++)
                        events[q] = new EventData(Uuid.FromGuid(Guid.NewGuid()), "TakeSomeSpaceEventLocal", data, metadata);

                    var corrid = Guid.NewGuid();

                    pending.Add(client.AppendToStreamAsync(
                        streams[rnd.Next(streamsCnt)],
                        StreamState.Any,
                        events,
                        deadline: TimeSpan.FromSeconds(10),
                        configureOperationOptions: options =>
                        {
                            options.ThrowOnAppendFailure = true;
                        }
                        ).ContinueWith(t =>
                        {
                            if (t.IsCompletedSuccessfully) success++;
                            else fail++;
                        }));
                }
                if (pending.Count > 0) await Task.WhenAll(pending);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await Client.DisposeAsync();
            await ManagementClient.DisposeAsync();
        }

        public override void Dispose() => base.Dispose();

        enum Command
        {
            Command1 = 1,
            Command2 = 2,
            Command3 = 3,
        }
    }
}
