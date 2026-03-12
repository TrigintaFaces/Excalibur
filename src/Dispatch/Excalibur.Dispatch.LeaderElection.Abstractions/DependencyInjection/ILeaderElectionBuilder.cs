// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.LeaderElection.DependencyInjection;

/// <summary>
/// Builder interface for configuring Excalibur Leader Election services.
/// </summary>
/// <remarks>
/// <para>
/// Provides a unified, fluent API for configuring leader election providers
/// and optional features (health checks, fencing tokens).
/// </para>
/// <para>
/// This follows the Microsoft builder pattern (like <c>IHealthChecksBuilder</c>)
/// and matches the established <c>ISagaBuilder</c> pattern in this codebase:
/// 1 property, 0 methods. All <c>Use*()</c> and <c>With*()</c> methods are
/// extension methods defined in the provider packages.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburLeaderElection(le => le
///     .UseInMemory()
///     .WithHealthChecks()
///     .WithFencingTokens());
/// </code>
/// </example>
public interface ILeaderElectionBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The service collection.</value>
	IServiceCollection Services { get; }
}
