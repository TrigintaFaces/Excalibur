// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.TestTypes;

/// <summary>
/// Simple test message for use in integration tests.
/// </summary>
public class TestMessage : IDispatchMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the content.
	/// </summary>
	public string Content { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp.
	/// </summary>
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
