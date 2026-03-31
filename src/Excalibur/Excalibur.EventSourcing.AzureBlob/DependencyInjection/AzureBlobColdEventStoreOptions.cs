// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.AzureBlob.DependencyInjection;

/// <summary>
/// Configuration options for the Azure Blob cold event store.
/// </summary>
public sealed class AzureBlobColdEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Azure Blob Storage connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the container name for cold event storage.
	/// </summary>
	/// <value>Default is "excalibur-cold-events".</value>
	[Required]
	public string ContainerName { get; set; } = "excalibur-cold-events";

	/// <summary>
	/// Gets or sets a value indicating whether to create the container if it does not exist.
	/// </summary>
	/// <value>Default is <see langword="true"/>.</value>
	public bool CreateContainerIfNotExists { get; set; } = true;
}
