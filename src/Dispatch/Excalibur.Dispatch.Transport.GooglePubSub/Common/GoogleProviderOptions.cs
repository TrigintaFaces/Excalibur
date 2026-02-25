// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Google Cloud Pub/Sub messaging provider.
/// </summary>
public sealed class GoogleProviderOptions : ProviderOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	/// <value>
	/// The Google Cloud project ID.
	/// </value>
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether to use the Pub/Sub emulator.
	/// </summary>
	/// <value>
	/// A value indicating whether to use the Pub/Sub emulator.
	/// </value>
	public bool UseEmulator { get; set; }

	/// <summary>
	/// Gets or sets the emulator host address.
	/// </summary>
	/// <value>
	/// The emulator host address.
	/// </value>
	public string? EmulatorHost { get; set; } = "localhost:8085";

	/// <summary>
	/// Gets or sets the request timeout.
	/// </summary>
	/// <value>
	/// The request timeout.
	/// </value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets a value indicating whether to validate connectivity on startup.
	/// </summary>
	/// <value>
	/// A value indicating whether to validate connectivity on startup.
	/// </value>
	public bool ValidateOnStartup { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of messages to pull in a batch.
	/// </summary>
	/// <value>
	/// The maximum number of messages to pull in a batch.
	/// </value>
	public int MaxMessages { get; set; } = 100;

	/// <summary>
	/// Gets or sets the acknowledgment deadline for messages.
	/// </summary>
	/// <value>
	/// The acknowledgment deadline for messages.
	/// </value>
	public TimeSpan AckDeadline { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to enable exactly once delivery.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable exactly once delivery.
	/// </value>
	public bool EnableExactlyOnceDelivery { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable message ordering.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message ordering.
	/// </value>
	public bool EnableMessageOrdering { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to auto-create topics and
	/// subscriptions when missing.
	/// </summary>
	/// <remarks>
	/// When enabled, the broker will attempt to create missing resources
	/// during validation and subscription creation.
	/// </remarks>
	/// <value>
	/// A value indicating whether to auto-create topics and subscriptions.
	/// </value>
	public bool AutoCreateResources { get; set; } = true;

	/// <summary>
	/// Gets or sets the flow control settings.
	/// </summary>
	/// <value>
	/// The flow control settings.
	/// </value>
	public FlowControlOptions FlowControl { get; set; } = new();

	/// <summary>
	/// Gets or sets the retry settings.
	/// </summary>
	/// <value>
	/// The retry settings.
	/// </value>
	public PubSubRetryOptions PubSubRetryOptions { get; set; } = new();
}
