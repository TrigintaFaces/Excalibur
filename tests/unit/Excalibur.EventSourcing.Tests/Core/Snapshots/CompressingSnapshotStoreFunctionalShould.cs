// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;
using System.Text;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots.Compression;
using Excalibur.EventSourcing.Snapshots.InMemory;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

/// <summary>
/// Functional tests for <see cref="CompressingSnapshotStore"/> covering compression/decompression
/// round-trips, small payload bypass, and mixed-mode reads.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CompressingSnapshotStoreFunctionalShould
{
	private static ISnapshot CreateSnapshot(string aggregateId, long version, byte[] data)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns("TestAggregate");
		A.CallTo(() => snapshot.Version).Returns(version);
		A.CallTo(() => snapshot.SnapshotId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => snapshot.Data).Returns(data);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		return snapshot;
	}

	[Fact]
	public async Task BrotliCompression_ShouldRoundTrip()
	{
		// Arrange
		var innerStore = new InMemorySnapshotStore();
		var options = Microsoft.Extensions.Options.Options.Create(new SnapshotCompressionOptions
		{
			Algorithm = SnapshotCompressionAlgorithm.Brotli,
			CompressionLevel = CompressionLevel.Fastest,
			MinimumSizeBytes = 0, // compress everything
		});

		var sut = new CompressingSnapshotStore(innerStore, options);
		var originalData = Encoding.UTF8.GetBytes("This is snapshot data that should be compressed and decompressed correctly.");
		var snapshot = CreateSnapshot("agg-1", 5, originalData);

		// Act
		await sut.SaveSnapshotAsync(snapshot, CancellationToken.None);
		var loaded = await sut.GetLatestSnapshotAsync("agg-1", "TestAggregate", CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(originalData);
		loaded.Version.ShouldBe(5);
	}

	[Fact]
	public async Task GZipCompression_ShouldRoundTrip()
	{
		// Arrange
		var innerStore = new InMemorySnapshotStore();
		var options = Microsoft.Extensions.Options.Options.Create(new SnapshotCompressionOptions
		{
			Algorithm = SnapshotCompressionAlgorithm.GZip,
			CompressionLevel = CompressionLevel.Fastest,
			MinimumSizeBytes = 0,
		});

		var sut = new CompressingSnapshotStore(innerStore, options);
		var originalData = Encoding.UTF8.GetBytes("GZip test data for compression round-trip verification.");
		var snapshot = CreateSnapshot("agg-1", 3, originalData);

		// Act
		await sut.SaveSnapshotAsync(snapshot, CancellationToken.None);
		var loaded = await sut.GetLatestSnapshotAsync("agg-1", "TestAggregate", CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(originalData);
	}

	[Fact]
	public async Task SmallPayload_ShouldSkipCompression()
	{
		// Arrange
		var innerStore = new InMemorySnapshotStore();
		var options = Microsoft.Extensions.Options.Options.Create(new SnapshotCompressionOptions
		{
			MinimumSizeBytes = 1000, // Only compress data >= 1000 bytes
		});

		var sut = new CompressingSnapshotStore(innerStore, options);
		var smallData = Encoding.UTF8.GetBytes("tiny");
		var snapshot = CreateSnapshot("agg-1", 1, smallData);

		// Act
		await sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Verify the inner store has uncompressed data (no magic prefix)
		var innerSnapshot = await innerStore.GetLatestSnapshotAsync("agg-1", "TestAggregate", CancellationToken.None);
		innerSnapshot.ShouldNotBeNull();
		innerSnapshot.Data.ShouldBe(smallData);

		// Reading through the compressing store should still work
		var loaded = await sut.GetLatestSnapshotAsync("agg-1", "TestAggregate", CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(smallData);
	}

	[Fact]
	public async Task GetLatestSnapshot_NonExistent_ShouldReturnNull()
	{
		// Arrange
		var innerStore = new InMemorySnapshotStore();
		var options = Microsoft.Extensions.Options.Options.Create(new SnapshotCompressionOptions());
		var sut = new CompressingSnapshotStore(innerStore, options);

		// Act
		var result = await sut.GetLatestSnapshotAsync("non-existent", "TestAggregate", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task LargePayload_ShouldCompressAndDecompress()
	{
		// Arrange
		var innerStore = new InMemorySnapshotStore();
		var options = Microsoft.Extensions.Options.Options.Create(new SnapshotCompressionOptions
		{
			Algorithm = SnapshotCompressionAlgorithm.Brotli,
			MinimumSizeBytes = 0,
		});

		var sut = new CompressingSnapshotStore(innerStore, options);

		// Create a large, compressible payload
		var largeData = Encoding.UTF8.GetBytes(new string('A', 10_000));
		var snapshot = CreateSnapshot("agg-large", 10, largeData);

		// Act
		await sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Verify the inner store has compressed data (smaller than original)
		var innerSnapshot = await innerStore.GetLatestSnapshotAsync("agg-large", "TestAggregate", CancellationToken.None);
		innerSnapshot.ShouldNotBeNull();
		innerSnapshot.Data.Length.ShouldBeLessThan(largeData.Length);

		// Reading through the compressing store should decompress
		var loaded = await sut.GetLatestSnapshotAsync("agg-large", "TestAggregate", CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(largeData);
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullOptions()
	{
		var innerStore = new InMemorySnapshotStore();
		Should.Throw<ArgumentNullException>(() =>
			new CompressingSnapshotStore(innerStore, null!));
	}
}
