// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Options.CloudEvents;

/// <summary>
/// Configuration options for CloudEvents support in Excalibur.Dispatch.
/// </summary>
/// <remarks>
/// Provides configuration for CloudEvents serialization modes, attribute mappings, and envelope property preservation according to DoD
/// requirements for envelope integrity.
/// </remarks>
public sealed class CloudEventOptions
{
	/// <summary>
	/// Gets or sets the CloudEvents Mode for message serialization.
	/// </summary>
	/// <remarks>
	/// Structured Mode uses application/cloudevents+json content type. Binary Mode maps CE attributes to transport-specific headers/attributes.
	/// </remarks>
	/// <value>The current <see cref="Mode"/> value.</value>
	public CloudEventMode Mode { get; set; } = CloudEventMode.Structured;

	/// <summary>
	/// Gets or sets the CloudEvents specification version to use.
	/// </summary>
	/// <value>The current <see cref="SpecVersion"/> value.</value>
	public CloudEventsSpecVersion SpecVersion { get; set; } = CloudEventsSpecVersion.V1_0;

	/// <summary>
	/// Gets or sets the default source URI for generated CloudEvents.
	/// </summary>
	/// <remarks> Used when message context doesn't specify a source. Should be a valid URI identifying the event producer. </remarks>
	/// <value>
	/// The default source URI for generated CloudEvents.
	/// </value>
	[Required]
	public Uri DefaultSource { get; set; } = new("urn:dispatch");

	/// <summary>
	/// Gets or sets a value indicating whether to validate CloudEvent schema.
	/// </summary>
	/// <value>The current <see cref="ValidateSchema"/> value.</value>
	public bool ValidateSchema { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include schema version in CloudEvents.
	/// </summary>
	/// <value>The current <see cref="IncludeSchemaVersion"/> value.</value>
	public bool IncludeSchemaVersion { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to automatically register message schemas.
	/// </summary>
	/// <value>The current <see cref="AutoRegisterSchemas"/> value.</value>
	public bool AutoRegisterSchemas { get; set; }

	/// <summary>
	/// Gets or sets the schema provider function for auto-registration.
	/// </summary>
	/// <value>The current <see cref="SchemaProvider"/> value.</value>
	public Func<Type, string>? SchemaProvider { get; set; }

	/// <summary>
	/// Gets or sets the schema version provider function.
	/// </summary>
	/// <value>The current <see cref="SchemaVersionProvider"/> value.</value>
	public Func<Type, string>? SchemaVersionProvider { get; set; }

	/// <summary>
	/// Gets or sets the custom validator for CloudEvents.
	/// </summary>
	/// <value>The current <see cref="CustomValidator"/> value.</value>
	public Func<CloudEvent, CancellationToken, Task<bool>>? CustomValidator { get; set; }

	/// <summary>
	/// Gets or sets the transformer for outgoing CloudEvents.
	/// </summary>
	/// <value>The current <see cref="OutgoingTransformer"/> value.</value>
	public Func<CloudEvent, IDispatchEvent, IMessageContext, CancellationToken, Task>? OutgoingTransformer { get; set; }

	/// <summary>
	/// Gets the set of extension attributes to exclude from CloudEvents.
	/// </summary>
	/// <remarks> Extensions listed here will not be copied from message context to CloudEvent attributes. </remarks>
	/// <value>The current <see cref="ExcludedExtensions"/> value.</value>
	public HashSet<string> ExcludedExtensions { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to preserve all envelope properties as CloudEvent attributes.
	/// </summary>
	/// <remarks>
	/// When true, ensures MessageId, CorrelationId, TenantId, UserId, TraceId, RetryCount, ScheduledTime, and Timestamp are preserved as CE attributes.
	/// </remarks>
	/// <value>The current <see cref="PreserveEnvelopeProperties"/> value.</value>
	public bool PreserveEnvelopeProperties { get; set; } = true;

	/// <summary>
	/// Gets or sets the prefix for Dispatch-specific CloudEvent extension attributes.
	/// </summary>
	/// <remarks> Used to namespace Excalibur.Dispatch envelope properties as CloudEvent extensions to avoid Tests.CloudProviders. </remarks>
	/// <value>The current <see cref="DispatchExtensionPrefix"/> value.</value>
	[Required]
	public string DispatchExtensionPrefix { get; set; } = "dispatch";

	/// <summary>
	/// Gets or sets a value indicating whether to use compressed serialization for large payloads.
	/// </summary>
	/// <value>The current <see cref="UseCompression"/> value.</value>
	public bool UseCompression { get; set; }

	/// <summary>
	/// Gets or sets the minimum payload size (in bytes) to trigger compression.
	/// </summary>
	/// <value>The current <see cref="CompressionThreshold"/> value.</value>
	[Range(0, int.MaxValue)]
	public int CompressionThreshold { get; set; } = 1024;

	/// <summary>
	/// Gets or sets a value indicating whether to enable DoD (Department of Defense) compliance Mode.
	/// </summary>
	/// <remarks>
	/// When enabled, enforces stricter validation requirements for envelope properties including mandatory correlation ID, user ID, and
	/// trace parent for audit compliance.
	/// </remarks>
	/// <value>The current <see cref="EnableDoDCompliance"/> value.</value>
	public bool EnableDoDCompliance { get; set; }

	/// <summary>
	/// Gets or sets the default CloudEvents Mode when not explicitly specified.
	/// </summary>
	/// <remarks> Provides a fallback Mode for CloudEvent serialization. Can be overridden per operation. </remarks>
	/// <value>The current <see cref="DefaultMode"/> value.</value>
	public CloudEventMode DefaultMode { get; set; } = CloudEventMode.Structured;
}
