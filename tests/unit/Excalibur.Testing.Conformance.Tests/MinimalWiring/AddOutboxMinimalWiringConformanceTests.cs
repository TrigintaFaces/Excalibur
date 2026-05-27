// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcalibur(x =&gt; x.AddOutbox(...))</c> builder
/// terminal in the minimal-wiring inventory.
/// </summary>
public sealed class AddOutboxMarker { }

/// <summary>
/// S798-B (task-514 §B2 Path 2+) regression pin — <b>coverage-gap closure</b> for the Outbox
/// subsystem of <see cref="MinimalWiringConformanceTestKit{T}"/>. Supersedes (in combination
/// with the Excalibur.Tests/DependencyInjection/WrapperDIComplianceShould.cs Outbox case)
/// the ADR-078 <c>IDispatcher</c>-boundary invariant assertion for <c>AddOutbox</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket:</b> B — <see cref="MinimalWiringBucket.ExplicitPrerequisite"/>.
/// The no-arg <c>services.AddExcaliburOutbox()</c> that <c>IExcaliburBuilder.AddOutbox(...)</c>
/// delegates to does not TryAdd the primary outbox contract — the consumer MUST supply their
/// <see cref="IOutboxStore"/> backend via <c>IOutboxBuilder.UseSqlServer(...)</c> / similar
/// provider call or an explicit registration. A bare <c>AddOutbox()</c> in a consumer-empty
/// container therefore fails loudly at startup validation — the Bucket B contract.
/// </para>
/// <para>
/// <b>ADR-078 invariant:</b> post-override, <see cref="IDispatcher"/> MUST resolve cleanly.
/// This is the contract <c>WrapperDIComplianceShould.cs</c> was asserting for <c>AddOutbox</c>
/// and which this pin takes over. Asserted in <see cref="AssertOverridePreserved(IServiceProvider)"/>.
/// </para>
/// <para>
/// <b>Override primitive:</b> <see cref="IOutboxStore"/> is the primary consumer-extensibility
/// seam — consumers pick their outbox backing store (SQL Server / Postgres / MongoDB / in-memory).
/// Per COMPASS msg 1661 binding, the pin pre-registers a fake <see cref="IOutboxStore"/>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddOutboxMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddOutboxMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddOutbox(static _ => { }));

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: <c>AddExcalibur(x =&gt; x.AddOutbox(_ =&gt; {}))</c>
	/// against a foundation-only container must leave <see cref="IDispatcher"/>
	/// resolvable. This is the contract <c>WrapperDIComplianceShould</c> was asserting
	/// for the Outbox subsystem.
	/// </remarks>
	protected override IReadOnlyList<Type> ExpectedResolvableServices { get; } =
		new[] { typeof(IDispatcher) };

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<IOutboxStore>());

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: every <c>AddExcalibur{Subsystem}</c> terminal routes
	/// <see cref="IDispatcher"/> resolution through <c>AddDispatch()</c>. This assertion
	/// is what <c>WrapperDIComplianceShould.cs</c> was pinning for the Outbox subsystem
	/// and which this conformance pin takes over per COMPASS msg 1654 Path 2+.
	/// </remarks>
	protected override void AssertOverridePreserved(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		using var scope = provider.CreateScope();
		var dispatcher = scope.ServiceProvider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull(
			"ADR-078: IDispatcher must be resolvable after AddExcalibur(x => x.AddOutbox()) " +
			"with the IOutboxStore prerequisite supplied. This is the boundary invariant " +
			"WrapperDIComplianceShould previously asserted for the Outbox subsystem.");
	}

	/// <summary>Bucket A isolation gate — IDispatcher resolves after AddOutbox with default config.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Override gate — consumer-supplied IOutboxStore stub survives invocation.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
