// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents batch metadata.
/// </summary>
/// <param name="Topic"> The Kafka topic name. </param>
/// <param name="Partition"> The partition number. </param>
/// <param name="FirstOffset"> The first offset in the batch. </param>
/// <param name="LastOffset"> The last offset in the batch. </param>
/// <param name="MessageCount"> The number of messages in the batch. </param>
/// <param name="ReceivedAt"> The timestamp when the batch was received. </param>
public sealed record BatchMetadata(
	string Topic,
	int Partition,
	long FirstOffset,
	long LastOffset,
	int MessageCount,
	DateTimeOffset ReceivedAt);
