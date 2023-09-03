using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Refresh;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.Http;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace Sparkify;

public static class DbManager
{
    public static DocumentStore Store { get; private set; }

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
    public static void CreateStore(string databaseName, string httpUriString, string tcpHostName, int tcpPort)
    {
        // test interserver connections
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };
        var client = new TcpClient
        {
            SendTimeout = 3000
        };

        try
        {
            ArgumentNullException.ThrowIfNull(httpUriString);
            var httpEndpoint = new Uri(httpUriString);
            httpClient.GetAsync(httpEndpoint).Wait();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to connect to RavenDB http server. Exception: {ex.Message}");
            throw;
        }
        finally
        {
            httpClient.Dispose();
        }

        try
        {
            ArgumentNullException.ThrowIfNull(tcpHostName);
            ArgumentNullException.ThrowIfNull(tcpPort);
            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(tcpHostName), tcpPort);
            client.Connect(tcpEndPoint);
        }
        catch (SocketException ex)
        {
            Debug.WriteLine($"Failed to connect to RavenDB tcp server. Exception: {ex.Message}");
            throw;
        }
        finally
        {
            client.Dispose();
        }

        Store = new DocumentStore
        {
            // Define the cluster node URLs (required)
            Urls = new[]
            {
                httpUriString
            },

            // Set conventions as necessary (optional)
            Conventions =
            {
                MaxNumberOfRequestsPerSession = 10000,
                /* If any of the documents has changed while the code is running
                a concurrency exception will be raised and the whole transaction will be aborted. */
                UseOptimisticConcurrency = false,
                AggressiveCache =
                {
                    // Cache documents for 5 minutes
                    Duration = TimeSpan.FromDays(1),
                    // Maximum total size of cached items is 1 GB
                    Mode = AggressiveCacheMode.TrackChanges
                },
                HttpVersion = HttpVersion.Version30
            },
            // Define a default database (optional)
            Database = databaseName
        };

        Store.Initialize();

        Store.SetRequestTimeout(TimeSpan.FromSeconds(3));

        if (!DatabaseExists(Store))
        {
            try
            {
                var database = new DatabaseRecord(databaseName);
                Store.Maintenance.Server.Send(new CreateDatabaseOperation(database));

                Store.Maintenance.Send(new ConfigureRefreshOperation(new RefreshConfiguration
                {
                    Disabled = false
                }));
            }
            catch (ConcurrencyException ex)
            {
                Debug.WriteLine($"Attempted to create DB '{databaseName}'. Exception: {ex.Message}");
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
        Store.AggressivelyCache();

        // store.SeedUsers().Wait();

        Store.SetRequestTimeout(TimeSpan.FromSeconds(30));

        IndexCreation.CreateIndexes(typeof(DbManager).Assembly, Store);
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
        catch (Exception ex) when (ex is RavenException ||
                                   ex is TimeoutException ||
                                   ex.InnerException is HttpRequestException)
        {
            Debug.WriteLine(ex);
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            throw;
        }
    }
}
