using System.Diagnostics;
using System.Net.Sockets;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace Data;

public static class DbManager
{
    private const string Name = "Sparkify";
    private static readonly string[] s_connectionStrings = { "http://192.168.1.200:8888" };

    /// The use of “Lazy” ensures that the document store is only created once
    /// without you having to worry about double locking or explicit thread safety issues
    private static readonly Lazy<IDocumentStore> s_store = new(CreateStore);

    public static IDocumentStore Store => s_store.Value;

    /// <summary>
    ///     The DocumentStoreHolder class holds a single instance of the Document Store object that will be used across
    ///     your client application. In addition to configuring your database, your cluster topology and a client certificate,
    ///     the Document Store exposes methods to perform operations such as:
    ///     - Open a Session for database transactions
    ///     - Create & deploy indexes
    ///     - Bulk documents actions
    ///     - Manage the server with low-level operations commands
    ///     - Changes API - receive messages from the server
    ///     - Perform custom actions in response to Session's events
    ///     - Subscription tasks management
    ///     - Export & Import database data.
    /// </summary>
    /// <returns>An <see cref="IDocumentStore" /> to further customize the added endpoints.</returns>
    private static IDocumentStore CreateStore()
    {
        // test interserver connections
        var httpClient = new HttpClient();
        var client = new TcpClient { SendTimeout = 3000 };

        try
        {

            httpClient.GetAsync("http://192.168.1.200:8888/studio/index.html").Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to RavenDB http server. Exception: {ex.Message}");
            throw;
        }
        finally
        {
            httpClient.Dispose();
        }

        try
        {
            client.Connect("192.168.1.200", 38888);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Failed to connect to RavenDB tcp server. Exception: {ex.Message}");
            throw;
        }
        finally
        {
            client.Dispose();
        }

        IDocumentStore store = new DocumentStore
        {
            // Define the cluster node URLs (required)
            Urls = s_connectionStrings,

            // Set conventions as necessary (optional)
            Conventions =
            {
                MaxNumberOfRequestsPerSession = 10000,
                /* If any of the documents has changed while the code is running
                a concurrency exception will be raised and the whole transaction will be aborted. */
                UseOptimisticConcurrency = false,
            },

            // Define a default database (optional)
            Database = Name

            // Initialize the Document Store
        }.Initialize();
        store.SetRequestTimeout(TimeSpan.FromSeconds(1));

        if (!DatabaseExists(store))
        {
            try
            {
                var database = new DatabaseRecord(Name);
                store.Maintenance.Server.Send(new CreateDatabaseOperation(database));
            }
            catch (ConcurrencyException ex)
            {
                Console.WriteLine($"Attempted to create DB '{Name}'. Exception: {ex.Message}");
            }
        }


        // var a1 = store.Maintenance.Send(new GetDetailedStatisticsOperation());
        // var a2 = store.Maintenance.Send(new GetStatisticsOperation());
        // var a3 =  store.Maintenance.Send(new GetDetailedCollectionStatisticsOperation());
        // var a4 = store.Maintenance.Send(new GetCollectionStatisticsOperation());
        // var a5 = store.Maintenance.Send(new GetIndexStatisticsOperation());
        // var a6 = store.Maintenance.Send(new GetIndexNamesOperation(0, 10));
        // store.Maintenance.Server.Send(new GetDatabaseNamesOperation(0, 10));
        // var subs = store.Subscriptions.GetSubscriptions(0,int.MaxValue);

        /* When enabled, the client will serve cached data immediately without checking
        the server to see if the data has changed. The server will notify the client
        when the data has changed and the client will update its cache accordingly.
        Most of the time, the data will not be stale, as the server will quickly notify
        the client when the data has changed. However, there are the edge cases such as
        latency or if the client is offline when the data changes, in which case the data
        cannot be guaranteed to be fresh.
        - When disabled, normal cache validation will occur, which means that the client
        will always check with the server to see if the data has changed, and the server
        will respond with a 304 if the data has not changed. This is the default behavior. */
        store.AggressivelyCache();

        DataGenerator.SeedUsers(store).Wait();

        IndexCreation.CreateIndexes(typeof(DbManager).Assembly, store);

        return store;
    }

    private static bool DatabaseExists(IDocumentStore documentStore)
    {
        try
        {
            documentStore.Maintenance.ForDatabase(documentStore.Database).Send(new GetStatisticsOperation());
            return true;
        }
        catch (DatabaseDoesNotExistException)
        {
            return false;
        }
        catch (Exception ex) when (ex is RavenException || ex is TimeoutException || ex.InnerException is HttpRequestException)
        {
            Console.WriteLine(ex);
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
}
