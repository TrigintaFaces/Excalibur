// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Projections;

/// <summary>Database-routed cutover and the separate native alias primitive against real infrastructure.</summary>
/// <remarks>Framework routing lives in SQLite with a lifetime foreign key. Native aliases change only through
/// their explicit administrative API; they do not automatically follow a framework reader pointer.</remarks>
public abstract class CutoverIntegrationTestBase
{
	/// <summary>A fingerprint of the length the roster contract requires.</summary>
	private const string PreviousFingerprint =
		"1111111111111111111111111111111111111111111111111111111111111111";

	/// <summary>A fingerprint distinct from the previous generation's.</summary>
	private const string NewFingerprint =
		"2222222222222222222222222222222222222222222222222222222222222222";

	/// <summary>
	/// Gets the SQLite connection string backing the real checkpoint store for one arm.
	/// </summary>
	/// <remarks>Each arm gets its own database, so no arm can observe another's roster.</remarks>
	protected abstract string CheckpointConnectionString { get; }

    /// <summary>Canonical destination provider prefix.</summary>
    protected abstract string DestinationPrefix { get; }

	/// <summary>
	/// Asserts the engine this suite needs is reachable, and fails the arm when it is not.
	/// </summary>
	protected abstract void RequireEngine();

	/// <summary>
	/// Creates an index on the engine so an alias can be pointed at it.
	/// </summary>
	/// <param name="indexName">The index to create.</param>
	protected abstract Task CreateIndexAsync(string indexName);

	/// <summary>
	/// Points <paramref name="aliasName"/> at <paramref name="indexName"/> as the starting state.
	/// </summary>
	/// <param name="aliasName">The alias readers query.</param>
	/// <param name="indexName">The index to bind it to.</param>
	protected abstract Task BindAliasAsync(string aliasName, string indexName);

	/// <summary>
	/// Reads which indices an alias resolves to, straight from the engine's own client.
	/// </summary>
	/// <param name="aliasName">The alias to resolve.</param>
	/// <returns>Every index the engine reports behind that alias.</returns>
	/// <remarks>
	/// Deliberately NOT routed through the repoint under test. An arm that verified the swap through the
	/// same code that performed it would assert that the component agrees with itself.
	/// </remarks>
	protected abstract Task<IReadOnlyCollection<string>> ReadAliasIndicesAsync(string aliasName);

	/// <summary>
	/// Creates the provider's repoint primitive for one alias.
	/// </summary>
	/// <param name="aliasName">The alias readers query.</param>
	/// <returns>The provider's implementation, addressing the running engine.</returns>
	protected abstract IProjectionGenerationRepoint CreateRepoint(string aliasName);

	/// <summary>
	/// A freshly bound generation has no checkpoint at all, and that absence must refuse the cutover
	/// even when the threshold is wide enough to admit any real lag.
	/// </summary>
	/// <remarks>
	/// <b>The threshold here is deliberately generous.</b> The head is 100 and the threshold is 1000, so
	/// a missing cursor read as position zero would yield a lag of 100, pass the gate, and swap readers
	/// onto an empty index. Nothing but the presence check can refuse this cutover, which is what makes
	/// the arm bind that check rather than the comparison beside it.
	/// </remarks>
	protected async Task RefuseTheCutoverWhenTheNewGenerationHasNoCursor()
	{
		RequireEngine();

		var scenario = await ArrangeAsync(previousCursor: 100, newCursor: null, head: 100).ConfigureAwait(false);

		var result = await scenario.Cutover.CutOverAsync(
			scenario.Request(maxAcceptableLag: 1000),
			CancellationToken.None).ConfigureAwait(false);

		result.Outcome.ShouldBe(ProjectionCutoverOutcome.NewGenerationCursorAbsent);
		result.NewGenerationCursor.ShouldBeNull();
		result.Lag.ShouldBeNull("an absent cursor yields no lag measurement, and must not be reported as zero");

		(await ReadAliasIndicesAsync(scenario.AliasName).ConfigureAwait(false))
			.ShouldBe([scenario.PreviousIndex], ignoreOrder: true);

		await ShouldStillHoldPreviousGenerationAsync(scenario).ConfigureAwait(false);
	}

