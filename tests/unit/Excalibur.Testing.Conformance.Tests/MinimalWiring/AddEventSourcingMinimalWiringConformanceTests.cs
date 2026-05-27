// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcalibur(x =&gt; x.AddEventSourcing(...))</c> builder
/// terminal in the minimal-wiring inventory.
/// </summary>
public sealed class AddEventSourcingMarker { }

/// <summary>
/// S798-B (task-514 §B2 Path 2+) regression pin — <b>coverage-gap closure</b> for the
/// EventSourcing subsystem of <see cref="MinimalWiringConformanceTestKit{T}"/>. Supersedes
/// (in combination with the Excalibur.Tests/DependencyInjection/WrapperDIComplianceShould.cs
/// EventSourcing case) the ADR-078 <c>IDispatcher</c>-boundary invariant assertion for
/// <c>AddEventSourcing</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket:</b> B — <see cref="MinimalWiringBucket.ExplicitPrerequisite"/>.
/// The no-arg <c>services.AddExcaliburEventSourcing()</c> that <c>IExcaliburBuilder.AddEventSourcing(...)</c>
/// delegates to <c>TryAdd</c>s only <c>ISnapshotStrategy</c> (NoSnapshotStrategy.Instance);
/// the consumer MUST supply their <see cref="IEventStore"/> backend via
/// <c>IEventSourcingBuilder.UseEventStore&lt;T&gt;(...)</c> or an explicit registration. A bare
/// <c>AddEventSourcing()</c> in a consumer-empty container therefore fails loudly at startup
/// validation — the Bucket B contract.
/// </para>
/// <para>
/// <b>ADR-078 invariant:</b> post-override, <see cref="IDispatcher"/> MUST resolve cleanly.
/// This is the contract <c>WrapperDIComplianceShould.cs</c> was asserting for <c>AddEventSourcing</c>
/// and which this pin takes over. Asserted in <see cref="AssertOverridePreserved(IServiceProvider)"/>.
/// </para>
/// <para>
/// <b>Override primitive:</b> <see cref="IEventStore"/> is the primary consumer-extensibility
/// seam — consumers pick their event store backend (in-memory / SQL Server / Postgres / Cosmos
/// / DynamoDB / Firestore / Redis) via this interface. Per COMPASS msg 1661 binding, the pin
/// pre-registers a fake <see cref="IEventStore"/>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddEventSourcingMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddEventSourcingMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddEventSourcing());

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: <c>AddExcalibur(x =&gt; x.AddEventSourcing())</c>
	/// against a foundation-only container must leave <see cref="IDispatcher"/>
	/// resolvable. This is the contract <c>WrapperDIComplianceShould</c> was asserting
	/// for the EventSourcing subsystem.
	/// </remarks>
	protected override IReadOnlyList<Type> ExpectedResolvableServices { get; } =
		new[] { typeof(IDispatcher) };

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<IEventStore>());

	/// <summary>Bucket A isolation gate — IDispatcher resolves after AddEventSourcing with defaults.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Override gate — consumer-supplied IEventStore stub survives invocation.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
