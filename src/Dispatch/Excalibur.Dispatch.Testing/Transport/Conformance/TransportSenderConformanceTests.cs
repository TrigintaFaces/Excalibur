// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// Abstract behavioral conformance tests for <see cref="ITransportSender"/> implementations.
/// Verifies that any transport sender correctly implements the transport sender contract.
/// </summary>
/// <remarks>
/// <para>
/// To use, inherit this class and implement <see cref="CreateSenderAsync"/> to provide
/// an instance of your transport sender. Each test method is <c>protected</c> and async,
/// so your test framework (xUnit, NUnit, MSTest) can call them from its own test methods.
/// </para>
/// <para>
/// Example (xUnit):
/// <code>
/// public class MyTransportSenderConformance : TransportSenderConformanceTests
/// {
///     protected override Task&lt;ITransportSender&gt; CreateSenderAsync()
///         =&gt; Task.FromResult&lt;ITransportSender&gt;(new MyTransportSender("test-dest"));
///
///     [Fact] public Task SendDeliversMessage() =&gt; VerifySendDeliversMessage();
///     [Fact] public Task SendBatchDeliversAll() =&gt; VerifySendBatchDeliversAllMessages();
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class TransportSenderConformanceTests
{
	/// <summary>
	/// Creates a new <see cref="ITransportSender"/> instance for testing.
	/// </summary>
	/// <returns>A sender instance configured for testing.</returns>
	protected abstract Task<ITransportSender> CreateSenderAsync();

	/// <summary>
	/// Optionally provides an <see cref="ITransportReceiver"/> paired with the sender
	/// to verify round-trip message delivery. Return <see langword="null"/> to skip round-trip tests.
	/// </summary>
	/// <returns>A receiver instance that reads from the same destination, or <see langword="null"/>.</returns>
	protected virtual Task<ITransportReceiver?> CreatePairedReceiverAsync() =>
		Task.FromResult<ITransportReceiver?>(null);

	/// <summary>
	/// Verifies that <see cref="ITransportSender.Destination"/> returns a non-empty string.
	/// </summary>
	protected async Task VerifyDestinationIsNotEmpty()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(sender.Destination))
		{
			throw new InvalidOperationException(
				"ITransportSender.Destination must return a non-empty string.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendAsync"/> accepts a message and returns a successful result.
	/// </summary>
	protected async Task VerifySendDeliversMessage()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);
		var message = TransportMessage.FromString("conformance-test-payload");

		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		if (!result.IsSuccess)
		{
			throw new InvalidOperationException(
				$"SendAsync should return a successful result. Got error: {result.Error?.Message}");
		}

		if (string.IsNullOrEmpty(result.MessageId))
		{
			throw new InvalidOperationException(
				"SendAsync success result should include a non-empty MessageId.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendAsync"/> preserves the message ID in the result.
	/// </summary>
	protected async Task VerifySendPreservesMessageId()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);
		var message = TransportMessage.FromString("id-test");
		var expectedId = message.Id;

		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		if (result.MessageId != expectedId)
		{
			throw new InvalidOperationException(
				$"SendAsync should return the message's Id in the result. Expected '{expectedId}', got '{result.MessageId}'.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendAsync"/> throws <see cref="ArgumentNullException"/>
	/// when the message parameter is null.
	/// </summary>
	protected async Task VerifySendThrowsOnNullMessage()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);

		try
		{
			await sender.SendAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new InvalidOperationException(
				"SendAsync should throw ArgumentNullException for null message.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendAsync"/> respects cancellation.
	/// </summary>
	protected async Task VerifySendRespectsCancellation()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		try
		{
			await sender.SendAsync(
				TransportMessage.FromString("cancel-test"), cts.Token).ConfigureAwait(false);
			throw new InvalidOperationException(
				"SendAsync should throw OperationCanceledException for cancelled token.");
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendBatchAsync"/> delivers all messages successfully.
	/// </summary>
	protected async Task VerifySendBatchDeliversAllMessages()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);
		var messages = new List<TransportMessage>
		{
			TransportMessage.FromString("batch-1"),
			TransportMessage.FromString("batch-2"),
			TransportMessage.FromString("batch-3"),
		};

		var result = await sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);

		if (result.TotalMessages != 3)
		{
			throw new InvalidOperationException(
				$"BatchSendResult.TotalMessages should be 3, got {result.TotalMessages}.");
		}

		if (result.SuccessCount != 3)
		{
			throw new InvalidOperationException(
				$"BatchSendResult.SuccessCount should be 3, got {result.SuccessCount}.");
		}

		if (result.FailureCount != 0)
		{
			throw new InvalidOperationException(
				$"BatchSendResult.FailureCount should be 0, got {result.FailureCount}.");
		}

		if (!result.IsCompleteSuccess)
		{
			throw new InvalidOperationException(
				"BatchSendResult.IsCompleteSuccess should be true.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendBatchAsync"/> returns individual results per message.
	/// </summary>
	protected async Task VerifySendBatchReturnsPerMessageResults()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);
		var messages = new List<TransportMessage>
		{
			TransportMessage.FromString("result-1"),
			TransportMessage.FromString("result-2"),
		};

		var result = await sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);

		if (result.Results.Count != 2)
		{
			throw new InvalidOperationException(
				$"BatchSendResult.Results should have 2 entries, got {result.Results.Count}.");
		}

		for (var i = 0; i < result.Results.Count; i++)
		{
			if (!result.Results[i].IsSuccess)
			{
				throw new InvalidOperationException(
					$"BatchSendResult.Results[{i}] should be successful.");
			}
		}
	}

	/// <summary>
	/// Verifies the batch contract stated on <see cref="BatchSendResult.Results"/>: exactly one result per
	/// input, index-aligned with the input list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the cross-provider arm for that contract. It exists because two transports were fixed to a
	/// positional contract independently and each documented it on its own sender, which is two
	/// coincidences rather than one guarantee — the next implementation would have been bound by nothing.
	/// </para>
	/// <para>
	/// <b>Alignment is checked through the results' own identifiers, not assumed.</b> A batch of
	/// uniformly successful messages looks identical however it is ordered, so an arm that only counted
	/// entries would pass a provider that groups its successes before its failures. This sends messages
	/// with distinct bodies and identifiers and requires the result at each index to name that index's
	/// input.
	/// </para>
	/// <para>
	/// <b>A provider whose results carry no input identity FAILS here rather than being skipped.</b> It is
	/// not that such a provider is known to be misaligned — it is that its alignment cannot be verified at
	/// all, and a check that cannot fail is not protection. Reporting "unverifiable" as a pass is how the
	/// original defect survived.
	/// </para>
	/// </remarks>
	protected async Task VerifySendBatchResultsAreOnePerInputInOrder()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);

		var messages = new List<TransportMessage>();
		for (var i = 0; i < 5; i++)
		{
			messages.Add(TransportMessage.FromString($"aligned-{i}"));
		}

		var result = await sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);

		// Clause 1 — exactly one result per input, and the counts describe that same list.
		if (result.Results.Count != messages.Count)
		{
			throw new InvalidOperationException(
				$"BatchSendResult.Results must carry exactly one entry per input: expected {messages.Count}, "
				+ $"got {result.Results.Count}. A caller cannot attribute a result to a message it cannot find.");
		}

		if (result.TotalMessages != messages.Count)
		{
			throw new InvalidOperationException(
				$"BatchSendResult.TotalMessages should be {messages.Count}, got {result.TotalMessages}.");
		}

		if (result.SuccessCount + result.FailureCount != messages.Count)
		{
			throw new InvalidOperationException(
				$"SuccessCount ({result.SuccessCount}) + FailureCount ({result.FailureCount}) should equal "
				+ $"{messages.Count}; the counts do not describe the results list.");
		}

		// Clause 2 — index alignment, verified from the identifiers the results carry.
		var inputIds = new HashSet<string>(messages.Select(static m => m.Id), StringComparer.Ordinal);
		var carriesInputIdentity = result.Results.All(
			r => r.MessageId is { Length: > 0 } id && inputIds.Contains(id));

		if (!carriesInputIdentity)
		{
			throw new InvalidOperationException(
				"BatchSendResult.Results does not carry the identity of the inputs, so index alignment "
				+ "cannot be verified. Each result's MessageId should name the TransportMessage it "
				+ "reports on. Until it does, a caller performing selective retry is trusting position "
				+ "with nothing to check it against.");
		}

		for (var i = 0; i < messages.Count; i++)
		{
			if (!string.Equals(result.Results[i].MessageId, messages[i].Id, StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					$"BatchSendResult.Results[{i}] reports message '{result.Results[i].MessageId}' but "
					+ $"input {i} was '{messages[i].Id}'. Results must be index-aligned with the inputs; "
					+ "grouping them by outcome makes selective retry resend the wrong messages.");
			}
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.SendBatchAsync"/> handles an empty batch.
	/// </summary>
	protected async Task VerifySendBatchHandlesEmptyBatch()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);
		var messages = new List<TransportMessage>();

		var result = await sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);

		if (result.TotalMessages != 0)
		{
			throw new InvalidOperationException(
				$"Empty batch should have TotalMessages=0, got {result.TotalMessages}.");
		}

		if (!result.IsCompleteSuccess)
		{
			throw new InvalidOperationException(
				"Empty batch should have IsCompleteSuccess=true.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.FlushAsync"/> completes without error.
	/// </summary>
	protected async Task VerifyFlushCompletesSuccessfully()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);

		await sender.FlushAsync(CancellationToken.None).ConfigureAwait(false);
		// No exception = pass
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSender.GetService"/> returns null for unknown types.
	/// </summary>
	protected async Task VerifyGetServiceReturnsNullForUnknownType()
	{
		await using var sender = await CreateSenderAsync().ConfigureAwait(false);

		var result = sender.GetService(typeof(IDisposable));

		if (result is not null)
		{
			throw new InvalidOperationException(
				"GetService should return null for unknown service types.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IAsyncDisposable.DisposeAsync"/> can be called multiple times.
	/// </summary>
	protected async Task VerifyDisposeAsyncIsIdempotent()
	{
		var sender = await CreateSenderAsync().ConfigureAwait(false);

		await sender.DisposeAsync().ConfigureAwait(false);
		await sender.DisposeAsync().ConfigureAwait(false);
		// No exception = pass
	}

	/// <summary>
	/// Verifies send-receive round-trip: message sent through sender can be received by paired receiver.
	/// Skipped if <see cref="CreatePairedReceiverAsync"/> returns null.
	/// </summary>
	protected async Task VerifySendReceiveRoundTrip()
	{
		var receiver = await CreatePairedReceiverAsync().ConfigureAwait(false);

		if (receiver is null)
		{
			return; // Skip if no paired receiver
		}

		await using (receiver.ConfigureAwait(false))
		{
			await using var sender = await CreateSenderAsync().ConfigureAwait(false);
			var payload = "round-trip-" + Guid.NewGuid().ToString("N");
			var message = TransportMessage.FromString(payload);

			await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

			var received = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);

			if (received.Count != 1)
			{
				throw new InvalidOperationException(
					$"Round-trip: expected 1 received message, got {received.Count}.");
			}

			var body = Encoding.UTF8.GetString(received[0].Body.Span);

			if (body != payload)
			{
				throw new InvalidOperationException(
					$"Round-trip: expected body '{payload}', got '{body}'.");
			}
		}
	}
}
