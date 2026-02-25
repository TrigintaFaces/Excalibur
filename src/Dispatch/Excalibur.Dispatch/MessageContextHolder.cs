// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Provides thread-local storage for the current message context.
/// </summary>
public static class MessageContextHolder
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
