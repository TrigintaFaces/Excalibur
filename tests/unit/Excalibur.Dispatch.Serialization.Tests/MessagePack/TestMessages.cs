// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Test message type for MessagePack serialization tests.
/// </summary>
[MessagePackObject]
public sealed class TestMessage
{
	/// <summary>
	/// Gets or sets the test ID.
	/// </summary>
	[Key(0)]
	public int Id { get; set; }

	/// <summary>
	/// Gets or sets the test name.
	/// </summary>
	[Key(1)]
	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test message type for pluggable serializer tests.
/// </summary>
[MessagePackObject]
public sealed class TestPluggableMessage
{
	/// <summary>
	/// Gets or sets the test value.
	/// </summary>
	[Key(0)]
	public int Value { get; set; }

	/// <summary>
	/// Gets or sets the test text.
	/// </summary>
	[Key(1)]
	public string Text { get; set; } = string.Empty;
}
