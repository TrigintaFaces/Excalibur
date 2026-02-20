// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Configuration options for the Kafka admin client.
/// </summary>
/// <remarks>
/// <para>
/// Provides configuration for administrative operations against Kafka clusters,
/// such as creating, deleting, and describing topics. The admin client uses
/// the same bootstrap servers as the producer/consumer but may have different
/// timeout requirements for administrative operations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddKafkaAdmin(options =>
/// {
///     options.BootstrapServers = "broker1:9092,broker2:9092";
///     options.OperationTimeout = TimeSpan.FromSeconds(30);
/// });
/// </code>
/// </example>
public sealed class KafkaAdminOptions
{
	/// <summary>
	/// Gets or sets the Kafka bootstrap servers.
	/// </summary>
	/// <value>The comma-separated list of broker addresses.</value>
	[Required]
	public string BootstrapServers { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timeout for administrative operations.
	/// </summary>
	/// <remarks>
	/// Administrative operations such as topic creation or deletion may take
	/// longer than normal produce/consume operations due to cluster coordination.
	/// </remarks>
	/// <value>The operation timeout. Default is 30 seconds.</value>
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
