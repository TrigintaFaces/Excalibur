// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Governance;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring IAM governance services on <see cref="IA3Builder"/>.
/// </summary>
public static class GovernanceServiceCollectionExtensions
{
	/// <summary>
	/// Adds IAM governance capabilities to the A3 authorization stack.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">A delegate to configure governance services.</param>
	/// <returns>The <see cref="IA3Builder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use the governance builder to enable specific capabilities:
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddRoles());
	/// </code>
	/// </remarks>
	public static IA3Builder AddGovernance(this IA3Builder builder, Action<IGovernanceBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var governanceBuilder = new GovernanceBuilder(builder.Services);
		configure(governanceBuilder);

		return builder;
	}
}
