// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS EventBridge.
/// </summary>
public sealed class AwsEventBridgeOptions : AwsProviderOptions
{
	/// <summary>
	/// Gets or sets the event bus name.
	/// </summary>
	/// <value>
	/// The event bus name.
	/// </value>
	public string EventBusName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether enables encryption when sending messages.
	/// </summary>
	/// <value>
	/// A value indicating whether enables encryption when sending messages.
	/// </value>
	public new bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the default source for events.
	/// </summary>
	/// <value>
	/// The default source for events.
	/// </value>
	public string DefaultSource { get; set; } = "Excalibur.Dispatch.Transport";

	/// <summary>
	/// Gets or sets the default detail type for events.
	/// </summary>
	/// <value>
	/// The default detail type for events.
	/// </value>
	public string DefaultDetailType { get; set; } = string.Empty;

	/// <summary>
	/// Gets the rule names to manage.
	/// </summary>
	/// <value>
	/// The rule names to manage.
	/// </value>
	public Collection<string> RuleNames { get; } = [];

	/// <summary>
	/// Gets or sets the retry policy.
	/// </summary>
	/// <value>
	/// The retry policy.
	/// </value>
	public new EventBridgeRetryPolicy? RetryPolicy { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable event archiving.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable event archiving.
	/// </value>
	public bool EnableArchiving { get; set; }

	/// <summary>
	/// Gets or sets the archive name.
	/// </summary>
	/// <value>
	/// The archive name.
	/// </value>
	public string? ArchiveName { get; set; }

	/// <summary>
	/// Gets or sets the retention days for archived events.
	/// </summary>
	/// <value>
	/// The retention days for archived events.
	/// </value>
	public int ArchiveRetentionDays { get; set; } = 7;
}
