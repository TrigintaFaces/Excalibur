// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Refuses to start an erasure pipeline that has no legal-hold service, unless the host has declared it
/// operates no legal holds.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ErasureService"/> takes its legal-hold service as an optional dependency and skips the hold
/// check when it is absent. That is the correct shape for a host that genuinely has no holds, and a silent
/// catastrophe for one that believes it has them: erasure is irreversible, so an erasure that proceeds past
/// an active hold cannot be undone by discovering the misconfiguration afterwards. The absence has to be a
/// declaration rather than an accident, which is what this validator turns it into.
/// </para>
/// <para>
/// It is deliberately not satisfied by the presence of a legal-hold STORE. A store holds the rows; the
/// service is what the erasure path consults. Registering the store alone leaves holds written, readable,
/// and never enforced — the configuration most likely to be mistaken for protection, because everything a
/// consumer can see looks correct.
/// </para>
/// <para>
/// The check inspects service <em>registration</em> through <see cref="IServiceProviderIsService"/> and
/// never resolves the probed service: this validator is a singleton holding the root provider, where
/// resolving a scoped service would throw or produce a rooted captive. Probing registration returns the
/// same verdict under either scope-validation setting, constructs nothing, and uses no reflection.
/// </para>
/// </remarks>
internal sealed class LegalHoldWiringValidator : IValidateOptions<ErasureOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="LegalHoldWiringValidator"/> class.
	/// </summary>
	/// <param name="services"> The root service provider, used to probe registration. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is null. </exception>
	public LegalHoldWiringValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ErasureOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// NOTE the absence of an early return for OperatesNoLegalHolds. Erasure now REQUIRES a legal-hold
		// service, so declaring "no holds" in options alone leaves the container unable to construct
		// erasure at all -- and it would fail on the first erasure request rather than at startup, which
		// is the worst possible moment for an irreversible operation to discover it is misconfigured.
		// The declaration still matters: it selects which message the consumer gets below. What it can no
		// longer do is skip the check.
		var isService = _services.GetService<IServiceProviderIsService>();

		if (isService is null)
		{
			// Registration cannot be probed on this container, so the question was not answered.
			// Reporting success here would turn "not measured" into "verified", which is the shape
			// this guard exists to prevent.
			return ValidateOptionsResult.Fail(
				"Erasure could not verify that a legal-hold service is registered, because this container "
				+ "does not supply IServiceProviderIsService. Erasure will not start rather than assume "
				+ "holds are enforced. Set ErasureOptions.OperatesNoLegalHolds if this deployment "
				+ "genuinely operates none.");
		}

		if (isService.IsService(typeof(ILegalHoldService)))
		{
			return ValidateOptionsResult.Success;
		}

		if (options.OperatesNoLegalHolds)
		{
			// The consumer made the decision and then did not act on it. Name the one call that does both.
			return ValidateOptionsResult.Fail(
				"Erasure is configured to operate no legal holds, but no ILegalHoldService is registered, so "
				+ "erasure cannot be constructed. Call AddNoLegalHolds(), which both registers the service "
				+ "that reports no holds and records the declaration.");
		}

		var storeOnly = isService.IsService(typeof(ILegalHoldStore));

		return ValidateOptionsResult.Fail(
			"Erasure is registered but no ILegalHoldService is, so every erasure would proceed without "
			+ "consulting legal holds — the hold check is skipped when the service is absent, and erasure "
			+ "is irreversible. "
			+ (storeOnly
				? "A legal-hold STORE is registered, which is the configuration most easily mistaken for "
					+ "protection: holds would be written and readable, and never enforced. Add "
					+ "AddLegalHoldService() alongside it. "
				: "Register a legal-hold store and AddLegalHoldService(), for example with the provider's "
					+ "compliance registration, which wires both. ")
			+ "If this deployment genuinely operates no legal holds, say so explicitly by setting "
			+ "ErasureOptions.OperatesNoLegalHolds — the absence must be a decision, not an oversight.");
	}
}
