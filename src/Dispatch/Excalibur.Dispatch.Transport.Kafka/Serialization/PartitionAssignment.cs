// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents a partition assignment.
/// </summary>
/// <param name="Topic"> The Kafka topic name. </param>
/// <param name="Partition"> The partition number. </param>
/// <param name="Offset"> The assigned offset. </param>
/// <param name="ConsumerId"> The consumer identifier. </param>
public sealed record PartitionAssignment(
	string Topic,
	int Partition,
	long Offset,
	string ConsumerId);
