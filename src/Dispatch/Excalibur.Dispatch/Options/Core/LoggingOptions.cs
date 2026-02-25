// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for enhanced logging.
/// </summary>
public sealed class LoggingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether enhanced logging is enabled.
	/// </summary>
	/// <value> Default is false. </value>
	public bool EnhancedLogging { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include correlation IDs in logs.
	/// </summary>
	/// <value> Default is true. </value>
	public bool IncludeCorrelationIds { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include execution context in logs.
	/// </summary>
	/// <value> Default is true. </value>
	public bool IncludeExecutionContext { get; set; } = true;
}
