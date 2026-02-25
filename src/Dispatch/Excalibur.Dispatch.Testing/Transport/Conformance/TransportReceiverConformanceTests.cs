// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// Abstract behavioral conformance tests for <see cref="ITransportReceiver"/> implementations.
/// Verifies that any transport receiver correctly implements the contract defined in ADR-116.
/// </summary>
/// <remarks>
/// <para>
/// To use, inherit this class and implement <see cref="CreateReceiverAsync"/> and
/// <see cref="SeedMessagesAsync"/> to provide an instance and seed test messages.
/// </para>
/// </remarks>
public abstract class TransportReceiverConformanceTests
{
	/// <summary>
	/// Creates a new <see cref="ITransportReceiver"/> instance for testing.
	/// </summary>
	/// <returns>A receiver instance configured for testing.</returns>
	protected abstract Task<ITransportReceiver> CreateReceiverAsync();

	/// <summary>
	/// Seeds messages into the transport so the receiver can pick them up.
	/// For InMemory transports, this means calling <c>Enqueue</c>.
	/// For real transports, this means sending messages via a sender.
	/// </summary>
	/// <param name="receiver">The receiver to seed messages for.</param>
	/// <param name="messages">The messages to seed.</param>
	protected abstract Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages);

	/// <summary>
	/// Creates a test <see cref="TransportReceivedMessage"/> with the given body.
	/// </summary>
	protected static TransportReceivedMessage CreateTestMessage(string body, string? id = null) =>
		new()
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Body = Encoding.UTF8.GetBytes(body),
			ContentType = "text/plain",
			EnqueuedAt = DateTimeOffset.UtcNow,
		};

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.Source"/> returns a non-empty string.
	/// </summary>
	protected async Task VerifySourceIsNotEmpty()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(receiver.Source))
		{
			throw new InvalidOperationException(
				"ITransportReceiver.Source must return a non-empty string.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.ReceiveAsync"/> returns seeded messages.
	/// </summary>
	protected async Task VerifyReceiveReturnsMessages()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		var seeded = new List<TransportReceivedMessage>
		{
			CreateTestMessage("msg-1"),
			CreateTestMessage("msg-2"),
		};

		await SeedMessagesAsync(receiver, seeded).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(10, CancellationToken.None).ConfigureAwait(false);

		if (received.Count != 2)
		{
			throw new InvalidOperationException(
				$"ReceiveAsync should return 2 messages, got {received.Count}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.ReceiveAsync"/> respects the maxMessages parameter.
	/// </summary>
	protected async Task VerifyReceiveRespectsMaxMessages()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		var seeded = new List<TransportReceivedMessage>
		{
			CreateTestMessage("limit-1"),
			CreateTestMessage("limit-2"),
			CreateTestMessage("limit-3"),
		};

		await SeedMessagesAsync(receiver, seeded).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(2, CancellationToken.None).ConfigureAwait(false);

		if (received.Count > 2)
		{
			throw new InvalidOperationException(
				$"ReceiveAsync(maxMessages=2) should return at most 2, got {received.Count}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.ReceiveAsync"/> returns empty when no messages available.
	/// </summary>
	protected async Task VerifyReceiveReturnsEmptyWhenNone()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(10, CancellationToken.None).ConfigureAwait(false);

		if (received.Count != 0)
		{
			throw new InvalidOperationException(
				$"ReceiveAsync should return 0 messages when none available, got {received.Count}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.ReceiveAsync"/> respects cancellation.
	/// </summary>
	protected async Task VerifyReceiveRespectsCancellation()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		try
		{
			await receiver.ReceiveAsync(1, cts.Token).ConfigureAwait(false);
			throw new InvalidOperationException(
				"ReceiveAsync should throw OperationCanceledException for cancelled token.");
		}
		catch (OperationCanceledException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.AcknowledgeAsync"/> completes without error.
	/// </summary>
	protected async Task VerifyAcknowledgeSucceeds()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		var seeded = new List<TransportReceivedMessage> { CreateTestMessage("ack-test") };
		await SeedMessagesAsync(receiver, seeded).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

		if (received.Count == 0)
		{
			throw new InvalidOperationException("No messages to acknowledge.");
		}

		await receiver.AcknowledgeAsync(received[0], CancellationToken.None).ConfigureAwait(false);
		// No exception = pass
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.AcknowledgeAsync"/> throws on null message.
	/// </summary>
	protected async Task VerifyAcknowledgeThrowsOnNull()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);

		try
		{
			await receiver.AcknowledgeAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new InvalidOperationException(
				"AcknowledgeAsync should throw ArgumentNullException for null message.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.RejectAsync"/> completes without error.
	/// </summary>
	protected async Task VerifyRejectSucceeds()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		var seeded = new List<TransportReceivedMessage> { CreateTestMessage("reject-test") };
		await SeedMessagesAsync(receiver, seeded).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

		if (received.Count == 0)
		{
			throw new InvalidOperationException("No messages to reject.");
		}

		await receiver.RejectAsync(received[0], "test-reason", requeue: false, CancellationToken.None)
			.ConfigureAwait(false);
		// No exception = pass
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.RejectAsync"/> with requeue flag completes.
	/// </summary>
	protected async Task VerifyRejectWithRequeueSucceeds()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		var seeded = new List<TransportReceivedMessage> { CreateTestMessage("requeue-test") };
		await SeedMessagesAsync(receiver, seeded).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

		if (received.Count == 0)
		{
			throw new InvalidOperationException("No messages to reject with requeue.");
		}

		await receiver.RejectAsync(received[0], "retry", requeue: true, CancellationToken.None)
			.ConfigureAwait(false);
		// No exception = pass
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.RejectAsync"/> accepts null reason.
	/// </summary>
	protected async Task VerifyRejectAcceptsNullReason()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		var seeded = new List<TransportReceivedMessage> { CreateTestMessage("null-reason") };
		await SeedMessagesAsync(receiver, seeded).ConfigureAwait(false);

		var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

		if (received.Count == 0)
		{
			throw new InvalidOperationException("No messages to reject.");
		}

		await receiver.RejectAsync(received[0], reason: null, requeue: false, CancellationToken.None)
			.ConfigureAwait(false);
		// No exception = pass
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.GetService"/> returns null for unknown types.
	/// </summary>
	protected async Task VerifyGetServiceReturnsNullForUnknownType()
	{
		await using var receiver = await CreateReceiverAsync().ConfigureAwait(false);

		var result = receiver.GetService(typeof(IDisposable));

		if (result is not null)
		{
			throw new InvalidOperationException(
				"GetService should return null for unknown service types.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportReceiver.DisposeAsync"/> can be called multiple times.
	/// </summary>
	protected async Task VerifyDisposeAsyncIsIdempotent()
	{
		var receiver = await CreateReceiverAsync().ConfigureAwait(false);

		await receiver.DisposeAsync().ConfigureAwait(false);
		await receiver.DisposeAsync().ConfigureAwait(false);
		// No exception = pass
	}
}
