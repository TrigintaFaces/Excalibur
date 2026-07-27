// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.Dispatch.Transport;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Self-test proving <see cref="TransportConformanceTestKit{TSender, TReceiver}"/> runs end-to-end against
/// a sample in-memory <see cref="IChannelSender"/>/<see cref="IChannelReceiver"/> pair and reports pass/fail
/// (wired-and-tested).
/// </summary>
/// <remarks>
/// The in-memory double round-trips messages through a shared JSON queue so the round-trip, metadata, and
/// concurrent-delivery branches of the kit are all exercised.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class InMemoryTransportConformanceTests
	: TransportConformanceTestKit<QueueChannelSender, QueueChannelReceiver>
{
	private readonly ConcurrentQueue<string> _queue = new();

	/// <inheritdoc />
	protected override Task<QueueChannelSender> CreateSenderAsync() =>
		Task.FromResult(new QueueChannelSender(_queue));

	/// <inheritdoc />
	protected override Task<QueueChannelReceiver> CreateReceiverAsync() =>
		Task.FromResult(new QueueChannelReceiver(_queue));

	[Fact] public Task Should_Send_And_Receive_Message_RoundTrip_Test() => Should_Send_And_Receive_Message_RoundTrip();
	[Fact] public Task Should_Preserve_Message_Metadata_Test() => Should_Preserve_Message_Metadata();
	[Fact] public Task Should_Handle_Concurrent_Messages_Test() => Should_Handle_Concurrent_Messages();
}

/// <summary>In-memory channel sender that serializes each message to a shared JSON queue.</summary>
public sealed class QueueChannelSender(ConcurrentQueue<string> queue) : IChannelSender
{
	public Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		queue.Enqueue(JsonSerializer.Serialize(message));
		return Task.CompletedTask;
	}
}

/// <summary>In-memory channel receiver that polls the shared JSON queue and deserializes each message.</summary>
public sealed class QueueChannelReceiver(ConcurrentQueue<string> queue) : IChannelReceiver
{
	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			if (queue.TryDequeue(out var json))
			{
				return JsonSerializer.Deserialize<T>(json);
			}

			await Task.Delay(5, cancellationToken).ConfigureAwait(false);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return default;
	}
}
