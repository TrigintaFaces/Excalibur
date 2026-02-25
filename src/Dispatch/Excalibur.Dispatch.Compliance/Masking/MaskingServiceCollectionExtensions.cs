// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring data masking services.
/// </summary>
public static class MaskingServiceCollectionExtensions
{
	/// <summary>
	/// Adds PII/PHI data masking services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="defaultRules">Optional default masking rules.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The data masker uses regex-based pattern matching to
	/// identify and mask sensitive data patterns including:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Email addresses (MASK-001)</description></item>
	/// <item><description>Phone numbers (MASK-001)</description></item>
	/// <item><description>Social Security Numbers (MASK-001)</description></item>
	/// <item><description>Credit card numbers (MASK-004, PCI-DSS)</description></item>
	/// <item><description>IP addresses</description></item>
	/// </list>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDataMasking(); // Uses default rules
	///
	/// services.AddDataMasking(MaskingRules.Hipaa); // HIPAA-compliant masking
	///
	/// services.AddDataMasking(new MaskingRules
	/// {
	///     MaskCardNumber = true,
	///     MaskSsn = true
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDataMasking(
		this IServiceCollection services,
		MaskingRules? defaultRules = null)
	{
		var rules = defaultRules ?? MaskingRules.Default;

		services.TryAddSingleton<IDataMasker>(new RegexDataMasker(rules));

		return services;
	}

	/// <summary>
	/// Adds PII/PHI data masking services with PCI-DSS compliance mode.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Enables masking of credit card numbers (MASK-004) for PCI-DSS compliance.
	/// </remarks>
	public static IServiceCollection AddPciDssDataMasking(this IServiceCollection services)
	{
		return services.AddDataMasking(MaskingRules.PciDss);
	}

	/// <summary>
	/// Adds PII/PHI data masking services with HIPAA compliance mode.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Enables masking of PHI patterns including email, phone, SSN, IP addresses,
	/// and dates of birth (MASK-002) for HIPAA compliance.
	/// </remarks>
	public static IServiceCollection AddHipaaDataMasking(this IServiceCollection services)
	{
		return services.AddDataMasking(MaskingRules.Hipaa);
	}

	/// <summary>
	/// Adds PII/PHI data masking services with strict masking (all patterns).
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Enables masking of all recognized patterns for maximum protection.
	/// </remarks>
	public static IServiceCollection AddStrictDataMasking(this IServiceCollection services)
	{
		return services.AddDataMasking(MaskingRules.Strict);
	}
}
