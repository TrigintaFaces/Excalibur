// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Interface for handlers that need access to the message context.
/// </summary>
public interface IMessageContextAware
{
	/// <summary>
	/// Sets the message context for the handler.
	/// </summary>
	/// <param name="context"> The message context. </param>
	void SetContext(IMessageContext context);
}
