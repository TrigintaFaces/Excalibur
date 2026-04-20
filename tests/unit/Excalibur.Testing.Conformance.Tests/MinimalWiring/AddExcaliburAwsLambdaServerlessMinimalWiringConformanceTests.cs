// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcaliburAwsLambdaServerless()</c>
/// serverless carve-out aggregator (`bd-sdhocq` Front-A entry A11).
/// </summary>
public sealed class AddExcaliburAwsLambdaServerlessMarker { }

/// <summary>
/// S803-E regression pin for <c>AddExcaliburAwsLambdaServerless()</c> — second of the
/// three serverless host-scaffolding aggregators (A10–A12) that <c>bd-sdhocq</c>'s
/// disposition grid carves out from the composition-root-only policy. Pin structure
/// mirrors the Azure Functions pin; differing only in the bootstrap aggregator and
/// default provider/optimizer implementations (<c>AwsLambdaHostProvider</c> +
/// <c>AwsLambdaColdStartOptimizer</c>).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddExcaliburAwsLambdaServerlessMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddExcaliburAwsLambdaServerlessMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcaliburAwsLambdaServerless();

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
			"IColdStartOptimizer must be resolvable after AddExcaliburAwsLambdaServerless — " +
			"either the consumer-supplied fake (override preserved via TryAdd) or the framework default.");
	}

	/// <summary>Bucket A isolation gate — bare AddExcaliburAwsLambdaServerless resolves serverless bridge primitives.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket A idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket A override gate — consumer-supplied IColdStartOptimizer survives invocation.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
