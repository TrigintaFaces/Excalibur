// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer;

/// <summary>
/// Startup guard that fails host start when SQL Server key escrow is registered without an
/// <see cref="IEncryptionProvider"/> to protect the escrowed material.
/// </summary>
/// <remarks>
/// <para>
/// Escrow encrypts every key it stores before it writes it, so without an encryption provider the
/// registration is not merely incomplete — it cannot function. Left unchecked, that surfaces on the
/// first escrow write at the earliest, and the failure a consumer actually cares about surfaces at
/// recovery, which is the one moment the feature exists for and the one moment when the fallback is
/// that the key material is gone. A host that refuses to start is a far better outcome than a host
/// that starts and cannot recover.
/// </para>
/// <para>
/// The check inspects service <em>registration</em> through <see cref="IServiceProviderIsService"/>
/// and never resolves the probed service: this validator is a singleton holding the root provider,
/// where resolving a scoped service would throw or produce a rooted captive. Probing registration
/// returns the same verdict under either scope-validation setting and constructs nothing. It is also
/// AOT-safe, using no reflection.
/// </para>
/// <para>
/// This validator deliberately carries no options checks. Those live in
/// <see cref="SqlServerKeyEscrowOptionsValidator"/>; keeping wiring separate from option shape means
/// neither check can mask the other, and a consumer sees both sets of failures at once rather than
/// one after the other across successive startups.
/// </para>
/// </remarks>
internal sealed class KeyEscrowEncryptionWiringValidator : IValidateOptions<SqlServerKeyEscrowOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyEscrowEncryptionWiringValidator"/> class.
	/// </summary>
	/// <param name="services"> The root service provider, used to probe registration. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is null. </exception>
	public KeyEscrowEncryptionWiringValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerKeyEscrowOptions options)
	{
		var isService = _services.GetService<IServiceProviderIsService>();

		if (isService is null)
		{
			// Registration cannot be probed on this container, so the question was not answered.
			// Reporting success here would turn "not measured" into "verified", which is the shape
			// this guard exists to prevent.
			return ValidateOptionsResult.Fail(
				"SQL Server key escrow could not verify that an IEncryptionProvider is registered, because "
				+ "this container does not supply IServiceProviderIsService. Escrow will not start rather "
				+ "than assume its encryption provider is present.");
		}

		if (isService.IsService(typeof(IEncryptionProvider)))
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"SQL Server key escrow is registered but no IEncryptionProvider is. Escrow encrypts every key "
			+ "it stores, so it cannot operate without one, and the failure would otherwise surface at "
			+ "recovery time -- when the escrowed key is what you no longer have. Register an encryption "
			+ "provider before AddSqlServerKeyEscrow, for example with AddComplianceEncryption(). "
			+ "In production, back it with a key service that outlives the process: a provider holding key "
			+ "material in memory loses it at restart, which loses everything escrowed under it.");
	}
}
