// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for the shared audit-integrity strategy.
/// </summary>
public static class AuditIntegrityServiceCollectionExtensions
{
	/// <summary>
	/// Registers the canonical keyed-MAC + hash-chain <see cref="IAuditIntegrityStrategy"/> used by every
	/// audit sink, plus the default options-backed <see cref="IAuditSigningKeyProvider"/> (keyed from
	/// <see cref="AuditIntegrityOptions"/>). Both use <c>TryAddSingleton</c>, so a consumer can override
	/// either with a richer implementation (e.g. a KMS-backed key provider) by registering it first.
	/// </summary>
	/// <remarks>
	/// The method is self-contained: it registers the <see cref="AuditIntegrityOptions"/> options binding
	/// and a start-up validator (<see cref="AuditIntegrityOptionsValidator"/> via <c>ValidateOnStart</c>),
	/// so a malformed <see cref="AuditIntegrityOptions.KeyId"/> fails fast at host startup rather than
	/// producing unverifiable integrity tags at runtime. A consumer still supplies the signing key (and may
	/// configure the options) via <c>services.Configure&lt;AuditIntegrityOptions&gt;(...)</c>.
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="System.ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddAuditIntegrity(this IServiceCollection services)
	{
		System.ArgumentNullException.ThrowIfNull(services);

		services.AddOptions<AuditIntegrityOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AuditIntegrityOptions>, AuditIntegrityOptionsValidator>());

		services.TryAddSingleton<IAuditSigningKeyProvider, OptionsAuditSigningKeyProvider>();
		services.TryAddSingleton<IAuditIntegrityStrategy, HmacAuditIntegrityStrategy>();
		return services;
	}
}
