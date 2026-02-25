// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.Processing;

/// <summary>
/// Configuration options for the CDC background processing hosted service.
/// </summary>
/// <remarks>
/// <para>
/// These options control the polling behavior and lifecycle of
/// <see cref="CdcProcessingHostedService"/>. They follow the same pattern
/// as <c>OutboxProcessingOptions</c>.
/// </para>
/// </remarks>
public sealed class CdcProcessingOptions
{
	/// <summary>
	/// Gets or sets the interval between polling cycles.
	/// </summary>
	/// <value>The polling interval. Default is 5 seconds.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets whether this instance is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the background processor is enabled; otherwise, <see langword="false"/>.
	/// Default is <see langword="true"/>.
	/// </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the drain timeout in seconds for graceful shutdown.
	/// </summary>
	/// <value>The drain timeout in seconds. Default is 30.</value>
	/// <remarks>
	/// When the service is stopping, this timeout controls how long to wait for
	/// in-flight processing to complete before forcing shutdown.
	/// </remarks>
	[Range(1, int.MaxValue)]
	public int DrainTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the drain timeout as a <see cref="TimeSpan"/>.
	/// </summary>
	/// <value>The drain timeout duration.</value>
	public TimeSpan DrainTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds);

	/// <summary>
	/// Gets or sets the number of consecutive errors before the service is considered unhealthy.
	/// </summary>
	/// <value>The consecutive error threshold. Default is 3.</value>
	[Range(1, int.MaxValue)]
	public int UnhealthyThreshold { get; set; } = 3;
}
