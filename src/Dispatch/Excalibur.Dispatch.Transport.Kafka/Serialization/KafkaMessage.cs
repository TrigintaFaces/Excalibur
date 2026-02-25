// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Confluent.Kafka;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents a Kafka message for serialization.
/// </summary>
/// <param name="Topic"> The Kafka topic name. </param>
/// <param name="Partition"> The partition number. </param>
/// <param name="Offset"> The message offset. </param>
/// <param name="Key"> The message key. </param>
/// <param name="Value"> The message payload. </param>
/// <param name="Headers"> The message headers. </param>
/// <param name="Timestamp"> The message timestamp. </param>
public sealed record KafkaMessage(
	string Topic,
	int Partition,
	long Offset,
	string? Key,
	byte[] Value,
	Dictionary<string, byte[]>? Headers,
	Timestamp Timestamp);
