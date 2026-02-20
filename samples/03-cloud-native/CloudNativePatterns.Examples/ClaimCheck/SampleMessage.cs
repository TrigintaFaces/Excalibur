// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Sample message class for demonstrations.
/// </summary>
public class SampleMessage
{
	public Guid Id { get; set; }
	public byte[] Payload { get; set; } = [];
	public DateTimeOffset Timestamp { get; set; }
	public Dictionary<string, string> Headers { get; } = [];
}
