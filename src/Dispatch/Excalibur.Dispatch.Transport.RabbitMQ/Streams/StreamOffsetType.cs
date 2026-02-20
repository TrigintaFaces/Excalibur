// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Specifies the type of stream offset for RabbitMQ stream consumption.
/// </summary>
public enum StreamOffsetType
{
	/// <summary>
	/// Start from the first available message in the stream.
	/// </summary>
	First = 0,

	/// <summary>
	/// Start from the last chunk in the stream.
	/// </summary>
	Last = 1,

	/// <summary>
	/// Start from new messages only (published after subscription).
	/// </summary>
	Next = 2,

	/// <summary>
	/// Start from a specific numeric offset.
	/// </summary>
	Offset = 3,

	/// <summary>
	/// Start from a specific timestamp.
	/// </summary>
	Timestamp = 4,
}
