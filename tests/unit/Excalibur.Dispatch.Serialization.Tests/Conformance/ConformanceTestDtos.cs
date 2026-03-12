// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using global::MemoryPack;

using global::MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// Plain POCO for SystemTextJson conformance tests.
/// </summary>
public sealed class JsonConformanceDto
{
	public string Name { get; set; } = string.Empty;
	public int Value { get; set; }
	public bool IsActive { get; set; }
	public List<string> Tags { get; set; } = [];
}

/// <summary>
/// MessagePack-attributed DTO for MessagePack conformance tests.
/// </summary>
[MessagePackObject]
public sealed class MsgPackConformanceDto
{
	[Key(0)]
	public string Name { get; set; } = string.Empty;

	[Key(1)]
	public int Value { get; set; }

	[Key(2)]
	public bool IsActive { get; set; }

	[Key(3)]
	public List<string> Tags { get; set; } = [];
}

/// <summary>
/// MemoryPack-attributed DTO for MemoryPack conformance tests.
/// </summary>
[MemoryPackable]
public partial class MemPackConformanceDto
{
	public string Name { get; set; } = string.Empty;
	public int Value { get; set; }
	public bool IsActive { get; set; }
	public List<string> Tags { get; set; } = [];
}
