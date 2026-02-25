// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Test message for Protobuf serialization tests.
/// Manually implemented to avoid proto compiler dependency in tests.
/// </summary>
public sealed class TestMessage : IMessage<TestMessage>
{
	private static readonly MessageParser<TestMessage> _parser = new(() => new TestMessage());

	public static MessageParser<TestMessage> Parser => _parser;

	public string Name { get; set; } = string.Empty;
	public int Value { get; set; }
	public bool IsActive { get; set; }

	[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	public MessageDescriptor Descriptor => throw new NotSupportedException("Test message does not require descriptor metadata");

	public int CalculateSize()
	{
		int size = 0;
		if (!string.IsNullOrEmpty(Name))
		{
			size += 1 + CodedOutputStream.ComputeStringSize(Name);
		}

		if (Value != 0)
		{
			size += 1 + CodedOutputStream.ComputeInt32Size(Value);
		}

		if (IsActive)
		{
			size += 1 + 1;
		}

		return size;
	}

	public void WriteTo(CodedOutputStream output)
	{
		if (!string.IsNullOrEmpty(Name))
		{
			output.WriteRawTag(10);
			output.WriteString(Name);
		}

		if (Value != 0)
		{
			output.WriteRawTag(16);
			output.WriteInt32(Value);
		}

		if (IsActive)
		{
			output.WriteRawTag(24);
			output.WriteBool(IsActive);
		}
	}

	public void MergeFrom(TestMessage other)
	{
		if (other == null)
		{
			return;
		}

		if (!string.IsNullOrEmpty(other.Name))
		{
			Name = other.Name;
		}

		if (other.Value != 0)
		{
			Value = other.Value;
		}

		if (other.IsActive)
		{
			IsActive = other.IsActive;
		}
	}

	public void MergeFrom(CodedInputStream input)
	{
		uint tag;
		while ((tag = input.ReadTag()) != 0)
		{
			switch (tag)
			{
				case 10:
					Name = input.ReadString();
					break;

				case 16:
					Value = input.ReadInt32();
					break;

				case 24:
					IsActive = input.ReadBool();
					break;

				default:
					input.SkipLastField();
					break;
			}
		}
	}

	public TestMessage Clone()
	{
		return new TestMessage
		{
			Name = Name,
			Value = Value,
			IsActive = IsActive,
		};
	}

	public bool Equals(TestMessage? other)
	{
		if (other == null)
		{
			return false;
		}

		return Name == other.Name && Value == other.Value && IsActive == other.IsActive;
	}

	public override bool Equals(object? obj)
	{
		return Equals(obj as TestMessage);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Name, Value, IsActive);
	}

	public override string ToString()
	{
		return $"{{ \"name\": \"{Name}\", \"value\": {Value}, \"isActive\": {(IsActive ? "true" : "false")} }}";
	}
}
