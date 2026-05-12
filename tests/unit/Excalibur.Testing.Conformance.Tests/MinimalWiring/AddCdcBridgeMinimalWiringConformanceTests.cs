// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;
using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for S822 bd-x10iv0 A4 — <c>IExcaliburBuilder.AddCdc(configure)</c>
/// bridge forwarding to <c>AddCdcProcessor(Action&lt;ICdcBuilder&gt;)</c>.
/// See <see cref="CdcExcaliburBuilderExtensions"/>.
/// </summary>
public sealed class AddCdcBridgeMarker { }

/// <summary>
/// Contract pin for Sprint 822 bd-x10iv0 (S792 Workstream A4 deferred from S793 D3):
/// the <c>IExcaliburBuilder.AddCdc</c> bridge forwards to
/// <c>services.AddCdcProcessor(Action&lt;ICdcBuilder&gt;)</c>. With a minimal
/// <c>UseSqlServer</c> callback, the realized terminal chain registers
/// <see cref="IDataAccessPolicyFactory"/> (<see cref="SqlDataAccessPolicyFactory"/>)
/// via <c>TryAdd</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket A</b> — configure-callback bridge shape. The bare bridge with an empty
/// callback (<c>_ =&gt; { }</c>) registers only options infrastructure — no concrete
/// services meaningful to assert. To exercise the bridge forwarding with a resolvable
/// service, this pin uses a minimal <c>UseSqlServer</c> callback (mirroring the
/// existing <see cref="AddCdcProcessorUseSqlServerMinimalWiringConformanceTests"/>
/// pattern) routed through the <c>IExcaliburBuilder</c> bridge.
/// </para>
/// <para>
/// This pin verifies the <c>IExcaliburBuilder</c> bridge path, complementing the
/// direct <c>IServiceCollection.AddCdcProcessor</c> extension pin.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddCdcBridgeMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddCdcBridgeMarker>
{
	private const string FakeConnectionString =
		"Server=localhost;Database=CdcBridgeConformance;Integrated Security=true;";

	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		services => services.AddExcalibur(x => x.AddCdc(cdc =>
			cdc.UseSqlServer(sql => sql
				.ConnectionString(FakeConnectionString)
				.DatabaseName("CdcBridgeConformance"))));

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IDataAccessPolicyFactory),
	};

	/// <inheritdoc />
	/// <remarks>
	/// CDC processor activation requires a live SQL pool which the conformance kit does not
	/// supply. Disable <c>ValidateOnBuild</c> — the pin asserts that the bridge-forwarded
	/// fluent chain's <b>registration surface</b> resolves <see cref="IDataAccessPolicyFactory"/>
	/// at build time. End-to-end CDC behavior is covered by the integration suite.
	/// </remarks>
	protected override bool ValidateOnBuild => false;

	/// <inheritdoc />
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services => services.AddSingleton(A.Fake<IDataAccessPolicyFactory>());

	/// <inheritdoc />
	protected override void AssertOverridePreserved(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		var factory = provider.GetRequiredService<IDataAccessPolicyFactory>();
		factory.ShouldNotBeNull();
		factory.GetType().Name.ShouldNotBe(nameof(SqlDataAccessPolicyFactory),
			"Consumer-supplied IDataAccessPolicyFactory must survive TryAdd through the bridge.");
	}

	/// <summary>Bucket A isolation gate — bridge-forwarded chain registers IDataAccessPolicyFactory.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket A idempotence gate — second invocation is no-op.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket A override gate — consumer-registered factory survives bridge forwarding.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