	/// <summary>
	/// A generation that has folded, but not far enough, is refused.
	/// </summary>
	protected async Task RefuseTheCutoverWhenTheNewGenerationsLagExceedsTheThreshold()
	{
		RequireEngine();

		var scenario = await ArrangeAsync(previousCursor: 100, newCursor: 10, head: 100).ConfigureAwait(false);

		var result = await scenario.Cutover.CutOverAsync(
			scenario.Request(maxAcceptableLag: 5),
			CancellationToken.None).ConfigureAwait(false);

		result.Outcome.ShouldBe(ProjectionCutoverOutcome.NewGenerationLagAboveThreshold);
		result.Lag.ShouldBe(90);

		(await ReadAliasIndicesAsync(scenario.AliasName).ConfigureAwait(false))
			.ShouldBe([scenario.PreviousIndex], ignoreOrder: true);

		await ShouldStillHoldPreviousGenerationAsync(scenario).ConfigureAwait(false);
	}

    /// <summary>A stale reader revision must not overwrite another transition.</summary>
    protected async Task RefuseStaleReaderRevision()
    {
        RequireEngine();
        var scenario = await ArrangeAsync(previousCursor: 100, newCursor: 100, head: 100).ConfigureAwait(false);
        var routes = (IProjectionGenerationStore)scenario.Checkpoints;
        var current = (await routes.ReadGenerationAsync(scenario.SubscriptionName, scenario.AliasName, CancellationToken.None).ConfigureAwait(false))!;
        (await routes.TrySetGenerationAsync(scenario.SubscriptionName, scenario.AliasName, current.Revision,
            current.Member.ProjectionKey, current.Member.BindingId, 100, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
        var result = await scenario.Cutover.CutOverAsync(scenario.Request(0), CancellationToken.None).ConfigureAwait(false);
        result.Outcome.ShouldBe(ProjectionCutoverOutcome.Contested);
        (await routes.ReadGenerationAsync(scenario.SubscriptionName, scenario.AliasName, CancellationToken.None).ConfigureAwait(false))!
            .Member.ProjectionKey.ShouldBe(scenario.PreviousKey);
        await ShouldStillHoldPreviousGenerationAsync(scenario).ConfigureAwait(false);
    }

	/// <summary>
	/// The liveness half: a caught-up generation is cut over, and the previous lifetime remains available for rollback.
	/// </summary>
	/// <remarks>
	/// Without this arm every safety arm above is satisfied by a cutover that never cuts over — safe,
	/// inert, and useless. It also holds under the mutation the absent-cursor arm exists to catch, which
	/// is what makes the pair discriminating rather than merely paired.
	/// </remarks>
	protected async Task CutOverAndRetainThePreviousGenerationWhenTheNewOneIsWithinTheThreshold()
	{
		RequireEngine();

		var scenario = await ArrangeAsync(previousCursor: 100, newCursor: 98, head: 100).ConfigureAwait(false);

		var result = await scenario.Cutover.CutOverAsync(
			scenario.Request(maxAcceptableLag: 5),
			CancellationToken.None).ConfigureAwait(false);

		result.Outcome.ShouldBe(ProjectionCutoverOutcome.CutOver);
		result.Lag.ShouldBe(2);

        var pointer = await ((IProjectionGenerationStore)scenario.Checkpoints).ReadGenerationAsync(
            scenario.SubscriptionName, scenario.AliasName, CancellationToken.None).ConfigureAwait(false);
        pointer.ShouldNotBeNull().Member.Destination.ShouldBe(DestinationPrefix + scenario.NewIndex);
        (await scenario.Checkpoints.ReleaseSubscriptionMemberAsync(scenario.SubscriptionName, scenario.NewKey,
            pointer.Member.BindingId, CancellationToken.None).ConfigureAwait(false)).ShouldBeFalse();
        (await ReadAliasIndicesAsync(scenario.AliasName).ConfigureAwait(false)).ShouldBe([scenario.PreviousIndex], ignoreOrder: true);
        // Native alias administration is a separate operation, not part of the database's lifetime transaction.
        await CreateRepoint(scenario.AliasName).RepointAsync(DestinationPrefix + scenario.PreviousIndex,
            DestinationPrefix + scenario.NewIndex, CancellationToken.None).ConfigureAwait(false);
        (await ReadAliasIndicesAsync(scenario.AliasName).ConfigureAwait(false)).ShouldBe([scenario.NewIndex], ignoreOrder: true);

        await ShouldStillHoldPreviousGenerationAsync(scenario).ConfigureAwait(false);
    }

	private async Task ShouldStillHoldPreviousGenerationAsync(Scenario scenario)
	{
		var roster = await scenario.Checkpoints
			.BindSubscriptionAsync(
				scenario.SubscriptionName,
				[new SubscriptionMember(scenario.PreviousKey, PreviousFingerprint, DestinationPrefix + scenario.PreviousIndex) { DestinationScope = "conformance/cluster" }],
				CancellationToken.None).ConfigureAwait(false);

		roster.Members.Select(static m => m.ProjectionKey).ShouldContain(scenario.PreviousKey);

		(await scenario.Checkpoints
			.GetCheckpointAsync(scenario.SubscriptionName, scenario.PreviousKey, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBe(100, "the previous generation keeps serving, so it keeps its cursor");
	}

	private async Task<Scenario> ArrangeAsync(long previousCursor, long? newCursor, long head)
	{
		var id = Guid.NewGuid().ToString("N");
		var alias = $"cutover-{id}";
		var previousIndex = $"{alias}-v1";
		var newIndex = $"{alias}-v2";
		var previousKey = $"Projections.Orders.Gen1.{id}";
		var newKey = $"Projections.Orders.Gen2.{id}";
		var subscription = $"orders-{id}";

		await CreateIndexAsync(previousIndex).ConfigureAwait(false);
		await CreateIndexAsync(newIndex).ConfigureAwait(false);
		await BindAliasAsync(alias, previousIndex).ConfigureAwait(false);

		var services = new ServiceCollection();
		_ = services.AddSqliteSubscriptionCheckpointStore(CheckpointConnectionString, "SubscriptionCheckpoints");
		var checkpoints = services.BuildServiceProvider().GetRequiredService<ISubscriptionCheckpointStore>();

		var roster = await checkpoints.BindSubscriptionAsync(
			subscription,
			[
				new SubscriptionMember(previousKey, PreviousFingerprint, DestinationPrefix + previousIndex) { DestinationScope = "conformance/cluster" },
				new SubscriptionMember(newKey, NewFingerprint, DestinationPrefix + newIndex) { DestinationScope = "conformance/cluster" },
			],
			CancellationToken.None).ConfigureAwait(false);

		// Binding creates no checkpoint, so the new generation has none until something folds into it.
		// Leaving it alone is how an arm reaches the absent-cursor state at all.
		(await checkpoints.AdvanceCheckpointAsync(subscription, previousKey, roster.Members.Single(m => m.ProjectionKey == previousKey).BindingId, null, previousCursor, CancellationToken.None)
			.ConfigureAwait(false))
			.ShouldBe(CheckpointAdvanceOutcome.Advanced);

		if (newCursor is { } folded)
		{
			(await checkpoints.AdvanceCheckpointAsync(subscription, newKey, roster.Members.Single(m => m.ProjectionKey == newKey).BindingId, null, folded, CancellationToken.None)
				.ConfigureAwait(false))
				.ShouldBe(CheckpointAdvanceOutcome.Advanced);
		}

        var routes = (IProjectionGenerationStore)checkpoints;
        (await routes.TrySetGenerationAsync(subscription, alias, null, previousKey,
            roster.Members.Single(m => m.ProjectionKey == previousKey).BindingId, previousCursor, CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
        var revision = (await routes.ReadGenerationAsync(subscription, alias, CancellationToken.None).ConfigureAwait(false))!.Revision;
		var headSource = new FixedHead(head);

		return new Scenario(
			alias,
			previousIndex,
			newIndex,
			subscription,
			previousKey,
			newKey,
			checkpoints,
			headSource,
			new ProjectionGenerationCutover(checkpoints, headSource), revision);
	}

	private sealed record Scenario(
		string AliasName,
		string PreviousIndex,
		string NewIndex,
		string SubscriptionName,
		string PreviousKey,
		string NewKey,
		ISubscriptionCheckpointStore Checkpoints,
		IGlobalStreamQuery Head,
		ProjectionGenerationCutover Cutover, Guid ReaderRevision)
	{
		public ProjectionGenerationCutoverRequest Request(long maxAcceptableLag) => new()
		{
			SubscriptionName = SubscriptionName,
			NewProjectionKey = NewKey,
			ReaderName = AliasName,
            ExpectedRevision = ReaderRevision,
			MaxAcceptableLag = maxAcceptableLag,
		};
	}

	/// <summary>
	/// Supplies the one number the arms do not measure.
	/// </summary>
	/// <remarks>
	/// Implements the interface directly rather than deriving from a framework base, so no inherited
	/// member can satisfy an assertion the arms meant to bind. The read paths throw: an arm that reached
	/// one would be measuring something this suite does not claim to cover.
	/// </remarks>
	private sealed class FixedHead(long head) : IGlobalStreamQuery
	{
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			throw new NotSupportedException("The cutover gate reads only the head position.");

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			throw new NotSupportedException("The cutover gate reads only the head position.");

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(head);
	}

}
