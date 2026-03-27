// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.Reporting;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering entitlement reporting services on <see cref="IGovernanceBuilder"/>.
/// </summary>
public static class EntitlementReportingGovernanceBuilderExtensions
{
	/// <summary>
	/// Adds entitlement reporting services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="IEntitlementReportProvider"/> with the default aggregating implementation
	/// and <see cref="IReportFormatter"/> with the built-in JSON formatter.
	/// Both registrations use <c>TryAdd</c> and can be overridden by consumer registrations.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddEntitlementReporting());
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddEntitlementReporting(this IGovernanceBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IReportFormatter, JsonReportFormatter>();
		builder.Services.TryAddSingleton<IEntitlementReportProvider, DefaultEntitlementReportProvider>();

		return builder;
	}
}
