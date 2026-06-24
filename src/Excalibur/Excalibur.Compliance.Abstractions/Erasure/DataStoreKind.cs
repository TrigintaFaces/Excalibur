// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Identifies the kind of store that holds personal data, used by the GDPR erasure coverage gate to
/// route each discovered <see cref="DataLocation"/> to the mechanism that actually erases it
/// (crypto-shred, a covering <see cref="IErasureContributor"/>, or a declared exemption).
/// </summary>
/// <remarks>
/// <para>
/// This is an <b>extensible</b>, string-backed kind (the Microsoft "names" pattern, e.g.
/// <c>System.Net.Mime.MediaTypeNames</c>) rather than a closed enum: Excalibur is a framework, so a
/// consumer may have <b>custom</b> stores holding personal data. A closed enum would force every custom
/// store into a single bucket, and a contributor declaring that bucket would then falsely "cover" all
/// unknown stores — a correctness hole. Use the well-known static members for first-party stores and
/// <see cref="Create(string)"/> for consumer-defined kinds.
/// </para>
/// <para>
/// Matching is case-insensitive value equality, so the kind behaves like an enum for set membership in
/// the coverage gate. The <see cref="Unknown"/> (default) kind is <b>never coverable</b> by a contributor
/// or an exemption — an unclassified location must block a <c>Completed</c> erasure, never silently pass.
/// </para>
/// </remarks>
public readonly struct DataStoreKind : IEquatable<DataStoreKind>
{
	private const string UnknownValue = "Unknown";

	private readonly string? _value;

	private DataStoreKind(string value) => _value = value;

	/// <summary>
	/// Creates a <see cref="DataStoreKind"/> for a consumer-defined store kind.
	/// </summary>
	/// <param name="value">The non-empty store-kind identifier (trimmed; matched case-insensitively).</param>
	/// <returns>A <see cref="DataStoreKind"/> wrapping the normalized value.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
	public static DataStoreKind Create(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		return new DataStoreKind(value.Trim());
	}

	/// <summary>
	/// Gets the normalized string value of this store kind.
	/// </summary>
	/// <value>The store-kind identifier, or <c>"Unknown"</c> for the default value.</value>
	public string Value => _value ?? UnknownValue;

	/// <summary>
	/// Gets a value indicating whether this is the unclassified (default) kind, which is never coverable.
	/// </summary>
	/// <value><see langword="true"/> for the default/unclassified kind; otherwise <see langword="false"/>.</value>
	public bool IsUnknown => string.IsNullOrEmpty(_value);

	/// <summary>The unclassified/default store kind. Never coverable — always forces a non-<c>Completed</c> erasure.</summary>
	public static DataStoreKind Unknown => default;

	/// <summary>The event store (covered by event tombstoning).</summary>
	public static DataStoreKind EventStore { get; } = new("EventStore");

	/// <summary>The snapshot store.</summary>
	public static DataStoreKind Snapshot { get; } = new("Snapshot");

	/// <summary>The transactional outbox store.</summary>
	public static DataStoreKind Outbox { get; } = new("Outbox");

	/// <summary>The inbox / deduplication store.</summary>
	public static DataStoreKind Inbox { get; } = new("Inbox");

	/// <summary>A read-model projection store.</summary>
	public static DataStoreKind Projection { get; } = new("Projection");

	/// <summary>The saga / process-manager state store.</summary>
	public static DataStoreKind Saga { get; } = new("Saga");

	/// <summary>The audit / security event store (exempt by default; see ADR-336 Amendment 1a).</summary>
	public static DataStoreKind Audit { get; } = new("Audit");

	/// <summary>A cache store.</summary>
	public static DataStoreKind Cache { get; } = new("Cache");

	/// <inheritdoc/>
	public bool Equals(DataStoreKind other) =>
		string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is DataStoreKind other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

	/// <summary>Determines whether two store kinds are equal (case-insensitive).</summary>
	public static bool operator ==(DataStoreKind left, DataStoreKind right) => left.Equals(right);

	/// <summary>Determines whether two store kinds are not equal (case-insensitive).</summary>
	public static bool operator !=(DataStoreKind left, DataStoreKind right) => !left.Equals(right);

	/// <inheritdoc/>
	public override string ToString() => Value;
}
