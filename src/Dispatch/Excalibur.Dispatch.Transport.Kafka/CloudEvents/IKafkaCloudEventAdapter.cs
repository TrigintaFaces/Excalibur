// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.CloudEvents;

using CloudEvent = CloudNative.CloudEvents.CloudEvent;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka adapter for CloudEvents supporting both structured and binary modes.
/// </summary>
/// <remarks>
/// Implements CloudEvents specification for Apache Kafka with DoD-compliant envelope property preservation. Supports structured mode
/// (JSON payload) and binary mode (message headers).
/// </remarks>
public interface IKafkaCloudEventAdapter : ICloudEventMapper<Message<string, string>>
{
	/// <summary>
	/// Converts a Kafka <see cref="ConsumeResult{TKey, TValue}" /> into a CloudEvent using the configured mapper.
	/// </summary>
	/// <param name="consumeResult"> The Kafka consume result containing the transport message. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The reconstructed CloudEvent instance. </returns>
	Task<CloudEvent> FromKafkaAsync(
		global::Confluent.Kafka.ConsumeResult<string, string> consumeResult,
		CancellationToken cancellationToken);

	/// <summary>
	/// Determines whether the supplied consume result represents a valid CloudEvent transport message.
	/// </summary>
	/// <param name="consumeResult"> The Kafka consume result to inspect. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The detected CloudEvent mode, or <c> null </c> when not applicable. </returns>
	ValueTask<CloudEventMode?> DetectModeAsync(
		global::Confluent.Kafka.ConsumeResult<string, string> consumeResult,
		CancellationToken cancellationToken);
}
