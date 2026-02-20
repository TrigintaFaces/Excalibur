// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Represents consumer group state.
/// </summary>
/// <param name="GroupId"> The consumer group identifier. </param>
/// <param name="State"> The current state of the consumer group. </param>
/// <param name="Members"> The collection of member identifiers. </param>
/// <param name="Protocol"> The partition assignment protocol. </param>
/// <param name="ProtocolType"> The protocol type. </param>
public sealed record ConsumerGroupState(
	string GroupId,
	string State,
	Collection<string> Members,
	string Protocol,
	string ProtocolType);
