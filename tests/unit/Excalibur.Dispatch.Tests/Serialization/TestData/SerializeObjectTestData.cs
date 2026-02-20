// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MemoryPack;

using MessagePack;

namespace Excalibur.Dispatch.Tests.Serialization.TestData;

/// <summary>
/// Interface for testing interface reference serialization scenarios.
/// </summary>
public interface ITestMessageInterface
{
	string Name { get; set; }
	int Value { get; set; }
}

/// <summary>
/// Concrete implementation of test interface for polymorphic serialization tests.
/// </summary>
[MemoryPackable]
[MessagePackObject]
public partial class ConcreteTestMessage : ITestMessageInterface
{
	[Key(0)]
	public string Name { get; set; } = string.Empty;

	[Key(1)]
	public int Value { get; set; }

	[Key(2)]
	public string ExtraData { get; set; } = string.Empty;
}

/// <summary>
/// Base class for inheritance serialization tests.
/// </summary>
[MemoryPackable]
[MessagePackObject]
public partial class BaseTestMessage
{
	[Key(0)]
	public string Name { get; set; } = string.Empty;

	[Key(1)]
	public int Value { get; set; }
}

/// <summary>
/// Derived class for inheritance serialization tests.
/// </summary>
[MemoryPackable]
[MessagePackObject]
public partial class DerivedTestMessage : BaseTestMessage
{
	[Key(2)]
	public string DerivedProperty { get; set; } = string.Empty;
}
