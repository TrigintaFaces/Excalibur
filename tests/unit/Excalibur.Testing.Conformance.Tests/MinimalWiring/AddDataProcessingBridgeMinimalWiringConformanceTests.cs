// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Testing.Conformance.DependencyInjection;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for S822 bd-x10iv0 A3 — <c>IExcaliburBuilder.AddDataProcessing(configure)</c>
/// bridge forwarding to <c>AddDataProcessing(Action&lt;IDataProcessingBuilder&gt;)</c>.
/// See <see cref="DataProcessingExcaliburBuilderExtensions"/>.
/// </summary>
public sealed class AddDataProcessingBridgeMarker { }

/// <summary>
/// Contract pin for Sprint 822 bd-x10iv0 (S792 Workstream A3 deferred from S793 D3):
/// the <c>IExcaliburBuilder.AddDataProcessing</c> bridge forwards to
/// <c>services.AddDataProcessing(Action&lt;IDataProcessingBuilder&gt;)</c> and registers
/// core services (<see cref="IDataProcessorRegistry"/>) via <c>TryAdd</c> even with
/// a no-op configure callback.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket A</b> — configure-callback bridge shape. Unlike non-callback bridges
/// (e.g., <c>AddDispatch()</c>), this extension requires a configure delegate. With
/// a no-op callback (<c>_ =&gt; { }</c>), the extension still registers core orchestration
/// services via <c>TryAddScoped</c>. The connection factory throws at resolution time
/// (not registration time), so <see cref="ValidateOnBuild"/> is <see langword="false"/>.
/// </para>
/// <para>
/// This pin verifies the <c>IExcaliburBuilder</c> bridge path, complementing the direct
/// <c>IServiceCollection.AddDataProcessing</c> extension coverage.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddDataProcessingBridgeMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddDataProcessingBridgeMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddDataProcessing(static _ => { }));

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IDataProcessorRegistry),
	};

	/// <inheritdoc />
	/// <remarks>
	/// The no-op configure callback leaves no connection factory configured; the keyed
	/// singleton factory throws <see cref="InvalidOperationException"/> at resolution time.
	/// <see cref="DataOrchestrationManager"/> depends on the keyed connection factory,
	/// so <c>ValidateOnBuild</c> must be disabled. The pin asserts registration-time
	/// surface only — <see cref="IDataProcessorRegistry"/> resolves from a factory delegate.
	/// </remarks>
	protected override bool ValidateOnBuild => false;

	/// <inheritdoc />
	/// <remarks>
	/// <see cref="IDataProcessorRegistry"/> is registered as <c>TryAddScoped</c> and cannot
	/// be resolved from the root provider when scope validation is enabled. Disabling scope
	/// validation allows the Isolation gate to call <c>GetService</c> on the root provider
	/// without triggering the scoped-from-root guard.
	/// </remarks>
	protected override bool ValidateScopes => false;

	/// <summary>Bucket A isolation gate — bridge registers IDataProcessorRegistry via TryAdd.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket A idempotence gate — second invocation is no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();
}
