// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Postgres.Tests;

/// <summary>
/// Binds the guarantee that an active legal hold blocks erasure, at the seam where it was silently lost:
/// the erasure pipeline consults holds through an optional dependency and skips the check when it is
/// absent, so an unwired hold service means every erasure proceeds unchecked, irreversibly.
/// </summary>
/// <remarks>
/// <para>
/// The safety arms assert the bad configuration is refused. The liveness arm is what stops a validator
/// that refuses everything from passing them: a deployment that declares it operates no holds must still
/// start. Both halves, or the suite is satisfied by a guard that has broken the framework.
/// </para>
/// <para>
/// Validation is triggered by resolving the options value, which is what runs every
/// <see cref="IValidateOptions{TOptions}"/> for the type — the same path the host takes at startup, and
/// the same one the data-subject-hashing pepper guard already surfaces through.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LegalHoldEnforcementWiringShould
{
	private const string ConnectionString = "Host=localhost;Database=holds;Username=u;Password=p";

	private static ServiceCollection Base()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// Not the subject of these arms: the hashing pepper has its own fail-closed guard.
		_ = services.Configure<DataSubjectHashingOptions>(o =>
			o.Pepper = "test-only-not-a-secret-0123456789abcdefghij");
		return services;
	}

	/// <summary>
	/// The full-stack entry point with compliance enabled must give the erasure pipeline a legal-hold
	/// SERVICE, not merely a store. RED before the fix: the metapackage wired the erasure store alone and
	/// this resolved <see langword="null"/>, so every erasure skipped the hold check.
	/// </summary>
	[Fact]
	public void Give_the_erasure_pipeline_a_legal_hold_service_when_compliance_is_enabled()
	{
		var services = Base();
		_ = services.AddExcaliburPostgres(pg =>
		{
			pg.ConnectionString = ConnectionString;
			pg.UseCompliance = true;
		});

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		scope.ServiceProvider.GetService<ILegalHoldService>().ShouldNotBeNull(
			"erasure consults legal holds through this service and SKIPS the check when it is absent, so a "
			+ "compliance stack without it destroys data irreversibly without ever reading the holds that "
			+ "exist to stop it");
	}

	/// <summary>
	/// SAFETY. Erasure registered with no legal-hold service, and no declaration that the deployment
	/// operates none, must refuse to start rather than erase past holds nobody checked.
	/// </summary>
	[Fact]
	public void Refuse_to_start_when_erasure_has_no_legal_hold_service()
	{
		var services = Base();
		_ = services.AddGdprErasureFromConfiguration(_ => { });
		_ = services.AddPostgresErasureStore(o => o.ConnectionString = ConnectionString);

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<ErasureOptions>>().Value);

		ex.Message.ShouldContain("legal", Case.Insensitive);
	}

	/// <summary>
	/// SAFETY, and the configuration most easily mistaken for protection: registering the provider's
	/// legal-hold STORE without the service leaves holds written, readable, and never enforced.
	/// </summary>
	[Fact]
	public void Refuse_to_start_when_only_the_hold_store_is_registered()
	{
		var services = Base();
		_ = services.AddGdprErasureFromConfiguration(_ => { });
		_ = services.AddPostgresErasureStore(o => o.ConnectionString = ConnectionString);
		_ = services.AddPostgresLegalHoldStore(o => o.ConnectionString = ConnectionString);

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<ErasureOptions>>().Value);

		ex.Message.ShouldContain("STORE",
			customMessage: "the refusal must name the store-without-service trap specifically, because that "
				+ "configuration looks correct from the outside");
	}

	/// <summary>
	/// LIVENESS. A deployment that declares it operates no legal holds must still start. Without this arm
	/// a guard that refused every configuration would satisfy the safety arms above while having broken
	/// erasure for everyone.
	/// </summary>
	[Fact]
	public void Start_when_the_host_declares_it_operates_no_legal_holds()
	{
		var services = Base();
		_ = services.AddGdprErasureFromConfiguration(_ => { });
		_ = services.AddPostgresErasureStore(o => o.ConnectionString = ConnectionString);
		_ = services.AddNoLegalHolds();

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => provider.GetRequiredService<IOptions<ErasureOptions>>().Value);

		// RESOLVE THE SERVICE, not just its options. This arm previously stopped at the options, which
		// made it blind to the failure mode the very next arm describes: a container can satisfy every
		// validator and still be unable to CONSTRUCT erasure. Options validation and constructibility are
		// two different claims, and only the second is what a consumer experiences.
		using var scope = provider.CreateScope();
		_ = Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<IErasureService>());
	}

	/// <summary>
	/// SAFETY. Declaring "no legal holds" in options WITHOUT supplying the service must be refused at
	/// startup, not at the first erasure.
	/// </summary>
	/// <remarks>
	/// This is the hole opened by making the legal-hold service required, and it is worth an arm of its
	/// own because it fails in the most expensive possible place if nothing catches it: the container
	/// builds, every validator passes, and the missing dependency surfaces as a resolution error on the
	/// first erasure request — an irreversible operation, at runtime, in production. The declaration and
	/// the service are supplied by a single call for exactly this reason, and this arm is what keeps the
	/// two from drifting back apart.
	/// </remarks>
	[Fact]
	public void Refuse_to_start_when_no_holds_is_declared_but_no_service_is_supplied()
	{
		var services = Base();
		_ = services.AddGdprErasureFromConfiguration(o => o.OperatesNoLegalHolds = true);
		_ = services.AddPostgresErasureStore(o => o.ConnectionString = ConnectionString);
		// deliberately NOT AddNoLegalHolds()

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IOptions<ErasureOptions>>().Value);

		ex.Message.ShouldContain("AddNoLegalHolds", Case.Insensitive);
	}

	/// <summary>
	/// LIVENESS. The configuration a consumer actually runs — the full-stack metapackage PLUS the erasure
	/// registration — must start.
	/// </summary>
	/// <remarks>
	/// The erasure registration is not incidental to this arm, it is what makes it mean anything. The
	/// metapackage registers compliance STORES and never the erasure service, so it never registers the
	/// options validator either: asserting "the metapackage alone starts" is satisfied by a container in
	/// which nothing validates, and would stay green with the guard deleted. Adding erasure puts the guard
	/// in the container, so this arm now discriminates — it is the same construction as the safety arms
	/// above, differing only in that the metapackage supplies the legal-hold service they lack.
	/// </remarks>
	[Fact]
	public void Start_when_compliance_is_enabled_through_the_metapackage_and_erasure_is_registered()
	{
		var services = Base();
		_ = services.AddExcaliburPostgres(pg =>
		{
			pg.ConnectionString = ConnectionString;
			pg.UseCompliance = true;
		});
		_ = services.AddGdprErasureFromConfiguration(_ => { });

		using var provider = services.BuildServiceProvider();

		_ = Should.NotThrow(() => provider.GetRequiredService<IOptions<ErasureOptions>>().Value);
	}
}
