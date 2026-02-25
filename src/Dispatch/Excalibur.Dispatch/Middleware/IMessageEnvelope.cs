// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Simple message envelope interface for extracting message identifiers.
/// </summary>
public interface IMessageEnvelope
{
	/// <summary>
	/// Gets the unique identifier of the message.
	/// </summary>
	/// <value>
	/// The unique identifier of the message.
	/// </value>
	string MessageId { get; }
}
