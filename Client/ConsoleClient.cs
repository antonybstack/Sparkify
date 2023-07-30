using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Data;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using Spectre.Console;

namespace Client;

// See Spectre.Console docs for more info: https://spectreconsole.net/prompts/text
// TODO: refactor to use command pattern: https://spectreconsole.net/cli/commands
public class ConsoleService : BackgroundService
{
    private readonly ILogger<ConsoleService> _logger;
    private readonly IDocumentStore _s;

    public ConsoleService(ILogger<ConsoleService> logger, IDocumentStore s)
    {
        _logger = logger;
        _s = s;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) =>
        await Task.Run(async () => await RunCommandLoop(stoppingToken), stoppingToken);

    public async Task RunCommandLoop(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var methods = GetType()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name.StartsWith("ExecuteCommand") && m.ReturnType == typeof(Task))
                .OrderBy(m => m.Name)
                .ToList();

            // Display the choices to the user
            AnsiConsole.MarkupLine("[green]Choose one of the following commands:[/]");
            for (var i = 0; i < methods.Count; i++)
            {
                AnsiConsole.MarkupLine($"[green]{i + 1}.[/] [yellow]{methods[i].Name}[/]");
            }

            // Read user choice
            var commandIndex = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                var isNumeric = int.TryParse(keyInfo.KeyChar.ToString(), out commandIndex);

                if (isNumeric && commandIndex > 0 && commandIndex <= methods.Count)
                {
                    break;
                }

