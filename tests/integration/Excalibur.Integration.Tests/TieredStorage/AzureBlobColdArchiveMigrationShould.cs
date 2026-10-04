// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.IO.Compression;
using System.Text;
using System.Text.Json;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.AzureBlob;
using Excalibur.EventSourcing.AzureBlob.DependencyInjection;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Testcontainers.Azurite;

using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class AzureBlobColdArchiveMigrationShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.Scoped("tenant-a");

	[Fact]
	public async Task RevalidateRetainedHistoryAfterAConditionalUploadConflict()
	{
		await using var server = CreateServer();
		await server.StartAsync();
		var container = new BlobContainerClient(server.GetConnectionString(), "retry-history");
		await container.CreateAsync();
		var legacy = new AzureBlobColdEventStore(container, NullLogger<AzureBlobColdEventStore>.Instance);
		await legacy.WriteAsync(Tenant, "aggregate", "Order", [Event(0)], CancellationToken.None);
		await legacy.ActivateTypedLayoutAsync(CancellationToken.None);
		await legacy.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var adapter = new AzureBlobColdArchiveMigrationStorage(container);
		var typedKey = adapter.GetTypedKey(Tenant, "aggregate", "Order");
		var typedBlob = container.GetBlobClient(typedKey);
		var before = await typedBlob.DownloadContentAsync();
		var receiptBefore = await container.GetBlobClient(adapter.GetReceiptKey(Tenant, "aggregate")).DownloadContentAsync();
		var conflict = new ConflictBlobClient(typedBlob, async ct =>
		{
			// A genuine competing write invalidates the already captured ETag.
			await typedBlob.UploadAsync(before.Value.Content, overwrite: true, cancellationToken: ct);
			await container.GetBlobClient(adapter.GetLegacyKey(Tenant, "aggregate")).DeleteAsync(cancellationToken: ct);
		});
		var intercepted = new ConflictContainerClient(container, typedKey, conflict);
		var typed = new AzureBlobColdEventStore(intercepted, NullLogger<AzureBlobColdEventStore>.Instance, ColdArchiveLayout.TypedV2);
		await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, "aggregate", "Order", [Event(1)], CancellationToken.None));
		conflict.UploadAttempts.ShouldBe(1);
		conflict.ObservedConflict.ShouldBeTrue();
		(await typedBlob.DownloadContentAsync()).Value.Content.ToArray().ShouldBe(before.Value.Content.ToArray());
		(await container.GetBlobClient(adapter.GetReceiptKey(Tenant, "aggregate")).DownloadContentAsync()).Value.Details.ETag
			.ShouldBe(receiptBefore.Value.Details.ETag);
	}

	[Fact]
	public async Task CutOverRuntimeOperationsAndRejectLostRetainedHistory()
	{
		await using var server = CreateServer();
		await server.StartAsync();
		var container = new BlobContainerClient(server.GetConnectionString(), "runtime");
		await container.CreateAsync();
		var legacy = new AzureBlobColdEventStore(container, NullLogger<AzureBlobColdEventStore>.Instance);
		var typed = new AzureBlobColdEventStore(container, NullLogger<AzureBlobColdEventStore>.Instance, ColdArchiveLayout.TypedV2);
		(await legacy.WriteAsync(Tenant, "aggregate", "Order", [Event(0), Event(2)], CancellationToken.None)).ShouldBe(0);
		await Should.ThrowAsync<InvalidOperationException>(() => typed.ReadAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		IColdEventStoreMigration migration = legacy;
		await migration.ActivateTypedLayoutAsync(CancellationToken.None);
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.WriteAsync(Tenant, "aggregate", "Order", [], CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.HasArchivedEventsAsync(Tenant, "aggregate", "Order", CancellationToken.None));
		await migration.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		(await typed.WriteAsync(Tenant, "aggregate", "Order", [Event(1)], CancellationToken.None)).ShouldBe(2);
		(await typed.WriteAsync(Tenant, "aggregate", "Order", [], CancellationToken.None)).ShouldBe(-1);
		(await typed.ReadAsync(Tenant, "aggregate", "Order", 0, CancellationToken.None)).Select(e => e.Version).ShouldBe(new long[] { 1, 2 });
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.ReadAsync(Tenant, "aggregate", "Order", CancellationToken.None));

		var adapter = new AzureBlobColdArchiveMigrationStorage(container);
		await container.GetBlobClient(adapter.GetLegacyKey(Tenant, "aggregate")).DeleteAsync();
		await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, "aggregate", "Order", [Event(1)], CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, "aggregate", "Order", [], CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.ReadAsync(Tenant, "aggregate", "Order", 100, CancellationToken.None));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.HasArchivedEventsAsync(Tenant, "aggregate", "Order", CancellationToken.None));
	}

	[Fact]
	public async Task KeepFreshTypedStreamsDistinctAndBlockLegacyRestart()
	{
		await using var server = CreateServer();
		await server.StartAsync();
		var container = new BlobContainerClient(server.GetConnectionString(), "fresh");
		await container.CreateAsync();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburEventSourcing(es => es.UseAzureBlobColdEventStore(blob =>
			blob.ConnectionString(server.GetConnectionString()).ContainerName("fresh")
				.CreateContainerIfNotExists(false).Layout(ColdArchiveLayout.TypedV2)));
		await using var provider = services.BuildServiceProvider();
		var typed = provider.GetRequiredService<IColdEventStore>();
		// Options mutation cannot silently switch the already captured runtime layout.
		provider.GetRequiredService<IOptions<AzureBlobColdEventStoreOptions>>().Value.Layout = ColdArchiveLayout.Legacy;
		await ((IColdEventStoreMigration)typed).ActivateTypedLayoutAsync(CancellationToken.None);
		(await typed.WriteAsync(Tenant, "aggregate", "Order", [Event(0)], CancellationToken.None)).ShouldBe(0);
		var other = Event(0) with { AggregateType = "Other", EventId = "other-event" };
		(await typed.WriteAsync(Tenant, "aggregate", "Other", [other], CancellationToken.None)).ShouldBe(0);
		(await typed.ReadAsync(Tenant, "aggregate", "Order", CancellationToken.None)).Single().EventId.ShouldBe("event-0");
		(await typed.ReadAsync(Tenant, "aggregate", "Other", CancellationToken.None)).Single().EventId.ShouldBe("other-event");
		var restartedLegacy = new AzureBlobColdEventStore(container, NullLogger<AzureBlobColdEventStore>.Instance);
		await Should.ThrowAsync<InvalidOperationException>(() => restartedLegacy.ReadAsync(Tenant, "aggregate", "Order", CancellationToken.None));
	}

	[Fact]
	public async Task MigrateExactBytesAndPreserveTheReadRevision()
	{
		await using var server = CreateServer();
		await server.StartAsync();
		var container = new BlobContainerClient(server.GetConnectionString(), "migration");
		await container.CreateAsync();
		var adapter = new AzureBlobColdArchiveMigrationStorage(container);
		var bytes = Compress(JsonSerializer.SerializeToUtf8Bytes(new List<StoredEvent> { Event(0), Event(2) }, AzureBlobColdEventStore.ArchiveTypeInfo));
		var legacyKey = adapter.GetLegacyKey(Tenant, "aggregate");
		var typedKey = adapter.GetTypedKey(Tenant, "aggregate", "Order");
		var uploaded = await container.GetBlobClient(legacyKey).UploadAsync(BinaryData.FromBytes(bytes),
			new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentEncoding = "gzip" } });
		var read = (await adapter.ReadAsync(legacyKey, CancellationToken.None)).ShouldNotBeNull();
		read.CopyContent().ShouldBe(bytes);
		read.Revision.ShouldBe(uploaded.Value.ETag.ToString());
		var coordinator = new ColdArchiveMigrationCoordinator(adapter);
		await coordinator.ActivateTypedLayoutAsync(CancellationToken.None);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", CancellationToken.None);
		var migrated = (await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", CancellationToken.None)).ShouldNotBeNull();
		migrated.CopyContent().ShouldBe(bytes);
		(await adapter.TryCreateAsync(typedKey, new byte[] { 9 }, CancellationToken.None)).ShouldBeFalse();
		(await adapter.ReadAsync(typedKey, CancellationToken.None)).ShouldNotBeNull().CopyContent().ShouldBe(bytes);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", CancellationToken.None));

		// The returned token belongs to the verified bytes; a later replacement invalidates it.
		await container.GetBlobClient(typedKey).UploadAsync(BinaryData.FromBytes(bytes), overwrite: true);
		var stale = await Should.ThrowAsync<RequestFailedException>(() => container.GetBlobClient(typedKey).UploadAsync(
			BinaryData.FromBytes(bytes), new BlobUploadOptions { Conditions = new BlobRequestConditions { IfMatch = new ETag(migrated.Revision) } }));
		stale.Status.ShouldBe(412);
	}

	[Fact]
	public async Task DistinguishAbsentBlobFromMissingContainer()
	{
		await using var server = CreateServer();
		await server.StartAsync();
		var container = new BlobContainerClient(server.GetConnectionString(), "migration");
		await container.CreateAsync();
		var adapter = new AzureBlobColdArchiveMigrationStorage(container);
		(await adapter.ReadAsync("absent", CancellationToken.None)).ShouldBeNull();
		await container.DeleteAsync();
		var missing = await Should.ThrowAsync<RequestFailedException>(() => adapter.ReadAsync("absent", CancellationToken.None));
		missing.ErrorCode.ShouldBe(BlobErrorCode.ContainerNotFound.ToString());
		await Should.ThrowAsync<RequestFailedException>(() => adapter.TryCreateAsync("absent", new byte[] { 1 }, CancellationToken.None));
	}

	[Fact]
	public void BindNamespaceToContainerButNotCredentialRenewal()
	{
		var first = new AzureBlobColdArchiveMigrationStorage(new BlobContainerClient(new Uri("https://account.blob.core.windows.net/archive?sig=first")));
		var renewed = new AzureBlobColdArchiveMigrationStorage(new BlobContainerClient(new Uri("https://account.blob.core.windows.net/archive?sig=second")));
		var other = new AzureBlobColdArchiveMigrationStorage(new BlobContainerClient(new Uri("https://account.blob.core.windows.net/other?sig=first")));
		first.NamespaceId.ShouldBe(renewed.NamespaceId);
		first.NamespaceId.ShouldNotBe(other.NamespaceId);
		first.NamespaceId.ShouldNotContain("sig");
		first.GetTypedKey(Tenant, "aggregate", "Order").ShouldNotBe(first.GetTypedKey(Tenant, "aggregate", "Other"));
		first.GetReceiptKey(Tenant, "aggregate").ShouldNotBe(first.GetLegacyKey(Tenant, "aggregate"));
	}

	[Theory]
	[InlineData("null")]
	[InlineData("[] []")]
	[InlineData("[{\"futureField\":1}]")]
	public async Task RejectUnsupportedArchiveContent(string json)
	{
		var adapter = new AzureBlobColdArchiveMigrationStorage(new BlobContainerClient(new Uri("https://account.blob.core.windows.net/archive")));
		await Should.ThrowAsync<JsonException>(() => adapter.DecodeAsync(new ColdArchiveMigrationObject(Compress(Encoding.UTF8.GetBytes(json)), "revision"), CancellationToken.None));
	}

	[Fact]
	public async Task RejectConflictingDuplicateEventProperties()
	{
		var adapter = new AzureBlobColdArchiveMigrationStorage(new BlobContainerClient(new Uri("https://account.blob.core.windows.net/archive")));
		var json = JsonSerializer.Serialize(new List<StoredEvent> { Event(0) }, AzureBlobColdEventStore.ArchiveTypeInfo);
		json = json.Replace("\"eventId\":", "\"eventId\":\"conflicting\",\"eventId\":", StringComparison.Ordinal);
		await Should.ThrowAsync<JsonException>(() => adapter.DecodeAsync(new ColdArchiveMigrationObject(Compress(Encoding.UTF8.GetBytes(json)), "revision"), CancellationToken.None));
	}

	private sealed class ConflictContainerClient(BlobContainerClient inner, string typedKey, BlobClient intercepted)
		: BlobContainerClient(inner.Uri)
	{
		public override BlobClient GetBlobClient(string blobName) => blobName == typedKey ? intercepted : inner.GetBlobClient(blobName);
	}

	private sealed class ConflictBlobClient(BlobClient inner, Func<CancellationToken, Task> beforeFirstUpload)
		: BlobClient(inner.Uri)
	{
		internal int UploadAttempts { get; private set; }
		internal bool ObservedConflict { get; private set; }

		public override Task<Response<BlobDownloadResult>> DownloadContentAsync(CancellationToken cancellationToken) =>
			inner.DownloadContentAsync(cancellationToken);

		public override async Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobUploadOptions options,
			CancellationToken cancellationToken = default)
		{
			UploadAttempts++;
			if (UploadAttempts == 1)
			{
				await beforeFirstUpload(cancellationToken);
			}

			try
			{
				return await inner.UploadAsync(content, options, cancellationToken);
			}
			catch (RequestFailedException ex) when (ex.Status == 412)
			{
				ObservedConflict = true;
				throw;
			}
		}
	}

	private static AzuriteContainer CreateServer() => new AzuriteBuilder()
		.WithImage("mcr.microsoft.com/azure-storage/azurite:3.36.0").WithCommand("--skipApiVersionCheck").Build();

	private static StoredEvent Event(long version) => new($"event-{version}", "aggregate", "Order", "Created", [1, 2], null, version, DateTimeOffset.UnixEpoch);

	private static byte[] Compress(byte[] bytes)
	{
		using var result = new MemoryStream();
		using (var gzip = new GZipStream(result, CompressionLevel.Optimal, leaveOpen: true))
		{
			gzip.Write(bytes);
		}

		return result.ToArray();
	}
}
