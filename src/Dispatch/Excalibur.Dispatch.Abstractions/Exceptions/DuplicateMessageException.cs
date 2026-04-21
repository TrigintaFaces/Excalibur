// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a duplicate message is detected and
/// <c>ThrowOnDuplicate</c> is configured as the duplicate behavior.
/// </summary>
public sealed class DuplicateMessageException : ApiException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateMessageException"/> class.
	/// </summary>
	/// <param name="messageId">The ID of the duplicate message.</param>
	public DuplicateMessageException(string messageId)
		: base($"Duplicate message detected: '{messageId}'.")
	{
		MessageId = messageId;
	}

	/// <summary>
	/// Gets the ID of the duplicate message.
	/// </summary>
	public string MessageId { get; }
}
