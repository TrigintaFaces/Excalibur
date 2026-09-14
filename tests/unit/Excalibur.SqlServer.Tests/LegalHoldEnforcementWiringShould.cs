// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.SqlServer.Tests;

/// <summary>
/// The SQL Server half of the legal-hold enforcement binding. Measured before the fix, this metapackage
/// behaved identically to its Postgres sibling — compliance enabled, erasure store wired, and no
/// legal-hold service at all — so both providers destroyed data without consulting holds.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LegalHoldEnforcementWiringShould
{
	private const string ConnectionString =
		"Server=localhost;Database=holds;Integrated Security=true;TrustServerCertificate=true";

	private static ServiceCollection Base()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-only-not-a-secret-0123456789abcdefghij");
		return services;
	}

	/// <summary>
	/// RED before the fix: the SQL Server metapackage wired key escrow and the erasure store, and no
	/// legal-hold service, so the hold check was skipped on every erasure.
	/// </summary>
	[Fact]
	public void Give_the_erasure_pipeline_a_legal_hold_service_when_compliance_is_enabled()
	{
		var services = Base();
		_ = services.AddExcaliburSqlServer(sql =>
		{
			sql.ConnectionString = ConnectionString;
			sql.UseCompliance = true;
		});

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		scope.ServiceProvider.GetService<ILegalHoldService>().ShouldNotBeNull(
			"erasure consults legal holds through this service and SKIPS the check when it is absent");
	}

	/// <summary>
	/// LIVENESS. The configuration a consumer actually runs — metapackage plus erasure — must start.
	/// </summary>
	/// <remarks>
	/// The erasure registration is what makes this arm discriminate: the metapackage registers compliance
	/// stores and never the erasure service, so without it nothing in the container validates and the
	/// assertion would hold with the guard deleted.
	/// </remarks>
	[Fact]
	public void Start_when_compliance_is_enabled_through_the_metapackage_and_erasure_is_registered()
	{
		var services = Base();
		_ = services.AddExcaliburSqlServer(sql =>
		{
			sql.ConnectionString = ConnectionString;
			sql.UseCompliance = true;
		});

		_ = services.AddGdprErasureFromConfiguration(_ => { });

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => provider.GetRequiredService<IOptions<ErasureOptions>>().Value);
	}
}
