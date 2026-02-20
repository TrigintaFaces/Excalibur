// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Represents a position in Firestore CDC using synthetic position tracking.
/// </summary>
/// <remarks>
/// <para>
/// Firestore does not have native CDC cursors like other providers. Position is tracked
/// using a combination of document UpdateTime and DocumentId for deterministic ordering.
/// </para>
/// <para>
/// Documents are ordered by UpdateTime first, then by DocumentId for documents with
/// the same UpdateTime. This ensures consistent ordering across restarts.
/// </para>
/// </remarks>
public sealed class FirestoreCdcPosition : ChangePosition, IEquatable<FirestoreCdcPosition>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,

	};

	/// <summary>
	/// Gets the collection path being watched.
	/// </summary>

	public string CollectionPath { get; }

	/// <summary>
	/// Gets the last processed document update time.
	/// </summary>
	/// <remarks>
	/// Used as the primary ordering key for position tracking.
	/// Documents with UpdateTime greater than this value are unprocessed.
	/// </remarks>

	public DateTimeOffset? UpdateTime { get; }

	/// <summary>
	/// Gets the last processed document ID.
	/// </summary>
	/// <remarks>
	/// Used as a secondary ordering key for documents with the same UpdateTime.
	/// This ensures deterministic ordering when multiple documents have identical timestamps.
	/// </remarks>

	public string? LastDocumentId { get; }

	/// <summary>
	/// Gets the timestamp when this position was recorded (for diagnostics).
	/// </summary>

	public override DateTimeOffset? Timestamp { get; }

	/// <summary>
	/// Gets a value indicating whether this position is valid for resuming.
	/// </summary>

	public override bool IsValid => UpdateTime.HasValue;

	/// <summary>
	/// Gets a value indicating whether this position represents the beginning of the stream.
	/// </summary>
	public bool IsBeginning => !UpdateTime.HasValue && Timestamp is null;

	private FirestoreCdcPosition(
		string collectionPath,
		DateTimeOffset? updateTime,
		string? lastDocumentId,
		DateTimeOffset? timestamp)
	{
		CollectionPath = collectionPath ?? string.Empty;
		UpdateTime = updateTime;
		LastDocumentId = lastDocumentId;
		Timestamp = timestamp;

	}

	/// <summary>
	/// Creates a position representing the beginning of the stream.
	/// </summary>
	/// <param name="collectionPath">The collection path.</param>
	/// <returns>A position that will read from the beginning.</returns>
	public static FirestoreCdcPosition Beginning(string collectionPath)
	{
		return new FirestoreCdcPosition(collectionPath, null, null, null);

	}

	/// <summary>
	/// Creates a position representing the current time (latest).
	/// </summary>
	/// <param name="collectionPath">The collection path.</param>
	/// <returns>A position that will read from now.</returns>
	public static FirestoreCdcPosition Now(string collectionPath)
	{
		var now = DateTimeOffset.UtcNow;
		return new FirestoreCdcPosition(collectionPath, now, null, now);

	}

	/// <summary>
	/// Creates a position from specific update time and document ID.
	/// </summary>
	/// <param name="collectionPath">The collection path.</param>
	/// <param name="updateTime">The last processed document update time.</param>
	/// <param name="lastDocumentId">The last processed document ID.</param>
	/// <returns>A position that will resume from the specified location.</returns>
	public static FirestoreCdcPosition FromUpdateTime(
		string collectionPath,
		DateTimeOffset updateTime,
		string? lastDocumentId)
	{
		return new FirestoreCdcPosition(collectionPath, updateTime, lastDocumentId, DateTimeOffset.UtcNow);

	}

	/// <summary>
	/// Updates the position with a new document.
	/// </summary>
	/// <param name="updateTime">The document update time.</param>
	/// <param name="documentId">The document ID.</param>
	/// <returns>A new position with the updated values.</returns>
	public FirestoreCdcPosition WithDocument(DateTimeOffset updateTime, string documentId)
	{
		return new FirestoreCdcPosition(CollectionPath, updateTime, documentId, DateTimeOffset.UtcNow);

	}

	/// <summary>
	/// Creates a copy with a different collection path.
	/// </summary>
	/// <param name="collectionPath">The new collection path.</param>
	/// <returns>A new position with the updated collection path.</returns>
	public FirestoreCdcPosition WithCollectionPath(string collectionPath)
	{
		return new FirestoreCdcPosition(collectionPath, UpdateTime, LastDocumentId, Timestamp);

	}

	/// <summary>
	/// Determines if a document should be processed based on this position.
	/// </summary>
	/// <param name="docUpdateTime">The document's update time.</param>
	/// <param name="docId">The document ID.</param>
	/// <returns><see langword="true"/> if the document is after this position; otherwise, <see langword="false"/>.</returns>
	public bool IsAfterPosition(DateTimeOffset docUpdateTime, string docId)

	{
		// If position has no UpdateTime, all documents are unprocessed
		if (!UpdateTime.HasValue)
		{
			return true;

		}

		// Document is after position if its UpdateTime is greater
		if (docUpdateTime > UpdateTime.Value)
		{
			return true;

		}

		// If UpdateTime is the same, use document ID for deterministic ordering
		if (docUpdateTime == UpdateTime.Value && LastDocumentId is not null)
		{
			return string.Compare(docId, LastDocumentId, StringComparison.Ordinal) > 0;
		}

		return false;

	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public override string ToToken() => ToBase64();

	/// <inheritdoc/>
	public override bool Equals(ChangePosition? other) => other is FirestoreCdcPosition firestore && Equals(firestore);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is FirestoreCdcPosition firestore && Equals(firestore);

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine(CollectionPath, UpdateTime, LastDocumentId);

	/// <summary>
	/// Serializes this position to a byte array for storage.
	/// </summary>
	/// <returns>A byte array representation of this position.</returns>
	/// <remarks>
	/// This method is used for interoperability with the core <c>CdcPositionResetEventArgs</c>
	/// which uses byte arrays for position storage.
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public byte[] ToBytes()
	{
		var data = new PositionData
		{
			CollectionPath = CollectionPath,
			UpdateTime = UpdateTime,
			LastDocumentId = LastDocumentId,
			Timestamp = Timestamp,
		};

		var json = JsonSerializer.Serialize(data, JsonOptions);
		return Encoding.UTF8.GetBytes(json);
	}

	/// <summary>
	/// Deserializes a position from a byte array.
	/// </summary>
	/// <param name="bytes">The byte array.</param>
	/// <returns>The deserialized position.</returns>
	/// <exception cref="FormatException">Thrown if the byte array format is invalid.</exception>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static FirestoreCdcPosition FromBytes(byte[] bytes)
	{
		ArgumentNullException.ThrowIfNull(bytes);

		try
		{
			var json = Encoding.UTF8.GetString(bytes);
			var data = JsonSerializer.Deserialize<PositionData>(json, JsonOptions);

			ArgumentNullException.ThrowIfNull(data, nameof(bytes));

			return new FirestoreCdcPosition(
				data.CollectionPath ?? string.Empty,
				data.UpdateTime,
				data.LastDocumentId,
				data.Timestamp);
		}
		catch (Exception ex) when (ex is not FormatException)
		{
			throw new FormatException("Failed to parse position from bytes", ex);
		}
	}

	/// <summary>
	/// Serializes this position to a base64 string for storage.
	/// </summary>
	/// <returns>A base64-encoded string representation.</returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public string ToBase64()
	{
		var data = new PositionData
		{
			CollectionPath = CollectionPath,
			UpdateTime = UpdateTime,
			LastDocumentId = LastDocumentId,
			Timestamp = Timestamp,
		};

		var json = JsonSerializer.Serialize(data, JsonOptions);
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

	}

	/// <summary>
	/// Deserializes a position from a base64 string.
	/// </summary>
	/// <param name="base64">The base64-encoded string.</param>
	/// <returns>The deserialized position.</returns>
	/// <exception cref="FormatException">Thrown if the string format is invalid.</exception>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static FirestoreCdcPosition FromBase64(string base64)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(base64);

		try
		{
			var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
			var data = JsonSerializer.Deserialize<PositionData>(json, JsonOptions);

			ArgumentNullException.ThrowIfNull(data, nameof(base64));

			return new FirestoreCdcPosition(
				data.CollectionPath ?? string.Empty,
				data.UpdateTime,
				data.LastDocumentId,
				data.Timestamp);
		}
		catch (FormatException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new FormatException("Failed to parse position from base64", ex);

		}
	}

	/// <summary>
	/// Attempts to deserialize a position from a base64 string.
	/// </summary>
	/// <param name="base64">The base64-encoded string.</param>
	/// <param name="position">The deserialized position if successful.</param>
	/// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
	public static bool TryFromBase64(string? base64, out FirestoreCdcPosition? position)
	{
		if (string.IsNullOrWhiteSpace(base64))
		{
			position = null;
			return false;
		}

		try
		{
			position = FromBase64(base64);
			return true;
		}
		catch
		{
			position = null;
			return false;

		}
	}

	/// <inheritdoc/>
	public bool Equals(FirestoreCdcPosition? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		return string.Equals(CollectionPath, other.CollectionPath, StringComparison.Ordinal) &&
			   UpdateTime == other.UpdateTime &&
			   string.Equals(LastDocumentId, other.LastDocumentId, StringComparison.Ordinal);

	}

	/// <inheritdoc/>
	public override string ToString()
	{
		if (!UpdateTime.HasValue)
		{
			return Timestamp.HasValue ? $"Latest({Timestamp.Value:O})" : "Beginning";
		}

		return LastDocumentId is not null
			? $"Position({UpdateTime.Value:O}, {LastDocumentId})"
			: $"Position({UpdateTime.Value:O})";

	}

	/// <summary>
	/// Internal data structure for JSON serialization.
	/// </summary>
	private sealed class PositionData
	{
		[JsonPropertyName("collectionPath")]
		public string? CollectionPath { get; set; }

		[JsonPropertyName("updateTime")]
		public DateTimeOffset? UpdateTime { get; set; }

		[JsonPropertyName("lastDocumentId")]
		public string? LastDocumentId { get; set; }

		[JsonPropertyName("timestamp")]
		public DateTimeOffset? Timestamp { get; set; }
	}
}
