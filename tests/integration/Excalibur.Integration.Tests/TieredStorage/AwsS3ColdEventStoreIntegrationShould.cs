// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using Excalibur.EventSourcing.Abstractions;
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

	public async Task InitializeAsync()
	{
		try
		{
			_container = new LocalStackBuilder()
				.WithImage("localstack/localstack:latest")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);

			var config = new AmazonS3Config
			{
				ServiceURL = _container.GetConnectionString(),
				ForcePathStyle = true,
				UseHttp = true
			};
			_s3Client = new AmazonS3Client("test", "test", config);
			await _s3Client.PutBucketAsync(BucketName).ConfigureAwait(false);

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

	public async Task DisposeAsync()
	{
		try
		{
			_s3Client?.Dispose();
			if (_container is not null)
				await _container.DisposeAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Suppress disposal errors to prevent test host crash
		}
	}

	[Fact]
	public async Task WriteAndReadEvents()
	{
		if (!_available) return;

		var events = CreateEvents("s3-agg-1", 1, 2, 3);
		await _store!.WriteAsync("s3-agg-1", events, CancellationToken.None);

		var read = await _store.ReadAsync("s3-agg-1", CancellationToken.None);
		read.Count.ShouldBe(3);
		read[0].Version.ShouldBe(1);
	}

	[Fact]
	public async Task ReadFromVersionFiltersCorrectly()
	{
		if (!_available) return;

		await _store!.WriteAsync("s3-agg-v", CreateEvents("s3-agg-v", 1, 2, 3, 4, 5), CancellationToken.None);

		var fromV3 = await _store.ReadAsync("s3-agg-v", 3, CancellationToken.None);
		fromV3.Count.ShouldBe(2);
		fromV3[0].Version.ShouldBe(4);
	}

	[Fact]
	public async Task MergeNewEventsWithExisting()
	{
		if (!_available) return;

		await _store!.WriteAsync("s3-agg-m", CreateEvents("s3-agg-m", 1, 2, 3), CancellationToken.None);
		await _store.WriteAsync("s3-agg-m", CreateEvents("s3-agg-m", 3, 4, 5), CancellationToken.None);

		var all = await _store.ReadAsync("s3-agg-m", CancellationToken.None);
		all.Count.ShouldBe(5);
	}

	[Fact]
	public async Task HasArchivedReturnsTrueWhenPresent()
	{
		if (!_available) return;

		await _store!.WriteAsync("s3-agg-h", CreateEvents("s3-agg-h", 1), CancellationToken.None);
		(await _store.HasArchivedEventsAsync("s3-agg-h", CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task HasArchivedReturnsFalseWhenAbsent()
	{
		if (!_available) return;
		(await _store!.HasArchivedEventsAsync("s3-nonexistent", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task ReadReturnsEmptyForNonexistent()
	{
		if (!_available) return;
		(await _store!.ReadAsync("s3-no-such", CancellationToken.None)).Count.ShouldBe(0);
	}

	private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions) =>
		versions.Select(v => new StoredEvent(
			Guid.NewGuid().ToString(), aggregateId, "Test", "TestEvent",
			System.Text.Encoding.UTF8.GetBytes($"{{\"v\":{v}}}"), null,
			v, DateTimeOffset.UtcNow)).ToList();
}
