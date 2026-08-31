// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Configuration options for the gRPC transport.
/// </summary>
/// <remarks>
/// This type intentionally exposes more than ten properties: it mirrors the native channel and
/// call configuration surface of the underlying gRPC client (address, deadlines, message-size
/// limits, keep-alive, retry, and TLS settings) so consumers can tune the transport from a single
/// options object. The property count reflects the provider's own configuration breadth rather
/// than a design that should be split into narrower types.
/// </remarks>
public sealed class GrpcTransportOptions
{
	/// <summary>
	/// Gets or sets the gRPC server address (e.g., "https://localhost:5001").
	/// </summary>
	/// <value>The server address URI.</value>
	[Required]
	public string ServerAddress { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether an unencrypted server address is refused.
	/// </summary>
	/// <value>
	/// <see langword="true"/> (the default) to refuse a <see cref="ServerAddress"/> whose scheme is not
	/// <c>https</c>; <see langword="false"/> to accept a cleartext HTTP/2 address.
	/// </value>
	/// <remarks>
	/// gRPC over cleartext HTTP/2 carries call metadata — including bearer tokens placed on the call — and
	/// every payload in the clear. Set this to <see langword="false"/> only when the hop is already
	/// encrypted (a service mesh sidecar terminating mTLS, for example) or for local development.
	/// </remarks>
	public bool RequireTls { get; set; } = true;

	/// <summary>
	/// Gets or sets the default deadline for gRPC calls, in seconds.
	/// </summary>
	/// <value>The deadline in seconds. Defaults to 30.</value>
	[Range(1, 3600)]
	public int DeadlineSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum send message size in bytes.
	/// </summary>
	/// <value>The maximum send message size, or <see langword="null"/> for default.</value>
	public int? MaxSendMessageSize { get; set; }

	/// <summary>
	/// Gets or sets the maximum receive message size in bytes.
	/// </summary>
	/// <value>The maximum receive message size, or <see langword="null"/> for default.</value>
	public int? MaxReceiveMessageSize { get; set; }

	/// <summary>
	/// Gets or sets the maximum inbound-payload length, in bytes, enforced at the ingress trust
	/// boundary before an inbound body is materialized/deserialized. Guards against allocation-based
	/// denial-of-service from an oversized payload. A <see langword="null"/> value opts out of the
	/// limit (unbounded).
	/// </summary>
	/// <value>The maximum inbound-payload length in bytes, or <see langword="null"/> to opt out. Defaults to 4 MiB.</value>
	[Range(1, int.MaxValue)]
	public int? MaxPayloadBytes { get; set; } = PayloadSizeGuard.DefaultMaxPayloadBytes;

	/// <summary>
	/// Gets or sets the destination service method path used for sending messages.
	/// </summary>
	/// <value>The gRPC method path. Defaults to "/dispatch.transport.DispatchTransport/Send".</value>
	public string SendMethodPath { get; set; } = "/dispatch.transport.DispatchTransport/Send";

	/// <summary>
	/// Gets or sets the method path for batch send operations.
	/// </summary>
	/// <value>The gRPC batch method path. Defaults to "/dispatch.transport.DispatchTransport/SendBatch".</value>
	public string SendBatchMethodPath { get; set; } = "/dispatch.transport.DispatchTransport/SendBatch";

	/// <summary>
	/// Gets or sets the method path for receive operations.
	/// </summary>
	/// <value>The gRPC receive method path. Defaults to "/dispatch.transport.DispatchTransport/Receive".</value>
	public string ReceiveMethodPath { get; set; } = "/dispatch.transport.DispatchTransport/Receive";

	/// <summary>
	/// Gets or sets the method path for subscribe (server streaming) operations.
	/// </summary>
	/// <value>The gRPC subscribe method path. Defaults to "/dispatch.transport.DispatchTransport/Subscribe".</value>
	public string SubscribeMethodPath { get; set; } = "/dispatch.transport.DispatchTransport/Subscribe";

	/// <summary>
	/// Gets or sets the logical destination name for routing.
	/// </summary>
	/// <value>The destination name. Defaults to "grpc-default".</value>
	public string Destination { get; set; } = "grpc-default";

	/// <summary>
	/// Gets or sets the HTTP/2 keep-alive ping delay, in seconds. The client sends a keep-alive
	/// ping after the connection has been idle for this duration, keeping long-lived subscribe
	/// streams alive through idle NAT/load-balancer timeouts.
	/// </summary>
	/// <value>The keep-alive ping delay in seconds. Defaults to 60.</value>
	[Range(1, 3600)]
	public int KeepAlivePingDelaySeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets the HTTP/2 keep-alive ping timeout, in seconds. If a keep-alive ping
	/// acknowledgement is not received within this duration the connection is considered dead
	/// and is torn down.
	/// </summary>
	/// <value>The keep-alive ping timeout in seconds. Defaults to 20.</value>
	[Range(1, 600)]
	public int KeepAlivePingTimeoutSeconds { get; set; } = 20;

	/// <summary>
	/// Gets or sets the pooled connection idle timeout, in seconds. Connections idle for longer
	/// than this are removed from the pool. A generous value avoids reconnect churn for
	/// intermittent traffic.
	/// </summary>
	/// <value>The pooled connection idle timeout in seconds. Defaults to 300.</value>
	[Range(1, 86400)]
	public int PooledConnectionIdleTimeoutSeconds { get; set; } = 300;

	/// <summary>
	/// Gets or sets a value indicating whether the underlying HTTP handler may open additional
	/// HTTP/2 connections when the concurrent-stream limit of an existing connection is reached.
	/// Recommended for high-throughput gRPC workloads.
	/// </summary>
	/// <value><see langword="true"/> to enable multiple HTTP/2 connections; otherwise <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnableMultipleHttp2Connections { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the gRPC native retry policy is configured on the
	/// channel via a default <c>ServiceConfig</c>.
	/// </summary>
	/// <value><see langword="true"/> to enable native retries; otherwise <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool EnableRetries { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of gRPC call attempts (the initial attempt plus retries).
	/// gRPC caps this at 5 by default.
	/// </summary>
	/// <value>The maximum number of attempts. Defaults to 5.</value>
	[Range(1, 5)]
	public int MaxRetryAttempts { get; set; } = 5;

	/// <summary>
	/// Gets or sets the initial backoff, in seconds, before the first retry. Subsequent backoffs
	/// grow by <see cref="RetryBackoffMultiplier"/> up to <see cref="RetryMaxBackoffSeconds"/>.
	/// </summary>
	/// <value>The initial retry backoff in seconds. Defaults to 1.</value>
	public double RetryInitialBackoffSeconds { get; set; } = 1;

	/// <summary>
	/// Gets or sets the maximum backoff, in seconds, between retries.
	/// </summary>
	/// <value>The maximum retry backoff in seconds. Defaults to 5.</value>
	public double RetryMaxBackoffSeconds { get; set; } = 5;

	/// <summary>
	/// Gets or sets the multiplier applied to the backoff after each retry attempt.
	/// </summary>
	/// <value>The backoff multiplier. Defaults to 1.5.</value>
	public double RetryBackoffMultiplier { get; set; } = 1.5;

	/// <summary>
	/// Gets the gRPC status codes that trigger a retry. Defaults to <see cref="StatusCode.Unavailable"/>.
	/// </summary>
	/// <value>The collection of retryable status codes.</value>
	public IList<StatusCode> RetryableStatusCodes { get; } = new List<StatusCode> { StatusCode.Unavailable };

	/// <summary>
	/// Gets or sets a value indicating whether hedging is used instead of the retry policy.
	/// When enabled, gRPC sends up to <see cref="MaxRetryAttempts"/> parallel attempts (gRPC
	/// permits either a retry policy or a hedging policy on a method, not both). Off by default.
	/// </summary>
	/// <value><see langword="true"/> to use hedging; otherwise <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool EnableHedging { get; set; }
}
