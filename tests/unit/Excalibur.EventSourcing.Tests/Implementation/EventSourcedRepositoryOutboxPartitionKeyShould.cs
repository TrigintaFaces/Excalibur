// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA1506 // Excessive class coupling -- repository wiring needs many DI collaborators by design

using Excalibur.Dispatch;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

using IEventStore = Excalibur.EventSourcing.IEventStore;
using AppendResult = Excalibur.EventSourcing.AppendResult;

namespace Excalibur.EventSourcing.Tests.Implementation;

/// <summary>
/// The outbox partition key for a staged integration event is the STREAM, not the tenant.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OutboundMessage.PartitionKey"/>'s own contract says messages sharing a partition key "MUST be
/// delivered in ascending <c>SequenceNumber</c> order", and the relational outbox claims order by partition
/// key, then sequence number, then creation time. Keying on the TENANT therefore declared every aggregate a
/// tenant owns to be one ordered partition — a promise nothing keeps, since <c>SequenceNumber</c> is left at
/// 0 and ordering inside the partition falls back to <c>CreatedAt</c> across the whole tenant. It also
/// serialises a tenant's entire event flow onto one partition on any transport that honours partition
/// ordering: a Kafka key, a Service Bus session, an SQS message group.
/// </para>
/// <para>
/// NON-VACUOUS: the pre-fix expression was <c>PartitionKey = tenantId ?? aggregateId</c>. Restoring it makes
/// <see cref="PartitionTwoStreamsOfOneTenantSeparately"/> RED, because both streams then carry the tenant id.
/// The single-stream arms alone would NOT discriminate it — one tenant with one aggregate produces the same
/// string either way — which is why the two-stream arm exists and why the tenant collision arm is separate.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcedRepositoryOutboxPartitionKeyShould
{
	private const string TenantHeader = OutboxHeaderNames.TenantId;

	[MessageName("Test.Es.PartitionKeyIntegrationEvent")]
	internal sealed record PartitionKeyIntegrationEvent : DomainEvent, IIntegrationEvent
	{
		public string? Tenant { get; init; }
	}

	internal sealed class PartitionKeyAggregate : AggregateRoot
	{
		public PartitionKeyAggregate() { }
		public PartitionKeyAggregate(string id) : base(id) { }

		public void DoWork(string? tenant)
		{
			// Metadata is init-only and defaults to NULL on DomainEvent, so the dictionary is supplied at
			// construction rather than mutated afterwards.
			RaiseEvent(new PartitionKeyIntegrationEvent
			{
				Tenant = tenant,
				Metadata = tenant is null
					? null
					: new Dictionary<string, object>(StringComparer.Ordinal) { [TenantHeader] = tenant },
			});
		}

		protected override bool ApplyEventInternal(IDomainEvent @event) => true;
	}

	/// <summary>Stage one aggregate's event and return the partition key the outbox was given.</summary>
	private static async Task<string?> StageAndCapturePartitionKeyAsync(string aggregateId, string? tenant)
	{
		var aggregate = new PartitionKeyAggregate(aggregateId);
		aggregate.DoWork(tenant);

		OutboundMessage? staged = null;
		var outboxStore = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => outboxStore.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Invokes((OutboundMessage m, CancellationToken _) => staged = m);

		var eventStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => eventStore.AppendAsync(
				A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 0));

		var repository = new EventSourcedRepository<PartitionKeyAggregate>(
			eventStore,
			A.Fake<IEventSerializer>(),
			id => new PartitionKeyAggregate(id),
			Options.Create(new EventSourcedRepositoryOptions
			{
				OutboxStagingStrategy = OutboxStagingStrategy.EventuallyConsistent
			}),
			outboxStore: outboxStore);

		await repository.SaveAsync(aggregate, CancellationToken.None).ConfigureAwait(false);

		staged.ShouldNotBeNull("the integration event must be staged to the outbox");
		return staged!.PartitionKey;
	}

	[Fact]
	public async Task PartitionTwoStreamsOfOneTenantSeparately()
	{
		// THE ARM THAT DISCRIMINATES THE FIX. Under `tenantId ?? aggregateId` both of these carry the same
		// tenant id, so every aggregate the tenant owns lands in one declared-ordered partition.
		var first = await StageAndCapturePartitionKeyAsync("agg-alpha", "tenant-a").ConfigureAwait(false);
		var second = await StageAndCapturePartitionKeyAsync("agg-beta", "tenant-a").ConfigureAwait(false);

		first.ShouldNotBe(second,
			"two aggregates of one tenant are two independent streams and must not share an ordered partition");
	}

	[Fact]
	public async Task KeepOneStreamInOnePartitionAcrossAppends()
	{
		// The counterpart: the fix must not over-partition. One stream is one partition, every time, or the
		// ordering the partition key exists to provide is lost between appends.
		var first = await StageAndCapturePartitionKeyAsync("agg-stable", "tenant-a").ConfigureAwait(false);
		var second = await StageAndCapturePartitionKeyAsync("agg-stable", "tenant-a").ConfigureAwait(false);

		first.ShouldBe(second, "the same stream must map to the same partition on every append");
	}

	[Fact]
	public async Task SeparateTheSameAggregateIdAcrossTenants()
	{
		// Aggregate ids are unique only WITHIN a tenant, so keying on the id alone would put two tenants'
		// unrelated streams in one partition -- the failure mode in the other direction.
		var first = await StageAndCapturePartitionKeyAsync("agg-shared", "tenant-a").ConfigureAwait(false);
		var second = await StageAndCapturePartitionKeyAsync("agg-shared", "tenant-b").ConfigureAwait(false);

		first.ShouldNotBe(second, "the same aggregate id in two tenants is two streams, not one");
	}

	[Fact]
	public async Task SeparateAnUntenantedStreamFromATenantedOne()
	{
		// The untenanted key omits the tenant segment rather than substituting a blank one, so an
		// untenanted stream cannot collide with a tenant whose identifier is empty.
		var untenanted = await StageAndCapturePartitionKeyAsync("agg-shared", tenant: null).ConfigureAwait(false);
		var tenanted = await StageAndCapturePartitionKeyAsync("agg-shared", "tenant-a").ConfigureAwait(false);

		untenanted.ShouldNotBeNullOrEmpty("an untenanted stream still has a partition");
		untenanted.ShouldNotBe(tenanted);
	}

	[Fact]
	public void QualifyTheKeyByTenantAndTypeDirectly()
	{
		// The helper itself, so the composition is pinned independently of the staging path that calls it.
		EventSourcedRepository<PartitionKeyAggregate>
			.BuildStreamPartitionKey("t1", "Order", "o1")
			.ShouldBe("t1/Order/o1");

		EventSourcedRepository<PartitionKeyAggregate>
			.BuildStreamPartitionKey(null, "Order", "o1")
			.ShouldBe("Order/o1");

		EventSourcedRepository<PartitionKeyAggregate>
			.BuildStreamPartitionKey(string.Empty, "Order", "o1")
			.ShouldBe("Order/o1", "an empty tenant is the untenanted case, not a tenant named empty-string");
	}
}
