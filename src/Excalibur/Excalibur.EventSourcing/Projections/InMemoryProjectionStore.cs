// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Excalibur.Data;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// An <see cref="IProjectionStore{TProjection}"/> that keeps projections in memory, for development,
/// tests and samples.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// <b>What it is for.</b> A first projection should run without a cluster. This answers the same filter
/// grammar the remote stores answer, through the same <see cref="FilterParser"/>, so a test written
/// against it says something about the store that replaces it.
/// </para>
/// <para>
/// <b>IT IS NOT TENANT-CONFINED, and it says so by presenting no capability.</b> The
/// <see cref="IProjectionStore{TProjection}"/> contract states that a store presenting no
/// <c>ITenantScopingCapability</c> is not confined by the framework, and this one presents none: two
/// tenants writing the same id address the same entry. A multi-tenant host must use a store that holds
/// that boundary, and the framework's capability requirement will refuse this one — which is the
/// intended outcome, not a limitation to work around.
/// </para>
/// <para>
/// <b>It keeps nothing after the process exits</b>, and it holds every projection it is given in memory,
/// so a rebuild over a large stream will grow until it does not fit. Both are properties of the thing,
/// not defects in it.
/// </para>
/// <para>
/// <b>A filter naming a property the projection does not have matches nothing</b>, which is what the
/// remote stores do with a field their index does not have. It is deliberately not an exception: an
/// implementation that threw where the others return an empty set would make a test pass or fail for
/// reasons the real store does not share.
/// </para>
/// <para>
/// <b>It records positions, so a projection that starts here can be replayed and rebuilt.</b> A
/// dictionary can hold a coordinate beside its value as easily as a row can, and the state and the
/// position are replaced TOGETHER as one immutable entry — so a reader never pairs one write's state
/// with another write's position, and the compare-and-set that
/// <see cref="IPositionedProjectionStore{TProjection}"/> requires is a real refusal rather than a
/// formality. Every write that can decline runs under one gate; reads stay lock-free.
/// </para>
/// </remarks>
public sealed class InMemoryProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>
	: IPositionedProjectionStore<TProjection>
	where TProjection : class
{
	private readonly ConcurrentDictionary<string, Entry> _projections = new(StringComparer.Ordinal);

	// Every write that can REFUSE is serialized here. The refusals compare the stored position and then
	// write, and a concurrent writer landing between those two steps is exactly the lost update the
	// contract exists to make impossible -- ConcurrentDictionary makes each operation atomic, never a
	// read-then-write pair.
	private readonly Lock _writes = new();

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(_projections.TryGetValue(id, out var entry) ? Rehydrate(entry) : null);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		cancellationToken.ThrowIfCancellationRequested();

		// UNPLACEABLE, written explicitly rather than by dropping the position. This state was not folded
		// from any known prefix, so a surviving position would assert a prefix the state does not contain
		// and the next conditional write would skip real events as already-applied.
		lock (_writes)
		{
			_projections[id] = new Entry(Snapshot(projection), ProjectionPosition.Unplaceable.ToStored());
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		cancellationToken.ThrowIfCancellationRequested();

		// Under the write gate so an erasure cannot land between a positioned write's check and its
		// store: a rebuild that raced a delete would otherwise reinstate what the erasure removed.
		lock (_writes)
		{
			// Discarded deliberately: the contract says deleting an absent projection succeeds silently.
			_ = _projections.TryRemove(id, out _);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var matches = Matching(filters);

		var order = options?.OrderBy;
		if (!string.IsNullOrWhiteSpace(order))
		{
			matches = options!.Descending
				? matches.OrderByDescending(projection => ValueOf(projection, order), ProjectionValueComparer.Instance)
				: matches.OrderBy(projection => ValueOf(projection, order), ProjectionValueComparer.Instance);
		}

		if (options?.Skip is > 0)
		{
			matches = matches.Skip(options.Skip.Value);
		}

		if (options?.Take is > 0)
		{
			matches = matches.Take(options.Take.Value);
		}

		return Task.FromResult<IReadOnlyList<TProjection>>([.. matches]);
	}

	/// <inheritdoc />
	// SUPPRESSED, not silenced, and the reason is a contract constraint rather than convenience.
	//
	// Counting with FILTERS has to evaluate them against projection values, which now means
	// rehydrating a snapshot -- the cost of making stored state un-aliasable. The interface's sibling
	// QueryAsync already declares this requirement; CountAsync does not, and it cannot be annotated
	// there because an AOT attribute on an interface member binds every implementation, including the
	// SQL stores that count in the database and materialize nothing.
	//
	// The unfiltered path below stays allocation-free and reflection-free, which is the common case.
	// A consumer publishing Native AOT and calling the FILTERED path on this store gets what
	// QueryAsync already warns about on every store; the in-memory store is a development and test
	// stand-in, and a projection body that cannot round-trip through System.Text.Json cannot be held
	// by any shipped store either.
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Filtered counting rehydrates snapshots; the interface member cannot carry the attribute without binding every provider. See the comment above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Filtered counting rehydrates snapshots; the interface member cannot carry the attribute without binding every provider. See the comment above.")]
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// No filters, no values to inspect: count the rows without rehydrating any of them.
		return filters is null || filters.Count == 0
			? Task.FromResult((long)_projections.Count)
			: Task.FromResult(Matching(filters).LongCount());
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
		string id,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		cancellationToken.ThrowIfCancellationRequested();

		// ONE observation, and no lock needed to get it: the entry is immutable and every write replaces
		// it wholesale, so there is no instant at which a reader can see one write's state beside another
		// write's position.
		if (!_projections.TryGetValue(id, out var entry))
		{
			// A row that does not exist is not a fold over any prefix. Unnumbered would assert a complete
			// fold over state that is not there.
			return Task.FromResult<(TProjection?, ProjectionPosition)>((null, ProjectionPosition.Unplaceable));
		}

		return Task.FromResult<(TProjection?, ProjectionPosition)>(
			(Rehydrate(entry), ProjectionPosition.FromStored(entry.StoredPosition)));
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task UpsertUnnumberedAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		cancellationToken.ThrowIfCancellationRequested();

		lock (_writes)
		{
			_projections[id] = new Entry(Snapshot(projection), ProjectionPosition.Unnumbered.ToStored());
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
		string id,
		TProjection projection,
		long? expectedPosition,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);

		// The negatives are the sentinel space for the two states that carry no number, so a caller naming
		// one as "the position I read" would match precisely the row a positioned write refuses by design.
		if (expectedPosition is { } claimed)
		{
			ArgumentOutOfRangeException.ThrowIfNegative(claimed, nameof(expectedPosition));
		}

		cancellationToken.ThrowIfCancellationRequested();

		lock (_writes)
		{
			if (!_projections.TryGetValue(id, out var entry))
			{
				// A caller that NAMED a position read a row that is now gone. Deletion is how erasure
				// removes personal data, so it must not be recreated -- only the absence-claiming caller
				// may insert.
				if (expectedPosition is not null)
				{
					return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Vanished, null));
				}

				_projections[id] = new Entry(Snapshot(projection), ProjectionPosition.At(newPosition).ToStored());

				return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
			}

			// BOTH conjuncts, and neither substitutes for the other: the equality orders concurrent writers
			// against each other, the inequality orders this writer against its own past -- which is what a
			// redelivery is, and it satisfies the equality by construction.
			if (expectedPosition is { } expected
				&& entry.StoredPosition == expected
				&& newPosition > entry.StoredPosition)
			{
				_projections[id] = new Entry(Snapshot(projection), ProjectionPosition.At(newPosition).ToStored());

				return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
			}

			// Nothing was written. A numberless row is TERMINAL rather than superseded: re-reading yields
			// the same row and the same refusal, so Superseded here would be an unbounded redelivery loop
			// against a projection only a rebuild can repair.
			var held = ProjectionPosition.FromStored(entry.StoredPosition);

			return Task.FromResult(held.Kind == ProjectionPositionKind.Positioned
				? new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, held.Value)
				: new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Unplaceable, null));
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
		string id,
		TProjection projection,
		long atPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentOutOfRangeException.ThrowIfNegative(atPosition);
		cancellationToken.ThrowIfCancellationRequested();

		lock (_writes)
		{
			// No create arm: an absent row means the projection was deleted, and re-folding the stream is
			// precisely the write that would reinstate what the erasure removed.
			if (!_projections.TryGetValue(id, out var entry))
			{
				return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Vanished, null));
			}

			if (entry.StoredPosition == atPosition)
			{
				// The state is rewritten and the position stays exactly where it was -- the events beneath
				// it changed, not the prefix it names.
				_projections[id] = new Entry(Snapshot(projection), entry.StoredPosition);

				return Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));
			}

			// A sentinel cannot equal a real position, so a numberless row falls to RequiresRebuild rather
			// than Superseded: there is nothing to re-read toward.
			return Task.FromResult(entry.StoredPosition < 0
				? new ProjectionRefoldResult(ProjectionRefoldOutcome.RequiresRebuild, null)
				: new ProjectionRefoldResult(ProjectionRefoldOutcome.Superseded, entry.StoredPosition));
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<ProjectionRebuildResult> RebuildAtPositionAsync(
		string id,
		TProjection projection,
		long newPosition,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
		cancellationToken.ThrowIfCancellationRequested();

		lock (_writes)
		{
			// Unconditional on POSITION -- a state folded from an empty seed has no prior prefix to be
			// conditional on -- but conditional on the row EXISTING, which is a different question and the
			// one a rebuild still has to ask.
			if (!_projections.ContainsKey(id))
			{
				return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Vanished));
			}

			_projections[id] = new Entry(Snapshot(projection), ProjectionPosition.At(newPosition).ToStored());

			return Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
		}
	}

	[RequiresUnreferencedCode("Deserializes the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Deserializes the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private IEnumerable<TProjection> Matching(IDictionary<string, object>? filters)
	{
		var stored = _projections.Values.Select(Rehydrate);

		return filters is null || filters.Count == 0
			? stored
			: stored.Where(projection => filters.All(filter => Satisfies(projection, filter)));
	}

	private static bool Satisfies(TProjection projection, KeyValuePair<string, object> filter)
	{
		// The SAME parser the remote stores use, so the grammar cannot drift between this store and the
		// one that replaces it.
		var parsed = FilterParser.Parse(filter.Key);

		var property = typeof(TProjection).GetProperty(parsed.PropertyName);

		if (property is null)
		{
			return false;
		}

		var actual = property.GetValue(projection);

		return parsed.Operator switch
		{
			FilterOperator.Equals => AreEqual(actual, filter.Value),
			FilterOperator.NotEquals => !AreEqual(actual, filter.Value),
			FilterOperator.GreaterThan => Compare(actual, filter.Value) > 0,
			FilterOperator.GreaterThanOrEqual => Compare(actual, filter.Value) >= 0,
			FilterOperator.LessThan => Compare(actual, filter.Value) < 0,
			FilterOperator.LessThanOrEqual => Compare(actual, filter.Value) <= 0,
			FilterOperator.In => IsIn(actual, filter.Value),
			FilterOperator.Contains => Text(actual).Contains(Text(filter.Value), StringComparison.OrdinalIgnoreCase),
			_ => AreEqual(actual, filter.Value),
		};
	}

	private static object? ValueOf(TProjection projection, string propertyName) =>
		typeof(TProjection).GetProperty(propertyName)?.GetValue(projection);

	private static bool AreEqual(object? actual, object? expected)
	{
		if (actual is null || expected is null)
		{
			return actual is null && expected is null;
		}

		// Filters arrive from dictionaries and configuration, so the filter's CLR type frequently differs
		// from the property's -- an int against a decimal, a string against an enum. Comparing after a
		// conversion is what makes "Amount = 100" match a decimal property.
		return Coerce(expected, actual.GetType()) is { } converted
			? actual.Equals(converted)
			: string.Equals(Text(actual), Text(expected), StringComparison.Ordinal);
	}

	private static int Compare(object? actual, object? expected)
	{
		if (actual is null || expected is null)
		{
			return actual is null && expected is null ? 0 : actual is null ? -1 : 1;
		}

		return Coerce(expected, actual.GetType()) is IComparable comparable && actual is IComparable left
			? left.CompareTo(comparable)
			: string.Compare(Text(actual), Text(expected), StringComparison.Ordinal);
	}

	private static bool IsIn(object? actual, object? expected) =>
		expected is IEnumerable candidates and not string
		&& candidates.Cast<object?>().Any(candidate => AreEqual(actual, candidate));

	private static object? Coerce(object value, Type target)
	{
		var nonNullable = Nullable.GetUnderlyingType(target) ?? target;

		if (nonNullable.IsInstanceOfType(value))
		{
			return value;
		}

		try
		{
			return nonNullable.IsEnum
				? Enum.Parse(nonNullable, Text(value), ignoreCase: true)
				: Convert.ChangeType(value, nonNullable, CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
		{
			// Not convertible: the caller's filter and the property are different kinds of thing, and the
			// comparison falls back to text rather than throwing -- a filter mismatch is an empty result
			// in every other store, not an error.
			return null;
		}
	}

	private static string Text(object? value) =>
		value switch
		{
			null => string.Empty,
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? string.Empty,
		};

	/// <summary>
	/// A stored state together with the encoded position of the last event folded into it.
	/// </summary>
	/// <remarks>
	/// Immutable and replaced wholesale, which is what makes the read one observation: there is no instant
	/// at which the state has been updated and the position has not. The position is held in its STORED
	/// encoding rather than as a <see cref="ProjectionPosition"/> so this store agrees with the eight
	/// providers by using the same encoder, instead of carrying a second representation of the same thing.
	/// </remarks>
	// HOLDS A SERIALIZED SNAPSHOT, not the caller's object, and that is the whole point of this type.
	//
	// Storing the instance aliased it on both boundaries: a read handed back the stored object, and a
	// write retained the caller's. So a handler that read the row, mutated it and then THREW had already
	// changed committed state -- with no second writer, no concurrency and no crash. The retry mutated
	// the same object again and advanced the position over a state that had folded one event twice. A
	// refused compare-and-set could not undo a mutation that had already happened through the shared
	// reference either, so locking the position check-and-replace protected nothing that mattered.
	//
	// Every store a consumer actually deploys serializes the projection into a column, so detaching here
	// is not extra strictness -- it is the behaviour the in-memory store exists to stand in for. Keeping
	// the snapshot as bytes rather than calling a copy helper at each boundary makes the aliasing
	// INEXPRESSIBLE: the stored value is not a TProjection, so no future boundary can hand the live one
	// out by forgetting to copy. Microsoft's in-memory IDistributedCache does the same thing for the
	// same reason.
	// The snapshot wire shape comes from the one canonical read-model factory every other projection
	// store sources, not from System.Text.Json's defaults. The in-memory store exists to stand in for a
	// deployed one, so a projection that round-trips here must round-trip the same way there; the
	// default (no-options) serializer writes a different shape and mis-reads through the canonical path.
	private static readonly System.Text.Json.JsonSerializerOptions SnapshotJsonOptions =
		ProjectionSerializationDefaults.CreateReadModelOptions();

	// Serialize on the way in. A snapshot taken here cannot be changed by the caller afterwards.
	[RequiresUnreferencedCode("Serializes the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Serializes the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static byte[] Snapshot(TProjection projection) =>
		System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(projection, SnapshotJsonOptions);

	// Deserialize on the way out, so each reader gets its OWN object. Two concurrent readers mutating
	// what they read cannot reach each other, and neither can reach the stored row.
	[RequiresUnreferencedCode("Deserializes the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Deserializes the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static TProjection Rehydrate(Entry entry) =>
		System.Text.Json.JsonSerializer.Deserialize<TProjection>(entry.Snapshot, SnapshotJsonOptions)
			?? throw new InvalidOperationException(
				$"The stored snapshot for a {typeof(TProjection).Name} projection deserialized to null. "
				+ "The projection type must round-trip through System.Text.Json.");

	private sealed record Entry(byte[] Snapshot, long StoredPosition);

	private sealed class ProjectionValueComparer : IComparer<object?>
	{
		internal static ProjectionValueComparer Instance { get; } = new();

		public int Compare(object? x, object? y) =>
			x is null || y is null
				? x is null && y is null ? 0 : x is null ? -1 : 1
				: x is IComparable comparable && x.GetType() == y.GetType()
					? comparable.CompareTo(y)
					: string.Compare(Text(x), Text(y), StringComparison.Ordinal);
	}
}
