// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bogus;
using Raven.Client.Documents;

namespace Sparkify;

/// See Bogus docs for more info: https://github.com/bchavez/Bogus
public static class DataGenerator
{
    public static async Task SeedUsers(this IDocumentStore store)
    {
        using var session = store.OpenAsyncSession();
        if (await session.Query<Blog>().AnyAsync())
        {
            return;
        }

        Randomizer.Seed = new Random(new DateTime(2023, 7, 23).ToUniversalTime().GetHashCode());

        var users = new Faker<Blog>()
            .RuleFor(u => u.Title, f => f.Lorem.Sentence(10))
            .RuleFor(u => u.Description, f => f.Lorem.Sentence(20))
            .Generate(1000);

        var bulkInsert = store.BulkInsert();
        foreach (var user in users)
        {
            await bulkInsert.StoreAsync(user);
        }

        await bulkInsert.DisposeAsync();
    }
}
