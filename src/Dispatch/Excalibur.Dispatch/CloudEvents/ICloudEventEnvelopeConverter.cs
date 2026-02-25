// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using CloudNative.CloudEvents;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.CloudEvents;

/// <summary>
/// Converts between MessageEnvelope and CloudEvent while preserving DoD-required envelope properties.
/// </summary>
/// <remarks>
/// Ensures round-trip conversion with no attribute loss for: MessageId, CorrelationId, TenantId, UserId, TraceId, RetryCount,
/// ScheduledTime, and Timestamp as specified by DoD requirements.
/// </remarks>
public interface ICloudEventEnvelopeConverter
{
	/// <summary>
	/// Converts a MessageEnvelope to a CloudEvent while preserving all envelope properties.
	/// </summary>
	/// <param name="envelope"> The message envelope to convert. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A CloudEvent with all envelope properties preserved as extension attributes. </returns>
	Task<CloudEvent> FromEnvelopeAsync(MessageEnvelope envelope, CancellationToken cancellationToken);

	/// <summary>
	/// Converts a CloudEvent back to a MessageEnvelope while restoring all envelope properties.
	/// </summary>
	/// <param name="cloudEvent"> The CloudEvent to convert. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A MessageEnvelope with all properties restored from CloudEvent attributes. </returns>
	Task<MessageEnvelope> ToEnvelopeAsync(CloudEvent cloudEvent, CancellationToken cancellationToken);
}
