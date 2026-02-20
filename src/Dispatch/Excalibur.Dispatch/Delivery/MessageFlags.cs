// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Message flags.
/// </summary>
[Flags]
[SuppressMessage("Design", "CA1028:Enum Storage should be Int32",
	Justification = "Byte is intentionally used for memory efficiency in high-throughput messaging scenarios")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
	Justification = "Flags suffix is appropriate for bit flags enum")]
public enum MessageFlags : byte
{
	/// <summary>
	/// No flags.
	/// </summary>
	None = 0,

	/// <summary>
	/// Message is compressed.
	/// </summary>
	Compressed = 1,

	/// <summary>
	/// Message is encrypted.
	/// </summary>
	Encrypted = 1 << 1,

	/// <summary>
	/// Message should be persisted.
	/// </summary>
	Persistent = 1 << 2,

	/// <summary>
	/// Message is priority.
	/// </summary>
	HighPriority = 1 << 3,

	/// <summary>
	/// Message has been validated.
	/// </summary>
	Validated = 1 << 4,
}
