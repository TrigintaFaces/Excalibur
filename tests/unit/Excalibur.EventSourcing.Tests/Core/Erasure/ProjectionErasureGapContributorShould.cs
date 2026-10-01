// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Erasure;

/// <summary>
/// An erasure cannot report success while a registered projection still holds the subject's data.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> Erasure tombstones event rows in place and notifies nothing, so a projection
/// that already folded a subject's events keeps them indefinitely. The coverage gate is conservative
/// over the inventory it is GIVEN — an uncovered location forces a non-Completed outcome — but
/// coverage is judged over locations the consumer's own inventory DISCOVERED, and nothing declares a
/// projection store as one. So on a default composition there was no projection to be uncovered
/// about, the certificate reported Completed, and every read model still held the data. A certificate
/// is handed to an auditor; reporting Completed there is a compliance statement, not a log line.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Delete the contributor's registration, or make it report success
/// unconditionally, and <see cref="Report_the_gap_when_projections_are_registered"/> goes RED. The two
/// liveness arms are what stop the safety arm being satisfied by a contributor that fails always —
/// which would make a Completed certificate unreachable for every host on this framework, projections
/// or not.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionErasureGapContributorShould
{
	/// <summary>SAFETY: with projections registered and nothing covering them, the erasure is partial.</summary>
	[Fact]
	public async Task Report_the_gap_when_projections_are_registered()
	{
		var registry = new StubRegistry(ProjectionMode.Inline);
		var sut = new ProjectionErasureGapContributor(Provider(registry));

		var result = await sut.EraseAsync(Context(), TestContext.Current.CancellationToken);

		result.Success.ShouldBeFalse(
			"a failed contributor is what the erasure service turns into an error, and Completed is "
			+ "reachable only with zero errors. Without this the certificate says Completed while every "
			+ "read model still holds the subject's data");
		result.ErrorMessage.ShouldNotBeNullOrWhiteSpace(
			"the report has to tell an operator what to do -- ReapplyAsync for the subject's own row, a "
			+ "rebuild for rows it shares. A refusal that only says no is a stall");
	}

	/// <summary>It must cover no store kind at all.</summary>
	/// <remarks>
	/// Declaring <c>DataStoreKind.Projection</c> would mark projection locations COVERED by the very
	/// thing that exists to say they are not — a marker separated from the wiring it attests, which is
	/// the same lie in the opposite direction.
	/// </remarks>
	[Fact]
	public void Declare_coverage_of_nothing()
	{
		var sut = new ProjectionErasureGapContributor(Provider(new StubRegistry(ProjectionMode.Inline)));

		sut.CoveredStoreKinds.ShouldBeEmpty();
		sut.CoveredStoreKinds.Contains(DataStoreKind.Projection).ShouldBeFalse(
			"covering the kind it cannot erase would mark those locations satisfied");
	}

	/// <summary>LIVENESS: a host with no projections is not blocked.</summary>
	/// <remarks>
	/// Without this arm the safety arm is satisfied by a contributor that fails unconditionally, which
	/// would make a Completed certificate unreachable for every consumer of this framework whether or
	/// not they have a single read model.
	/// </remarks>
	[Fact]
	public async Task Stand_down_when_no_projections_are_registered()
	{
		var sut = new ProjectionErasureGapContributor(Provider(new StubRegistry()));

		var result = await sut.EraseAsync(Context(), TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue("there is no read model to be wrong about");
		result.DischargedLocations.ShouldBeEmpty("standing down is not the same as having erased something");
	}

	/// <summary>LIVENESS: a consumer who took over projection erasure is not blocked forever.</summary>
	/// <remarks>
	/// The gap report is the framework admitting it cannot do this. A consumer who has built it must be
	/// able to reach Completed, or a visible gap becomes a permanent stall — the failure mode this
	/// whole area keeps producing.
	/// </remarks>
	[Fact]
	public async Task Stand_down_when_another_contributor_covers_projections()
	{
		var registry = new StubRegistry(ProjectionMode.Inline);
		var services = new ServiceCollection();
		_ = services.AddSingleton<IProjectionRegistry>(registry);
		_ = services.AddSingleton<IErasureContributor>(new ConsumerProjectionContributor());
		var provider = services.BuildServiceProvider();

		var sut = new ProjectionErasureGapContributor(provider);

		var result = await sut.EraseAsync(Context(), TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue(
			"the consumer's contributor is what the coverage gate judges now. Continuing to report the "
			+ "gap would make Completed unreachable for a host that had solved it");
	}

	/// <summary>LIVENESS: an EPHEMERAL projection is not a gap and must not be reported as one.</summary>
	/// <remarks>
	/// An ephemeral projection is computed on demand from the stream and persists no row, so once the
	/// events are tombstoned the next computation carries none of the subject's data — there is nothing
	/// for an erasure to have missed. A control that fires when it should not is as wrong as one that
	/// stays silent when it should: an operator who sees this on every erasure stops reading it.
	/// </remarks>
	[Fact]
	public async Task Stand_down_for_a_projection_that_persists_nothing()
	{
		var sut = new ProjectionErasureGapContributor(Provider(new StubRegistry(ProjectionMode.Ephemeral)));

		var result = await sut.EraseAsync(Context(), TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue("an ephemeral projection holds no row an erasure could fail to reach");
	}

	/// <summary>SAFETY: one persisted projection among ephemeral ones still reports the gap.</summary>
	/// <remarks>
	/// The partner to the arm above, and the one that fails if the mode filter is written as "all of
	/// them are ephemeral" versus "none of them persists". Mixed registration is the ordinary case.
	/// </remarks>
	[Fact]
	public async Task Report_the_gap_when_one_registered_projection_persists()
	{
		var sut = new ProjectionErasureGapContributor(
			Provider(new StubRegistry(ProjectionMode.Ephemeral, ProjectionMode.Async)));

		var result = await sut.EraseAsync(Context(), TestContext.Current.CancellationToken);

		result.Success.ShouldBeFalse("the asynchronous projection persists rows that keep the subject's data");
	}

	/// <summary>LIVENESS: a persisted projection the erasure DID clear must not be reported as a gap.</summary>
	/// <remarks>
	/// The erasure now clears a projection whose id is the aggregate id, so continuing to report it would
	/// make Completed unreachable for a host whose read models were all reached — the stall this report
	/// exists to avoid, arriving from the other direction. The discriminator is the registration's clear
	/// delegate: the SAME property the erasure dispatches on, so the report cannot drift from the wiring.
	/// </remarks>
	[Fact]
	public async Task Stand_down_for_a_persisted_projection_the_erasure_clears()
	{
		var sut = new ProjectionErasureGapContributor(Provider(StubRegistry.Clearable(ProjectionMode.Inline)));

		var result = await sut.EraseAsync(Context(), TestContext.Current.CancellationToken);

		result.Success.ShouldBeTrue(
			"this projection was cleared per aggregate as part of the erasure, so there is no residue to "
			+ "report");
	}

	/// <summary>SAFETY: a clearable projection is still reported when the erasure cleared NOTHING.</summary>
	/// <remarks>
	/// A SELECTIVE erasure names data categories and event-store erasure is whole-aggregate, so the
	/// event-store contributor refuses it and tombstones nothing. No projection was cleared. Reading only
	/// the delegate here would credit a clearing that never happened — the marker-separated-from-the-
	/// wiring failure in its subtlest form, because every type is present and correctly bound.
	/// </remarks>
	[Fact]
	public async Task Report_a_clearable_projection_when_the_erasure_is_selective()
	{
		var sut = new ProjectionErasureGapContributor(Provider(StubRegistry.Clearable(ProjectionMode.Inline)));

		var result = await sut.EraseAsync(
			Context() with { Scope = ErasureScope.Selective }, TestContext.Current.CancellationToken);

		result.Success.ShouldBeFalse(
			"a selective erasure tombstones nothing, so every persisted read model still holds whatever it "
			+ "folded -- including the ones that WOULD have been cleared");
	}

	private static IServiceProvider Provider(IProjectionRegistry registry)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(registry);

		return services.BuildServiceProvider();
	}

	private static ErasureContributorContext Context() =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			IdType = DataSubjectIdType.Hash,
			Scope = ErasureScope.User,
		};

	private sealed class StubRegistry(params ProjectionMode[] modes) : IProjectionRegistry
	{
		private readonly List<ProjectionRegistration> _registrations =
			[.. modes.Select(m => new ProjectionRegistration(typeof(object), m, new object(), inlineApply: null))];

		/// <summary>
		/// A registry whose projections carry a clear delegate — what a per-aggregate projection gets from
		/// the builder, and what tells this contributor the erasure reached it.
		/// </summary>
		public static StubRegistry Clearable(params ProjectionMode[] modes)
		{
			var registry = new StubRegistry();

			foreach (var mode in modes)
			{
				registry.Register(new ProjectionRegistration(
					typeof(object),
					mode,
					new object(),
					inlineApply: null,
					clearForAggregate: static (_, _, _, _) => Task.CompletedTask));
			}

			return registry;
		}

		public ProjectionRegistration? GetRegistration(Type projectionType) => _registrations.Count > 0 ? _registrations[0] : null;

		public IReadOnlyList<ProjectionRegistration> GetAll() => _registrations;

		public IReadOnlyList<ProjectionRegistration> GetByMode(ProjectionMode mode) => _registrations;

		public void Register(ProjectionRegistration registration) => _registrations.Add(registration);
	}

	/// <summary>
	/// A consumer's own projection erasure. Implements the contract DIRECTLY so the arm binds the
	/// interface's requirement rather than a first-party base's convenience.
	/// </summary>
	private sealed class ConsumerProjectionContributor : IErasureContributor
	{
		public string Name => "ConsumerProjections";

		public IReadOnlySet<DataStoreKind> CoveredStoreKinds { get; } =
			new HashSet<DataStoreKind> { DataStoreKind.Projection };

		public Task<ErasureContributorResult> EraseAsync(
			ErasureContributorContext context,
			CancellationToken cancellationToken) =>
			Task.FromResult(ErasureContributorResult.Succeeded(1));
	}
}
