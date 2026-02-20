// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Cloud provider types.
/// </summary>
public enum CloudProviderType
{
	/// <summary>
	/// Amazon Web Services.
	/// </summary>
	Aws = 0,

	/// <summary>
	/// Microsoft Azure.
	/// </summary>
	Azure = 1,

	/// <summary>
	/// Google Cloud Platform.
	/// </summary>
	Google = 2,

	/// <summary>
	/// Apache Kafka.
	/// </summary>
	Kafka = 3,

	/// <summary>
	/// RabbitMQ.
	/// </summary>
	RabbitMQ = 4,

	/// <summary>
	/// gRPC.
	/// </summary>
	Grpc = 5,

	/// <summary>
	/// Custom provider.
	/// </summary>
	Custom = 6,
}
