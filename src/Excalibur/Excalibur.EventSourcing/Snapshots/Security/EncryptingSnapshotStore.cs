// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Snapshots.Security;

/// <summary>
/// Decorates an <see cref="ISnapshotStore"/> with transparent encryption and decryption
/// of snapshot data using an <see cref="ISnapshotEncryptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <see cref="DelegatingSnapshotStore"/> decorator pattern.
/// On save, snapshot data is encrypted before delegation. On load, snapshot data
/// is decrypted after retrieval from the inner store.
/// </para>
/// <para>
/// Only the <see cref="ISnapshot.Data"/> payload is encrypted; metadata and identifiers
/// remain in plaintext for indexing and querying.
/// </para>
/// </remarks>
public sealed class EncryptingSnapshotStore : DelegatingSnapshotStore
{
	private readonly ISnapshotEncryptor _encryptor;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingSnapshotStore"/> class.
	/// </summary>
	/// <param name="inner">The inner snapshot store to delegate to.</param>
	/// <param name="encryptor">The encryptor for snapshot data.</param>
	public EncryptingSnapshotStore(ISnapshotStore inner, ISnapshotEncryptor encryptor)
		: base(inner)
	{
		_encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
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

		var decryptedData = await _encryptor.DecryptAsync(snapshot.Data, cancellationToken)
			.ConfigureAwait(false);

		return new DecryptedSnapshot(snapshot, decryptedData);
	}

	/// <inheritdoc />
	public override async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var encryptedData = await _encryptor.EncryptAsync(snapshot.Data, cancellationToken)
			.ConfigureAwait(false);

		var encryptedSnapshot = new EncryptedSnapshotWrapper(snapshot, encryptedData);
		await base.SaveSnapshotAsync(encryptedSnapshot, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Wraps an existing snapshot with decrypted data bytes.
	/// </summary>
	private sealed class DecryptedSnapshot : ISnapshot
	{
		private readonly ISnapshot _original;
		private readonly byte[] _decryptedData;

		internal DecryptedSnapshot(ISnapshot original, byte[] decryptedData)
		{
			_original = original;
			_decryptedData = decryptedData;
		}

		public string SnapshotId => _original.SnapshotId;
		public string AggregateId => _original.AggregateId;
		public long Version => _original.Version;
		public DateTimeOffset CreatedAt => _original.CreatedAt;
		public byte[] Data => _decryptedData;
		public string AggregateType => _original.AggregateType;
		public IDictionary<string, object>? Metadata => _original.Metadata;
	}

	/// <summary>
	/// Wraps an existing snapshot with encrypted data bytes.
	/// </summary>
	private sealed class EncryptedSnapshotWrapper : ISnapshot
	{
		private readonly ISnapshot _original;
		private readonly byte[] _encryptedData;

		internal EncryptedSnapshotWrapper(ISnapshot original, byte[] encryptedData)
		{
			_original = original;
			_encryptedData = encryptedData;
		}

		public string SnapshotId => _original.SnapshotId;
		public string AggregateId => _original.AggregateId;
		public long Version => _original.Version;
		public DateTimeOffset CreatedAt => _original.CreatedAt;
		public byte[] Data => _encryptedData;
		public string AggregateType => _original.AggregateType;
		public IDictionary<string, object>? Metadata => _original.Metadata;
	}
}
