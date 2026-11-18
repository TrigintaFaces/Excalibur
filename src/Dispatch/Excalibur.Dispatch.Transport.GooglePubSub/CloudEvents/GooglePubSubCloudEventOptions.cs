// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Pub/Sub-specific CloudEvent configuration options.
/// </summary>
public sealed class GooglePubSubCloudEventOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to use ordering keys for message ordering.
	/// </summary>
	/// <remarks> When enabled, CloudEvents will use partition keys as Pub/Sub ordering keys for ordered delivery. </remarks>
	/// <value>
	/// A value indicating whether to use ordering keys for message ordering.
	/// </value>
	public bool UseOrderingKeys { get; set; } = true;

	/// <summary>
	/// Gets or sets the compression and monitoring configuration for CloudEvents.
	/// </summary>
	/// <value>
	/// The compression and monitoring configuration.
	/// </value>
	public CloudEventTransportOptions Transport { get; set; } = new();
}

/// <summary>
/// Configuration options for CloudEvent transport concerns including compression and monitoring.
/// </summary>
public sealed class CloudEventTransportOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable message compression for large CloudEvents.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message compression for large CloudEvents.
	/// </value>
	public bool EnableCompression { get; set; }

	/// <summary>
	/// Gets or sets the threshold (in bytes) for triggering message compression.
	/// </summary>
	/// <value>
	/// The threshold (in bytes) for triggering message compression.
	/// </value>
	public int CompressionThreshold { get; set; } = 1024 * 1024; // 1MB

	/// <summary>
	/// Gets or sets the maximum length, in bytes, that an inbound compressed CloudEvent may occupy once
	/// decompressed, or <see langword="null"/> to opt out of the limit.
	/// </summary>
	/// <value>
	/// The maximum decompressed length in bytes. Defaults to 4 MiB, matching the framework-wide inbound
	/// payload default. Raise it for larger legitimate payloads.
	/// </value>
	/// <remarks>
	/// <para>
	/// This bounds the <em>output</em> of decompression, which is a different quantity from the inbound
	/// size the receiver already checks. The compression ratio is chosen by whoever sent the message, so a
	/// payload small enough to accept on the wire can still declare or expand to an arbitrarily large
	/// buffer; without an output bound, a few hundred bytes of input names the allocation.
	/// </para>
	/// <para>
	/// An over-limit payload is <strong>rejected</strong>, never truncated — a partially decompressed body
	/// would be handed to a deserializer as though it were the whole message.
	/// </para>
	/// </remarks>
	public int? MaxDecompressedBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;
}
