// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents a batch of Kafka messages.
/// </summary>
/// <param name="Messages"> The collection of Kafka messages in the batch. </param>
/// <param name="Metadata"> The batch metadata. </param>
public sealed record KafkaBatch(
	Collection<KafkaMessage> Messages,
	BatchMetadata Metadata);
