// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bogus;
using Raven.Client.Documents;
using Raven.Client.Documents.BulkInsert;
using Raven.Client.Documents.Session;

namespace Data;

/// See Bogus docs for more info: https://github.com/bchavez/Bogus
public static class DataGenerator
{
    public static async Task SeedUsers(IDocumentStore s)
    {
        using IAsyncDocumentSession session = s.OpenAsyncSession();
        if (await session.Query<User>().AnyAsync())
        {
            return;
        }

        Randomizer.Seed = new Random(new DateTime(2023, 7, 23).ToUniversalTime().GetHashCode());

        List<User>? users = new Faker<User>()
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .Generate(1000);

        BulkInsertOperation? bulkInsert = s.BulkInsert();
        foreach (User user in users)
        {
            await bulkInsert.StoreAsync(user);
        }

        await bulkInsert.DisposeAsync();
    }
}
