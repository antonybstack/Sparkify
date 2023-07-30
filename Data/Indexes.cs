// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Raven.Client.Documents.Indexes;

namespace Data;

public class ActiveAccounts : AbstractIndexCreationTask<PaymentEvent>
{
    public ActiveAccounts() =>
        Map = payments => from payment in payments
            let user = LoadDocument<User>(payment.ReferenceId)
            select new IndexEntry { AccountName = user.FirstName + " " + user.LastName };

    private class IndexEntry
    {
        public string AccountName { get; set; }
    }
}

public class AccountsWithBalance : AbstractIndexCreationTask<PaymentEvent, AccountsWithBalance.IndexEntry>
{
    public AccountsWithBalance()
    {
        // Maps for indexing only
        Map = paymentEvents => from evt in paymentEvents
            where evt.EventType == EventType.PaymentRequested
            let user = LoadDocument<User>(evt.ReferenceId)
            select new IndexEntry { FullName = user.FirstName + " " + user.LastName, Balance = evt.Amount };
        // Reduces to aggregate the results
        Reduce = results => from result in results
            group result by result.FullName
            into g
            select new IndexEntry { FullName = g.Key, Balance = g.Sum(x => x.Balance) };
    }

    public class IndexEntry
    {
        public string FullName { get; set; }
        public int Balance { get; set; }
    }
}

// public class Account_ByAmount : AbstractIndexCreationTask<Event, Account_ByAmount.IndexEntry>
// {
//     public class IndexEntry
//     {
//         public string AccountId { get; set; }
//         public int Amount { get; set; }
//     }

//     // public Account_ByAmount()
//     // {
//     //     Map = events => from evt in events
//     //                     let change = evt.Data as Payment
//     //                     where evt.EventType == EventType.PaymentRequested && change != null
//     //                     select new IndexEntry
//     //                     {
//     //                         AccountId = change.AccountId,
//     //                         Amount = change.Amount
//     //                     };
//     // }
//     public Account_ByAmount()
//     {
//         Map = events => from evt in events
//                         where evt.EventType == EventType.PaymentRequested
//                         let change = evt.Data as Payment
//                         where change != null
//                         select new IndexEntry
//                         {
//                             AccountId = change.AccountId,
//                             Amount = change.Amount
//                         };
//     }

// }
