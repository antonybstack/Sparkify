// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Raven.Client.Documents.Indexes;

// namespace Data;

// public class ActiveAccounts : AbstractIndexCreationTask<PaymentEvent>
// {
//     public ActiveAccounts() =>
//         Map = payments => from payment in payments
//             let user = LoadDocument<User>(payment.ReferenceId)
//             select new IndexEntry { AccountName = user.FirstName + " " + user.LastName };
//
//     private class IndexEntry
//     {
//         public string AccountName { get; set; }
//     }
// }
//
// public sealed class AccountsWithBalance : AbstractIndexCreationTask<PaymentEvent, AccountsWithBalance.IndexEntry>
// {
//     public AccountsWithBalance()
//     {
//         // Maps for indexing only
//         Map = paymentEvents => from evt in paymentEvents
//             where evt.EventType == EventType.PaymentRequested
//             let user = LoadDocument<User>(evt.ReferenceId)
//             select new IndexEntry
//             {
//                 Id = user.Id,
//                 FullName = user.FirstName + " " + user.LastName,
//                 Balance = evt.Amount
//             };
//         // Reduces to aggregate the results
//         Reduce = results => from result in results
//             group result by new
//             {
//                 result.Id,
//                 result.FullName
//             }
//             into g
//             select new IndexEntry { Id = g.Key.Id, FullName = g.Key.FullName, Balance = g.Sum(x => x.Balance) };
//     }
//
//     public sealed class IndexEntry : IEntity
//     {
//         public string Id { get; init; }
//         public string FullName { get; set; }
//         public int Balance { get; set; }
//     }
// }
//
// public sealed class UsersWithBalance : AbstractMultiMapIndexCreationTask<UsersWithBalance.IndexEntry>
// {
//     public UsersWithBalance()
//     {
//         // Map of Users
//         AddMap<User>(users =>
//             from user in users
//             select new IndexEntry
//             {
//                 Id = user.Id,
//                 FullName = user.FirstName + " " + user.LastName,
//                 Balance = 0
//             });
//
//         // Map of Payments
//         AddMap<PaymentEvent>(payments =>
//             from payment in payments
//             where payment.EventType == EventType.PaymentRequested
//             // let user = LoadDocument<User>(payment.ReferenceId)
//             select new IndexEntry
//             {
//                 Id = payment.ReferenceId,
//                 FullName = string.Empty,
//                 Balance = payment.Amount
//             });
//
//         // Reduce to aggregate the results
//         Reduce = results =>
//             from result in results
//             group result by result.Id
//             into g
//             select new IndexEntry
//             {
//                 Id = g.Key,
//                 FullName = g.FirstOrDefault(x => x.FullName != string.Empty).FullName,
//                 Balance = g.Sum(x => x.Balance)
//             };
//     }
//
//
//     public sealed class IndexEntry : IEntity
//     {
//         public string Id { get; init; }
//         public string FullName { get; set; }
//         public int Balance { get; set; }
//     }
// }

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
