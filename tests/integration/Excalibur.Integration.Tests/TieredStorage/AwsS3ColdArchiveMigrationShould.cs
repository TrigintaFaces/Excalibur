// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

using Amazon.S3;
using Amazon.S3.Model;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.AwsS3;
using Excalibur.EventSourcing.AwsS3.DependencyInjection;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Testcontainers.LocalStack;

using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class AwsS3ColdArchiveMigrationShould
{
	private static readonly KeyedTenantPartition Tenant = KeyedTenantPartition.Scoped("tenant-a");
	private static readonly byte[] ConflictingBytes = [9];
	private const string Bucket = "cold-migration";

	[Fact]
	public async Task UseRegisteredFrozenLayoutAndRefuseLostRetainedHistory()
	{
		await using var server = new LocalStackBuilder().WithImage("localstack/localstack:4").Build();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		await server.StartAsync(timeout.Token);
		using var client = Client(server.GetConnectionString());
		await client.PutBucketAsync(Bucket, timeout.Token);
		var legacy = new AwsS3ColdEventStore(client, Bucket, "runtime", NullLogger<AwsS3ColdEventStore>.Instance);
		(await legacy.WriteAsync(Tenant, "aggregate", "Order", [Event(0), Event(2)], timeout.Token)).ShouldBe(0);
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburEventSourcing(es => es.UseAwsS3ColdEventStore(s3 =>
			s3.Client(client).BucketName(Bucket).KeyPrefix("runtime").Layout(ColdArchiveLayout.TypedV2)));
		await using var provider = services.BuildServiceProvider();
		var typed = provider.GetRequiredService<IColdEventStore>();
		provider.GetRequiredService<IOptions<AwsS3ColdEventStoreOptions>>().Value.Layout = ColdArchiveLayout.Legacy;
		await Should.ThrowAsync<InvalidOperationException>(() => typed.ReadAsync(Tenant, "aggregate", "Order", timeout.Token));
		var migration = (IColdEventStoreMigration)typed;
		await migration.ActivateTypedLayoutAsync(timeout.Token);
		await Should.ThrowAsync<InvalidOperationException>(() => typed.HasArchivedEventsAsync(Tenant, "aggregate", "Order", timeout.Token));
		await Should.ThrowAsync<InvalidOperationException>(() => legacy.WriteAsync(Tenant, "aggregate", "Order", [], timeout.Token));
		await migration.MigrateAsync(Tenant, "aggregate", "Order", timeout.Token);
		(await typed.WriteAsync(Tenant, "aggregate", "Order", [Event(1)], timeout.Token)).ShouldBe(2);
		(await typed.WriteAsync(Tenant, "aggregate", "Order", [], timeout.Token)).ShouldBe(-1);
		(await typed.ReadAsync(Tenant, "aggregate", "Order", 0, timeout.Token)).Select(e => e.Version).ShouldBe(new long[] { 1, 2 });
		var other = Event(0) with { AggregateType = "Other", EventId = "other" };
		(await typed.WriteAsync(Tenant, "aggregate", "Other", [other], timeout.Token)).ShouldBe(0);
		(await typed.ReadAsync(Tenant, "aggregate", "Other", timeout.Token)).Single().EventId.ShouldBe("other");

		var adapter = new AwsS3ColdArchiveMigrationStorage(client, Bucket, "runtime");
		var fresh = Event(0) with { AggregateId = "fresh", EventId = "fresh-event" };
		(await typed.WriteAsync(Tenant, "fresh", "Order", [fresh], timeout.Token)).ShouldBe(0);
		(await adapter.ReadAsync(adapter.GetReceiptKey(Tenant, "fresh"), timeout.Token)).ShouldBeNull();
		(await adapter.ReadAsync(adapter.GetLegacyKey(Tenant, "fresh"), timeout.Token)).ShouldBeNull();
		await client.DeleteObjectAsync(Bucket, adapter.GetLegacyKey(Tenant, "aggregate"), timeout.Token);
		await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, "aggregate", "Order", [Event(1)], timeout.Token));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.WriteAsync(Tenant, "aggregate", "Order", [], timeout.Token));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.ReadAsync(Tenant, "aggregate", "Order", 100, timeout.Token));
		await Should.ThrowAsync<InvalidOperationException>(() => typed.HasArchivedEventsAsync(Tenant, "aggregate", "Other", timeout.Token));
		var restarted = new AwsS3ColdEventStore(client, Bucket, "runtime", NullLogger<AwsS3ColdEventStore>.Instance);
		await Should.ThrowAsync<InvalidOperationException>(() => restarted.ReadAsync(Tenant, "aggregate", "Order", timeout.Token));
		await Should.ThrowAsync<InvalidOperationException>(() => restarted.ReadAsync(Tenant, "fresh", "Order", timeout.Token));
	}

	[Fact]
	public async Task MigrateOriginalGzipBytesAndValidateAppendedBaseline()
	{
		await using var server = new LocalStackBuilder().WithImage("localstack/localstack:4").Build();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		await server.StartAsync(timeout.Token);
		using var client = Client(server.GetConnectionString());
		await client.PutBucketAsync(Bucket, timeout.Token);
		var adapter = new AwsS3ColdArchiveMigrationStorage(client, new Uri(server.GetConnectionString()), Bucket, "events");
		var legacy = adapter.GetLegacyKey(Tenant, "aggregate");
		var typed = adapter.GetTypedKey(Tenant, "aggregate", "Order");
		var original = Archive(Event(0), Event(2));
		using var originalStream = new MemoryStream(original);
		var put = new PutObjectRequest { BucketName = Bucket, Key = legacy, InputStream = originalStream };
		put.Headers.ContentEncoding = "gzip";
		var uploaded = await client.PutObjectAsync(put, timeout.Token);
		var observed = (await adapter.ReadAsync(legacy, timeout.Token)).ShouldNotBeNull();
		observed.CopyContent().ShouldBe(original);
		observed.Revision.ShouldBe(uploaded.ETag);

		var coordinator = new ColdArchiveMigrationCoordinator(adapter);
		await coordinator.ActivateTypedLayoutAsync(timeout.Token);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", timeout.Token);
		var migrated = (await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", timeout.Token)).ShouldNotBeNull();
		migrated.CopyContent().ShouldBe(original);
		(await adapter.TryCreateAsync(typed, ConflictingBytes, timeout.Token)).ShouldBeFalse();
		(await adapter.ReadAsync(typed, timeout.Token)).ShouldNotBeNull().CopyContent().ShouldBe(original);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.EnsureLegacyAllowedAsync(Tenant, "aggregate", timeout.Token));

		// S3 ETags can be content-derived: change the bytes rather than assuming a rewrite changes ETag.
		var extended = Archive(Event(0), Event(1), Event(2), Event(3));
		using var extendedStream = new MemoryStream(extended);
		await client.PutObjectAsync(new PutObjectRequest
		{
			BucketName = Bucket, Key = typed, InputStream = extendedStream, IfMatch = migrated.Revision,
		}, timeout.Token);
		using var staleStream = new MemoryStream(original);
		var stale = await Should.ThrowAsync<AmazonS3Exception>(() => client.PutObjectAsync(new PutObjectRequest
		{
			BucketName = Bucket, Key = typed, InputStream = staleStream, IfMatch = migrated.Revision,
		}, timeout.Token));
		stale.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
		await coordinator.MigrateAsync(Tenant, "aggregate", "Order", timeout.Token);
		(await coordinator.ReadTypedAsync(Tenant, "aggregate", "Order", timeout.Token)).ShouldNotBeNull().CopyContent().ShouldBe(extended);
		(await adapter.ReadAsync(legacy, timeout.Token)).ShouldNotBeNull().CopyContent().ShouldBe(original);

		// A migrated destination is required even when a caller requests another aggregate type.
		await client.DeleteObjectAsync(Bucket, typed, timeout.Token);
		await Should.ThrowAsync<InvalidOperationException>(() => coordinator.ReadTypedAsync(Tenant, "aggregate", "Other", timeout.Token));
	}

	[Fact]
	public async Task DistinguishAbsentObjectFromMissingBucket()
	{
		await using var server = new LocalStackBuilder().WithImage("localstack/localstack:4").Build();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		await server.StartAsync(timeout.Token);
		using var client = Client(server.GetConnectionString());
		await client.PutBucketAsync(Bucket, timeout.Token);
		var adapter = new AwsS3ColdArchiveMigrationStorage(client, new Uri(server.GetConnectionString()), Bucket, "events");
		(await adapter.ReadAsync("absent", timeout.Token)).ShouldBeNull();
		await client.DeleteBucketAsync(Bucket, timeout.Token);
		var missing = await Should.ThrowAsync<AmazonS3Exception>(() => adapter.ReadAsync("absent", timeout.Token));
		missing.ErrorCode.ShouldBe("NoSuchBucket");
		await Should.ThrowAsync<AmazonS3Exception>(() => adapter.TryCreateAsync("absent", ConflictingBytes, timeout.Token));
	}

	[Theory]
	[InlineData("null")]
	[InlineData("[] []")]
	public async Task RejectUnsupportedJson(string json)
	{
		using var client = Client("http://localhost:4566");
		var adapter = new AwsS3ColdArchiveMigrationStorage(client, new Uri("http://localhost:4566"), Bucket, "events");
		await Should.ThrowAsync<JsonException>(() => adapter.DecodeAsync(
			new ColdArchiveMigrationObject(Compress(Encoding.UTF8.GetBytes(json)), "revision"), CancellationToken.None));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectUnknownOrDuplicateFieldsInOtherwiseValidEvents(bool duplicate)
	{
		using var client = Client("http://localhost:4566");
		var adapter = new AwsS3ColdArchiveMigrationStorage(client, new Uri("http://localhost:4566"), Bucket, "events");
		var valid = JsonSerializer.Serialize(new List<StoredEvent> { Event(0) }, AwsS3ColdEventStore.ArchiveTypeInfo);
		var control = await adapter.DecodeAsync(new ColdArchiveMigrationObject(Compress(Encoding.UTF8.GetBytes(valid)), "revision"), CancellationToken.None);
		control.Count.ShouldBe(1);
		var invalid = valid.Replace("\"eventId\":", duplicate ? "\"eventId\":\"conflicting\",\"eventId\":" : "\"futureField\":1,\"eventId\":", StringComparison.Ordinal);
		invalid.ShouldNotBe(valid);
		await Should.ThrowAsync<JsonException>(() => adapter.DecodeAsync(
			new ColdArchiveMigrationObject(Compress(Encoding.UTF8.GetBytes(invalid)), "revision"), CancellationToken.None));
	}

	private static AmazonS3Client Client(string endpoint) => new("test", "test", new AmazonS3Config
	{
		ServiceURL = endpoint, ForcePathStyle = true, UseHttp = true, Timeout = TimeSpan.FromSeconds(15), MaxErrorRetry = 1,
	});

	private static StoredEvent Event(long version) => new($"event-{version}", "aggregate", "Order", "Created", [1, 2], null, version, DateTimeOffset.UnixEpoch);
	private static byte[] Archive(params StoredEvent[] events) => Compress(JsonSerializer.SerializeToUtf8Bytes(events.ToList(), AwsS3ColdEventStore.ArchiveTypeInfo));
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
