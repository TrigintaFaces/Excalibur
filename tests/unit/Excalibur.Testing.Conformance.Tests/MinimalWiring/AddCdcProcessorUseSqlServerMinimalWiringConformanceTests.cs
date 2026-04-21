// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>AddCdcProcessor(cdc =&gt; cdc.UseSqlServer(...))</c> fluent-chain
/// terminal in the minimal-wiring inventory.
/// </summary>
public sealed class AddCdcProcessorUseSqlServerMarker { }

/// <summary>
/// Regression pin for S790 FIX 1 (commit <c>071ae479d</c>):
/// <see cref="Excalibur.Cdc.SqlServer.CdcBuilderSqlServerExtensions.UseSqlServer(ICdcBuilder, Action{ISqlServerCdcBuilder})"/>
/// must <c>TryAdd</c> a default <see cref="IDataAccessPolicyFactory"/>
/// (<see cref="SqlDataAccessPolicyFactory"/>) so a consumer can compose the full CDC fluent
/// chain against an otherwise-empty <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// Bucket A — this is the canonical minimal-wiring shape: a fluent-chain terminal
/// that <c>TryAdd</c>s its framework-internal siblings at registration time.
/// </para>
/// <para>
/// The invoke delegate packages the full realized-terminal chain per
/// spec §4.1: <c>AddCdcProcessor</c> → <c>UseSqlServer</c>. Fake connection string
/// satisfies <c>SqlServerCdcOptions.Validate()</c> without requiring a live SQL Server.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddCdcProcessorUseSqlServerMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddCdcProcessorUseSqlServerMarker>
{
	private const string FakeConnectionString =
		"Server=localhost;Database=ConformanceStub;Integrated Security=true;";

	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddCdcProcessor(cdc =>
			cdc.UseSqlServer(sql => sql
				.ConnectionString(FakeConnectionString)
				.DatabaseName("ConformanceStub")));

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IDataAccessPolicyFactory),
	};

	/// <inheritdoc />
	/// <remarks>
	/// CDC processor activation requires a live SQL pool which the conformance kit does not
	/// supply. Disable <c>ValidateOnBuild</c> — the pin asserts that the fluent chain's
	/// <b>registration surface</b> resolves <see cref="IDataAccessPolicyFactory"/> at
	/// build time. End-to-end CDC behavior is covered by the integration suite.
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
			"Consumer-supplied IDataAccessPolicyFactory must survive TryAdd in FIX 1.");
	}

	/// <summary>Bucket A isolation gate — registration surface resolves IDataAccessPolicyFactory.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket A idempotence gate — second invocation is no-op.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket A override gate — consumer-registered factory survives.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
