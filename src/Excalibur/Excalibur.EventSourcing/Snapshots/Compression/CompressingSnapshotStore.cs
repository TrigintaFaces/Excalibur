// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO.Compression;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Snapshots.Compression;

/// <summary>
/// Decorates an <see cref="ISnapshotStore"/> with transparent compression and decompression
/// of snapshot data using Brotli or GZip.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <see cref="DelegatingSnapshotStore"/> decorator pattern.
/// On save, snapshot data is compressed before delegation. On load, snapshot data
/// is decompressed after retrieval from the inner store.
/// </para>
/// <para>
/// Compressed data is prefixed with a magic byte to distinguish compressed from
/// uncompressed data, enabling transparent mixed-mode reads during migration.
/// </para>
/// </remarks>
public sealed class CompressingSnapshotStore : DelegatingSnapshotStore
{
	/// <summary>
	/// Magic byte prefix for Brotli-compressed snapshot data.
	/// </summary>
	internal static readonly byte[] BrotliMagic = [0x45, 0x58, 0x42, 0x52]; // "EXBR"

	/// <summary>
	/// Magic byte prefix for GZip-compressed snapshot data.
	/// </summary>
	internal static readonly byte[] GZipMagic = [0x45, 0x58, 0x47, 0x5A]; // "EXGZ"

	private readonly IOptions<SnapshotCompressionOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompressingSnapshotStore"/> class.
	/// </summary>
	/// <param name="inner">The inner snapshot store to delegate to.</param>
	/// <param name="options">The compression options.</param>
	public CompressingSnapshotStore(
		ISnapshotStore inner,
		IOptions<SnapshotCompressionOptions> options)
		: base(inner)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	public override async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		var snapshot = await base.GetLatestSnapshotAsync(aggregateId, aggregateType, cancellationToken)
			.ConfigureAwait(false);

		if (snapshot is null)
		{
			return null;
		}

		var decompressedData = Decompress(snapshot.Data);

		// If data was not compressed (no magic prefix), return as-is
		if (ReferenceEquals(decompressedData, snapshot.Data))
		{
			return snapshot;
		}

		return new DecompressedSnapshot(snapshot, decompressedData);
	}

	/// <inheritdoc />
	public override async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var opts = _options.Value;
		var data = snapshot.Data;

		// Skip compression for small payloads
		if (data.Length < opts.MinimumSizeBytes)
		{
			await base.SaveSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
			return;
		}

		var compressedData = Compress(data, opts.Algorithm, opts.CompressionLevel);
		var compressedSnapshot = new CompressedSnapshotWrapper(snapshot, compressedData);
		await base.SaveSnapshotAsync(compressedSnapshot, cancellationToken).ConfigureAwait(false);
	}

	private static byte[] Compress(byte[] data, SnapshotCompressionAlgorithm algorithm, CompressionLevel level)
	{
		using var output = new MemoryStream();

		// Write magic prefix
		var magic = algorithm == SnapshotCompressionAlgorithm.GZip ? GZipMagic : BrotliMagic;
		output.Write(magic, 0, magic.Length);

		// Write original size (4 bytes, little-endian) for pre-allocation on decompress
		var sizeBytes = BitConverter.GetBytes(data.Length);
		output.Write(sizeBytes, 0, sizeBytes.Length);

		// Compress
		using (var compressionStream = CreateCompressionStream(output, algorithm, level))
		{
			compressionStream.Write(data, 0, data.Length);
		}

		return output.ToArray();
	}

	private static byte[] Decompress(byte[] data)
	{
		if (data.Length < 8) // Magic (4) + size (4) minimum
		{
			return data;
		}

		SnapshotCompressionAlgorithm? algorithm = null;

		if (HasMagicPrefix(data, BrotliMagic))
		{
			algorithm = SnapshotCompressionAlgorithm.Brotli;
		}
		else if (HasMagicPrefix(data, GZipMagic))
		{
			algorithm = SnapshotCompressionAlgorithm.GZip;
		}

		if (algorithm is null)
		{
			return data;
		}

		var originalSize = BitConverter.ToInt32(data, 4);
		using var input = new MemoryStream(data, 8, data.Length - 8);
		using var decompressionStream = CreateDecompressionStream(input, algorithm.Value);
		using var output = new MemoryStream(originalSize);
		decompressionStream.CopyTo(output);
		return output.ToArray();
	}

	private static bool HasMagicPrefix(byte[] data, byte[] magic)
	{
		if (data.Length < magic.Length)
		{
			return false;
		}

		for (var i = 0; i < magic.Length; i++)
		{
			if (data[i] != magic[i])
			{
				return false;
			}
		}

		return true;
	}

	private static Stream CreateCompressionStream(Stream output, SnapshotCompressionAlgorithm algorithm, CompressionLevel level)
	{
		return algorithm switch
		{
			SnapshotCompressionAlgorithm.GZip => new GZipStream(output, level, leaveOpen: true),
			_ => new BrotliStream(output, level, leaveOpen: true),
		};
	}

	private static Stream CreateDecompressionStream(Stream input, SnapshotCompressionAlgorithm algorithm)
	{
		return algorithm switch
		{
			SnapshotCompressionAlgorithm.GZip => new GZipStream(input, CompressionMode.Decompress, leaveOpen: true),
			_ => new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true),
		};
	}

	/// <summary>
	/// Wraps an existing snapshot with decompressed data bytes.
	/// </summary>
	private sealed class DecompressedSnapshot : ISnapshot
	{
		private readonly ISnapshot _original;
		private readonly byte[] _decompressedData;

		internal DecompressedSnapshot(ISnapshot original, byte[] decompressedData)
		{
			_original = original;
			_decompressedData = decompressedData;
		}

		public string SnapshotId => _original.SnapshotId;
		public string AggregateId => _original.AggregateId;
		public long Version => _original.Version;
		public DateTimeOffset CreatedAt => _original.CreatedAt;
		public byte[] Data => _decompressedData;
		public string AggregateType => _original.AggregateType;
		public IDictionary<string, object>? Metadata => _original.Metadata;
	}

	/// <summary>
	/// Wraps an existing snapshot with compressed data bytes.
	/// </summary>
	private sealed class CompressedSnapshotWrapper : ISnapshot
	{
		private readonly ISnapshot _original;
		private readonly byte[] _compressedData;

		internal CompressedSnapshotWrapper(ISnapshot original, byte[] compressedData)
		{
			_original = original;
			_compressedData = compressedData;
		}

		public string SnapshotId => _original.SnapshotId;
		public string AggregateId => _original.AggregateId;
		public long Version => _original.Version;
		public DateTimeOffset CreatedAt => _original.CreatedAt;
		public byte[] Data => _compressedData;
		public string AggregateType => _original.AggregateType;
		public IDictionary<string, object>? Metadata => _original.Metadata;
	}
}
