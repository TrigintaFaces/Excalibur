// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for the logging middleware.
/// </summary>
public sealed class LoggingMiddlewareOptions
{
	/// <summary>
	/// Gets or sets the log level for successful message processing.
	/// </summary>
	/// <value>Default is <see cref="LogLevel.Information"/>.</value>
	public LogLevel SuccessLevel { get; set; } = LogLevel.Information;

	/// <summary>
	/// Gets or sets the log level for failed message processing.
	/// </summary>
	/// <value>Default is <see cref="LogLevel.Error"/>.</value>
	public LogLevel FailureLevel { get; set; } = LogLevel.Error;

	/// <summary>
	/// Gets or sets a value indicating whether to include message payload in logs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Setting this to <see langword="true"/> may expose sensitive data in logs.
	/// Consider the security implications before enabling in production.
	/// </para>
	/// </remarks>
	/// <value>Default is <see langword="false"/>.</value>
	public bool IncludePayload { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include timing information in logs.
	/// </summary>
	/// <value>Default is <see langword="true"/>.</value>
	public bool IncludeTiming { get; set; } = true;

	/// <summary>
	/// Gets the set of message types to exclude from logging.
	/// </summary>
	/// <remarks>
	/// Use this to exclude high-frequency or noise messages (e.g., health checks).
	/// </remarks>
	public HashSet<Type> ExcludeTypes { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to log the start of message processing.
	/// </summary>
	/// <value>Default is <see langword="true"/>.</value>
	public bool LogStart { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to log the completion of message processing.
	/// </summary>
	/// <value>Default is <see langword="true"/>.</value>
	public bool LogCompletion { get; set; } = true;
}
