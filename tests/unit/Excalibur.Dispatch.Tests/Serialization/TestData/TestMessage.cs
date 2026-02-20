// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MemoryPack;

using MessagePack;

namespace Excalibur.Dispatch.Tests.Serialization.TestData;

/// <summary>
/// Test message class with MemoryPack and MessagePack serialization attributes.
/// Used for testing pluggable serializers.
/// </summary>
[MemoryPackable]
[MessagePackObject]
public partial class TestMessage
{
	[Key(0)]
	public string Name { get; set; } = string.Empty;

	[Key(1)]
	public int Value { get; set; }

	[Key(2)]
	public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Complex test message for nested object serialization tests.
/// </summary>
[MemoryPackable]
[MessagePackObject]
public partial class ComplexTestMessage
{
	[Key(0)]
	public string Id { get; set; } = string.Empty;

	[Key(1)]
	public TestMessage? Nested { get; set; }

	[Key(2)]
	public List<string> Tags { get; set; } = [];

	[Key(3)]
	public Dictionary<string, int> Metadata { get; set; } = [];
}
