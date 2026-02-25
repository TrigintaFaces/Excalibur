// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Default implementation of <see cref="IMessageContextAccessor" /> that stores context in an <see cref="AsyncLocal{T}" />.
/// </summary>
public sealed class MessageContextAccessor : IMessageContextAccessor
{
	/// <summary>
	/// Gets or sets the current message context.
	/// </summary>
	/// <value>
	/// The current message context.
	/// </value>
	public IMessageContext? MessageContext
	{
		get => MessageContextHolder.Current;
		set => MessageContextHolder.Current = value;
	}
}
