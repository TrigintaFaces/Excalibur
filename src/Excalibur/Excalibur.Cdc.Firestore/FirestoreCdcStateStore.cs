// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;

using Google.Cloud.Firestore;

using Grpc.Core;

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
	/// <remarks>
	/// An unconditional Firestore delete succeeds whether or not the document was there and reports
	/// nothing either way, so the answer has to come from the server. <see cref="Precondition.MustExist"/>
	/// is the SDK's own way to ask for it: the delete is rejected when the document is absent, in one
	/// round trip and with no read to race against. A check-then-delete would need two, and the window
	/// between them is exactly where a concurrent delete makes the answer wrong.
	/// </remarks>
	public async Task<bool> DeletePositionAsync(
		string consumerId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);

		LogDeletingPosition(consumerId);

		var docRef = _db.Collection(_collectionName).Document(consumerId);

		try
		{
			_ = await docRef.DeleteAsync(Precondition.MustExist, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (RpcException ex) when (ex.StatusCode is StatusCode.NotFound or StatusCode.FailedPrecondition)
		{
			// The document was not there. Both codes are reported for an unmet exists-precondition
			// depending on the backend, and neither is a fault -- "there was nothing to delete" is one of
			// the two answers this method exists to give. Every other status propagates.
			return false;
		}
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await GetPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	/// <remarks>
	/// Translates this store's two provider-specific refusals into the outcomes the abstraction defines, so
	/// a consumer handling a declined checkpoint needs neither this package nor Grpc.Core to do it.
	/// <see cref="FirestoreStalePositionException"/> is the monotonic guard's deliberate refusal and carries
	/// the store's own reading of both positions, so it maps to
	/// <see cref="CdcPositionSaveOutcome.RefusedAsStale"/>. An ABORTED status is the opposite case: the
	/// transaction never established which position should win, so it maps to
	/// <see cref="CdcPositionSaveOutcome.Contended"/> and must not be reported as staleness — doing so would
	/// tell a lone processor that a second one is ahead of it.
	/// </remarks>
	async Task<CdcPositionSaveOutcome> ICdcStateStore.SavePositionAsync(
		string consumerId,
		ChangePosition position,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		if (position is not FirestoreCdcPosition firestorePosition)
		{
			firestorePosition = FirestoreCdcPosition.FromBase64(position.ToToken());
		}

		try
		{
			await SavePositionAsync(consumerId, firestorePosition, cancellationToken).ConfigureAwait(false);
			return CdcPositionSaveOutcome.Advanced;
		}
		catch (FirestoreStalePositionException)
		{
			return CdcPositionSaveOutcome.RefusedAsStale;
		}
		catch (RpcException rpc) when (rpc.StatusCode is StatusCode.Aborted or StatusCode.FailedPrecondition)
		{
			// Firestore reports transaction contention as ABORTED once RunTransactionAsync has exhausted its
			// own retry budget, and the emulator adds "Transaction lock timeout." under the same status. Both
			// mean the write did not apply and nothing was decided, which is exactly Contended. Cancellation
			// is deliberately NOT caught here: a cancelled save is the caller's own doing and must surface.
			LogSavePositionContended(consumerId, rpc.Status.Detail);
			return CdcPositionSaveOutcome.Contended;
		}
	}

	/// <inheritdoc/>
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
