// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Events;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Roles;
using Excalibur.A3.Authorization.Roles.Events;
using Excalibur.Dispatch;
using Excalibur.Domain;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.A3.Tests.A3.Grants;

/// <summary>
/// jmt6uh — genuine read-path regression coverage for the shipped tz2fks envelope fix (anchor 04bf08a0c),
/// for Grant and Role specifically. The bead's own 2026-07-22 comment found that the existing
/// AddGrantCommandHandlerShould/RevokeGrantCommandHandlerShould files pair GetByIdAsync with Grant/Role
/// only INCIDENTALLY — those calls are on a faked <c>IAggregateRepository</c>, configured to
/// <c>.Returns(existingGrant)</c> directly, never exercising <see cref="EventSourcedRepository{TAggregate}"/>'s
/// real deserialize-then-<see cref="AggregateRoot.LoadFromHistory"/> path at all. Verified independently
/// here (re-reading those files) before writing this: that finding holds. This file is the missing genuine
/// coverage — it drives <see cref="EventSourcedRepository{TAggregate}.GetByIdAsync"/> itself, through a real
/// <see cref="JsonEventSerializer"/> (not a mock), so a deserialization/version-restoration regression
/// would actually surface here.
/// </summary>
/// <remarks>
/// Structural note, recorded rather than silently assumed: at HEAD, <see cref="IDomainEvent"/> no longer
/// declares <c>Version</c> at all (removed under the envelope-authoritative redesign — see
/// <see cref="HistoricEvent"/>'s own doc comment: "replay reads the version from this envelope, never from
/// the event payload... there is no HistoricEvent without a Version"). That makes tz2fks's ORIGINAL failure
/// mode (an event silently never stamped, staying at the default 0) structurally unreachable today, not
/// merely fixed. This suite is still real, non-redundant coverage: it proves the CURRENT mechanism — version
/// sourced from <see cref="StoredEvent.Version"/>, restored by <see cref="EventSourcedRepository{TAggregate}"/>
/// on every load — actually round-trips correctly for these two shipped, real (not synthetic) aggregates,
/// which nothing in the existing suite did.
/// </remarks>
[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class GrantAndRoleRehydrationShould
{
	public GrantAndRoleRehydrationShould() =>
		ApplicationContext.Init(new Dictionary<string, string?> { ["APP_CONTEXT_NAME"] = "test" });

	/// <summary>LIVENESS + SAFETY — a Grant persisted through two real events rehydrates correctly and in isolation.</summary>
	[Fact]
	public async Task RehydrateAGrant_WithCorrectStateVersionAndNoCrossStreamLeakage()
	{
		var serializer = new JsonEventSerializer(TestEventTypeRegistry.Instance);
		var grantId = "user-1:tenant-1:role:admin";
		var otherGrantId = "user-2:tenant-1:role:admin";

		var added = new GrantAdded(
			"user-1", "Ada Lovelace", "TestApp", "tenant-1", "role", "admin", null, "issuer-1", DateTimeOffset.UtcNow.AddMinutes(-10));
		var revoked = new GrantRevoked(
			"user-1", "Ada Lovelace", "TestApp", "tenant-1", "role", "admin", null, "issuer-2", DateTimeOffset.UtcNow);

		var otherAdded = new GrantAdded(
			"user-2", "Grace Hopper", "TestApp", "tenant-1", "role", "admin", null, "issuer-1", DateTimeOffset.UtcNow.AddMinutes(-5));

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(grantId, "Grant", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>
			{
				ToStoredEvent(serializer, added, grantId, "Grant", 0),
				ToStoredEvent(serializer, revoked, grantId, "Grant", 1),
			});
		_ = A.CallTo(() => eventStore.LoadAsync(otherGrantId, "Grant", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { ToStoredEvent(serializer, otherAdded, otherGrantId, "Grant", 0) });

		var repository = new EventSourcedRepository<Grant>(
			eventStore, serializer, Grant.Create, Options.Create(new EventSourcedRepositoryOptions()));

		var result = await repository.GetByIdAsync(grantId, CancellationToken.None);

		result.ShouldNotBeNull();
		result.Version.ShouldBe(2, "two events (versions 0 and 1) were appended — Version is the applied COUNT, so it must be 2, not stop at 1 after the first");
		result.UserId.ShouldBe("user-1", "the LIVENESS arm: state must come from the events actually applied, not merely be non-null");
		result.FullName.ShouldBe("Ada Lovelace");
		result.RevokedBy.ShouldBe("issuer-2", "the second (GrantRevoked) event must have applied — a version-loss regression would stop replay after the first event");
		result.RevokedOn.ShouldNotBeNull();

		// SAFETY: a different stream's aggregate does not carry this stream's revocation.
		var other = await repository.GetByIdAsync(otherGrantId, CancellationToken.None);
		other.ShouldNotBeNull();
		other.UserId.ShouldBe("user-2");
		other.RevokedBy.ShouldBeNull("a different aggregate's stream must not leak this stream's events");
		other.Version.ShouldBe(1, "the other stream has exactly one event — a cross-stream leak would inflate this beyond 1");
	}

	/// <summary>LIVENESS + SAFETY — a Role persisted through two real events rehydrates correctly and in isolation.</summary>
	[Fact]
	public async Task RehydrateARole_WithCorrectStateVersionAndNoCrossStreamLeakage()
	{
		var serializer = new JsonEventSerializer(TestEventTypeRegistry.Instance);
		var roleId = "role-1";
		var otherRoleId = "role-2";

		IDomainEvent created = new RoleCreated
		{
			RoleId = "role-1",
			Name = "Administrator",
			Description = "Full access",
			TenantId = "tenant-1",
			ActivityGroupNames = ["admins"],
			CreatedBy = "creator-1",
		};
		IDomainEvent modified = new RoleModified
		{
			RoleId = "role-1",
			Name = "Super Administrator",
			Description = "Full access, renamed",
		};

		IDomainEvent otherCreated = new RoleCreated
		{
			RoleId = "role-2",
			Name = "Viewer",
			Description = "Read only",
			TenantId = "tenant-1",
			ActivityGroupNames = ["viewers"],
			CreatedBy = "creator-1",
		};

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.LoadAsync(roleId, "Role", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>
			{
				ToStoredEvent(serializer, created, roleId, "Role", 0),
				ToStoredEvent(serializer, modified, roleId, "Role", 1),
			});
		_ = A.CallTo(() => eventStore.LoadAsync(otherRoleId, "Role", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { ToStoredEvent(serializer, otherCreated, otherRoleId, "Role", 0) });

		var repository = new EventSourcedRepository<Role>(
			eventStore, serializer, Role.Create, Options.Create(new EventSourcedRepositoryOptions()));

		var result = await repository.GetByIdAsync(roleId, CancellationToken.None);

		result.ShouldNotBeNull();
		result.Version.ShouldBe(2, "two events were appended — Version is the applied count, so it must be 2");
		result.Name.ShouldBe("Super Administrator", "the RoleModified event must have applied on top of RoleCreated's initial name");

		var other = await repository.GetByIdAsync(otherRoleId, CancellationToken.None);
		other.ShouldNotBeNull();
		other.Name.ShouldBe("Viewer", "a different role's stream must not leak this stream's rename");
		other.Version.ShouldBe(1);
	}

	private sealed class TestEventTypeRegistry : Excalibur.Dispatch.IEventTypeRegistry
	{
		public static readonly TestEventTypeRegistry Instance = new();

		private static readonly Dictionary<string, Type> ByName = new(StringComparer.Ordinal)
		{
			[Excalibur.Dispatch.MessageNameHelper.GetName(typeof(GrantAdded))] = typeof(GrantAdded),
			[Excalibur.Dispatch.MessageNameHelper.GetName(typeof(GrantRevoked))] = typeof(GrantRevoked),
			[Excalibur.Dispatch.MessageNameHelper.GetName(typeof(RoleCreated))] = typeof(RoleCreated),
			[Excalibur.Dispatch.MessageNameHelper.GetName(typeof(RoleModified))] = typeof(RoleModified),
		};

		public Type? ResolveType(string eventTypeName) => ByName.GetValueOrDefault(eventTypeName);

		public string? GetTypeName(Type eventType) => Excalibur.Dispatch.MessageNameHelper.GetName(eventType);
	}

	private static StoredEvent ToStoredEvent(
		JsonEventSerializer serializer, IDomainEvent domainEvent, string aggregateId, string aggregateType, long version) =>
		new(
			EventId: Guid.NewGuid().ToString(),
			AggregateId: aggregateId,
			AggregateType: aggregateType,
			EventType: serializer.GetTypeName(domainEvent.GetType()),
			EventData: serializer.SerializeEvent(domainEvent),
			Metadata: null,
			Version: version,
			Timestamp: DateTimeOffset.UtcNow);
}
