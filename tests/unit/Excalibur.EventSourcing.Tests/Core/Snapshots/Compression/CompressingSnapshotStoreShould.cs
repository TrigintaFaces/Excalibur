// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots.Compression;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots.Compression;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class CompressingSnapshotStoreShould
{
	private readonly ISnapshotStore _innerStore;
	private readonly SnapshotCompressionOptions _options;
	private readonly CompressingSnapshotStore _sut;

	// Mirror internal magic bytes from CompressingSnapshotStore
	private static readonly byte[] BrotliMagic = [0x45, 0x58, 0x42, 0x52]; // "EXBR"
	private static readonly byte[] GZipMagic = [0x45, 0x58, 0x47, 0x5A]; // "EXGZ"

	public CompressingSnapshotStoreShould()
	{
		_innerStore = A.Fake<ISnapshotStore>();
		_options = new SnapshotCompressionOptions
		{
			Algorithm = SnapshotCompressionAlgorithm.Brotli,
			CompressionLevel = CompressionLevel.Fastest,
			MinimumSizeBytes = 10
		};
		_sut = new CompressingSnapshotStore(
			_innerStore,
			Microsoft.Extensions.Options.Options.Create(_options));
	}

	[Fact]
	public async Task SaveSnapshotAsync_CompressData_WhenAboveMinSize()
	{
		// Arrange
		var originalData = new byte[100];
		Random.Shared.NextBytes(originalData);
		var snapshot = CreateSnapshot("agg-1", originalData);

		ISnapshot? capturedSnapshot = null;
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.SaveSnapshotAsync(A<ISnapshot>._, A<CancellationToken>._))
			.Invokes((ISnapshot s, CancellationToken _) => capturedSnapshot = s)
			.Returns(ValueTask.CompletedTask);
#pragma warning restore CA2012

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		capturedSnapshot.ShouldNotBeNull();
		capturedSnapshot.Data.ShouldNotBe(originalData);
		// Verify magic prefix for Brotli
		capturedSnapshot.Data[0].ShouldBe(BrotliMagic[0]);
		capturedSnapshot.Data[1].ShouldBe(BrotliMagic[1]);
	}

	[Fact]
	public async Task SaveSnapshotAsync_SkipCompression_WhenBelowMinSize()
	{
		// Arrange
		var smallData = new byte[5];
		var snapshot = CreateSnapshot("agg-1", smallData);

		ISnapshot? capturedSnapshot = null;
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.SaveSnapshotAsync(A<ISnapshot>._, A<CancellationToken>._))
			.Invokes((ISnapshot s, CancellationToken _) => capturedSnapshot = s)
			.Returns(ValueTask.CompletedTask);
#pragma warning restore CA2012

		// Act
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Assert
		capturedSnapshot.ShouldNotBeNull();
		capturedSnapshot.Data.ShouldBe(smallData);
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_DecompressData()
	{
		// Arrange
		var originalData = new byte[100];
		for (var i = 0; i < originalData.Length; i++)
		{
			originalData[i] = (byte)(i % 10);
		}

		// Compress the data to simulate what was stored
		var compressedData = CompressWithMagic(originalData, SnapshotCompressionAlgorithm.Brotli);
		var storedSnapshot = CreateSnapshot("agg-1", compressedData);

#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync("agg-1", "TestType", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(storedSnapshot));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Data.ShouldBe(originalData);
		result.AggregateId.ShouldBe("agg-1");
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnNull_WhenInnerReturnsNull()
	{
		// Arrange
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetLatestSnapshotAsync_ReturnUncompressedData_WhenNoMagicPrefix()
	{
		// Arrange
		var uncompressedData = new byte[] { 1, 2, 3, 4, 5 };
		var snapshot = CreateSnapshot("agg-1", uncompressedData);

#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync("agg-1", "TestType", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));
#pragma warning restore CA2012

		// Act
		var result = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Data.ShouldBe(uncompressedData);
	}

	[Fact]
	public async Task RoundTrip_BrotliCompression()
	{
		// Arrange
		var originalData = new byte[200];
		for (var i = 0; i < originalData.Length; i++)
		{
			originalData[i] = (byte)(i % 256);
		}

		_options.Algorithm = SnapshotCompressionAlgorithm.Brotli;
		var snapshot = CreateSnapshot("agg-1", originalData);

		ISnapshot? savedSnapshot = null;
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.SaveSnapshotAsync(A<ISnapshot>._, A<CancellationToken>._))
			.Invokes((ISnapshot s, CancellationToken _) => savedSnapshot = s)
			.Returns(ValueTask.CompletedTask);
#pragma warning restore CA2012

		// Act - Save (compress)
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Setup load to return the compressed data
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync("agg-1", "TestType", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(savedSnapshot));
#pragma warning restore CA2012

		// Act - Load (decompress)
		var loaded = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(originalData);
	}

	[Fact]
	public async Task RoundTrip_GZipCompression()
	{
		// Arrange
		_options.Algorithm = SnapshotCompressionAlgorithm.GZip;

		var originalData = new byte[200];
		for (var i = 0; i < originalData.Length; i++)
		{
			originalData[i] = (byte)(i % 256);
		}

		var snapshot = CreateSnapshot("agg-1", originalData);

		ISnapshot? savedSnapshot = null;
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.SaveSnapshotAsync(A<ISnapshot>._, A<CancellationToken>._))
			.Invokes((ISnapshot s, CancellationToken _) => savedSnapshot = s)
			.Returns(ValueTask.CompletedTask);
#pragma warning restore CA2012

		// Act - Save (compress)
		await _sut.SaveSnapshotAsync(snapshot, CancellationToken.None);

		// Verify GZip magic
		savedSnapshot!.Data[0].ShouldBe(GZipMagic[0]);

		// Setup load to return the compressed data
#pragma warning disable CA2012
		A.CallTo(() => _innerStore.GetLatestSnapshotAsync("agg-1", "TestType", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(savedSnapshot));
#pragma warning restore CA2012

		// Act - Load (decompress)
		var loaded = await _sut.GetLatestSnapshotAsync("agg-1", "TestType", CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe(originalData);
	}

	[Fact]
	public async Task SaveSnapshotAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveSnapshotAsync(null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var opts = Microsoft.Extensions.Options.Options.Create(new SnapshotCompressionOptions());
		Should.Throw<ArgumentNullException>(() => new CompressingSnapshotStore(_innerStore, null!));
	}

	private static ISnapshot CreateSnapshot(string aggregateId, byte[] data)
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns($"snap-{aggregateId}");
		A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		A.CallTo(() => snapshot.AggregateType).Returns("TestType");
		A.CallTo(() => snapshot.Version).Returns(1);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => snapshot.Data).Returns(data);
		A.CallTo(() => snapshot.Metadata).Returns(null);
		return snapshot;
	}

	private static byte[] CompressWithMagic(byte[] data, SnapshotCompressionAlgorithm algorithm)
	{
		using var output = new MemoryStream();

		var magic = algorithm == SnapshotCompressionAlgorithm.GZip
			? GZipMagic
			: BrotliMagic;

		output.Write(magic, 0, magic.Length);
		output.Write(BitConverter.GetBytes(data.Length), 0, 4);

		using (Stream compressionStream = algorithm == SnapshotCompressionAlgorithm.GZip
			? new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true)
			: new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
		{
			compressionStream.Write(data, 0, data.Length);
		}

		return output.ToArray();
	}
}
