// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Abstractions;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcalibur(x =&gt; x.AddSagas(...))</c> builder
/// terminal in the minimal-wiring inventory. See <c>management/specs/conformance-minimal-wiring-inventory.csv</c>.
/// </summary>
public sealed class AddSagasMarker { }

/// <summary>
/// S798-B (task-514 §B2 Path 2+) regression pin — <b>coverage-gap closure</b> for the Saga
/// subsystem of <see cref="MinimalWiringConformanceTestKit{T}"/>. Supersedes (in combination
/// with the Excalibur.Tests/DependencyInjection/WrapperDIComplianceShould.cs Saga case) the
/// ADR-078 <c>IDispatcher</c>-boundary invariant assertion for <c>AddSagas</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket:</b> B — <see cref="MinimalWiringBucket.ExplicitPrerequisite"/>.
/// The no-arg <c>services.AddExcaliburSaga()</c> that <c>IExcaliburBuilder.AddSagas(...)</c>
/// delegates to <c>TryAdd</c>s only coordination primitives (<c>ISagaTypeRegistry</c>,
/// <c>ISagaDispatchRegistry</c>); the consumer MUST supply their saga state store backend
/// (<see cref="ISagaStateStore"/>) via <c>ISagaBuilder.UseStateStore(...)</c> or an explicit
/// registration. A bare <c>AddSagas()</c> in a consumer-empty container therefore fails
/// loudly at startup validation — the Bucket B contract.
/// </para>
/// <para>
/// <b>ADR-078 invariant:</b> post-override, <see cref="IDispatcher"/> MUST resolve cleanly.
/// This is the contract <c>WrapperDIComplianceShould.cs</c> was asserting for <c>AddSagas</c>
/// and which this pin takes over. Asserted in <see cref="AssertOverridePreserved(IServiceProvider)"/>.
/// </para>
/// <para>
/// <b>Override primitive:</b> <see cref="ISagaStateStore"/> is the primary consumer-extensibility
/// seam — consumers pick their saga persistence backend (in-memory / SQL Server / Postgres /
/// Mongo) via this interface. Per COMPASS msg 1661 binding, the pin pre-registers a fake
/// <see cref="ISagaStateStore"/>; if the saga builder's prerequisite-validation surface names a
/// different primitive (e.g., the error names <c>ISagaTypeRegistry</c> instead because validation
/// order differs), the <see cref="ExpectedPrerequisiteMessageFragment"/> will need to match the
/// observable error rather than my expectation — flag on task-514 if the Isolation gate fails
/// with a fragment mismatch.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddSagasMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddSagasMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddSagas());

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: <c>AddExcalibur(x =&gt; x.AddSagas())</c> against a
	/// foundation-only container must leave <see cref="IDispatcher"/> resolvable. This
	/// is the contract <c>WrapperDIComplianceShould</c> was asserting for the Saga subsystem.
	/// </remarks>
	protected override IReadOnlyList<Type> ExpectedResolvableServices { get; } =
		new[] { typeof(IDispatcher) };

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<ISagaStateStore>());

	/// <summary>Bucket A isolation gate — IDispatcher resolves after AddSagas with defaults.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Override gate — consumer-supplied ISagaStateStore stub survives invocation.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
