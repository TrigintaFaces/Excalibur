// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Inbox.DependencyInjection;

/// <summary>
/// Builder interface for configuring Excalibur Inbox services.
/// </summary>
/// <remarks>
/// <para>
/// Provides a unified, fluent API for configuring inbox providers.
/// </para>
/// <para>
/// This follows the Microsoft builder pattern (like <c>IHealthChecksBuilder</c>)
/// and matches the established <c>ISagaBuilder</c> and <c>ILeaderElectionBuilder</c>
/// patterns in this codebase: 1 property, 0 methods. All <c>Use*()</c> methods are
/// extension methods defined in the provider packages.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburInbox(inbox => inbox
///     .UseSqlServer(connectionString)
///     // or .UsePostgres(connectionString)
///     // or .UseInMemory()
/// );
/// </code>
/// </example>
public interface IInboxBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The service collection.</value>
	IServiceCollection Services { get; }
}
