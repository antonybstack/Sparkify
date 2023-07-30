using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Exceptions;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace Data;

public static class DbManager
{
    private const string Name = "Sparkify";
    private static readonly string[] s_connectionStrings = { "http://127.0.0.1:8888" };

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
                UseOptimisticConcurrency = true,
                Serialization = new NewtonsoftJsonSerializationConventions
                {
                    CustomizeJsonSerializer = serializer =>
                    {
                        // If a property is missing during serialization, default values are assigned
                        serializer.NullValueHandling = NullValueHandling.Ignore;
                        serializer.ObjectCreationHandling = ObjectCreationHandling.Replace;
                        // If a property is missing during deserialization, exceptions are silently ignored
                        serializer.Error += (_, args) => args.ErrorContext.Handled = true;
                    }
                }
            },

            // Define a default database (optional)
            Database = Name

            // Initialize the Document Store
        }.Initialize();

        /*
        - When enabled, the client will serve cached data immediately without checking
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

        try
        {
            var database = new DatabaseRecord(Name);
            store.Maintenance.Server.Send(new CreateDatabaseOperation(database));
        }
        catch (ConcurrencyException ex)
        {
            Console.WriteLine($"Attempted to create DB '{Name}'. Exception: {ex.Message}");
        }

        Generator.Generate(store).Wait();

        // Create indexes
        // new PaymentsByAccountName().Execute(store);
        // new AccountByAmount().Execute(store);
        // create all indexes from assembly
        IndexCreation.CreateIndexes(typeof(DbManager).Assembly, store);


        return store;
    }
}
