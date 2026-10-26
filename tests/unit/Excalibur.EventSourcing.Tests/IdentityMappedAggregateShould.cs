// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.IdentityMap;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Binds <see cref="IdentityMappedAggregateExtensions.CreateOrUpdateAsync{TAggregate, TKey}"/> to the
/// defect it exists to make unwriteable: a committed identity binding whose event stream never arrived.
/// </summary>
/// <remarks>
/// The torn state is reachable in production from any interruption between the two writes — a deadline,
/// a crash, an eviction — and it does not clear, because the flag a caller would branch on reports
/// "mapping already existed" from then on. These arms pin that the STREAM decides, not the flag.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class IdentityMappedAggregateShould
{
	private const string System = "Legacy";
	private const string ExternalId = "CUST-1";
	private const string AggregateType = "Mapped";

	[Fact]
	public async Task CreateTheAggregateWhenABindingExistsButItsStreamNeverArrived()
	{
		// The tear: the identity was reserved by an earlier run that died before appending.
		var store = NewStore();
		var reserved = Guid.NewGuid();
		var torn = await store.TryBindAsync(System, ExternalId, AggregateType, reserved.ToString(), default);
		torn.MappingWasCreated.ShouldBeTrue("the arm is only meaningful if THIS call made the mapping");

		var repository = A.Fake<IEventSourcedRepository<IdentityMappedTestAggregate, Guid>>();
		A.CallTo(() => repository.GetByIdAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IdentityMappedTestAggregate?>(null));   // no stream -- the torn half
		A.CallTo(() => repository.SaveAsync(A<IdentityMappedTestAggregate>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new SaveResult(1, null, ProjectionFreshness.NotRequested)));

		var created = false;
		var updated = false;

		_ = await store.CreateOrUpdateAsync(
			repository, System, ExternalId, AggregateType, Guid.NewGuid(),
			create: id =>
			{
				created = true;
				id.ShouldBe(reserved, "the reserved id must be reused, never a fresh one");
				return new IdentityMappedTestAggregate(id);
			},
			update: _ => updated = true,
			default);

		created.ShouldBeTrue("a binding with no stream must be CREATED, not treated as an update");
		updated.ShouldBeFalse();
	}

	[Fact]
	public async Task UpdateTheAggregateWhenItsStreamDoesExist()
	{
		// LIVENESS. Without this arm an implementation that always creates would pass the arm above.
		var store = NewStore();
		var reserved = Guid.NewGuid();
		_ = await store.TryBindAsync(System, ExternalId, AggregateType, reserved.ToString(), default);

		var existing = new IdentityMappedTestAggregate(reserved);
		var repository = A.Fake<IEventSourcedRepository<IdentityMappedTestAggregate, Guid>>();
		A.CallTo(() => repository.GetByIdAsync(reserved, A<CancellationToken>._))
			.Returns(Task.FromResult<IdentityMappedTestAggregate?>(existing));
		A.CallTo(() => repository.SaveAsync(A<IdentityMappedTestAggregate>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new SaveResult(2, null, ProjectionFreshness.NotRequested)));

		var created = false;
		IdentityMappedTestAggregate? passedToUpdate = null;

		_ = await store.CreateOrUpdateAsync(
			repository, System, ExternalId, AggregateType, Guid.NewGuid(),
			create: id => { created = true; return new IdentityMappedTestAggregate(id); },
			update: a => passedToUpdate = a,
			default);

		created.ShouldBeFalse("an existing stream must not be re-created");
		passedToUpdate.ShouldBeSameAs(existing);
	}

	[Fact]
	public async Task ReuseTheExistingMappingRatherThanTheCandidateIdOnEveryLaterCall()
	{
		var store = NewStore();
		var reserved = Guid.NewGuid();
		_ = await store.TryBindAsync(System, ExternalId, AggregateType, reserved.ToString(), default);

		var repository = A.Fake<IEventSourcedRepository<IdentityMappedTestAggregate, Guid>>();
		A.CallTo(() => repository.GetByIdAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IdentityMappedTestAggregate?>(null));
		A.CallTo(() => repository.SaveAsync(A<IdentityMappedTestAggregate>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new SaveResult(1, null, ProjectionFreshness.NotRequested)));

		var candidate = Guid.NewGuid();
		Guid seen = default;

		_ = await store.CreateOrUpdateAsync(
			repository, System, ExternalId, AggregateType, candidate,
			create: id => { seen = id; return new IdentityMappedTestAggregate(id); },
			update: _ => { },
			default);

		seen.ShouldBe(reserved);
		seen.ShouldNotBe(candidate, "a fresh candidate id must be discarded when a mapping already exists");
	}

	private static MapStore NewStore() => new();

	/// <summary>
	/// Implements <see cref="IIdentityMapStore"/> DIRECTLY -- no first-party base supplies a member here,
	/// so the arms bind the interface's own reserve-if-absent contract rather than re-testing a base class.
	/// </summary>
	private sealed class MapStore : IIdentityMapStore
	{
		private readonly Dictionary<string, string> _map = [];

		private static string Key(string system, string externalId, string aggregateType) =>
			string.Join('', system, externalId, aggregateType);

		public ValueTask<string?> ResolveAsync(string externalSystem, string externalId, string aggregateType,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(_map.TryGetValue(Key(externalSystem, externalId, aggregateType), out var id) ? id : null);

		public ValueTask BindAsync(string externalSystem, string externalId, string aggregateType,
			string aggregateId, CancellationToken cancellationToken)
		{
			_map[Key(externalSystem, externalId, aggregateType)] = aggregateId;
			return ValueTask.CompletedTask;
		}

		public ValueTask<IdentityBindResult> TryBindAsync(string externalSystem, string externalId,
			string aggregateType, string aggregateId, CancellationToken cancellationToken)
		{
			var key = Key(externalSystem, externalId, aggregateType);
			if (_map.TryGetValue(key, out var existing))
			{
				return ValueTask.FromResult(new IdentityBindResult(existing, false));
			}

			_map[key] = aggregateId;
			return ValueTask.FromResult(new IdentityBindResult(aggregateId, true));
		}

		public ValueTask<bool> UnbindAsync(string externalSystem, string externalId, string aggregateType,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(_map.Remove(Key(externalSystem, externalId, aggregateType)));
	}

}

/// <summary>Minimal aggregate for the arms above; top-level so a dynamic proxy can reference it.</summary>
public sealed class IdentityMappedTestAggregate : AggregateRoot<Guid>
{
	public IdentityMappedTestAggregate()
	{ }

	public IdentityMappedTestAggregate(Guid id) : base(id)
	{ }

	protected override bool ApplyEventInternal(IDomainEvent @event) => true;
}
