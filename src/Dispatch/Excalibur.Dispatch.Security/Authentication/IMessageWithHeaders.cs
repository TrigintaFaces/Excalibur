// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Interface for messages that carry headers.
/// </summary>
public interface IMessageWithHeaders
{
	/// <summary>
	/// Gets the message headers.
	/// </summary>
	/// <value>
	/// A dictionary containing the message headers as key-value pairs.
	/// </value>
	IDictionary<string, string> Headers { get; }
}
