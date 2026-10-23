// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Tests.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// A replay onto a store that records no positions cannot make its writes conditional, so a rebuild
/// racing the projection host would silently overwrite newer state with older and both would report
/// success. The framework refuses that combination unless the host states it has stopped the other
/// writers.
/// </summary>
/// <remarks>
/// <b>Built on a REAL ServiceProvider, deliberately.</b> A faked IServiceProvider returns a dummy for an
/// unconfigured GetService rather than null, so a fake would satisfy the acknowledgement check and these
/// arms would pass without the refusal existing at all. Production DI returns null for an unregistered
/// service, which is the behaviour under test.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class UnpositionedRebuildRefusalShould
{
	// Implements BOTH interfaces directly, inheriting no first-party base. A fixture that inherited one
	// would be re-testing the base rather than the contract, and the capability under test here is
	// precisely "does this type implement IPositionedProjectionStore".
	private sealed class PositionedStore<T> : IProjectionStore<T>, IPositionedProjectionStore<T>
		where T : class
	{
		public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			Task.FromResult<T?>(null);

		public Task UpsertAsync(string id, T projection, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

		public Task<IReadOnlyList<T>> QueryAsync(
			IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<T>>([]);

		public Task<long> CountAsync(
			IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public Task<(T? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string id, CancellationToken cancellationToken) =>
			Task.FromResult<(T?, ProjectionPosition)>((null, ProjectionPosition.Unnumbered));

		public Task UpsertUnnumberedAsync(string id, T projection, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string id, T projection, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken) =>
			Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));

		public Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id, T projection, long atPosition, CancellationToken cancellationToken) =>
			Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));

		public Task<ProjectionRebuildResult> RebuildAtPositionAsync(
			string id, T projection, long newPosition, CancellationToken cancellationToken) =>
			Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
	}

	private static ProjectionReplayFold<OrderSummary> Fold(IServiceProvider sp, bool positioned)
	{
		var projection = new MultiStreamProjection<OrderSummary>();
		if (!positioned)
		{
			return new ProjectionReplayFold<OrderSummary>(
				projection, new TestProjectionStore<OrderSummary>(), positioned: null, sp);
		}

		var store = new PositionedStore<OrderSummary>();
		return new ProjectionReplayFold<OrderSummary>(projection, store, store, sp);
	}

	[Fact]
	public void RefuseAReplayOntoAStoreThatRecordsNoPositions()
	{
		// SAFETY. Without a coordinate every write is a blind overwrite, so a rebuild racing the host
		// loses the newer state and nothing detects it. Refusing at construction means no row is written
		// at all -- a partly rebuilt projection is worse than one not rebuilt.
		using var provider = new ServiceCollection().BuildServiceProvider();

		var ex = Should.Throw<InvalidOperationException>(() => Fold(provider, positioned: false));

		ex.Message.ShouldContain("IPositionedProjectionStore");
		ex.Message.ShouldContain(
			"AllowUnpositionedProjectionRebuild",
			customMessage: "the refusal must name the remedy, not merely the problem");
	}

	[Fact]
	public void AllowItOnceTheHostHasStatedTheOtherWritersAreStopped()
	{
		// The escape hatch exists because exclusivity can be guaranteed by means the framework cannot
		// see -- a consumer's own store, or a test. It acknowledges exclusivity, not safety.
		var services = new ServiceCollection();
		_ = services.AllowUnpositionedProjectionRebuild();
		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => Fold(provider, positioned: false));
	}

	[Fact]
	public void NotRefuseAStoreThatDoesRecordPositions()
	{
		// LIVENESS, and the arm that stops this becoming a blanket refusal. Every persistent store this
		// framework ships implements IPositionedProjectionStore; if the check fired for them too, the
		// safety arm above would still pass while rebuild was broken for everyone.
		using var provider = new ServiceCollection().BuildServiceProvider();

		_ = Should.NotThrow(() => Fold(provider, positioned: true));
	}
}
