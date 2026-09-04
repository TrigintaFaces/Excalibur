// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides flow-local storage for the current message context.
/// </summary>
/// <remarks>
/// Internal by design. Consumer code reaches the current context by DECLARING that it wants one --
/// a handler exposes a settable <see cref="IMessageContext"/> property or takes
/// <see cref="IMessageContextAccessor"/>, and a service that cannot take a dispatch-time parameter
/// injects <see cref="IMessageContextAccessor"/>. Both declarations are visible to the dispatcher,
/// which is what lets it skip publishing an ambient context on the ultra-local fast path (an
/// ExecutionContext copy-on-write, measured at 44% of the standard dispatch path and all of its
/// allocation) for handlers that declared they do not read one.
/// <para>
/// A public static read was the one declaration the dispatcher could NOT see, so it forced the cost
/// to be paid on every dispatch in case somebody was reading. Making this internal is what turns
/// "we cannot know" into "there is nothing to know".
/// </para>
/// </remarks>
internal static class MessageContextHolder
{
	private static readonly AsyncLocal<IMessageContext?> _current = new();

	/// <summary>
	/// Gets or sets the current message context.
	/// </summary>
	/// <value>
	/// The current message context.
	/// </value>
	public static IMessageContext? Current
	{
		get => _current.Value;
		set => _current.Value = value;
	}

	/// <summary>
	/// Clears the current message context.
	/// </summary>
	public static void Clear() => _current.Value = null;
}
