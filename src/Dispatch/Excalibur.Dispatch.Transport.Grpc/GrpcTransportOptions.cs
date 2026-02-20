// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Grpc;

/// <summary>
/// Configuration options for the gRPC transport.
/// </summary>
public sealed class GrpcTransportOptions
{
	/// <summary>
	/// Gets or sets the gRPC server address (e.g., "https://localhost:5001").
	/// </summary>
	/// <value>The server address URI.</value>
	[Required]
	public string ServerAddress { get; set; } = string.Empty;

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
}
