// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Event Hubs-specific CloudEvent configuration options.
/// </summary>
public sealed class AzureEventHubsCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use partition keys for Event Hubs CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to use partition keys for Event Hubs CloudEvents.
	/// </value>
	public bool UsePartitionKeys { get; set; } = true;

	/// <summary>
	/// Gets or sets the default partition key strategy.
	/// </summary>
	/// <value>
	/// The default partition key strategy.
	/// </value>
	public PartitionKeyStrategy PartitionKeyStrategy { get; set; } = PartitionKeyStrategy.CorrelationId;
}