                AnsiConsole.WriteLine("Invalid input. Please enter a number between 1 and " + methods.Count);
            }

            // Command commandEnum = (Command)Enum.Parse(typeof(Command), commands[commandIndex - 1]);
            // Execute the selected method
            MethodInfo selectedMethod = methods[commandIndex - 1];
            var task = (Task)selectedMethod.Invoke(this, null)!;
            await task;

            AnsiConsole.WriteLine();
            await AnsiConsole.Status().Spinner(Spinner.Known.Aesthetic).Start("Processing...", _ => task);
            await task;

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]complete.[/]");
            AnsiConsole.WriteLine();
        }
    }

    private async Task ExecuteCommand1()
    {
        // var eventData = new EventData(
        //     eventId: Uuid.NewUuid(),
        //     type: "payment",
        //     data: JsonSerializer.SerializeToUtf8Bytes(
        //         new Payment { Amount = RandomNumberGenerator.GetInt32(100, 10000) }),
        //     metadata: JsonSerializer.SerializeToUtf8Bytes(
        //         new { correlationId = Uuid.NewUuid() })
        // );

        // await Client.AppendToStreamAsync(
        //     "payments-user1",
        //     StreamState.Any,
        //     new[] { eventData }
        // );
        var sw = new Stopwatch();
        /* The Session, which is obtained from the Document Store,
           is a Unit of Work that represents a single business transaction on a particular database. */
        using (IAsyncDocumentSession session = _s.OpenAsyncSession())
        {
            // Create a new entity
            var obj = new PaymentEvent { Amount = RandomNumberGenerator.GetInt32(100, 10000) };

            // Store the entity in the Session's internal map
            await session.StoreAsync(obj);
            // From now on, any changes that will be made to the entity will be tracked by the Session.
            // However, the changes will be persisted to the server only when 'SaveChanges()' is called.
            var id = session.Advanced.GetDocumentId(obj); // returns 'payments/1-A'
            await session.SaveChangesAsync();
            // At this point the entity is persisted to the database as a new document.
            // Since no database was specified when opening the Session, the Default Database is used.
        }

        sw.Stop();
        Console.WriteLine("Time taken: " + sw.ElapsedMilliseconds + "ms");
    }

    private async Task ExecuteCommandCreateAccount()
    {
        var sw = new Stopwatch();
        /* The Session, which is obtained from the Document Store,
           is a Unit of Work that represents a single business transaction on a particular database. */
        using IAsyncDocumentSession session = _s.OpenAsyncSession();
        var newAccount = new Account { Name = Ulid.NewUlid().ToString() };
        await session.StoreAsync(newAccount);
        // var accountId = session.Advanced.GetDocumentId(newAccount);

        // Payment newPayment = new Payment
        // {
        //     AccountId = accountId,
        //     Amount = RandomNumberGenerator.GetInt32(100, 10000),
        // };

        // await session.StoreAsync(newPayment);
        // var paymentId = session.Advanced.GetDocumentId(newPayment);

        // var account = session.Advanced.Lazily.LoadAsync<Account>(accountId);
        // var payment = session.Advanced.Lazily.LoadAsync<Payment>(paymentId);

        // // in-memory now, do not have to load lazily
        // Account account = await session
        //     .Include<Account>(x => x.Payments)
        //     .LoadAsync<Account>(accountId);

        // using var session1 = Store.OpenSession();
        // var order = session1.Load<Account>(
        //     "accounts/129-A",
        //     i => i.IncludeDocuments(x => x.Payments));

        // foreach (string id in account.Payments)
        // {
        //     Payment payment = await session.LoadAsync<Payment>(id);
        //     payment.Amount++;
        // }
        // Console.WriteLine(JsonConvert.SerializeObject(account.Value));
        // Console.WriteLine(JsonConvert.SerializeObject(payment.Value));
        await session.SaveChangesAsync();
    }


    private async Task ExecuteCommandLoad()
    {
        using IAsyncDocumentSession session = _s.OpenAsyncSession();
        SessionInfo test = session.Advanced.SessionInfo;

        PaymentEvent obj = await session.LoadAsync<PaymentEvent>("accounts/129-A");
        var cv = session.Advanced.GetChangeVectorFor(obj);
        // obj.Amount = RandomNumberGenerator.GetInt32(100, 10000);

        await session.SaveChangesAsync();
        // obj = await session.LoadAsync<Payment>("accounts/129-A");
        // var obj1 = session.Advanced.Lazily.LoadAsync<Payment>("accounts/129-A");
        // Console.WriteLine((await obj1.Value).Amount);
    }

    // private async Task ExecuteCommandLoad1()
    // {
    //     using var session = Store.OpenAsyncSession();
    //     var accounts = await session
    //         .Query<Account>()
    //         .Include(x => x.)
    //         .Where(x => !x.Payments.Contains("payments/257-A"))
    //         .Select(x => new Account
    //         {
    //             // validate Payments is not null and correct type
    //             Payments = x.Payments.Where<string>(y => y != null).ToList()
    //         })
    //         .ToListAsync();
    //     foreach (var account in accounts)
    //     {
    //         // console write line the json of account object
    //         Console.WriteLine(JsonConvert.SerializeObject(account));
    //     }
    // }


    // private async Task ExecuteCommandDelete()
    // {
    //     using (IDocumentSession session = Store.OpenSession())
    //     {

    //         Payment obj = session.Query<Payment>().FirstOrDefault();
    //         if (obj == null)
    //         {
    //             Console.WriteLine("Document not found");
    //             return;
    //         }
    //         var cv = session.Advanced.GetChangeVectorFor(obj);
    //         session.CountersFor(obj).Increment("Likes", 1);

    //         //session.Delete(obj); // throws ConcurrencyException if the document was changed

    //         // session.Delete("Payments/1-A", cv); // throws ConcurrencyException if the document was changed

    //         // session.Delete("Payments/1-A"); // no concurrency check

    //         session.SaveChanges();
    //     }
    // }

    private async Task ExecuteCommand2()
    {
        var seatLock = new Tuple<int, int, string>(2,
            2,
            "Antony");
        using IAsyncDocumentSession session = _s.OpenAsyncSession();
        session.Advanced.UseOptimisticConcurrency = false;
        await session.StoreAsync(seatLock, "seats/locks/" + 4 + "/" + 4);
        session.Advanced.GetMetadataFor(seatLock)["@expires"] = DateTime.UtcNow.AddMinutes(15);
        try
        {
            await session.SaveChangesAsync();
            //     return Ok(new
            //     {
            //         SeatCV = session.Advanced.GetChangeVectorFor(seatLock);
            // });
        }


        catch (ConcurrencyException)
        {
            // seat already taken
        }

        session.Advanced.UseOptimisticConcurrency = true;
    }

    // private async Task ExecuteCommand2()
    // {

    //     EventStoreClient.ReadStreamResult result = Client.ReadStreamAsync(
    //         Direction.Forwards,
    //         "payments-user1",
    //         StreamPosition.Start,
    //         resolveLinkTos: true);

    //     List<ResolvedEvent> events = await result.ToListAsync();
    //     // foreach event source db event, deserialize the data and add to a list
    //     var list = new List<object>();
    //     foreach (ResolvedEvent @event in events)
    //     {
    //         object data = JsonSerializer.Deserialize<object>(@event.Event.Data.Span);
    //         list.Add(data);
    //     }
    //     // convert the list to json string
    //     string json = JsonSerializer.Serialize(list,
    //         new JsonSerializerOptions { WriteIndented = true });

    //     var panel = new Panel(new JsonText(json)
    //         .BracesColor(Color.Red)
    //         .BracketColor(Color.Green)
    //         .ColonColor(Color.Blue)
    //         .CommaColor(Color.Red)
    //         .StringColor(Color.Green)
    //         .NumberColor(Color.Blue)
    //         .BooleanColor(Color.Red)
    //         .NullColor(Color.Green));
    //     panel.Header = new PanelHeader("[blue]payments-stream[/]", Justify.Center);
    //     panel.Border = BoxBorder.Rounded;
    //     panel.Padding = new Padding(1, 1, 1, 1);
    //     panel.Expand();
    //     AnsiConsole.Write(panel);
    // }

    private async Task ExecuteCommand3()
    {
        const int clientsCnt = 1;
        const long requestsCnt = 20000;
        const int streamsCnt = 1;
        const int size = 10;
        const int batchSize = 1;
        const string streamNamePrefix = "TestLocal";
        using IAsyncDocumentSession session = _s.OpenAsyncSession();
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
        var data = Encoding.UTF8.GetBytes("{ \"DATA\" : \"" + new string('*', size) + "\"}");
        var metadata = Encoding.UTF8.GetBytes("{ \"METADATA\" : \"" + new string('$', size) + "\"}");

        var streams = Enumerable.Range(0, streamsCnt).Select(x =>
            string.IsNullOrWhiteSpace(streamNamePrefix)
                ? Guid.NewGuid().ToString()
                : $"{streamNamePrefix}-{x}").ToArray();

        _logger.LogInformation("Writing streams randomly between {first} and {last}",
            streams.FirstOrDefault(),
            streams.LastOrDefault());

        var start = new TaskCompletionSource();
        DateTime utcNow = DateTime.UtcNow;
        var capacity = 2000 / clientsCnt;
        var clientTasks = new List<Task>();
        var success = 0;
        var fail = 0;

        var sw = Stopwatch.StartNew();
        var payload = new { data = new string('*', size) };
        await RunClient(requestsCnt);
        sw.Stop();
        TimeSpan elapsed = sw.Elapsed;
        var rate = 1000.0 * requestsCnt / elapsed.TotalMilliseconds;
        AnsiConsole.MarkupLine(
            "DONE TOTAL {0} WRITES IN {1} ({2:0.0}/s).",
            requestsCnt, elapsed.TotalMilliseconds, rate);
        AnsiConsole.MarkupLine("[green]Successes: {0}, failures: {1}[/]", success, fail);

        async Task RunClient(long count)
        {
            var rnd = new Random();
            var pending = new List<Task>((int)count + 1);

            for (var j = 0; j < count; ++j)
            {
                pending.Add(StoreEvent());
            }

            await Task.WhenAll(pending);

            async Task StoreEvent()
            {
                try
                {
                    using IAsyncDocumentSession session = _s.OpenAsyncSession();
                    //var @event = new EventData(Uuid.FromGuid(Guid.NewGuid()), "TakeSomeSpaceEventLocal", data, metadata);
                    await session.StoreAsync(payload, $"test/{Guid.NewGuid()}");
                    await session.SaveChangesAsync();
                    Interlocked.Increment(ref success);
                }
                catch (RavenException)
                {
                    Interlocked.Increment(ref fail);
                }
            }
        }

        async Task RunClients(long count, int clients)
        {
            var clientTasks = new List<Task>();
            for (var i = 0; i < clients; ++i)
            {
                clientTasks.Add(RunClient(count));
            }

            await Task.WhenAll(clientTasks);
        }
    }

    private async Task ExecuteCommand4() =>
        await CreateAndStartConnectionAsync("http://localhost:6002/hub",
            HttpTransportType.ServerSentEvents);

    public async Task CreateAndStartConnectionAsync(string url, HttpTransportType transportType)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(url, options => options.Transports = transportType)
            .Build();

        connection.Closed += ex =>
        {
            if (ex == null)
            {
                Trace.WriteLine("Connection terminated");
                // connectionState = ConnectionState.Disconnected;
            }
            else
            {
                _logger.LogError("Connection terminated with error", ex);
                // connectionState = ConnectionState.Faulted;
            }

            return Task.CompletedTask;
        };

        await ConnectAsync();

        async Task ConnectAsync()
        {
            for (var connectCount = 0; connectCount <= 3; connectCount++)
            {
                try
                {
                    await connection.StartAsync();
                    // connectionState = ConnectionState.Connected;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Connection.Start Failed", ex);

                    if (connectCount == 3)
                    {
                        // connectionState = ConnectionState.Faulted;
                        throw;
                    }
                }

                await Task.Delay(1000);
            }
        }
    }

    private async Task ExecuteCommandLoadIndex()
    {
        //new Payments_ByAccountName().Execute(Store);
        using IAsyncDocumentSession session = _s.OpenAsyncSession();
        List<PaymentEvent>? paymentsWithAccountName = await session
            .Query<PaymentEvent>()
            // .Where(x => x.AccountName == "01H5G69FAGMGEM6F3964GSBY34")
            // .OfType<Payment>()
            .ToListAsync();

        Console.WriteLine(JsonConvert.SerializeObject(paymentsWithAccountName));


        PaymentEvent? paymentsWithAccountName1 = await session
            .LoadAsync<PaymentEvent>("payments/33-A");
        // .Where(x => x.AccountName == "01H5G69FAGMGEM6F3964GSBY34")
        // .OfType<Payment>()
        Console.WriteLine(JsonConvert.SerializeObject(paymentsWithAccountName1));
    }

    private async Task ExecuteCommandCreateEvent()
    {
        var sw = Stopwatch.StartNew();

        // var firstAccount = await session.Query<Account>()
        //     .FirstOrDefaultAsync();
        // var firstAccountId = session.Advanced.GetDocumentId(firstAccount);
        // // get random account from ravendb
        // var randomAccount = session.Query<Account>().CountLazilyAsync();
        // Console.WriteLine(JsonConvert.SerializeObject(await randomAccount.Value));

        // var query = session.Query<Account>().Select(x => new {
        //     LastModified = MetadataFor(x).Value<DateTime>("Last-Modified"),
        // });
        // var command = new GetDocumentsCommand(0, 10);
        // session.Advanced.RequestExecutor.Execute(command, session.Advanced.Context);
        // var order = (BlittableJsonReaderObject)command.Result.Results[0];

        // var command1 = new GetDocumentsCommand("Accounts/", null, true);
        // session.Advanced.RequestExecutor.Execute(command1, session.Advanced.Context);
        // var testsss = (BlittableJsonReaderObject)command.Result.Results[0];
        using (IAsyncDocumentSession session = _s.OpenAsyncSession())
        {
            IQueryable<string> query = session.Query<Account>().Select(x => x.Id);
            List<string>? test = await query.ToListAsync();
            Console.WriteLine(JsonConvert.SerializeObject(test));
            // get list of account ids from ravendb
            // var test = from a in query
            //            group a by a into g
            //            select g.Key;

            // var test12 = await query.ToListAsync();


            // var enumerator = session.Advanced.StreamAsync(query);

            // await using (var streamResults = await session.Advanced.StreamAsync(query))
            // {
            //     while (await streamResults.MoveNextAsync())
            //     {
            //         Console.WriteLine(streamResults.Current.Id);
            //         var @event = new PaymentEvent
            //         {
            //             EventType = EventType.PaymentRequested,
            //             ReferenceId = streamResults.Current.Id,
            //             Amount = RandomNumberGenerator.GetInt32(100, 10000)
            //         };
            //         await session.StoreAsync(@event, "events/");
            //     }

            // }


            // var test = await query.ToListAsync();
            // get list of account ids from ravendb
            // await using (IAsyncEnumerator<StreamResult<PaymentEvent>> streamResults =
            //              await session.Advanced.StreamAsync(query, out StreamQueryStatistics streamQueryStats))
            // {
            //     // Read from the stream
            //     while (await streamResults.MoveNextAsync())
            //     {
            //         // Process the received result
            //         StreamResult<PaymentEvent> currentResult = streamResults.Current;

            //         // Get the document from the result
            //         // This entity will Not be tracked by the session
            //         var employee = currentResult.Document;

            //         // The currentResult item also provides the following:
            //         var employeeId = currentResult.Id;
            //         var documentMetadata = currentResult.Metadata;
            //         var documentChangeVector = currentResult.ChangeVector;

            //         // Can get info from the stats, i.e. get number of total results
            //         int totalResults = streamQueryStats.TotalResults;
            //         // Get the Auto-Index that was used/created with this dynamic query
            //         string indexUsed = streamQueryStats.IndexName;
            //         // Console.WriteLine(JsonConvert.SerializeObject(employee));
            //     }
            // }
            // bulk insert 1000 payment events
            // for (int i = 0; i < 1000; i++)
            // {
            //     var @eventTemp = new PaymentEvent
            //     {
            //         EventType = EventType.PaymentRequested,
            //         ReferenceId = firstAccountId,
            //         Amount = RandomNumberGenerator.GetInt32(100, 10000)
            //     };
            //     await session.StoreAsync(@eventTemp, "events/");
            // }

            foreach (var id in test)
            {
                var @event = new PaymentEvent
                {
                    EventType = EventType.PaymentRequested,
                    ReferenceId = id,
                    Amount = RandomNumberGenerator.GetInt32(100, 10000)
                };
                await session.StoreAsync(@event, "events/");
            }


            // await session.StoreAsync(event1, "events/");
            // await session.StoreAsync(event1, "events/test");
            // Store.Maintenance.Send(new NextIdentityForOperation("eventss"));
            // await session.StoreAsync(@event, "events/");
            // add expiration to event
            // session.Advanced.GetMetadataFor(@event)["@expires"] = DateTime.UtcNow.AddSeconds(30);
            await session.SaveChangesAsync();
        }

        sw.Stop();
        Console.WriteLine("Time taken: " + sw.ElapsedMilliseconds + "ms");
        Console.WriteLine("Time taken: " + sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L)) + "μs");
    }

    private async Task ExecuteCommandLoadEvent()
    {
        //new Payments_ByAccountName().Execute(Store);
        using IAsyncDocumentSession session = _s.OpenAsyncSession();
        List<AccountsWithBalance.IndexEntry>? results = await session
            .Query<AccountsWithBalance.IndexEntry, AccountsWithBalance>()
            // .Where(x => x.ReferenceId == "orders/1")
            .ToListAsync();

        Console.WriteLine(JsonConvert.SerializeObject(results));
    }
}
