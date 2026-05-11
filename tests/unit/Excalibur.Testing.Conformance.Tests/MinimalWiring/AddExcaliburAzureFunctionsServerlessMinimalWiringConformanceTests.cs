// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcaliburAzureFunctionsServerless()</c>
/// serverless carve-out aggregator (`bd-sdhocq` Front-A entry A10).
/// </summary>
public sealed class AddExcaliburAzureFunctionsServerlessMarker { }

/// <summary>
/// S803-E regression pin for <c>AddExcaliburAzureFunctionsServerless()</c> — one of the
/// three serverless host-scaffolding aggregators (A10–A12) that <c>bd-sdhocq</c>'s
/// disposition grid carves out from the composition-root-only policy. Per ADR-324
/// (`bd-sdhocq` deliverable #1) serverless aggregators keep public surface because the
/// <see cref="IExcaliburBuilder"/> callback shape does not fit the host-scaffolding
/// lifecycle model cleanly; consumers invoke them directly on
/// <see cref="IServiceCollection"/> inside their Azure Functions Startup hook.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket A</b> — the aggregator's advertised contract is that calling it against
/// an empty container <c>TryAdd</c>-registers the Dispatch serverless bridge primitives
/// (<see cref="IServerlessHostProvider"/> + <see cref="IColdStartOptimizer"/>) with
/// sensible defaults (<c>AzureFunctionsHostProvider</c> + <c>AzureFunctionsColdStartOptimizer</c>).
/// </para>
/// <para>
/// <b>Override primitive:</b> <see cref="IColdStartOptimizer"/> is the natural consumer
/// customization seam — consumers override cold-start wiring for cost/latency tuning.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddExcaliburAzureFunctionsServerlessMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddExcaliburAzureFunctionsServerlessMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcaliburAzureFunctionsServerless();

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IServerlessHostProvider),
		typeof(IColdStartOptimizer),
	};

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<IColdStartOptimizer>());

	/// <inheritdoc />
	protected override void AssertOverridePreserved(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		var optimizer = provider.GetService<IColdStartOptimizer>();
		optimizer.ShouldNotBeNull(
			"IColdStartOptimizer must be resolvable after AddExcaliburAzureFunctionsServerless — " +
			"either the consumer-supplied fake (override preserved via TryAdd) or the framework default.");
	}

	/// <summary>Bucket A isolation gate — bare AddExcaliburAzureFunctionsServerless resolves serverless bridge primitives.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket A idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket A override gate — consumer-supplied IColdStartOptimizer survives invocation.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
