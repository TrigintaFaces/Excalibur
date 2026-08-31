// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0007 // Use implicit type (var)
#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Transport;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract conformance test kit for transport <see cref="IChannelSender"/>/<see cref="IChannelReceiver"/>
/// implementations. Verifies the universally-expressible body-level send/receive contract.
/// </summary>
/// <typeparam name="TSender">The transport sender type.</typeparam>
/// <typeparam name="TReceiver">The transport receiver type.</typeparam>
/// <remarks>
/// <para>
/// Inherit from this kit and implement <see cref="CreateSenderAsync"/> and <see cref="CreateReceiverAsync"/>
/// to return a connected sender/receiver pair (wired to the same destination) for the transport under test.
/// The kit verifies round-trip delivery, body-level metadata preservation, and concurrent delivery without
/// loss. Override <see cref="ReceiveTimeout"/> to tune the receive window and <see cref="DisposeTransportAsync"/>
/// to release resources.
/// </para>
/// <para>
/// This is the body-only core contract. Optional/advanced transport capabilities — server-side filtering,
/// CloudEvents protocol binding, ack/nack redelivery proofs, and throughput/latency SLOs — require a
/// richer receive context than <see cref="IChannelReceiver"/> exposes and are intentionally out of scope
/// for this core kit.
/// </para>
/// <para>
/// The kit exposes plain <c>public virtual</c> methods with no test-framework attributes; add the
/// attributes your test framework requires (for example <c>[Fact]</c>) on thin overrides in your derived
/// class.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class TransportConformanceTestKit<TSender, TReceiver> : ConformanceTestKit
	where TSender : IChannelSender
	where TReceiver : IChannelReceiver
{
	/// <summary>
	/// Gets the receive window used to bound receive operations so a stalled delivery fails deterministically
	/// rather than hanging. The happy path returns as soon as the message arrives.
	/// </summary>
	protected virtual TimeSpan ReceiveTimeout => TimeSpan.FromSeconds(30);

	/// <summary>
	/// Creates and initializes the transport sender.
	/// </summary>
	/// <returns>A ready sender wired to the same destination as the receiver.</returns>
	protected abstract Task<TSender> CreateSenderAsync();

	/// <summary>
	/// Creates and initializes the transport receiver.
	/// </summary>
	/// <returns>A ready receiver wired to the same destination as the sender.</returns>
	protected abstract Task<TReceiver> CreateReceiverAsync();

	/// <summary>
	/// Disposes transport resources after each test. The default implementation does nothing.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task DisposeTransportAsync() => Task.CompletedTask;

	private async Task RunAsync(Func<TSender, TReceiver, Task> body)
	{
		var sender = await CreateSenderAsync().ConfigureAwait(false);
		var receiver = await CreateReceiverAsync().ConfigureAwait(false);
		try
		{
			await body(sender, receiver).ConfigureAwait(false);
		}
		finally
		{
			await DisposeTransportAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Verifies a message survives a send/receive round-trip with its body intact.</summary>
	public virtual Task Should_Send_And_Receive_Message_RoundTrip() => RunAsync(async (sender, receiver) =>
	{
		var testMessage = new TransportTestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Test message content",
			Timestamp = DateTimeOffset.UtcNow,
		};

		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await receiver.ReceiveAsync<TransportTestMessage>(cts.Token).ConfigureAwait(false);

		if (received is null)
		{
			throw new TestFixtureAssertionException("Expected to receive the sent message.");
		}

		if (received.Id != testMessage.Id || received.Content != testMessage.Content)
		{
			throw new TestFixtureAssertionException("Received message body should match the sent message.");
		}

		if ((received.Timestamp - testMessage.Timestamp).Duration() > TimeSpan.FromMilliseconds(100))
		{
			throw new TestFixtureAssertionException("Received timestamp should match the sent timestamp.");
		}
	});

	/// <summary>Verifies body-level metadata fields (correlation, user, tenant) survive the round-trip.</summary>
	public virtual Task Should_Preserve_Message_Metadata() => RunAsync(async (sender, receiver) =>
	{
		var testMessage = new TransportTestMessageWithMetadata
		{
			Id = Guid.NewGuid().ToString(),
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
			UserId = "test-user-123",
			TenantId = "tenant-456",
			Content = "Test content",
		};

		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await receiver.ReceiveAsync<TransportTestMessageWithMetadata>(cts.Token).ConfigureAwait(false);

		if (received is null)
		{
			throw new TestFixtureAssertionException("Expected to receive the sent message.");
		}

		if (received.MessageId != testMessage.MessageId
			|| received.CorrelationId != testMessage.CorrelationId
			|| received.UserId != testMessage.UserId
			|| received.TenantId != testMessage.TenantId)
		{
			throw new TestFixtureAssertionException("Body-level metadata should be preserved across the round-trip.");
		}
	});

	/// <summary>Verifies many concurrently-sent messages are all received with no loss or duplication.</summary>
	public virtual Task Should_Handle_Concurrent_Messages() => RunAsync(async (sender, receiver) =>
	{
		const int messageCount = 100;
		var sentMessages = new List<TransportTestMessage>();
		for (int i = 0; i < messageCount; i++)
		{
			sentMessages.Add(new TransportTestMessage
			{
				Id = Guid.NewGuid().ToString(),
				Content = $"Message {i}",
				Timestamp = DateTimeOffset.UtcNow,
			});
		}

		var sendTasks = sentMessages.Select(msg => sender.SendAsync(msg, CancellationToken.None)).ToList();
		await Task.WhenAll(sendTasks).ConfigureAwait(false);

		var receivedMessages = new List<TransportTestMessage>();
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		for (int i = 0; i < messageCount; i++)
		{
			var received = await receiver.ReceiveAsync<TransportTestMessage>(cts.Token).ConfigureAwait(false);
			if (received is not null)
			{
				receivedMessages.Add(received);
			}
		}

		if (receivedMessages.Count != messageCount)
		{
			throw new TestFixtureAssertionException(
				$"Expected {messageCount} messages received but was {receivedMessages.Count}.");
		}

		var receivedIds = receivedMessages.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
		var sentIds = sentMessages.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
		if (!receivedIds.SetEquals(sentIds))
		{
			throw new TestFixtureAssertionException("All messages should be received with no loss or duplicates.");
		}
	});
}

/// <summary>
/// A test transport message used by <see cref="TransportConformanceTestKit{TSender, TReceiver}"/>.
/// </summary>
public sealed class TransportTestMessage
{
	/// <summary>Gets or sets the message identifier.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the message content.</summary>
	public string Content { get; set; } = string.Empty;

	/// <summary>Gets or sets the message timestamp.</summary>
	public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// A test transport message carrying body-level metadata, used by
/// <see cref="TransportConformanceTestKit{TSender, TReceiver}"/>.
/// </summary>
public sealed class TransportTestMessageWithMetadata
{
	/// <summary>Gets or sets the message identifier.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the transport message identifier.</summary>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>Gets or sets the correlation identifier.</summary>
	public string CorrelationId { get; set; } = string.Empty;

	/// <summary>Gets or sets the user identifier.</summary>
	public string UserId { get; set; } = string.Empty;

	/// <summary>Gets or sets the tenant identifier.</summary>
	public string TenantId { get; set; } = string.Empty;

	/// <summary>Gets or sets the message content.</summary>
	public string Content { get; set; } = string.Empty;
}
