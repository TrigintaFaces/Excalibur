// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.AwsS3;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.LocalStack;

using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

/// <summary>
/// Integration tests for AWS S3 cold event store using LocalStack TestContainers.
/// Tests the full IColdEventStore contract through real S3-compatible storage.
/// Note: Uses reflection to instantiate internal AwsS3ColdEventStore since the
/// DI extension doesn't yet support custom endpoint URLs (Beads task tracked).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "AwsS3")]
[Trait("Component", "TieredStorage")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Disposed in DisposeAsync via IAsyncLifetime")]
public sealed class AwsS3ColdEventStoreIntegrationShould : IAsyncLifetime
{
	// A SCOPED tenant, not Untenanted. Untenanted compiles equally well and would leave the
	// tenant-keyed object path unexercised -- the shape the cold-store contract exists to enforce.
	private static readonly KeyedTenantPartition Tenant =
		KeyedTenantPartition.Scoped("cold-store-tenant");

	private const string BucketName = "cold-events-test";

	private LocalStackContainer? _container;
	private IAmazonS3? _s3Client;
	private IColdEventStore? _store;
	private bool _available;
	private Exception? _unavailableCause;

	/// <summary>Appends the captured startup cause to a skip reason, so the log says WHY.</summary>
	private string SkipReason(string reason) =>
		_unavailableCause is null ? reason : reason + " Cause: " + _unavailableCause.GetType().Name + ": " + _unavailableCause.Message;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new LocalStackBuilder()
				.WithImage("localstack/localstack:4")
				.Build();

			using var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			await _container.StartAsync(startCts.Token).ConfigureAwait(false);

			var config = new AmazonS3Config
			{
				ServiceURL = _container.GetConnectionString(),
				ForcePathStyle = true,
				UseHttp = true,
				Timeout = TimeSpan.FromSeconds(10),
				MaxErrorRetry = 1
			};
			_s3Client = new AmazonS3Client("test", "test", config);
			await _s3Client.PutBucketAsync(BucketName, startCts.Token).ConfigureAwait(false);

			_store = new AwsS3ColdEventStore(
				_s3Client, BucketName, "events",
				NullLogger<AwsS3ColdEventStore>.Instance);
			_available = true;
		}
		catch (Exception ex)
		{
			// Preserve the cause. Reporting every startup failure as "infrastructure unavailable"
			// makes a fixable fault -- an image pull, a port collision, a schema-init error --
			// indistinguishable from an absent daemon, and undiagnosable from the CI log.
			_unavailableCause = ex;
			_available = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			_s3Client?.Dispose();
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash
		}
	}

	/// <summary>
	/// Creates a cancellation token with a 30-second timeout to prevent test host hangs
	/// when LocalStack is slow or unresponsive.
	/// </summary>
	private static CancellationToken CreateTestTimeout() =>
		new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;

	[Fact]
	public async Task WriteAndReadEvents()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] S3 (LocalStack/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var ct = CreateTestTimeout();
		var events = CreateEvents("s3-agg-1", 1, 2, 3);
		await _store!.WriteAsync(Tenant, "s3-agg-1", events, ct);

		var read = await _store.ReadAsync(Tenant, "s3-agg-1", ct);
		read.Count.ShouldBe(3);
		read[0].Version.ShouldBe(1);
	}

	[Fact]
	public async Task ReadFromVersionFiltersCorrectly()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] S3 (LocalStack/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var ct = CreateTestTimeout();
		await _store!.WriteAsync(Tenant, "s3-agg-v", CreateEvents("s3-agg-v", 1, 2, 3, 4, 5), ct);

		var fromV3 = await _store.ReadAsync(Tenant, "s3-agg-v", 3, ct);
		fromV3.Count.ShouldBe(2);
		fromV3[0].Version.ShouldBe(4);
	}

	[Fact]
	public async Task MergeNewEventsWithExisting()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] S3 (LocalStack/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var ct = CreateTestTimeout();
		await _store!.WriteAsync(Tenant, "s3-agg-m", CreateEvents("s3-agg-m", 1, 2, 3), ct);
		await _store.WriteAsync(Tenant, "s3-agg-m", CreateEvents("s3-agg-m", 3, 4, 5), ct);

		var all = await _store.ReadAsync(Tenant, "s3-agg-m", ct);
		all.Count.ShouldBe(5);
	}

	[Fact]
	public async Task HasArchivedReturnsTrueWhenPresent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] S3 (LocalStack/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));

		var ct = CreateTestTimeout();
		await _store!.WriteAsync(Tenant, "s3-agg-h", CreateEvents("s3-agg-h", 1), ct);
		(await _store.HasArchivedEventsAsync(Tenant, "s3-agg-h", ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task HasArchivedReturnsFalseWhenAbsent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] S3 (LocalStack/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));
		(await _store!.HasArchivedEventsAsync(Tenant, "s3-nonexistent", CreateTestTimeout())).ShouldBeFalse();
	}

	[Fact]
	public async Task ReadReturnsEmptyForNonexistent()
	{
		Assert.SkipWhen(!_available, SkipReason("[infrastructure-unavailable] S3 (LocalStack/Docker) is not available, so this fact did NOT execute. It is reported skipped, never passed: a test that returns early on missing infrastructure is satisfied by doing nothing."));
		(await _store!.ReadAsync(Tenant, "s3-no-such", CreateTestTimeout())).Count.ShouldBe(0);
	}

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions) =>
		versions.Select(v => new StoredEvent(
			Guid.NewGuid().ToString(), aggregateId, "Test", "TestEvent",
			System.Text.Encoding.UTF8.GetBytes($"{{\"v\":{v}}}"), null,
			v, DateTimeOffset.UtcNow)).ToList();
}
