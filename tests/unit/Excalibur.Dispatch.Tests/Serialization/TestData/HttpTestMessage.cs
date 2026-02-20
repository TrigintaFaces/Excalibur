// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Serialization.TestData;

/// <summary>
/// Test message class for HTTP serialization tests.
/// Uses only JSON-compatible properties without binary serializer attributes.
/// </summary>
public sealed class HttpTestMessage
{
	public string? UserName { get; set; }
	public int Age { get; set; }
	public string? NullableField { get; set; }
}
