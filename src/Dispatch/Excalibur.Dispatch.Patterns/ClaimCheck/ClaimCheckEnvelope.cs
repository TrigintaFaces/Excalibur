// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Envelope for claim check references.
/// </summary>
internal sealed record ClaimCheckEnvelope
{
	public ClaimCheckReference Reference { get; set; } = null!;

	public string MessageType { get; set; } = string.Empty;

	public string SerializerName { get; set; } = string.Empty;

	public int OriginalSize { get; set; }
}
