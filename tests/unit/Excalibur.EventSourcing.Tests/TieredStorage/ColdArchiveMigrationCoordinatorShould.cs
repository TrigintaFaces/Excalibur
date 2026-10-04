// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class ColdArchiveMigrationCoordinatorShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.Scoped("tenant-a");

	[Fact]
	public async Task CopyTheCompleteSparseArchiveAndRetainTheSource()
	{
		var store = CreateStore();
		var original = store.Objects[store.LegacyKey];
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var result = await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		result.ShouldNotBeNull().HasSameContent(original).ShouldBeTrue();
		store.Objects[store.LegacyKey].ShouldBeSameAs(original);
		store.Objects.ContainsKey(store.ReceiptKey).ShouldBeTrue();
		(await store.DecodeAsync(result, CancellationToken.None)).Select(e => e.Version).ShouldBe([0L, 2L]);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RecoverAfterAnAcknowledgementIsLost(bool publication)
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		store.AfterCreate = key =>
		{
			if (key == (publication ? store.ReceiptKey : store.TypedKey))
			{
				throw new IOException("Acknowledgement lost after durable creation.");
			}

			return Task.CompletedTask;
		};
		await Should.ThrowAsync<IOException>(() => coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.AfterCreate = null;
		if (publication)
		{
			// The published state permits a real later append before the migrator retries.
			store.PutEvents(store.TypedKey, [Event(0), Event(1), Event(2), Event(3)]);
		}

		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var result = await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		(await store.DecodeAsync(result.ShouldNotBeNull(), CancellationToken.None)).Count.ShouldBe(publication ? 4 : 2);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RecognizePublicationRacingTheOrphanInspection(bool afterConflictReceiptRead)
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		if (afterConflictReceiptRead)
		{
			store.PutEvents(store.TypedKey, [Event(0), Event(2)]);
			var receiptReads = 0;
			store.AfterRead = async key =>
			{
				if (key == store.ReceiptKey && ++receiptReads == 2)
				{
					store.AfterRead = null;
					await new ColdArchiveMigrationCoordinator(store).MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
					store.PutEvents(store.TypedKey, [Event(0), Event(1), Event(2)]);
				}
			};
		}
		else
		{
			store.BeforeCreate = async key =>
			{
				if (key == store.TypedKey)
				{
					store.BeforeCreate = null;
					await new ColdArchiveMigrationCoordinator(store).MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
					store.PutEvents(store.TypedKey, [Event(0), Event(1), Event(2)]);
				}
			};
		}

		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var result = await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		(await store.DecodeAsync(result.ShouldNotBeNull(), CancellationToken.None)).Count.ShouldBe(3);
	}

	[Theory]
	[InlineData("source-missing")]
	[InlineData("source-revision")]
	[InlineData("source-bytes")]
	[InlineData("destination-missing")]
	[InlineData("destination-suffix")]
	[InlineData("destination-changed")]
	public async Task RejectBrokenPublishedHistoryEvenWhenAnotherTypeIsRequested(string damage)
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var source = store.Objects[store.LegacyKey];
		switch (damage)
		{
			case "source-missing":
				store.Objects.Remove(store.LegacyKey);
				break;
			case "source-revision":
				store.Put(store.LegacyKey, source.CopyContent());
				break;
			case "source-bytes":
				store.Objects[store.LegacyKey] = new ColdArchiveMigrationObject(Encoding.UTF8.GetBytes("[]"), source.Revision);
				break;
			case "destination-missing":
				store.Objects.Remove(store.TypedKey);
				break;
			case "destination-suffix":
				store.PutEvents(store.TypedKey, [Event(2)]);
				break;
			default:
				store.PutEvents(store.TypedKey, [Event(0), Event(2) with { EventData = [9] }]);
				break;
		}

		var creates = store.CreateAttempts.Count;
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Other", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.CreateAttempts.Count.ShouldBe(creates);
	}

	[Theory]
	[InlineData("version")]
	[InlineData("algorithm")]
	[InlineData("namespace")]
	[InlineData("tenant")]
	[InlineData("aggregate")]
	[InlineData("type")]
	[InlineData("legacy-key")]
	[InlineData("typed-key")]
	public async Task RejectForeignReceiptReferencesBeforeFollowingThem(string field)
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var receipt = JsonSerializer.Deserialize(store.Objects[store.ReceiptKey].CopyContent(),
			ColdArchiveMigrationJsonContext.Default.ColdArchiveMigrationReceipt).ShouldNotBeNull();
		receipt = field switch
		{
			"version" => receipt with { FormatVersion = 2 },
			"algorithm" => receipt with { DigestAlgorithm = "Other" },
			"namespace" => receipt with { NamespaceId = "another-bucket" },
			"tenant" => receipt with { TenantId = "another-tenant" },
			"aggregate" => receipt with { AggregateId = "another-aggregate" },
			"type" => receipt with { AggregateType = "Other" },
			"legacy-key" => receipt with { LegacyKey = "foreign-legacy" },
			_ => receipt with { TypedKey = "foreign-typed" },
		};
		store.Put(store.ReceiptKey, JsonSerializer.SerializeToUtf8Bytes(receipt,
			ColdArchiveMigrationJsonContext.Default.ColdArchiveMigrationReceipt));
		store.Reads.Clear();
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.Reads.ShouldBe([store.GetLayoutKey(), store.ReceiptKey]);
	}

	[Theory]
	[InlineData("null")]
	[InlineData("{")]
	[InlineData("{}")]
	public async Task NeverTreatMalformedReceiptsAsAbsence(string content)
	{
		var store = CreateStore();
		store.Put(store.ReceiptKey, Encoding.UTF8.GetBytes(content));
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<Exception>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
		store.CreateAttempts.ShouldBeEmpty();
	}

	[Fact]
	public async Task RefusePublicationWhenTheSourceChangesAfterCopy()
	{
		var store = CreateStore();
		store.AfterCreate = key =>
		{
			if (key == store.TypedKey)
			{
				store.PutEvents(store.LegacyKey, [Event(0), Event(1), Event(2)]);
			}

			return Task.CompletedTask;
		};
		await Should.ThrowAsync<InvalidOperationException>(() => new ColdArchiveMigrationCoordinator(store)
			.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.Objects.ContainsKey(store.ReceiptKey).ShouldBeFalse();
	}

	[Fact]
	public async Task RefuseAnUnpublishedSuffixWithoutOverwritingIt()
	{
		var store = CreateStore();
		store.PutEvents(store.TypedKey, [Event(2)]);
		var suffix = store.Objects[store.TypedKey];
		await Should.ThrowAsync<InvalidOperationException>(() => new ColdArchiveMigrationCoordinator(store)
			.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.Objects[store.TypedKey].ShouldBeSameAs(suffix);
		store.Objects.ContainsKey(store.ReceiptKey).ShouldBeFalse();
	}

	[Fact]
	public async Task PermitAnotherTypeOnlyAfterCheckingTheOriginalMigration()
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Other", CancellationToken.None));
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		(await coordinator.ReadTypedAsync(Tenant, "aggregate", "Other", CancellationToken.None)).ShouldBeNull();
		var otherKey = store.GetTypedKey(Tenant, "aggregate", "Other");
		store.PutEvents(otherKey, [Event(0) with { AggregateType = "Other" }]);
		(await coordinator.ReadTypedAsync(Tenant, "aggregate", "Other", CancellationToken.None)).ShouldNotBeNull();
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.MigrateAsync(Tenant, "aggregate", "Other", CancellationToken.None));
	}

	[Fact]
	public async Task AllowAFreshTypedStreamWithoutInventingALegacyMigration()
	{
		var store = new MemoryStorage();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.ActivateTypedLayoutAsync(CancellationToken.None);
		(await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None)).ShouldBeNull();
		store.PutEvents(store.TypedKey, [Event(0)]);
		(await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None)).ShouldNotBeNull();
		store.Objects.ContainsKey(store.ReceiptKey).ShouldBeFalse();
		await Should.ThrowAsync<InvalidOperationException>(() => new ColdArchiveMigrationCoordinator(store)
			.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
	}

	[Fact]
	public async Task RequireExplicitActivationWithoutWritingFromReadsOrMigration()
	{
		var store = new MemoryStorage();
		store.PutEvents(store.LegacyKey, [Event(0)]);
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.CreateAttempts.ShouldBeEmpty();
		await coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None);
	}

	[Fact]
	public async Task RecoverLostActivationAcknowledgementWithoutReplacingMarker()
	{
		var store = new MemoryStorage();
		store.AfterCreate = _ => throw new IOException("Lost activation acknowledgement");
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<IOException>(() => coordinator.ActivateTypedLayoutAsync(CancellationToken.None));
		var published = store.Objects[store.GetLayoutKey()];
		store.AfterCreate = null;
		await new ColdArchiveMigrationCoordinator(store).ActivateTypedLayoutAsync(CancellationToken.None);
		store.Objects[store.GetLayoutKey()].ShouldBeSameAs(published);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
	}

	[Fact]
	public async Task KeepActivationAfterInterruptionAndResumeUnmigratedStreams()
	{
		var store = new MemoryStorage();
		store.PutEvents(store.LegacyKey, [Event(0)]);
		await new ColdArchiveMigrationCoordinator(store).ActivateTypedLayoutAsync(CancellationToken.None);
		var restarted = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<InvalidOperationException>(() => restarted.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => restarted.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.Objects.ContainsKey(store.ReceiptKey).ShouldBeFalse();
		await restarted.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		(await restarted.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None)).ShouldNotBeNull();
	}

	[Theory]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("malformed")]
	[InlineData("{\"FormatVersion\":2,\"NamespaceId\":\"test://account/bucket/prefix\",\"Layout\":\"TypedV2\"}")]
	[InlineData("{\"FormatVersion\":1,\"NamespaceId\":\"other\",\"Layout\":\"TypedV2\"}")]
	[InlineData("{\"FormatVersion\":1,\"NamespaceId\":\"test://account/bucket/prefix\",\"Layout\":\"Legacy\"}")]
	[InlineData("{\"FormatVersion\":2,\"FormatVersion\":1,\"NamespaceId\":\"test://account/bucket/prefix\",\"Layout\":\"TypedV2\"}")]
	[InlineData("{\"FormatVersion\":1,\"NamespaceId\":\"test://account/bucket/prefix\",\"Layout\":\"TypedV2\",\"FutureField\":1}")]
	public async Task RejectConflictingOrMalformedActivationWithoutOverwritingIt(string json)
	{
		var store = new MemoryStorage();
		store.Put(store.GetLayoutKey(), Encoding.UTF8.GetBytes(json));
		var original = store.Objects[store.GetLayoutKey()];
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<Exception>(() => coordinator.ActivateTypedLayoutAsync(CancellationToken.None));
		await Should.ThrowAsync<Exception>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
		store.Objects[store.GetLayoutKey()].ShouldBeSameAs(original);
		store.CreateAttempts.ShouldBe([store.GetLayoutKey()]);
	}

	[Fact]
	public async Task NeverCacheNegativeActivationChecks()
	{
		var store = new MemoryStorage();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None);
		await coordinator.ActivateTypedLayoutAsync(CancellationToken.None);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "another-stream", CancellationToken.None));
	}

	[Fact]
	public async Task KeepOldReceiptGuardEvenWhenNamespaceMarkerIsAbsent()
	{
		var store = new MemoryStorage();
		store.Put(store.ReceiptKey, "malformed"u8.ToArray());
		await Should.ThrowAsync<InvalidOperationException>(() => new ColdArchiveMigrationCoordinator(store)
			.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
	}

	[Fact]
	public void OwnObjectBytesAndReturnCopies()
	{
		byte[] bytes = [1, 2];
		var snapshot = new ColdArchiveMigrationObject(bytes, "revision");
		var digest = snapshot.Sha256;
		bytes[0] = 9;
		snapshot.CopyContent()[0] = 8;
		snapshot.CopyContent().ShouldBe(new byte[] { 1, 2 });
		snapshot.Sha256.ShouldBe(digest);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectUnsupportedArchiveFieldsRatherThanLoseThemOnRewrite(bool published)
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		if (published)
		{
			await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		}

		var key = published ? store.TypedKey : store.LegacyKey;
		var original = Encoding.UTF8.GetString(store.Objects[key].CopyContent());
		store.Put(key, Encoding.UTF8.GetBytes(original.Replace("\"EventId\":", "\"FutureField\":1,\"EventId\":", StringComparison.Ordinal)));
		var creates = store.CreateAttempts.Count;
		await Should.ThrowAsync<JsonException>(() => published
			? coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None)
			: coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		store.CreateAttempts.Count.ShouldBe(creates);
	}

	[Fact]
	public async Task RejectUnsupportedReceiptFields()
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var json = Encoding.UTF8.GetString(store.Objects[store.ReceiptKey].CopyContent());
		store.Put(store.ReceiptKey, Encoding.UTF8.GetBytes(json[..^1] + ",\"FutureField\":1}"));
		await Should.ThrowAsync<JsonException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
	}

	[Fact]
	public async Task RejectConflictingDuplicateReceiptProperties()
	{
		var store = CreateStore();
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var json = Encoding.UTF8.GetString(store.Objects[store.ReceiptKey].CopyContent());
		store.Put(store.ReceiptKey, Encoding.UTF8.GetBytes(json.Replace("\"FormatVersion\":1", "\"FormatVersion\":2,\"FormatVersion\":1", StringComparison.Ordinal)));
		await Should.ThrowAsync<JsonException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
	}

	[Fact]
	public async Task PropagateStorageFailuresInsteadOfTreatingThemAsAbsence()
	{
		var store = CreateStore();
		store.AfterRead = _ => throw new UnauthorizedAccessException("Storage authorization failed.");
		var coordinator = new ColdArchiveMigrationCoordinator(store);
		await Should.ThrowAsync<UnauthorizedAccessException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<UnauthorizedAccessException>(() => coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		await Should.ThrowAsync<UnauthorizedAccessException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));
		store.CreateAttempts.ShouldBeEmpty();
	}

	private static MemoryStorage CreateStore()
	{
		var store = new MemoryStorage();
		store.Put(store.GetLayoutKey(), JsonSerializer.SerializeToUtf8Bytes(
			new ColdArchiveLayoutMarker(1, store.NamespaceId, "TypedV2"), ColdArchiveMigrationJsonContext.Default.ColdArchiveLayoutMarker));
		store.PutEvents(store.LegacyKey, [Event(0), Event(2)]);
		return store;
	}

	private static StoredEvent Event(long version) =>
		new($"event-{version}", "aggregate", "Order", "Created", [1, 2], null, version, DateTimeOffset.UnixEpoch);

	private sealed class MemoryStorage : IColdArchiveMigrationStorage
	{
		private static readonly JsonSerializerOptions DecodeOptions = new()
		{
			UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		};

		private int _revision;
		public string NamespaceId => "test://account/bucket/prefix";
		public string GetLayoutKey() => "layout-v1.json";
		public Dictionary<string, ColdArchiveMigrationObject> Objects { get; } = new(StringComparer.Ordinal);
		public List<string> Reads { get; } = [];
		public List<string> CreateAttempts { get; } = [];
		public Func<string, Task>? AfterRead { get; set; }
		public Func<string, Task>? BeforeCreate { get; set; }
		public Func<string, Task>? AfterCreate { get; set; }
		public string LegacyKey => GetLegacyKey(Tenant, "aggregate");
		public string TypedKey => GetTypedKey(Tenant, "aggregate", "Order");
		public string ReceiptKey => GetReceiptKey(Tenant, "aggregate");
		public string GetLegacyKey(KeyedTenantPartition tenant, string aggregateId) =>
			$"legacy/{ColdStorageKey.TenantSegment(tenant)}/{ColdStorageKey.AggregateSegment(aggregateId)}";
		public string GetTypedKey(KeyedTenantPartition tenant, string aggregateId, string aggregateType) =>
			ColdStorageKey.StreamPath(tenant, aggregateType, aggregateId);
		public string GetReceiptKey(KeyedTenantPartition tenant, string aggregateId) => GetLegacyKey(tenant, aggregateId) + ".receipt";

		public async Task<ColdArchiveMigrationObject?> ReadAsync(string key, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Reads.Add(key);
			Objects.TryGetValue(key, out var result);
			if (AfterRead is not null)
			{
				await AfterRead(key);
			}

			return result;
		}

		public async Task<bool> TryCreateAsync(string key, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CreateAttempts.Add(key);
			if (BeforeCreate is not null)
			{
				await BeforeCreate(key);
			}

			if (Objects.ContainsKey(key))
			{
				return false;
			}

			Put(key, content);
			if (AfterCreate is not null)
			{
				await AfterCreate(key);
			}

			return true;
		}

		public Task<IReadOnlyList<StoredEvent>> DecodeAsync(ColdArchiveMigrationObject archive, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			IReadOnlyList<StoredEvent> events = JsonSerializer.Deserialize<StoredEvent[]>(archive.CopyContent(), DecodeOptions)!;
			return Task.FromResult(events);
		}

		public void Put(string key, ReadOnlyMemory<byte> bytes) => Objects[key] = new ColdArchiveMigrationObject(bytes, (++_revision).ToString(System.Globalization.CultureInfo.InvariantCulture));
		public void PutEvents(string key, IReadOnlyList<StoredEvent> events) => Put(key, JsonSerializer.SerializeToUtf8Bytes(events));
	}
}
