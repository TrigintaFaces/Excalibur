// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Author-not-implementer regression lock for bd-e6y51s.
/// </summary>
/// <remarks>
/// <para>
/// Seam under lock: <c>AggregateRoot{TKey}.ApplySnapshot(ISnapshot)</c>
/// (src/Excalibur/Excalibur.Domain/Model/AggregateRoot.cs:369-380). Pre-fix the base
/// <c>ApplySnapshot</c> was a SILENT no-op: an aggregate that overrode
/// <see cref="AggregateRoot{TKey}.CreateSnapshot"/> (so a snapshot WAS produced) but forgot to
/// override <c>ApplySnapshot</c> would, on <see cref="AggregateRoot{TKey}.LoadFromSnapshot"/>,
/// load EMPTY/default state and then stamp <c>Version</c> from the snapshot -> silent state
/// corruption. The fix makes the base <c>ApplySnapshot</c> fail-closed by throwing
/// <see cref="NotSupportedException"/>, symmetric with the base <c>CreateSnapshot</c>.
/// </para>
/// <para>
/// Non-vacuity: against the pre-fix code (base <c>ApplySnapshot</c> = silent no-op) the lock is
/// RED -- <see cref="AggregateRoot{TKey}.LoadFromSnapshot"/> would return normally with no throw,
/// failing the <c>Should.Throw&lt;NotSupportedException&gt;</c> assertion. Against the fixed code it
/// is GREEN. The production RED-proof (mutating the impl) is deferred post-commit per the impl-
/// reserved handoff; this file is the author-not-implementer test seat.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class AggregateRootApplySnapshotFailClosedShould
{
	[Fact]
	public void LoadFromSnapshot_ThrowsNotSupportedException_WhenApplySnapshotNotOverridden()
	{
		// Arrange -- an aggregate that does NOT override ApplySnapshot, so LoadFromSnapshot reaches
		// the fail-closed base implementation. (In production this state arises when CreateSnapshot
		// was overridden, producing a snapshot, but ApplySnapshot was forgotten.)
		var aggregate = new NonSnapshotConsumingAggregate("agg-1");
		var snapshot = new TestSnapshot
		{
			AggregateId = "agg-1",
			Version = 42,
			AggregateType = nameof(NonSnapshotConsumingAggregate),
			Data = System.Text.Encoding.UTF8.GetBytes("snapshot state"),
		};

		// Act & Assert -- the base ApplySnapshot must throw NotSupportedException rather than
		// silently no-op'ing and then having LoadFromSnapshot stamp Version from the snapshot.
		var exception = Should.Throw<NotSupportedException>(() => aggregate.LoadFromSnapshot(snapshot));
		exception.Message.ShouldContain(nameof(NonSnapshotConsumingAggregate));
	}

	[Fact]
	public void LoadFromSnapshot_DoesNotStampVersion_WhenApplySnapshotNotOverridden()
	{
		// Arrange
		var aggregate = new NonSnapshotConsumingAggregate("agg-1");
		var snapshot = new TestSnapshot
		{
			AggregateId = "agg-1",
			Version = 42,
			AggregateType = nameof(NonSnapshotConsumingAggregate),
			Data = System.Text.Encoding.UTF8.GetBytes("snapshot state"),
		};

		// Act -- LoadFromSnapshot throws before stamping Version (fail-closed). Pre-fix the silent
		// no-op would let LoadFromSnapshot complete and set Version = 42 over empty state.
		_ = Should.Throw<NotSupportedException>(() => aggregate.LoadFromSnapshot(snapshot));

		// Assert -- state was NOT corrupted: Version stays at its default rather than being stamped
		// from a snapshot the aggregate never applied.
		aggregate.Version.ShouldBe(0);
	}

	#region Test Aggregate

	/// <summary>
	/// Aggregate that intentionally does NOT override <c>ApplySnapshot</c>, exercising the
	/// fail-closed base implementation under lock. In production this mirrors an aggregate that
	/// overrode <see cref="AggregateRoot{TKey}.CreateSnapshot"/> (producing a snapshot) but forgot
	/// the matching <c>ApplySnapshot</c> override.
	/// </summary>
	private sealed class NonSnapshotConsumingAggregate : AggregateRoot
	{
		public NonSnapshotConsumingAggregate(string id) : base(id)
		{
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No-op: not exercised by this lock.
		}

		// Note: ApplySnapshot is deliberately NOT overridden -> base fail-closed throw is under test.
	}

	#endregion Test Aggregate

	#region Test Snapshot

	private sealed class TestSnapshot : ISnapshot
	{
		public string SnapshotId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = string.Empty;
		public string AggregateType { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public ReadOnlyMemory<byte> Data { get; init; }
		public IDictionary<string, object>? Metadata { get; init; }
	}

	#endregion Test Snapshot
}
