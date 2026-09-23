// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Cdc.Firestore;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Firestore;

/// <summary>
/// Real-Firestore-emulator lock on the CDC processor's backpressure: a snapshot larger than the channel
/// must be delivered in full, not truncated.
/// </summary>
/// <remarks>
/// <para>
/// The processor buffers changes in a bounded channel. It used to hand each change over with a
/// non-blocking write and treat a full channel as an error — which abandoned the remainder of the
/// snapshot. Firestore does not re-deliver a snapshot, so those changes were gone; the listener callback
/// logged and returned normally, so no consumer and no host ever learned of it; and because the stored
/// position advances behind whatever IS handled, a later change moved the checkpoint past the gap. A
/// checkpoint is a claim that everything before it was delivered, and that claim became false silently.
/// </para>
/// <para>
/// <b>Why this needs the emulator rather than a unit test.</b> The subject is what the real Firestore
/// listener does when the handler cannot keep up — the snapshot, its change set, and the callback are all
/// produced by the SDK. A hand-built snapshot would be testing a fake's delivery, which is the mock-grade
/// version of the very thing that failed here.
/// </para>
/// <para>
/// <b>RED by construction on the pre-fix code.</b> The documents are written BEFORE the listener starts,
/// so the initial snapshot carries all of them at once and necessarily exceeds the channel capacity set
/// below. With a blocked handler the channel fills after the first few, and the old non-blocking write
/// then failed for every remaining change in that one snapshot.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("SubComponent", "CdcBackpressure")]
[Collection(FirestoreCdcStateStoreTestCollection.CollectionName)]
public sealed class FirestoreCdcBackpressureIntegrationShould
{
	/// <summary>Far more changes than the channel can hold, so the snapshot cannot be buffered whole.</summary>
	private const int DocumentCount = 12;

	/// <summary>Small enough that a blocked handler fills the channel almost immediately.</summary>
	private const int ChannelCapacity = 2;

	private readonly FirestoreCdcStateStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreCdcBackpressureIntegrationShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared emulator fixture.</param>
	public FirestoreCdcBackpressureIntegrationShould(FirestoreCdcStateStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// SAFETY. Every change in an oversized snapshot reaches the handler once the consumer catches up.
	/// </summary>
	[Fact]
	public async Task Deliver_every_change_when_a_snapshot_exceeds_the_channel_capacity()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock is about what real Firestore delivers under backpressure and is never skipped.");

		var collectionPath = $"cdc_backpressure_{Guid.NewGuid():N}";
		var expectedIds = await SeedDocumentsAsync(collectionPath);

		var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var delivered = new ConcurrentQueue<string>();
		var allArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		await using var processor = CreateProcessor(collectionPath);
		using var stopping = new CancellationTokenSource();

		// StartAsync does NOT return once started -- it runs the consume loop inline until its token is
		// cancelled. Awaiting it here would block this test before the gate below is ever released, so it
		// runs as a background task and is stopped by cancelling the token at the end.
		var processing = processor.StartAsync(
			async (change, handlerToken) =>
			{
				// Hold the consumer so the channel fills while the listener is still mid-snapshot. This is
				// the state in which changes used to be discarded.
				await released.Task.ConfigureAwait(false);

				handlerToken.ThrowIfCancellationRequested();
				delivered.Enqueue(change.DocumentId);

				if (delivered.Count == DocumentCount)
				{
					allArrived.TrySetResult();
				}
			},
			stopping.Token);

		// Let the listener receive the snapshot and run into the full channel. The assertion below does not
		// depend on this being long enough -- it only makes the blocked state the one under test rather than
		// a race that resolves before the channel ever fills.
		await Task.Delay(TimeSpan.FromSeconds(5));

		released.SetResult();

		var finished = await Task.WhenAny(allArrived.Task, Task.Delay(TimeSpan.FromSeconds(90)));

		// ASSERT BEFORE CLEANING UP. Stopping the processor tears down an in-flight Firestore call, and the
		// vendor surfaces that as an RpcException wrapping the cancellation. Cleaning up first let that
		// exception replace the verdict, so a run reported a gRPC fault when what it had actually measured
		// was delivery.
		finished.ShouldBe(
			allArrived.Task,
			$"only {delivered.Count} of {DocumentCount} changes were delivered. A snapshot larger than the "
			+ "channel must wait for capacity; discarding the remainder loses changes Firestore will not "
			+ "send again.");

		delivered.ShouldBe(expectedIds, ignoreOrder: true);

		await StopAsync(stopping, processing);
	}

	/// <summary>
	/// Stops the consume loop, tolerating however the vendor surfaces the cancellation.
	/// </summary>
	/// <remarks>
	/// Cancellation is the documented way to stop this processor, so anything thrown once cancellation has
	/// been requested is the stop itself, not a fault. The Firestore client wraps it in an
	/// <c>RpcException</c> rather than rethrowing <see cref="OperationCanceledException"/>, which a
	/// type-specific catch misses.
	/// </remarks>
	private static async Task StopAsync(CancellationTokenSource stopping, Task processing)
	{
		await stopping.CancelAsync();

		try
		{
			await processing;
		}
		catch (Exception) when (stopping.IsCancellationRequested)
		{
		}
	}

	/// <summary>
	/// Writes the documents before any listener exists, so they arrive as one initial snapshot.
	/// </summary>
	private async Task<IReadOnlyCollection<string>> SeedDocumentsAsync(string collectionPath)
	{
		var collection = _fixture.Db.Collection(collectionPath);
		var ids = new List<string>(DocumentCount);

		for (var i = 0; i < DocumentCount; i++)
		{
			var id = $"doc-{i:D2}";
			_ = await collection.Document(id).SetAsync(new Dictionary<string, object> { ["index"] = i });
			ids.Add(id);
		}

		return ids;
	}

	private FirestoreCdcProcessor CreateProcessor(string collectionPath) =>
		new(
			_fixture.Db,
			Microsoft.Extensions.Options.Options.Create(new FirestoreCdcOptions
			{
				CollectionPath = collectionPath,
				ProcessorName = "backpressure-lock",
				ChannelCapacity = ChannelCapacity,
			}),
			new FirestoreCdcStateStore(_fixture.Db, NullLogger<FirestoreCdcStateStore>.Instance),
			NullLogger<FirestoreCdcProcessor>.Instance);
}
