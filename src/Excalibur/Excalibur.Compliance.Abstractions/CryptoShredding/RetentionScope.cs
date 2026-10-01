// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// Where a personal-data value sits, for the purpose of deciding which key protects it: the aggregate
/// type it is stored inside.
/// </summary>
/// <remarks>
/// <para>
/// <b>A scope is a VALUE, never an absence.</b> It is exactly one of <see cref="NotInAnAggregate"/> or
/// <see cref="For(string)"/>, so "not stored inside an aggregate" and "the caller did not know"
/// cannot arrive as the same thing. A nullable string carried three meanings that resolved identically —
/// and resolved toward destruction, which is the direction that cannot be walked back.
/// </para>
/// <para>
/// <b>It is a distinct type for a second reason.</b> Both of its parts are strings, and so is the data
/// subject identifier they travel beside. Passed as bare strings, transposing two of them compiles, runs,
/// and encrypts every subject's data under one shared handle — a mistake no diagnostic at any tier would
/// report. A scope cannot be passed where an identifier is expected.
/// </para>
/// <para>
/// <b>The unit is the aggregate type, whole, and that is a legal fact rather than a simplification.</b> An
/// obligation to keep a record attaches to the RECORD. A statute requiring sales records to be kept does
/// not require the buyer and permit deleting the salesperson — a partly-erased record is a MUTATED record,
/// and a mutated record has no evidentiary value, which is the entire reason for keeping it. So every data
/// subject named in a retained aggregate type is covered by that record's obligation.
/// </para>
/// </remarks>
public readonly struct RetentionScope : IEquatable<RetentionScope>
{
	private RetentionScope(string aggregateType) => AggregateType = aggregateType;

	/// <summary>
	/// Gets the scope for a value that is not stored inside an aggregate.
	/// </summary>
	/// <value>The scope carrying no aggregate type.</value>
	/// <remarks>
	/// The default, so a value reaching the key manager without a deliberate scope resolves to the data
	/// subject's own key — the key an erasure destroys — rather than to one it would not reach.
	/// </remarks>
	public static RetentionScope NotInAnAggregate => default;

	/// <summary>
	/// Gets the aggregate type the value is stored inside.
	/// </summary>
	/// <value>The aggregate type, or <see langword="null"/> when the value is not inside an aggregate.</value>
	public string? AggregateType { get; }

	/// <summary>
	/// Gets a value indicating whether this scope names an aggregate.
	/// </summary>
	/// <value><see langword="true"/> when it names an aggregate type; otherwise <see langword="false"/>.</value>
	public bool IsInAnAggregate => AggregateType is not null;

	/// <summary>Compares two scopes for equality.</summary>
	/// <param name="left">The left scope.</param>
	/// <param name="right">The right scope.</param>
	/// <returns><see langword="true"/> when they name the same aggregate type.</returns>
	public static bool operator ==(RetentionScope left, RetentionScope right) => left.Equals(right);

	/// <summary>Compares two scopes for inequality.</summary>
	/// <param name="left">The left scope.</param>
	/// <param name="right">The right scope.</param>
	/// <returns><see langword="true"/> when they differ.</returns>
	public static bool operator !=(RetentionScope left, RetentionScope right) => !left.Equals(right);

	/// <summary>
	/// Creates the scope for a value stored inside an aggregate.
	/// </summary>
	/// <param name="aggregateType">The aggregate type, as the event store records it.</param>
	/// <returns>The scope naming that aggregate type.</returns>
	/// <exception cref="ArgumentException">Thrown when the argument is null, empty or whitespace.</exception>
	public static RetentionScope For(string aggregateType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		return new RetentionScope(aggregateType);
	}

	/// <inheritdoc/>
	public bool Equals(RetentionScope other) =>
		string.Equals(AggregateType, other.AggregateType, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is RetentionScope other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() =>
		AggregateType is null ? 0 : StringComparer.Ordinal.GetHashCode(AggregateType);

	/// <summary>Returns a diagnostic description of the scope.</summary>
	/// <returns>The aggregate type, or a marker for the unscoped case.</returns>
	public override string ToString() => AggregateType ?? "(not in an aggregate)";
}
