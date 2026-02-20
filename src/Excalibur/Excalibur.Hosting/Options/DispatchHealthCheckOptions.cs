// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Options;

/// <summary>
/// Options for controlling which Dispatch health checks are registered
/// by <see cref="Microsoft.Extensions.DependencyInjection.DispatchHealthCheckExtensions.AddDispatchHealthChecks"/>.
/// </summary>
/// <remarks>
/// All flags default to <see langword="true"/>. Set individual flags to <see langword="false"/>
/// to skip registration of specific health checks even when the underlying service is available.
/// Health checks are only registered when both the flag is <see langword="true"/> AND the
/// corresponding service type is found in the DI container.
/// </remarks>
public sealed class DispatchHealthCheckOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to include the outbox health check.
	/// </summary>
	/// <value><see langword="true"/> to include outbox health check; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool IncludeOutbox { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include the inbox health check.
	/// </summary>
	/// <value><see langword="true"/> to include inbox health check; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool IncludeInbox { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include the saga health check.
	/// </summary>
	/// <value><see langword="true"/> to include saga health check; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool IncludeSaga { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include the leader election health check.
	/// </summary>
	/// <value><see langword="true"/> to include leader election health check; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool IncludeLeaderElection { get; set; } = true;
}
