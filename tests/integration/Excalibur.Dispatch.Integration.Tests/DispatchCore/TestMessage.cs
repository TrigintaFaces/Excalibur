// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore;

public class TestMessage
{
	public required string Id { get; set; } = Guid.NewGuid().ToString();

	public required string Content { get; set; } = string.Empty;

	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
