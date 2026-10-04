// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Gcs;

using Google;
using Google.Cloud.Storage.V1;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Excalibur.EventSourcing.Gcs.DependencyInjection;

using Shouldly;

using Xunit;

using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// Locks the durable-watermark contract of the Google Cloud Storage cold event store.
/// </summary>
/// <remarks>
/// <para>
/// The return value of <c>WriteAsync</c> authorizes the caller to DELETE hot events up to it, so every
/// arm here is about data loss rather than convenience: a watermark one version too high destroys the
/// only surviving copy of the versions cold never stored.
/// </para>
/// <para>
/// The two sibling cold stores exercise this contract against real containers. This suite deliberately
/// does not, and the reason is not cost: the properties below are about ORDER and ARITHMETIC, and a
/// container makes both harder to observe, not easier. <see cref="StorageClient"/> is abstract with the
/// three members this store calls declared virtual, so substituting it is a supported extension point
/// rather than a mock of a sealed type — and it lets the upload be HELD OPEN, which is the only way to
/// prove the store does not acknowledge durability before the storage receipt arrives. No emulator can
/// demonstrate that, because no emulator lets a test decide when the upload completes.
/// </para>
/// <para>
/// <b>Coverage boundary.</b> These arms cover the durable-watermark contract and the conditional-write
/// retry: the empty-batch ack, the contiguous prefix, gap-filling membership, the durability ordering,
/// and a lost conditional write whose retry must re-read. They do NOT cover retry EXHAUSTION (a writer
/// that loses its condition more times than the loop allows), the object-key encoding, or read-path
/// filtering beyond what the write arms observe.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class GcsColdEventStoreShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.FromStoredValue("tenant-a");
	private const string AggregateId = "aggregate-1";

	[Fact]
	public async Task MigrateTypedHistoryAndRefuseMissingBaselineOnEveryReadPath()
	{
		var legacy = NewStore(out var client);
		using (client)
		{
			await legacy.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0), Event(2)], CancellationToken.None);
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddExcaliburEventSourcing(es => es.UseGcsColdEventStore(gcs =>
				gcs.Client(client).BucketName("bucket").ObjectPrefix("prefix").Layout(ColdArchiveLayout.TypedV2)));
			await using var provider = services.BuildServiceProvider();
			var typed = provider.GetRequiredService<IColdEventStore>();
			provider.GetRequiredService<IOptions<GcsColdEventStoreOptions>>().Value.Layout = ColdArchiveLayout.Legacy;
			var migration = (IColdEventStoreMigration)typed;
			await Should.ThrowAsync<InvalidOperationException>(() => typed.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None));
			await migration.ActivateTypedLayoutAsync(CancellationToken.None);
			await Should.ThrowAsync<InvalidOperationException>(() => typed.HasArchivedEventsAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None));
			await migration.MigrateAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None);
			(await typed.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(1)], CancellationToken.None)).ShouldBe(2);
			(await typed.WriteAsync(Tenant, AggregateId, "Aggregate", [], CancellationToken.None)).ShouldBe(-1);
			(await typed.ReadAsync(Tenant, AggregateId, "Aggregate", 0, CancellationToken.None)).Select(e => e.Version).ShouldBe([1L, 2L]);
			var adapter = new GcsColdArchiveMigrationStorage(client, "bucket", "prefix");
			var fresh = Event(0) with { AggregateId = "fresh", EventId = "fresh-event" };
			(await typed.WriteAsync(Tenant, "fresh", "Aggregate", [fresh], CancellationToken.None)).ShouldBe(0);
			(await adapter.ReadAsync(adapter.GetLegacyKey(Tenant, "fresh"), CancellationToken.None)).ShouldBeNull();
			(await adapter.ReadAsync(adapter.GetReceiptKey(Tenant, "fresh"), CancellationToken.None)).ShouldBeNull();
			using var restarted = new GcsColdEventStore(client, "bucket", "prefix", NullLogger<GcsColdEventStore>.Instance);
			await Should.ThrowAsync<InvalidOperationException>(() => restarted.ReadAsync(Tenant, "fresh", "Aggregate", CancellationToken.None));
			var other = Event(0) with { AggregateType = "Other", EventId = "other-event" };
			(await typed.WriteAsync(Tenant, AggregateId, "Other", [other], CancellationToken.None)).ShouldBe(0);
			(await typed.ReadAsync(Tenant, AggregateId, "Other", CancellationToken.None)).Single().EventId.ShouldBe("other-event");
			client.Remove(adapter.GetLegacyKey(Tenant, AggregateId));
			await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(1)], CancellationToken.None));
			await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, AggregateId, "Aggregate", [], CancellationToken.None));
			await Should.ThrowAsync<InvalidOperationException>(() => typed.ReadAsync(Tenant, AggregateId, "Aggregate", 100, CancellationToken.None));
			await Should.ThrowAsync<InvalidOperationException>(() => typed.HasArchivedEventsAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None));
			await Should.ThrowAsync<InvalidOperationException>(() => legacy.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None));
		}
	}

	[Fact]
	public async Task RevalidateTypedHistoryAfterLosingAGenerationRace()
	{
		var legacy = NewStore(out var client);
		using (client)
		{
			await legacy.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0)], CancellationToken.None);
			var typed = new GcsColdEventStore(client, "bucket", "prefix", NullLogger<GcsColdEventStore>.Instance, ColdArchiveLayout.TypedV2);
			await typed.ActivateTypedLayoutAsync(CancellationToken.None);
			await typed.MigrateAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None);
			var adapter = new GcsColdArchiveMigrationStorage(client, "bucket", "prefix");
			var beforeUploads = client.UploadCount;
			client.CommitConcurrentlyBeforeNextUpload(async () =>
			{
				var result = await typed.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(2)], CancellationToken.None);
				client.Remove(adapter.GetLegacyKey(Tenant, AggregateId));
				return result;
			});
			await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(1)], CancellationToken.None));
			client.ConditionalConflicts.ShouldBe(1);
			client.UploadCount.ShouldBe(beforeUploads + 1);
			var raw = (await adapter.ReadAsync(adapter.GetTypedKey(Tenant, AggregateId, "Aggregate"), CancellationToken.None)).ShouldNotBeNull();
			(await adapter.DecodeAsync(raw, CancellationToken.None)).Select(e => e.Version).ShouldBe([0L, 2L]);
		}
	}

	[Fact]
	public async Task ReturnUnsortedLegacyEventsInVersionOrder()
	{
		var store = NewStore(out var client);
		await SeedArchiveAsync(client, System.Text.Json.JsonSerializer.Serialize(
			new List<StoredEvent> { Event(2), Event(0), Event(1) }, GcsColdEventStore.ArchiveTypeInfo));
		(await store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None))
			.Select(e => e.Version).ShouldBe([0L, 1L, 2L]);
		(await store.ReadAsync(Tenant, AggregateId, "Aggregate", 0, CancellationToken.None))
			.Select(e => e.Version).ShouldBe([1L, 2L]);
	}

	[Fact]
	public async Task RejectPresentNullArchiveWithoutOverwritingIt()
	{
		var store = NewStore(out var client);
		await SeedArchiveAsync(client, "null");
		var uploads = client.UploadCount;
		await Should.ThrowAsync<System.Text.Json.JsonException>(() =>
			store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None));
		await Should.ThrowAsync<System.Text.Json.JsonException>(() =>
			store.HasArchivedEventsAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None));
		await Should.ThrowAsync<System.Text.Json.JsonException>(() =>
			store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0)], CancellationToken.None));
		client.UploadCount.ShouldBe(uploads);
	}

	private static async Task SeedArchiveAsync(FakeStorageClient client, string json)
	{
		using var buffer = new MemoryStream();
		await using (var gzip = new System.IO.Compression.GZipStream(buffer,
			System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
		{
			await gzip.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
		}
		buffer.Position = 0;
		_ = await client.UploadObjectAsync("bucket",
			$"prefix/{ColdStorageKey.TenantSegment(Tenant)}/{ColdStorageKey.AggregateSegment(AggregateId)}/events.json.gz",
			"application/gzip", buffer);
	}

	[Fact]
	public async Task RejectWrongAggregateTypeAcrossEveryArchiveOperation()
	{
		var store = NewStore(out var client);
		_ = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0)], CancellationToken.None);
		var uploads = client.UploadCount;

		await Should.ThrowAsync<InvalidOperationException>(() =>
			store.ReadAsync(Tenant, AggregateId, "DifferentType", CancellationToken.None));
		// Filtering must not hide an identity mismatch in the stored prefix.
		await Should.ThrowAsync<InvalidOperationException>(() =>
			store.ReadAsync(Tenant, AggregateId, "DifferentType", 100, CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() =>
			store.HasArchivedEventsAsync(Tenant, AggregateId, "DifferentType", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() =>
			store.WriteAsync(Tenant, AggregateId, "DifferentType", [Event(0)], CancellationToken.None));

		client.UploadCount.ShouldBe(uploads);
		(await store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None)).Count.ShouldBe(1);
	}

	[Fact]
	public async Task RefuseToAcknowledgeASuffixAsACompletePrefix()
	{
		var store = NewStore(out _);
		(await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(5), Event(6)], CancellationToken.None)).ShouldBe(-1);
		(await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0), Event(1), Event(2), Event(3), Event(4)], CancellationToken.None)).ShouldBe(6);
	}

	[Fact]
	public async Task RejectAConflictingRetryWithoutOverwritingColdData()
	{
		var store = NewStore(out var client);
		_ = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0)], CancellationToken.None);
		var uploads = client.UploadCount;
		await Should.ThrowAsync<InvalidOperationException>(() => store.WriteAsync(
			Tenant, AggregateId, "Aggregate", [Event(0) with { EventData = [99] }], CancellationToken.None));
		client.UploadCount.ShouldBe(uploads);
		(await store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None))[0].EventData.ShouldBe(Event(0).EventData);
	}

	[Fact]
	public async Task ReturnMinusOneForAnEmptyBatch()
	{
		// The defined early-return ack: "nothing durably added by this call; delete nothing". Returning
		// anything else here would authorize a deletion this call did not earn.
		var store = NewStore(out var client);

		var watermark = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [], CancellationToken.None);

		watermark.ShouldBe(-1);
		client.UploadCount.ShouldBe(0, "an empty batch must not write");
		client.ReadCount.ShouldBe(1, "an empty write must validate current archive state before acknowledging it");
	}

	[Fact]
	public async Task ReportTheContiguousPrefixRatherThanTheSubmittedMaximum()
	{
		// THE DATA-LOSS ARM. Cold holds {0,1}; the caller submits {5,6}. Every submitted version is now
		// durable, so a store reporting the submitted maximum would return 6 -- and the caller, holding
		// the only other copy of 2, 3 and 4, would delete across the gap. The contract says contiguous
		// prefix, so the honest answer is 1.
		var store = NewStore(out _);
		_ = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0), Event(1)], CancellationToken.None);

		var watermark = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(5), Event(6)], CancellationToken.None);

		watermark.ShouldBe(1);

		// ...and the gap events really are stored; the low watermark is a statement about contiguity,
		// not a claim that the write was dropped.
		var stored = await store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None);
		stored.Select(e => e.Version).ShouldBe([0L, 1L, 5L, 6L]);
	}

	[Fact]
	public async Task StoreVersionsThatFallInsideAnExistingGapAndThenAdvanceTheWatermark()
	{
		// Presence is a SET question. Selecting "versions above the existing maximum" would discard
		// {2,3,4} as already-present against a cold set of {0,1,5} whose max is 5, silently dropping
		// three events the caller was told were archived. Filling the gap must also heal the watermark.
		var store = NewStore(out _);
		_ = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0), Event(1), Event(5)], CancellationToken.None);

		var watermark = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(2), Event(3), Event(4)], CancellationToken.None);

		watermark.ShouldBe(5);
		var stored = await store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None);
		stored.Select(e => e.Version).ShouldBe([0L, 1L, 2L, 3L, 4L, 5L], "a gap-filling batch must be sorted into place");
	}

	[Fact]
	public async Task NotAcknowledgeDurabilityBeforeTheUploadCompletes()
	{
		// THE ORDERING ARM, and the reason this suite substitutes the client instead of using a
		// container. The watermark is a durability claim, so it must not be returned while the bytes are
		// still in flight: a store that computed its answer and returned before awaiting the upload would
		// pass every arm above and still authorize deleting hot events that cold had not yet accepted.
		//
		// The upload is held open on a signal this test owns. While it is pending the write MUST NOT have
		// completed; releasing the signal MUST complete it. Deterministic -- no delays, no polling.
		var store = NewStore(out var client);
		using var uploadReached = new SemaphoreSlim(0, 1);
		var releaseUpload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		client.GateUpload(uploadReached, releaseUpload.Task);

		var write = store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(0)], CancellationToken.None);

		// Wait for the store to actually reach the upload, so the assertion below cannot pass merely
		// because the write had not started yet -- that would be a green over an experiment never run.
		(await uploadReached.WaitAsync(TimeSpan.FromSeconds(30))).ShouldBeTrue(
			"the store never reached the upload; the arm proved nothing");

		write.IsCompleted.ShouldBeFalse(
			"WriteAsync returned a durable watermark while the upload was still pending -- the "
			+ "acknowledgement precedes the storage receipt it claims to represent");

		releaseUpload.SetResult();

		(await write).ShouldBe(0);
	}

	[Fact]
	public async Task PreserveAConcurrentWritersEventsWhenItLosesTheConditionalWrite()
	{
		// THE CONCURRENCY ARM. The store writes conditionally (IfGenerationMatch) and retries on a
		// precondition failure. What the retry must do is RE-READ: a loop that simply re-uploaded its
		// own merged set would overwrite whatever the winner committed, and the losing writer would
		// silently destroy the winner's events -- the lost update this conditional write exists to stop.
		//
		// The race is real, not simulated: when the first upload is about to be attempted, a SECOND
		// store over the same storage commits version 9 through the ordinary write path. The first
		// store then loses its condition, must re-read, and must merge onto what it now finds.
		var store = NewStore(out var client);
		_ = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(5)], CancellationToken.None);

		var racingStore = new GcsColdEventStore(client, "bucket", "prefix", NullLogger<GcsColdEventStore>.Instance);
		client.CommitConcurrentlyBeforeNextUpload(() =>
			racingStore.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(9)], CancellationToken.None));

		var watermark = await store.WriteAsync(Tenant, AggregateId, "Aggregate", [Event(6)], CancellationToken.None);

		var stored = await store.ReadAsync(Tenant, AggregateId, "Aggregate", CancellationToken.None);
		stored.Select(e => e.Version).ShouldBe([
			5L,
			6L,
			9L
			],
			"the racing writer's version 9 must survive. If it is missing, the retry re-uploaded its own "
			+ "stale merge over the winner instead of re-reading -- a lost update, and the exact failure "
			+ "the conditional write is there to prevent.");

		// The winning writes survive, but versions 0..4 are still unproven.
		watermark.ShouldBe(-1);
		(await store.WriteAsync(Tenant, AggregateId, "Aggregate",
			[Event(0), Event(1), Event(2), Event(3), Event(4)], CancellationToken.None)).ShouldBe(6);
	}

	private static GcsColdEventStore NewStore(out FakeStorageClient client)
	{
		client = new FakeStorageClient();
		return new GcsColdEventStore(client, "bucket", "prefix", NullLogger<GcsColdEventStore>.Instance);
	}

	private static StoredEvent Event(long version) => new(
		EventId: $"event-{version}",
		AggregateId: AggregateId,
		AggregateType: "Aggregate",
		EventType: "Probe",
		EventData: [1, 2, 3],
		Metadata: null,
		Version: version,
		Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(version));

	/// <summary>
	/// An in-memory <see cref="StorageClient"/> holding real object bytes, so the store's own gzip and
	/// JSON round-trip runs unmodified -- only the network is replaced.
	/// </summary>
	/// <remarks>
	/// Generations are tracked because the store writes conditionally (<c>IfGenerationMatch</c>) and a
	/// fake that ignored the precondition would quietly turn the optimistic-concurrency path into an
	/// unconditional overwrite, certifying a store that had lost the guard.
	/// </remarks>
	private sealed class FakeStorageClient : StorageClient
	{
		private readonly Lazy<StorageClient> _raw = new(() => new GcsRawStorageClientBuilder { ApiKey = "unit-test" }.Build());
		public override Google.Apis.Storage.v1.StorageService Service => _raw.Value.Service;
		public override void Dispose()
		{
			try
			{
				if (_raw.IsValueCreated)
				{
					_raw.Value.Dispose();
				}
			}
			finally
			{
				base.Dispose();
			}
		}

		public int ConditionalConflicts { get; private set; }
		public void Remove(string key) => _objects.Remove(key).ShouldBeTrue();

		private static GcsObject Metadata(string bucket, string name, (byte[] Bytes, long Generation) entry)
		{
			// Independent bit-at-a-time Castagnoli checksum for the fake service's stored bytes.
			var crc = uint.MaxValue;
			foreach (var value in entry.Bytes)
			{
				crc ^= value;
				for (var bit = 0; bit < 8; bit++)
				{
					crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0x82f63b78u);
				}
			}

			var checksum = new byte[4];
			System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(checksum, ~crc);
			return new GcsObject { Bucket = bucket, Name = name, Generation = entry.Generation,
				Size = (ulong)entry.Bytes.Length, Crc32c = Convert.ToBase64String(checksum) };
		}
		public override Task<Google.Apis.Storage.v1.Data.Bucket> GetBucketAsync(string bucket,
			GetBucketOptions? options = null, CancellationToken cancellationToken = default) =>
			Task.FromResult(new Google.Apis.Storage.v1.Data.Bucket { Name = bucket });
		private readonly Dictionary<string, (byte[] Bytes, long Generation)> _objects = new(StringComparer.Ordinal);
		private long _nextGeneration = 1;
		private SemaphoreSlim? _uploadReached;
		private Task? _uploadGate;

		public int UploadCount { get; private set; }

		public int ReadCount { get; private set; }

		private Func<Task<long>>? _concurrentCommit;

		/// <summary>
		/// Runs <paramref name="commit"/> immediately before the next upload is applied, so that upload
		/// finds the generation moved and fails its precondition -- a real interleaving, not a synthetic
		/// 412. Fires once; the hook is cleared before the commit runs so the nested write is not itself
		/// intercepted.
		/// </summary>
		/// <param name="commit">The concurrent write to land first.</param>
		public void CommitConcurrentlyBeforeNextUpload(Func<Task<long>> commit) => _concurrentCommit = commit;

		/// <summary>Holds every upload open until <paramref name="gate"/> completes.</summary>
		public void GateUpload(SemaphoreSlim reached, Task gate)
		{
			_uploadReached = reached;
			_uploadGate = gate;
		}

		public override Task<GcsObject> GetObjectAsync(
			string bucket,
			string objectName,
			GetObjectOptions? options = null,
			CancellationToken cancellationToken = default) =>
			_objects.TryGetValue(objectName, out var entry)
				? Task.FromResult(Metadata(bucket, objectName, entry))
				: Task.FromException<GcsObject>(NotFound(objectName));

		public override async Task<GcsObject> DownloadObjectAsync(
			string bucket,
			string objectName,
			Stream destination,
			DownloadObjectOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
		{
			ReadCount++;
			if (!_objects.TryGetValue(objectName, out var entry))
			{
				throw NotFound(objectName);
			}
			if ((options?.Generation is { } requested && requested != entry.Generation)
				|| (options?.IfGenerationMatch is { } condition && condition != entry.Generation))
			{
				throw new GoogleApiException("storage", "generation mismatch") { HttpStatusCode = HttpStatusCode.PreconditionFailed };
			}

			await destination.WriteAsync(entry.Bytes, cancellationToken).ConfigureAwait(false);
			return Metadata(bucket, objectName, entry);
		}

		public override async Task<GcsObject> UploadObjectAsync(
			string bucket,
			string objectName,
			string? contentType,
			Stream source,
			UploadObjectOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<Google.Apis.Upload.IUploadProgress>? progress = null)
		{
			if (_concurrentCommit is not null)
			{
				var commit = _concurrentCommit;
				_concurrentCommit = null;
				_ = await commit().ConfigureAwait(false);
			}

			_ = _uploadReached?.Release();
			if (_uploadGate is not null)
			{
				await _uploadGate.ConfigureAwait(false);
			}

			// Honour the conditional write the store relies on for optimistic concurrency: 0 means
			// "create only if absent", any other value means "update only if unchanged".
			var expected = options?.IfGenerationMatch;
			var current = _objects.TryGetValue(objectName, out var existing) ? existing.Generation : 0L;
			if (expected is not null && expected != current)
			{
				ConditionalConflicts++;
				throw new GoogleApiException("storage", $"generation mismatch for '{objectName}'")
				{
					HttpStatusCode = HttpStatusCode.PreconditionFailed,
				};
			}

			using var buffer = new MemoryStream();
			await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
			_objects[objectName] = (buffer.ToArray(), _nextGeneration++);
			UploadCount++;
			return Metadata(bucket, objectName, _objects[objectName]);
		}

		private static GoogleApiException NotFound(string objectName) =>
			new("storage", $"no such object '{objectName}'") { HttpStatusCode = HttpStatusCode.NotFound };
	}
}
