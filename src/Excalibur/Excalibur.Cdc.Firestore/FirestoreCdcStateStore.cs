// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Firestore-backed state store for CDC position tracking.
/// </summary>
/// <remarks>
/// Stores positions in a dedicated Firestore collection for durability.
/// </remarks>
public sealed partial class FirestoreCdcStateStore : IFirestoreCdcStateStore
{
	private readonly FirestoreDb _db;
	private readonly string _collectionName;
	private readonly ILogger<FirestoreCdcStateStore> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreCdcStateStore"/> class.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreCdcStateStore(FirestoreDb db, ILogger<FirestoreCdcStateStore> logger)
			: this(db, new FirestoreCdcStateStoreOptions(), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreCdcStateStore"/> class.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="collectionName">The collection name for storing positions.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreCdcStateStore(
			FirestoreDb db,
			string collectionName,
			ILogger<FirestoreCdcStateStore> logger)
			: this(db, new FirestoreCdcStateStoreOptions { CollectionName = collectionName }, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreCdcStateStore"/> class with options.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="options">The CDC state store options.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreCdcStateStore(
			FirestoreDb db,
			IOptions<FirestoreCdcStateStoreOptions> options,
			ILogger<FirestoreCdcStateStore> logger)
			: this(db, options?.Value ?? throw new ArgumentNullException(nameof(options)), logger)
	{
	}

	private FirestoreCdcStateStore(
			FirestoreDb db,
			FirestoreCdcStateStoreOptions options,
			ILogger<FirestoreCdcStateStore> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);
		options.Validate();

		_db = db;
		_collectionName = options.CollectionName;
		_logger = logger;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	public async Task<FirestoreCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		LogGettingPosition(processorName);

		var docRef = _db.Collection(_collectionName).Document(processorName);
		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			LogPositionNotFound(processorName);
			return null;
		}

		var positionData = snapshot.GetValue<string>("positionData");
		if (string.IsNullOrWhiteSpace(positionData))
		{
			return null;
		}

		if (!FirestoreCdcPosition.TryFromBase64(positionData, out var position))
		{
			return null;
		}

		return position;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	public async Task SavePositionAsync(
		string processorName,
		FirestoreCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ArgumentNullException.ThrowIfNull(position);

		LogSavingPosition(processorName);

		var docRef = _db.Collection(_collectionName).Document(processorName);

		var data = new Dictionary<string, object>
		{
			["processorName"] = processorName,
			["positionData"] = position.ToBase64(),
			["updatedAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
			["collectionPath"] = position.CollectionPath,
		};

		// Optimistic-concurrency guard (native Firestore transaction): refuse to regress the CDC
		// watermark. Reading the current position and writing the new one inside one transaction
		// makes the check-and-set atomic against a concurrent processor advancing the same key.
		await _db.RunTransactionAsync(
			async transaction =>
			{
				var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);
				if (snapshot.Exists
					&& snapshot.TryGetValue<string>("positionData", out var existingData)
					&& FirestoreCdcPosition.TryFromBase64(existingData, out var current)
					&& current!.UpdateTime is { } currentWatermark
					&& position.UpdateTime is { } incomingWatermark
					&& currentWatermark > incomingWatermark)
				{
					throw new FirestoreStalePositionException(
						$"Refusing to persist a stale CDC position for processor '{processorName}': the stored watermark ({currentWatermark:O}) is newer than the incoming position ({incomingWatermark:O}).");
				}

				transaction.Set(docRef, data, SetOptions.Overwrite);
			},
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		LogDeletingPosition(processorName);

		var docRef = _db.Collection(_collectionName).Document(processorName);
		_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await GetPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		if (position is not FirestoreCdcPosition firestorePosition)
		{
			firestorePosition = FirestoreCdcPosition.FromBase64(position.ToToken());
		}

		return SavePositionAsync(consumerId, firestorePosition, cancellationToken);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		await DeletePositionAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, whose type graph is not statically analyzable.")]
	[RequiresDynamicCode("CDC position tokens are serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "The CDC state-store contracts are implemented by providers that never reach reflective serialization, so the requirement cannot be declared on the contract without binding those too. It is declared on this Firestore implementation instead.")]
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var collection = _db.Collection(_collectionName);
		var snapshot = await collection.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		foreach (var doc in snapshot.Documents)
		{
			var positionData = doc.GetValue<string>("positionData");
			if (!string.IsNullOrWhiteSpace(positionData) &&
				FirestoreCdcPosition.TryFromBase64(positionData, out var position) &&
				position is not null)
			{
				yield return (doc.Id, position);
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		_disposed = true;
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_disposed = true;
	}
}
