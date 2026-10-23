// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Queries;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class EventStoreAppendPositionArmShould
{
	[Fact]
	public Task AcceptTheActualEndpointEvenWhenPositionsHaveGaps() =>
		new Probe(reportInteriorPosition: false).AppendAsync_LargerThanOneStatement_ShouldRemainContiguousAndOrdered();

	[Fact]
	public async Task RefuseAnInteriorPositionThatPassesBothAllocationInequalities()
	{
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(
			new Probe(reportInteriorPosition: true).AppendAsync_LargerThanOneStatement_ShouldRemainContiguousAndOrdered);
		error.Message.ShouldContain("stored global positions");
	}

	private sealed class Probe(bool reportInteriorPosition) : EventStoreConformanceTestKit
	{
		protected override void ConfigureProvider(IServiceCollection services, IJsonTypeInfoResolver? eventTypeInfoResolver)
		{
			var store = new PositionedStore(reportInteriorPosition);
			services.AddSingleton<IEventStore>(store);
			services.AddSingleton<IGlobalStreamQuery>(store);
		}
	}

	// A controlled implementation for proving that the real kit arm distinguishes truthful endpoints
	// from interior ones. This is a test of the instrument, not evidence about provider atomicity.
	private sealed class PositionedStore(bool reportInteriorPosition) : IEventStore, IGlobalStreamQuery
	{
		private readonly List<StoredEvent> _events = [];
		public ValueTask<AppendResult> AppendAsync(string aggregateId, string aggregateType,
			IEnumerable<IDomainEvent> events, long expectedVersion, CancellationToken cancellationToken)
		{
			var batch = events.ToList();
			var first = 10L + _events.Count * 2L;
			for (var i = 0; i < batch.Count; i++)
			{
				_events.Add(new StoredEvent(batch[i].EventId, aggregateId, aggregateType,
					batch[i].GetType().Name, [], null, i, batch[i].OccurredAt)
				{ GlobalPosition = first + i * 2L });
			}
			var last = _events[^1].GlobalPosition;
			if (reportInteriorPosition && batch.Count > 1) { last -= 2; }
			return ValueTask.FromResult(AppendResult.CreateSuccess(batch.Count - 1, first, last));
		}

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(string aggregateId, string aggregateType,
			CancellationToken cancellationToken) => LoadAsync(aggregateId, aggregateType, -1, cancellationToken);

		public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(string aggregateId, string aggregateType,
			long fromVersion, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>(_events.Where(e => e.AggregateId == aggregateId
				&& e.AggregateType == aggregateType && e.Version > fromVersion).ToArray());

		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(GlobalStreamPosition position, int maxCount,
			CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
			_events.Where(e => e.GlobalPosition > position.Position).Take(maxCount).ToArray());

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(string eventType,
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>(_events.Where(e => e.GlobalPosition > position.Position
				&& e.EventType == eventType).Take(maxCount).ToArray());

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(_events.Count == 0 ? 0L : _events[^1].GlobalPosition);
	}
}
