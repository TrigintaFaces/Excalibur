// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a configuration change event.
/// </summary>
public sealed class ConfigurationChangeEvent
{
	/// <summary>
	/// Gets or sets the unique identifier for this configuration change event.
	/// </summary>
	/// <value>
	/// The unique identifier for this configuration change event.
	/// </value>
	public Guid EventId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the configuration change occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the configuration change occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the type of configuration change.
	/// </summary>
	/// <value>
	/// The type of configuration change.
	/// </value>
	public ConfigurationChangeType ChangeType { get; set; }

	/// <summary>
	/// Gets or sets the configuration section that was changed.
	/// </summary>
	/// <value>
	/// The configuration section that was changed.
	/// </value>
	public string ConfigurationSection { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the user or system that made the configuration change.
	/// </summary>
	/// <value>
	/// The user or system that made the configuration change.
	/// </value>
	public string ChangedBy { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the previous value before the configuration change.
	/// </summary>
	/// <value>
	/// The previous value before the configuration change.
	/// </value>
	public string? PreviousValue { get; set; }

	/// <summary>
	/// Gets or sets the new value after the configuration change.
	/// </summary>
	/// <value>
	/// The new value after the configuration change.
	/// </value>
	public string? NewValue { get; set; }

	/// <summary>
	/// Gets or sets the reason for making this configuration change.
	/// </summary>
	/// <value>
	/// The reason for making this configuration change.
	/// </value>
	public string? ChangeReason { get; set; }

	/// <summary>
	/// Gets or sets additional metadata associated with this configuration change event.
	/// </summary>
	/// <value>
	/// Additional metadata associated with this configuration change event.
	/// </value>
	public Dictionary<string, object> AdditionalData { get; set; } = [];
}
