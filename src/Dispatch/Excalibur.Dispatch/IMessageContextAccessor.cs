// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides access to the current message context during message processing operations.
/// </summary>
public interface IMessageContextAccessor
{
	/// <summary>
	/// Gets or sets the current message context. Can be null when no message is being processed.
	/// </summary>
	/// <value>
	/// The current message context. Can be null when no message is being processed.
	/// </value>
	IMessageContext? MessageContext { get; set; }
}
