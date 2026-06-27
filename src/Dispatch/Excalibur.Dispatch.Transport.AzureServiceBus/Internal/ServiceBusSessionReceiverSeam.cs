// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// Session-aware <see cref="IServiceBusReceiverSeam"/> implementation (ne79ro, FR-A2) that delivers
/// messages in <b>per-session FIFO order</b>. It accepts one session at a time via
/// <see cref="ServiceBusClient.AcceptNextSessionAsync(string, ServiceBusSessionReceiverOptions, CancellationToken)"/>
/// — a <see cref="ServiceBusSessionReceiver"/> locks a single session and the broker guarantees ordered
/// delivery within that session. When the current session drains (an empty receive), it is released so
/// the next call locks another session, preserving intra-session ordering while still making
/// cross-session progress.
/// </summary>
/// <remarks>
/// This is the session counterpart to <see cref="ServiceBusReceiverAdapter"/>: it is the only place in
/// the session receive path that touches the live SDK session type, so tests substitute at the
/// <see cref="IServiceBusReceiverSeam"/> seam (ADR-142 §D7). It is wired by
/// <c>AddAzureServiceBusTransport</c> when <c>Processor.RequiresSession</c> is enabled; otherwise the
/// non-session <see cref="ServiceBusReceiverAdapter"/> is used (no behavior change for non-session
/// consumers).
/// <para>
/// <b>Load-bearing invariant (settle-before-release):</b> a received batch MUST be settled
/// (<see cref="CompleteMessageAsync"/>/<see cref="AbandonMessageAsync"/>/<see cref="DeadLetterMessageAsync"/>)
/// while its owning session lock is still held — i.e. BEFORE the next <see cref="ReceiveMessagesAsync"/>
/// returns empty and releases the session. The framework's pull loop satisfies this by settling each
/// batch before requesting the next (same contract as <c>TransportSubscriber</c>). A future
/// concurrent-prefetch or pipelined consumer that issues the next receive before settling the prior
/// batch MUST preserve this ordering (e.g. per-message session affinity); otherwise a settle can race a
/// released session and fail. Do not relax the release-on-drain logic without re-establishing this
/// guarantee.
/// </para>
/// </remarks>
internal sealed partial class ServiceBusSessionReceiverSeam : IServiceBusReceiverSeam
{
	private readonly ServiceBusClient _client;
	private readonly string _entityName;
	private readonly ServiceBusSessionReceiverOptions _options;
	private readonly ILogger _logger;

	// Guards _session so concurrent ReceiveMessagesAsync / settle calls never race on the session swap.
	private readonly SemaphoreSlim _sessionGate = new(1, 1);
	private ServiceBusSessionReceiver? _session;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ServiceBusSessionReceiverSeam"/> class.
	/// </summary>
	/// <param name="client">The Service Bus client used to accept sessions.</param>
	/// <param name="entityName">The queue or subscription entity name.</param>
	/// <param name="options">The session receiver options (prefetch, receive mode).</param>
	/// <param name="logger">The logger instance.</param>
	public ServiceBusSessionReceiverSeam(
		ServiceBusClient client,
		string entityName,
		ServiceBusSessionReceiverOptions options,
		ILogger logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_entityName = entityName ?? throw new ArgumentNullException(nameof(entityName));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var session = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);
			if (session is null)
			{
				// No session currently available — caller receives an empty batch and retries.
				return [];
			}

			var messages = await session.ReceiveMessagesAsync(maxMessages, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (messages.Count == 0)
			{
				// Current session drained — release it so the next call locks a different session.
				// (Settling for the prior batch happens before the next ReceiveAsync in the pull loop,
				// so the lock is still held when those messages are acknowledged/rejected.)
				await ReleaseSessionAsync().ConfigureAwait(false);
			}

			return messages;
		}
		finally
		{
			_ = _sessionGate.Release();
		}
	}

	/// <inheritdoc/>
	public Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
		=> SettleAsync(message, static (s, m, ct) => s.CompleteMessageAsync(m, ct), cancellationToken);

	/// <inheritdoc/>
	public Task AbandonMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
		=> SettleAsync(message, static (s, m, ct) => s.AbandonMessageAsync(m, cancellationToken: ct), cancellationToken);

	/// <inheritdoc/>
	public Task DeadLetterMessageAsync(
		ServiceBusReceivedMessage message,
		string? deadLetterReason,
		CancellationToken cancellationToken)
		=> SettleAsync(message, (s, m, ct) => s.DeadLetterMessageAsync(m, deadLetterReason, cancellationToken: ct), cancellationToken);

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _sessionGate.WaitAsync().ConfigureAwait(false);
		try
		{
			await ReleaseSessionAsync().ConfigureAwait(false);
		}
		finally
		{
			_ = _sessionGate.Release();
			_sessionGate.Dispose();
		}
	}

	private async Task SettleAsync(
		ServiceBusReceivedMessage message,
		Func<ServiceBusSessionReceiver, ServiceBusReceivedMessage, CancellationToken, Task> settle,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Settle against the currently-locked session receiver. In the pull loop a batch is settled
		// before the next ReceiveAsync, so the owning session is still held.
		var session = _session
			?? throw new InvalidOperationException(
				"No active Azure Service Bus session to settle the message against. The session lock may " +
				"have expired or the message was already settled.");

		await settle(session, message, cancellationToken).ConfigureAwait(false);
	}

	private async Task<ServiceBusSessionReceiver?> EnsureSessionAsync(CancellationToken cancellationToken)
	{
		if (_session is not null)
		{
			return _session;
		}

		try
		{
			_session = await _client.AcceptNextSessionAsync(_entityName, _options, cancellationToken)
				.ConfigureAwait(false);
			LogSessionAccepted(_session.SessionId);
			return _session;
		}
		catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
		{
			// No session was available within the SDK's try-timeout — not an error; the caller receives
			// an empty batch and polls again.
			return null;
		}
	}

	private async Task ReleaseSessionAsync()
	{
		if (_session is null)
		{
			return;
		}

		var sessionId = _session.SessionId;
		await _session.DisposeAsync().ConfigureAwait(false);
		_session = null;
		LogSessionReleased(sessionId);
	}

	[LoggerMessage(AzureServiceBusEventId.SessionAccepted, LogLevel.Debug,
		"Service Bus session receiver: session {SessionId} accepted for ordered delivery")]
	private partial void LogSessionAccepted(string sessionId);

	[LoggerMessage(AzureServiceBusEventId.SessionReleased, LogLevel.Debug,
		"Service Bus session receiver: session {SessionId} released (drained)")]
	private partial void LogSessionReleased(string sessionId);
}
