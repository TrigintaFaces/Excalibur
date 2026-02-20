// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.TestTypes;

/// <summary>
/// Test event data for serverless and cloud function tests.
/// </summary>
public class TestEventData
{
	/// <summary>
	/// Gets or sets the message content.
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the value.
	/// </summary>
	public int Value { get; set; }

	/// <summary>
	/// Gets or sets additional data.
	/// </summary>
	public Dictionary<string, object?> Data { get; set; } = new();
}
