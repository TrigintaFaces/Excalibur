// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.CloudEvents;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// RabbitMQ adapter for CloudEvents supporting both structured and binary modes.
/// </summary>
/// <remarks>
/// Implements CloudEvents specification for RabbitMQ with DoD-compliant envelope property preservation. Supports structured mode (JSON
/// payload) and binary mode (message headers).
/// </remarks>
public interface IRabbitMqCloudEventAdapter :
	ICloudEventMapper<(IBasicProperties properties, ReadOnlyMemory<byte> body)>
{
	/// <summary>
	/// Validates that RabbitMQ message properties and body contain valid CloudEvent data.
	/// </summary>
	/// <param name="properties"> The RabbitMQ basic properties. </param>
	/// <param name="body"> The message body. </param>
	/// <returns> True if the message contains valid CloudEvent data. </returns>
	bool IsValidCloudEventMessage(IBasicProperties properties, ReadOnlyMemory<byte> body);

	/// <summary>
	/// Attempts to detect the CloudEvent mode used by the supplied RabbitMQ message tuple.
	/// </summary>
	/// <param name="properties"> The RabbitMQ basic properties. </param>
	/// <param name="body"> The message body. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The detected mode when available; otherwise <c> null </c>. </returns>
	ValueTask<CloudEventMode?> TryDetectMode(
		IBasicProperties properties,
		ReadOnlyMemory<byte> body,
		CancellationToken cancellationToken);
}
