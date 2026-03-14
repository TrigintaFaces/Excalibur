// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization.MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// Sprint 623 A.1: Serializer conformance tests for <see cref="MessagePackSerializer"/>.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test project")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test project")]
public sealed class MessagePackConformanceShould : SerializerConformanceTestsBase
{
	protected override ISerializer CreateSerializer() => new MessagePackSerializer();

	protected override object CreateTestObject() => new MsgPackConformanceDto
	{
		Name = "conformance-test",
		Value = 42,
		IsActive = true,
		Tags = ["tag1", "tag2", "tag3"],
	};

	protected override object CreateEmptyTestObject() => new MsgPackConformanceDto();

	protected override Type TestObjectType => typeof(MsgPackConformanceDto);

	protected override void AssertObjectsEqual(object expected, object actual)
	{
		var e = (MsgPackConformanceDto)expected;
		var a = (MsgPackConformanceDto)actual;
		a.Name.ShouldBe(e.Name);
		a.Value.ShouldBe(e.Value);
		a.IsActive.ShouldBe(e.IsActive);
		a.Tags.ShouldBe(e.Tags);
	}

	protected override void SerializeTyped(ISerializer serializer, object value, System.Buffers.IBufferWriter<byte> bufferWriter)
		=> serializer.Serialize((MsgPackConformanceDto)value, bufferWriter);

	protected override object CreateLargeTestObject()
	{
		var largeString = new string('x', 500_000);
		return new MsgPackConformanceDto
		{
			Name = largeString,
			Value = int.MaxValue,
			IsActive = true,
			Tags = Enumerable.Range(0, 10_000).Select(i => $"tag-{i}-{largeString[..50]}").ToList(),
		};
	}
}
