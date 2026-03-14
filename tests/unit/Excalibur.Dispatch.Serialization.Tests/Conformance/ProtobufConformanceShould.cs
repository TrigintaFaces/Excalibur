// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization.Protobuf;
using Excalibur.Dispatch.Serialization.Tests.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// Sprint 623 A.1: Serializer conformance tests for <see cref="ProtobufSerializer"/>.
/// Uses the existing <see cref="TestMessage"/> IMessage implementation from the Protobuf test folder.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test project")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test project")]
public sealed class ProtobufConformanceShould : SerializerConformanceTestsBase
{
	protected override ISerializer CreateSerializer() => new ProtobufSerializer();

	protected override object CreateTestObject() => new TestMessage
	{
		Name = "conformance-test",
		Value = 42,
		IsActive = true,
	};

	protected override object CreateEmptyTestObject() => new TestMessage();

	protected override Type TestObjectType => typeof(TestMessage);

	protected override void AssertObjectsEqual(object expected, object actual)
	{
		var e = (TestMessage)expected;
		var a = (TestMessage)actual;
		a.Name.ShouldBe(e.Name);
		a.Value.ShouldBe(e.Value);
		a.IsActive.ShouldBe(e.IsActive);
	}

	protected override void SerializeTyped(ISerializer serializer, object value, System.Buffers.IBufferWriter<byte> bufferWriter)
		=> serializer.Serialize((TestMessage)value, bufferWriter);

	protected override object CreateLargeTestObject()
	{
		// Protobuf TestMessage has Name (string), Value (int), IsActive (bool).
		// Build a large string to exceed 1MB.
		var largeString = new string('x', 1_100_000);
		return new TestMessage
		{
			Name = largeString,
			Value = int.MaxValue,
			IsActive = true,
		};
	}
}
