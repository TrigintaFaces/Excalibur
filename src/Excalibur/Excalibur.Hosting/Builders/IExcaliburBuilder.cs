// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting.Builders;

/// <summary>
/// Unified builder root for configuring Excalibur subsystems.
/// </summary>
/// <remarks>
/// <para>
/// Feature-specific fluent methods (event sourcing, outbox, CDC, saga, leader election)
/// are provided by the corresponding feature packages as extension methods.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcalibur(excalibur =>
/// {
///     // Feature methods are available when feature packages are referenced.
///     // Example:
///     // excalibur.AddEventSourcing(...).AddOutbox(...);
/// });
/// </code>
/// </para>
/// </remarks>
public interface IExcaliburBuilder
{
	/// <summary>
	/// Gets the service collection for advanced registration scenarios.
	/// </summary>
	IServiceCollection Services { get; }
}
