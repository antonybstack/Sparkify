// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Sparkify.Features.Payment;

public interface IEventChannel
{
    Channel<PaymentEvent> RegisterClient(string clientId);
    bool UnregisterClient(string clientId);
    Task BroadcastAsync(PaymentEvent paymentEvent);
    IAsyncEnumerable<PaymentEvent> ReadAllAsync(string clientId, CancellationToken ct);
}

internal sealed class EventChannel : IEventChannel
{
    private readonly ConcurrentDictionary<string, Channel<PaymentEvent>> ClientChannels = new();

    public Channel<PaymentEvent> RegisterClient(string clientId)
    {
        var channel = Channel.CreateUnbounded<PaymentEvent>();
        ClientChannels[clientId] = channel;
        return channel;
    }

    public bool UnregisterClient(string clientId)
    {
        // get the channel for the client, dispose, and remove it
        if (ClientChannels.TryRemove(clientId, out var channel))
        {
            channel.Writer.Complete();
            return true;
        }
        return false;
    }

    public async Task BroadcastAsync(PaymentEvent paymentEvent)
    {
        foreach (var channel in ClientChannels.Values)
        {
            try
            {
                await channel.Writer.WriteAsync(paymentEvent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error writing to channel: " + ex.Message);
            }
        }
    }

    public async IAsyncEnumerable<PaymentEvent> ReadAllAsync(string clientId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (ClientChannels.TryGetValue(clientId, out var channel))
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                while (channel.Reader.TryRead(out var paymentEvent))
                {
                    yield return paymentEvent;
                }
            }
        }
    }
}

public class ClientManager
{
    private readonly BroadcastBlock<PaymentEvent> BroadcastBlock;

    public ClientManager()
    {
        BroadcastBlock = new BroadcastBlock<PaymentEvent>(paymentEvent => paymentEvent);
    }

    public void Subscribe(ITargetBlock<PaymentEvent> target) =>
        BroadcastBlock.LinkTo(target);

    public void Broadcast(PaymentEvent paymentEvent) =>
        BroadcastBlock.Post(paymentEvent);
}