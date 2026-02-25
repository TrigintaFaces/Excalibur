// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Specifies the configuration for creating or describing a Kafka topic.
/// </summary>
/// <param name="Name">The topic name.</param>
/// <param name="Partitions">The number of partitions for the topic.</param>
/// <param name="ReplicationFactor">The replication factor for the topic.</param>
/// <param name="Config">Optional topic-level configuration overrides (e.g., retention.ms, cleanup.policy).</param>
public sealed record TopicSpecification(
	string Name,
	int Partitions,
	short ReplicationFactor,
	IReadOnlyDictionary<string, string>? Config = null);
