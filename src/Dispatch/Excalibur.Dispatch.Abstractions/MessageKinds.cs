// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the kinds of messages that can be processed by the dispatch system. Used for middleware applicability filtering and pipeline
/// profile selection.
/// </summary>
[Flags]
public enum MessageKinds
{
	/// <summary>
	/// No specific message kind.
	/// </summary>
	None = 0,

	/// <summary>
	/// Action messages (commands/queries) that expect a response or trigger an operation.
	/// </summary>
	Action = 1 << 0,

	/// <summary>
	/// Event messages that notify about something that has happened.
	/// </summary>
	Event = 1 << 1,

	/// <summary>
	/// Document messages that carry structured data without specific behavior.
	/// </summary>
	Document = 1 << 2,

	/// <summary>
	/// All message kinds.
	/// </summary>
	All = Action | Event | Document,
}
