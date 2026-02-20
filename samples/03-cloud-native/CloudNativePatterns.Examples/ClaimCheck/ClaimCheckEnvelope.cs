// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

public class ClaimCheckEnvelope
{
	public ClaimCheckReference ClaimCheckReference { get; set; } = null!;
	public string MessageType { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
}
