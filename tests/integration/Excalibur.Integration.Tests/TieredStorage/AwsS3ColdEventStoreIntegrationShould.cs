// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.AwsS3;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.LocalStack;

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
	private const string BucketName = "cold-events-test";

	private LocalStackContainer? _container;
	private IAmazonS3? _s3Client;
	private IColdEventStore? _store;
	private bool _available;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new LocalStackBuilder()
				.WithImage("localstack/localstack:latest")
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
		catch (Exception)
		{
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
		if (!_available) return;

		var ct = CreateTestTimeout();
		var events = CreateEvents("s3-agg-1", 1, 2, 3);
		await _store!.WriteAsync("s3-agg-1", events, ct);

		var read = await _store.ReadAsync("s3-agg-1", ct);
		read.Count.ShouldBe(3);
		read[0].Version.ShouldBe(1);
	}

	[Fact]
	public async Task ReadFromVersionFiltersCorrectly()
	{
		if (!_available) return;

		var ct = CreateTestTimeout();
		await _store!.WriteAsync("s3-agg-v", CreateEvents("s3-agg-v", 1, 2, 3, 4, 5), ct);

		var fromV3 = await _store.ReadAsync("s3-agg-v", 3, ct);
		fromV3.Count.ShouldBe(2);
		fromV3[0].Version.ShouldBe(4);
	}

	[Fact]
	public async Task MergeNewEventsWithExisting()
	{
		if (!_available) return;

		var ct = CreateTestTimeout();
		await _store!.WriteAsync("s3-agg-m", CreateEvents("s3-agg-m", 1, 2, 3), ct);
		await _store.WriteAsync("s3-agg-m", CreateEvents("s3-agg-m", 3, 4, 5), ct);

		var all = await _store.ReadAsync("s3-agg-m", ct);
		all.Count.ShouldBe(5);
	}

	[Fact]
	public async Task HasArchivedReturnsTrueWhenPresent()
	{
		if (!_available) return;

		var ct = CreateTestTimeout();
		await _store!.WriteAsync("s3-agg-h", CreateEvents("s3-agg-h", 1), ct);
		(await _store.HasArchivedEventsAsync("s3-agg-h", ct)).ShouldBeTrue();
	}

	[Fact]
	public async Task HasArchivedReturnsFalseWhenAbsent()
	{
		if (!_available) return;
		(await _store!.HasArchivedEventsAsync("s3-nonexistent", CreateTestTimeout())).ShouldBeFalse();
	}

	[Fact]
	public async Task ReadReturnsEmptyForNonexistent()
	{
		if (!_available) return;
		(await _store!.ReadAsync("s3-no-such", CreateTestTimeout())).Count.ShouldBe(0);
	}

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions) =>
		versions.Select(v => new StoredEvent(
			Guid.NewGuid().ToString(), aggregateId, "Test", "TestEvent",
			System.Text.Encoding.UTF8.GetBytes($"{{\"v\":{v}}}"), null,
			v, DateTimeOffset.UtcNow)).ToList();
}
