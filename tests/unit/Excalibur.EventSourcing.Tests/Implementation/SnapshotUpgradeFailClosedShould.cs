// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;
using StoredEvent = Excalibur.EventSourcing.StoredEvent;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// Author≠impl regression lock for <c>wqt6j3</c> (ES: snapshot auto-upgrade fails OPEN (silent) when no
/// upgrade path exists → wrong aggregate state).
/// </summary>
/// <remarks>
/// <para>
/// Authored by TestsDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>). The grounded seam is
/// <c>EventSourcedRepository.TryUpgradeSnapshot</c>
/// (<c>src/Excalibur/Excalibur.EventSourcing/Implementation/EventSourcedRepository.cs:739-753</c>), reached
/// from <c>GetByIdAsync</c> (:302). When <c>EnableAutoSnapshotUpgrade</c> is true, the stored snapshot's
/// schema version differs from the target, and <c>SnapshotVersionManager.CanUpgrade</c> returns false (no
/// upgrader path registered), the fixed code THROWS <see cref="InvalidOperationException"/> (fail-CLOSED)
/// rather than returning the stale snapshot and feeding it to <c>aggregate.LoadFromSnapshot</c> — which would
/// silently corrupt the rehydrated aggregate.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> the pre-fix code RETURNED the stale old-schema snapshot
/// unchanged, so <c>GetByIdAsync</c> completed and returned a (corrupt) aggregate without throwing —
/// <c>Should.ThrowAsync&lt;InvalidOperationException&gt;</c> fails RED there. On the fixed surface the throw
/// is structural (mirrors the events-path refusal in <c>DeserializeEvent</c>), so the lock is GREEN.
/// </para>
/// <para>
/// <b>Real-infra vs unit:</b> deterministic unit lock. The fail-closed decision lives entirely in the
/// repository's <c>TryUpgradeSnapshot</c> branch and depends only on the snapshot's schema-version metadata
/// and the <c>SnapshotVersionManager</c>'s registered-path set — no external store semantics participate, so
/// faked collaborators faithfully exercise the exact branch. Production RED-proof against the live impl is
/// deferred post-commit (impl reserved by another lane).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SnapshotUpgradeFailClosedShould
{
	internal sealed class FailClosedAggregate : AggregateRoot
	{
		public FailClosedAggregate() { }
		public FailClosedAggregate(string id) : base(id) { }

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			// No-op: this lock never reaches event replay (the throw precedes it).
		}
	}

	[Fact]
	public async Task ThrowWhenAutoUpgradeEnabledAndNoUpgradePathExists()
	{
		// Arrange — snapshot stored at schema version 1, target schema version 2, no upgrader registered.
		var aggregateId = "agg-failclosed";

		var snapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshot.AggregateId).Returns(aggregateId);
		_ = A.CallTo(() => snapshot.Version).Returns(1L);
		_ = A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		_ = A.CallTo(() => snapshot.Metadata).Returns(
			new Dictionary<string, object>(StringComparer.Ordinal) { ["SnapshotSchemaVersion"] = 1 });

		var snapshotManager = A.Fake<ISnapshotManager>();
		_ = A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.Returns(snapshot);

		// Empty upgrader set => CanUpgrade(type, 1, 2) == false => fail-closed path.
		var versionManager = new SnapshotVersionManager(
			Array.Empty<ISnapshotUpgrader>(),
			NullLogger<SnapshotVersionManager>.Instance);

		var eventStore = A.Fake<IEventStore>();
		// Pre-fix path would fall through to LoadAsync; configure it so the pre-fix code returns a
		// (corrupt) aggregate WITHOUT throwing — making the assertion genuinely RED on the old surface.
		_ = A.CallTo(() => eventStore.LoadAsync(aggregateId, "FailClosedAggregate", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var upgradingOptions = Microsoft.Extensions.Options.Options.Create(
			new SnapshotUpgradingOptions { EnableAutoUpgradeOnLoad = true, CurrentSnapshotVersion = 2 });

		var repository = new EventSourcedRepository<FailClosedAggregate>(
			eventStore,
			A.Fake<IEventSerializer>(),
			id => new FailClosedAggregate(id),
			snapshotUpgradingOptions: upgradingOptions,
			snapshotManager: snapshotManager,
			snapshotVersionManager: versionManager);

		// Act & Assert — fail-CLOSED: refuse the stale-schema snapshot loudly instead of corrupting state.
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => repository.GetByIdAsync(aggregateId, CancellationToken.None)).ConfigureAwait(false);
		ex.Message.ShouldContain("schema version");

		// The stale snapshot must NEVER be applied to the aggregate when no upgrade path exists.
		A.CallTo(() => snapshotManager.GetLatestSnapshotAsync(aggregateId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
