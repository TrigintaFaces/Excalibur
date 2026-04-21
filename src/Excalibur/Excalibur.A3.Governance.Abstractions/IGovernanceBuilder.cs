// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.A3.Governance;

/// <summary>
/// Builder interface for configuring IAM governance services.
/// </summary>
/// <remarks>
/// <para>
/// Provides a unified, fluent API for configuring governance capabilities:
/// role management, access reviews, separation of duties, and provisioning workflows.
/// </para>
/// <para>
/// Follows the <c>ISagaBuilder</c> pattern: a single <see cref="Services"/> property
/// with typed extension methods for each capability.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburA3Core()
///     .AddGovernance(g => g
///         .AddRoles()
///         .AddAccessReviews()
///         .AddSeparationOfDuties());
/// </code>
/// </example>
public interface IGovernanceBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The service collection.</value>
	IServiceCollection Services { get; }
}
