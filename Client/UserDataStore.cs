using System.Collections.Concurrent;
using Data;

namespace Client;

public class AccountDataStore
{
    private readonly ConcurrentDictionary<string, Account> _accounts = new();

    public void AddOrUpdateAccount(string userId, Account account) =>
        _accounts.AddOrUpdate(userId, account, (_, __) => account);

    public IReadOnlyDictionary<string, Account> Accounts => _accounts;
}
