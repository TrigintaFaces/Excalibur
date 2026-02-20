// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Describes the current state and configuration of a Kafka topic.
/// </summary>
/// <param name="Name">The topic name.</param>
/// <param name="Partitions">The number of partitions.</param>
/// <param name="ReplicationFactor">The replication factor.</param>
/// <param name="Config">The current topic-level configuration entries.</param>
/// <param name="IsInternal">Whether this is an internal Kafka topic.</param>
public sealed record TopicDescription(
	string Name,
	int Partitions,
	short ReplicationFactor,
	IReadOnlyDictionary<string, string> Config,
	bool IsInternal);
