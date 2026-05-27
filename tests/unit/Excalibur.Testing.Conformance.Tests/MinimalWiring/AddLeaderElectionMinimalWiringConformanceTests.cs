// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcalibur(x =&gt; x.AddLeaderElection(...))</c> builder
/// terminal in the minimal-wiring inventory.
/// </summary>
public sealed class AddLeaderElectionMarker { }

/// <summary>
/// S798-B (task-514 §B2 Path 2+) regression pin — <b>coverage-gap closure</b> for the
/// LeaderElection subsystem of <see cref="MinimalWiringConformanceTestKit{T}"/>. Supersedes
/// (in combination with the Excalibur.Tests/DependencyInjection/WrapperDIComplianceShould.cs
/// LeaderElection case) the ADR-078 <c>IDispatcher</c>-boundary invariant assertion for
/// <c>AddLeaderElection</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket:</b> B — <see cref="MinimalWiringBucket.ExplicitPrerequisite"/>.
/// The no-arg <c>services.AddExcaliburLeaderElection()</c> that <c>IExcaliburBuilder.AddLeaderElection(...)</c>
/// delegates to does not TryAdd an <see cref="ILeaderElection"/> implementation — the consumer
/// MUST pick the algorithm (in-memory / Redis / SQL Server / Postgres / Mongo / Kubernetes /
/// Consul) via an explicit <c>ILeaderElectionBuilder.Use{Backend}(...)</c> call or direct DI
/// registration. A bare <c>AddLeaderElection()</c> in a consumer-empty container therefore
/// fails loudly at startup validation — the Bucket B contract.
/// </para>
/// <para>
/// <b>ADR-078 invariant:</b> post-override, <see cref="IDispatcher"/> MUST resolve cleanly.
/// This is the contract <c>WrapperDIComplianceShould.cs</c> was asserting for <c>AddLeaderElection</c>
/// and which this pin takes over. Asserted in <see cref="AssertOverridePreserved(IServiceProvider)"/>.
/// </para>
/// <para>
/// <b>Override primitive:</b> <see cref="ILeaderElection"/> is THE consumer-extensibility
/// seam for this subsystem — the algorithm choice is entirely consumer-driven. Per COMPASS
/// msg 1661 binding, the pin pre-registers a fake <see cref="ILeaderElection"/>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddLeaderElectionMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddLeaderElectionMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddLeaderElection(static _ => { }));

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: <c>AddExcalibur(x =&gt; x.AddLeaderElection(_ =&gt; {}))</c>
	/// against a foundation-only container must leave <see cref="IDispatcher"/>
	/// resolvable. This is the contract <c>WrapperDIComplianceShould</c> was asserting
	/// for the LeaderElection subsystem.
	/// </remarks>
	protected override IReadOnlyList<Type> ExpectedResolvableServices { get; } =
		new[] { typeof(IDispatcher) };

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<ILeaderElection>());

	/// <summary>Bucket A isolation gate — IDispatcher resolves after AddLeaderElection with defaults.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Override gate — consumer-supplied ILeaderElection stub survives invocation.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
