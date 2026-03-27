// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3.Governance;

/// <summary>
/// Default implementation of <see cref="IGovernanceBuilder"/>.
/// </summary>
internal sealed class GovernanceBuilder : IGovernanceBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GovernanceBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection being configured.</param>
	public GovernanceBuilder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		Services = services;
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }
}
