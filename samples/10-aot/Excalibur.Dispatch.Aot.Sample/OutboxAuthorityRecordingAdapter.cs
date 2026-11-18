// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>A recording endpoint for this local control, not an external broker implementation.</summary>
internal sealed class OutboxAuthorityRecordingAdapter : IMessageBusAdapter
{
    private readonly ConcurrentQueue<string> _accepted = new();

    internal Func<IMessageContext, CancellationToken, Task>? OnPublish { get; set; }
    internal string[] Accepted => _accepted.ToArray();

    public string Name => "native-authority-recording";
    public bool IsConnected => true;

    public async Task<IMessageResult> PublishAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
    {
        OutboxAuthoritySmoke.Require(message is OutboxAuthorityMessage, "the concrete generated payload must be restored");
        var payload = (OutboxAuthorityMessage)message;
        OutboxAuthoritySmoke.Require(payload.Sequence > 0 && !string.IsNullOrWhiteSpace(payload.Label), "the generated payload must round-trip");
        if (OnPublish is { } callback) { await callback(context, cancellationToken).ConfigureAwait(false); }
        _accepted.Enqueue(payload.Label);
        return MessageResult.Success();
    }

    public Task SubscribeAsync(string subscriptionName,
        Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> messageHandler,
        MessageBusOptions? options, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This recording-only sample does not subscribe to a broker.");

    public Task UnsubscribeAsync(string subscriptionName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This recording-only sample has no subscriptions.");
}
