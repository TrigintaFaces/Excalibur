// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Globalization;

using Excalibur.Compliance;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Projections;

/// <summary>
/// Real-DI + real-SQL-Server arm for the <c>projections-rebuild-is-erasure-path</c> manifest row: an
/// erased subject's events are never handed to a projection, while every other subject's still are.
/// </summary>
/// <remarks>
/// <para>
/// <b>Guarantee under test.</b> <c>Excalibur.EventSourcing/ARCHITECTURE.md</c> section 4 - the rebuild
/// "recognizes that marker structurally, before attempting to resolve or deserialize the event, and skips
/// it -- <i>it is never handed to a projection handler</i>, so it can never populate projection state."
/// </para>
/// <para>
/// <b>Where the property is asserted, and why that is a derivation rather than a convenience.</b> The
/// falsifiable sentence states the guarantee at the <i>handler</i> boundary; "so it can never populate
/// projection state" is a consequence the sentence itself derives. For materialized views that boundary is
/// the view builder, so this arm asserts what
/// <see cref="IMaterializedViewBuilder{TView}.Apply"/> was handed. Asserting instead on the persisted view
/// would be strictly weaker: an empty view is satisfied by the event being skipped (the guarantee) AND by a
/// builder that ran and wrote nothing (a bug), and nothing distinguishes those two outcomes downstream.
/// </para>
/// <para>
/// <b>Why the view store is present at all.</b> <c>MaterializedViewProcessor</c> takes
/// <see cref="IMaterializedViewStore"/> as a required constructor parameter, so a store is a dependency the
/// type will not construct without -- not a sink anything here is asserted against. It is the SQL Server
/// store, running in the same container as the event store: five materialized view stores ship, and the
/// three living in the <c>EventSourcing.*</c>/<c>Data.MongoDB</c> packages are the atomic ones, so no second
/// container is needed for this arm and the substrate under the processor is the stronger of the two kinds.
/// </para>
/// <para>
/// <b>Why this surface and not <c>ProjectionRebuildService</c>.</b> That type is <c>internal sealed</c> with
/// no reference anywhere in <c>src/</c> outside its own declaring file and no registration extension, so no
/// consumer can construct or reach it. <see cref="IMaterializedViewProcessor"/> is what a consumer's host
/// actually rebuilds through -- registered by <c>AddMaterializedViews</c> and scheduled by the projection
/// rebuild job -- so a lock on the unreachable sibling would leave this guarantee untested on every path a
/// consumer can take.
/// </para>
/// <para>
/// <b>Why the whole composition is the builder path.</b> <c>SqlServerGlobalStreamQuery</c> is internal and is
/// registered by exactly one seam, <c>UseSqlServer(...)</c>; the piecemeal <c>AddSqlServerEventStore(...)</c>
/// overload does not register it. A rebuild therefore cannot be composed except the way a consumer composes
/// it, which is the point of the row.
/// </para>
/// <para>
/// <b>Both halves.</b> SAFETY - the erased subject's event is never handed to the builder. LIVENESS - the
/// other subject's is, and the rebuild runs to completion; a rebuild that threw at the tombstone, or that
/// handed nothing to anybody, would satisfy the safety assertion while making every erased subject's stream
/// permanently un-replayable.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
public sealed class SqlServerProjectionRebuildErasureEndToEndShould
{
	private const string AggregateType = "Subject";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerProjectionRebuildErasureEndToEndShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	[MessageName("Test.SqlServerProjectionRebuildErasure.SubjectActivity")]
	private sealed record SubjectActivity(string SubjectId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// The projection state. Only its shape matters here - the assertion is on what the builder was handed,
	/// not on what was persisted.
	/// </summary>
	private sealed class SubjectActivityView
	{
		public string SubjectId { get; set; } = string.Empty;

		public int AppliedCount { get; set; }
	}

	/// <summary>
	/// A projection that records every event handed to it. It implements
	/// <see cref="IMaterializedViewBuilder{TView}"/> directly and inherits no framework base, so the arm binds
	/// the interface's own contract rather than a base class's convenience implementation of it.
	/// </summary>
	private sealed class RecordingSubjectActivityViewBuilder : IMaterializedViewBuilder<SubjectActivityView>
	{
		private readonly ConcurrentQueue<string> _handedToApply = new();

		/// <summary>
		/// Gets the subject ids handed to <see cref="Apply"/>, in order. This is the observation point: an
		/// event that reaches a projection handler appears here, and one the rebuild skipped cannot.
		/// </summary>
		public IReadOnlyCollection<string> HandedToApply => _handedToApply;

		public string ViewName => "e2e-projection-rebuild-erasure";

		public IReadOnlyList<Type> HandledEventTypes { get; } = [typeof(SubjectActivity)];

		public string? GetViewId(IDomainEvent @event) => ((SubjectActivity)@event).SubjectId;

		public SubjectActivityView Apply(SubjectActivityView view, IDomainEvent @event)
		{
			var activity = (SubjectActivity)@event;

			_handedToApply.Enqueue(activity.SubjectId);

			view.SubjectId = activity.SubjectId;
			view.AppliedCount++;

			return view;
		}

		public SubjectActivityView CreateNew() => new();
	}

	/// <summary>
	/// Interprets the data-subject hash AS the aggregate id, so the arm controls exactly which stream the
	/// real erasure path tombstones.
	/// </summary>
	private sealed class HashIsAggregateIdMapping : IAggregateDataSubjectMapping
	{
		public Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
			string dataSubjectIdHash, string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AggregateReference>>(
				[new AggregateReference(dataSubjectIdHash, AggregateType)]);
	}

	[Fact]
	public async Task NeverHandAnErasedSubjectsEventsToAProjection_WhileStillRebuildingEveryOtherSubject()
	{
		var cancellationToken = TestContext.Current.CancellationToken;

		_fixture.DockerAvailable.ShouldBeTrue(
			"a rebuild that repopulates an erased subject's projection re-creates data we told a regulator "
			+ "was destroyed - this real-SQL-Server arm must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// The rebuild replays the WHOLE global stream, so the table must hold only this arm's events: an
		// event type left behind by a sibling class would not resolve and the replay would halt on it.
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		await using var provider = BuildConsumerComposition();

		// The store provisions its own tables, which is how a consumer's first host start creates them.
		await ((SqlServerMaterializedViewStore)provider.GetRequiredService<IMaterializedViewStore>())
			.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

		var eventStore = provider.GetRequiredKeyedService<IEventStore>("default");
		var erasedSubject = "subject-" + Guid.NewGuid().ToString("N");
		var retainedSubject = "subject-" + Guid.NewGuid().ToString("N");

		_ = await eventStore.AppendAsync(
			erasedSubject,
			AggregateType,
			[new SubjectActivity(erasedSubject), new SubjectActivity(erasedSubject)],
			-1,
			cancellationToken).ConfigureAwait(false);

		_ = await eventStore.AppendAsync(
			retainedSubject,
			AggregateType,
			[new SubjectActivity(retainedSubject)],
			-1,
			cancellationToken).ConfigureAwait(false);

		// The real erasure path, through the real contributor - the tombstone is whatever production writes,
		// not a marker this arm hand-builds.
		var erasure = await provider.GetRequiredService<IErasureContributor>()
			.EraseAsync(
				new ErasureContributorContext
				{
					RequestId = Guid.NewGuid(),
					DataSubjectIdHash = erasedSubject,
					IdType = default,
					Scope = ErasureScope.User,
					TenantId = null,
				},
				cancellationToken).ConfigureAwait(false);

		erasure.Success.ShouldBeTrue("precondition: the erase must actually run before a rebuild can skip it");
		(await SurvivingPayloadCountAsync(erasedSubject).ConfigureAwait(false)).ShouldBe(
			0,
			"precondition: the erased subject's payloads are gone from SQL Server, so the rows the rebuild "
			+ "reads really are tombstones");
		(await SurvivingPayloadCountAsync(retainedSubject).ConfigureAwait(false)).ShouldBe(
			1,
			"precondition: the other subject's event survives the erase, so a later absence from the "
			+ "projection would be the rebuild's doing and not the erase's");

		var projection = (RecordingSubjectActivityViewBuilder)provider
			.GetRequiredService<IMaterializedViewBuilder<SubjectActivityView>>();

		await provider.GetRequiredService<IMaterializedViewProcessor>()
			.RebuildAsync(cancellationToken).ConfigureAwait(false);

		// SAFETY, first half. A rebuild that resolved the tombstoned rows would hand them here and repopulate
		// the very projection state the erasure destroyed.
		projection.HandedToApply.ShouldNotContain(
			erasedSubject,
			"an erased subject's event was handed to a projection during a rebuild, so the rebuild "
			+ "re-creates projection state for a subject whose data we are obliged to have destroyed");

		// SAFETY, second half - and this is the arm that makes the first one mean something. Erasure destroys
		// the payload, so a tombstone that DID reach the builder would arrive with its subject id gone and the
		// assertion above would pass while the guarantee was broken. Counting closes that: the erased subject
		// contributed two of the three events in the stream, so anything handed over beyond the single
		// retained event came from the tombstoned stream, whatever it deserialized to.
		projection.HandedToApply.Count.ShouldBe(
			1,
			"the rebuild handed more events to the projection than the one surviving event in the stream, so "
			+ "it is replaying tombstoned rows - an assertion on the erased subject's id alone cannot see "
			+ "this, because erasure is what removed that id from the payload");

		// LIVENESS. Without this the arm above is satisfied by a rebuild that halts at the first tombstone,
		// or by one that hands nothing to anybody - both of which make erasure a denial of service on every
		// other subject's projection rather than a guarantee.
		projection.HandedToApply.ShouldContain(
			retainedSubject,
			"the rebuild handed no event to the projection for a subject that was never erased, so it "
			+ "either halted at the tombstone or is not reaching projections at all");
	}

	/// <summary>
	/// Composes the rebuild the way a consumer does: the SQL Server provider seam (event store, global stream
	/// query and view store), erasure, and a registered projection.
	/// </summary>
	private ServiceProvider BuildConsumerComposition()
	{
		var connectionString = _fixture.ConnectionString;

		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddExcaliburEventSourcing(eventSourcing =>
		{
			// Registered explicitly rather than by assembly scan: the serializer rejects an unregistered type
			// name by default, which is the secure default a consumer runs under.
			_ = eventSourcing.RegisterEventTypes<SubjectActivity>();
			_ = eventSourcing.UseEventStoreErasure<HashIsAggregateIdMapping>();
			_ = eventSourcing.UseSqlServer(sql => sql
				.ConnectionString(connectionString)
				.UseMaterializedViewStore());
		});

		_ = services.AddMaterializedViews(views =>
			views.AddBuilder<SubjectActivityView, RecordingSubjectActivityViewBuilder>());

		return services.BuildServiceProvider();
	}

	private async Task<int> SurvivingPayloadCountAsync(string aggregateId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2100 // Schema and table are constants owned by the fixture.
		await using var command = new SqlCommand(
			$"SELECT COUNT(*) FROM [{_fixture.SchemaName}].[{_fixture.TableName}] "
			+ "WHERE AggregateId = @aggId AND EventData IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@aggId", aggregateId);

		return Convert.ToInt32(
			await command.ExecuteScalarAsync().ConfigureAwait(false), CultureInfo.InvariantCulture);
	}
}
