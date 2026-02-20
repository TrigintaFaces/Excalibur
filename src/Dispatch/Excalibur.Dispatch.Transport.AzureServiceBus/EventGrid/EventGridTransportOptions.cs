// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Configuration options for the Azure Event Grid transport.
/// </summary>
public sealed class EventGridTransportOptions
{
	/// <summary>
	/// Gets or sets the Event Grid topic endpoint URI (e.g., "https://mytopic.westus2-1.eventgrid.azure.net/api/events").
	/// </summary>
	/// <value>The topic endpoint URI.</value>
	[Required]
	public string TopicEndpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the access key for the Event Grid topic.
	/// When <see langword="null"/>, managed identity (DefaultAzureCredential) is used.
	/// </summary>
	/// <value>The access key, or <see langword="null"/> for managed identity.</value>
	public string? AccessKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use managed identity for authentication.
	/// </summary>
	/// <value><see langword="true"/> to use managed identity; otherwise, <see langword="false"/>.</value>
	public bool UseManagedIdentity { get; set; }

	/// <summary>
	/// Gets or sets the event schema mode.
	/// </summary>
	/// <value>The schema mode. Defaults to <see cref="EventGridSchemaMode.CloudEvents"/>.</value>
	public EventGridSchemaMode SchemaMode { get; set; } = EventGridSchemaMode.CloudEvents;

	/// <summary>
	/// Gets or sets the logical destination name for routing.
	/// </summary>
	/// <value>The destination name. Defaults to "eventgrid-default".</value>
	public string Destination { get; set; } = "eventgrid-default";

	/// <summary>
	/// Gets or sets the default event type for published events.
	/// </summary>
	/// <value>The default event type. Defaults to "Excalibur.Dispatch.TransportMessage".</value>
	public string DefaultEventType { get; set; } = "Excalibur.Dispatch.TransportMessage";

	/// <summary>
	/// Gets or sets the default event source for published events.
	/// </summary>
	/// <value>The default event source. Defaults to "/excalibur/dispatch".</value>
	public string DefaultEventSource { get; set; } = "/excalibur/dispatch";
}
