// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Storage.Queues.Models;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Handles CloudEvents parsing and conversion for Azure Storage Queue messages.
/// </summary>
public interface ICloudEventProcessor
{
	/// <summary>
	/// Attempts to parse a CloudEvent from the message text.
	/// </summary>
	/// <param name="messageText"> The message text to parse. </param>
	/// <param name="cloudEvent"> The parsed CloudEvent if successful. </param>
	/// <returns> True if parsing succeeded; otherwise, false. </returns>
	bool TryParseCloudEvent(string messageText, out CloudEvent? cloudEvent);

	/// <summary>
	/// Converts a CloudEvent to a dispatch event with proper context.
	/// </summary>
	/// <param name="cloudEvent"> The CloudEvent to convert. </param>
	/// <param name="queueMessage"> The original queue message. </param>
	/// <param name="context"> The message context. </param>
	/// <returns> The converted dispatch event. </returns>
	IDispatchEvent ConvertToDispatchEvent(CloudEvent cloudEvent, QueueMessage queueMessage, IMessageContext context);

	/// <summary>
	/// Updates the message context with CloudEvent metadata.
	/// </summary>
	/// <param name="context"> The message context to update. </param>
	/// <param name="cloudEvent"> The CloudEvent containing metadata. </param>
	void UpdateContextFromCloudEvent(IMessageContext context, CloudEvent cloudEvent);
}
