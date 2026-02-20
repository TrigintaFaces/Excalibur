// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Simple test message for basic conformance tests.
/// </summary>
public class TestMessage
{
	public string Id { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Test message with metadata fields.
/// </summary>
public class TestMessageWithMetadata : TestMessage
{
	public string MessageId { get; set; } = string.Empty;
	public string CorrelationId { get; set; } = string.Empty;
	public string UserId { get; set; } = string.Empty;
	public string TenantId { get; set; } = string.Empty;
}
