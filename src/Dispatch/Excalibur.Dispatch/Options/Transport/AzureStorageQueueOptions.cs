// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Transport;

/// <summary>
/// Options for Azure Storage Queue transport.
/// </summary>
public sealed class AzureStorageQueueOptions
{
	/// <summary>
	/// Gets or sets the connection string.
	/// </summary>
	/// <value> The Azure Storage account connection string. </value>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the maximum messages to retrieve at once.
	/// </summary>
	/// <value> The batch size requested per dequeue operation. </value>
	public int MaxMessages { get; set; } = 32;

	/// <summary>
	/// Gets or sets the visibility timeout for messages.
	/// </summary>
	/// <value> The visibility timeout applied after dequeuing a message. </value>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Gets or sets the polling interval.
	/// </summary>
	/// <value> The delay between queue polling attempts. </value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
}
