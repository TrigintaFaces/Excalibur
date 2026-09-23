// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization.MessagePack;

using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// Sprint 623 A.1: Serializer conformance tests for <see cref="MessagePackSerializer"/>.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test project")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test project")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MessagePackConformanceShould : SerializerConformanceTestKit
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

	// ---- Arm wiring. The kit ships without a test framework, so each arm is forwarded here.
	// ConformanceSuite_ShouldWireEveryArm below fails if any kit arm is missing a forwarder.

	/// <summary>Runs the kit's <c>Name_ShouldReturnNonNullNonEmptyString</c> arm.</summary>
	[Fact]
	public void Name_ShouldReturnNonNullNonEmptyString_Test() => Name_ShouldReturnNonNullNonEmptyString();

	/// <summary>Runs the kit's <c>Version_ShouldReturnNonNullNonEmptyString</c> arm.</summary>
	[Fact]
	public void Version_ShouldReturnNonNullNonEmptyString_Test() => Version_ShouldReturnNonNullNonEmptyString();

	/// <summary>Runs the kit's <c>ContentType_ShouldReturnValidIanaMediaType</c> arm.</summary>
	[Fact]
	public void ContentType_ShouldReturnValidIanaMediaType_Test() => ContentType_ShouldReturnValidIanaMediaType();

	/// <summary>Runs the kit's <c>Name_ShouldReturnConsistentValue</c> arm.</summary>
	[Fact]
	public void Name_ShouldReturnConsistentValue_Test() => Name_ShouldReturnConsistentValue();

	/// <summary>Runs the kit's <c>RoundTrip_ObjectApi_ShouldProduceIdenticalObject</c> arm.</summary>
	[Fact]
	public void RoundTrip_ObjectApi_ShouldProduceIdenticalObject_Test() => RoundTrip_ObjectApi_ShouldProduceIdenticalObject();

	/// <summary>Runs the kit's <c>RoundTrip_ObjectApi_MultipleRoundTrips_ShouldBeIdempotent</c> arm.</summary>
	[Fact]
	public void RoundTrip_ObjectApi_MultipleRoundTrips_ShouldBeIdempotent_Test() => RoundTrip_ObjectApi_MultipleRoundTrips_ShouldBeIdempotent();

	/// <summary>Runs the kit's <c>RoundTrip_EmptyObject_ShouldProduceIdenticalObject</c> arm.</summary>
	[Fact]
	public void RoundTrip_EmptyObject_ShouldProduceIdenticalObject_Test() => RoundTrip_EmptyObject_ShouldProduceIdenticalObject();

	/// <summary>Runs the kit's <c>RoundTrip_LargePayload_ShouldWorkWithoutError</c> arm.</summary>
	[Fact]
	public void RoundTrip_LargePayload_ShouldWorkWithoutError_Test() => RoundTrip_LargePayload_ShouldWorkWithoutError();

	/// <summary>Runs the kit's <c>ConcurrentSerializeDeserialize_ShouldBeThreadSafe</c> arm.</summary>
	[Fact]
	public void ConcurrentSerializeDeserialize_ShouldBeThreadSafe_Test() => ConcurrentSerializeDeserialize_ShouldBeThreadSafe();

	/// <summary>Runs the kit's <c>ConcurrentSerialize_BufferWriter_ShouldBeThreadSafe</c> arm.</summary>
	[Fact]
	public void ConcurrentSerialize_BufferWriter_ShouldBeThreadSafe_Test() => ConcurrentSerialize_BufferWriter_ShouldBeThreadSafe();

	/// <summary>Runs the kit's <c>RoundTrip_GenericApi_ShouldProduceIdenticalObject</c> arm.</summary>
	[Fact]
	public void RoundTrip_GenericApi_ShouldProduceIdenticalObject_Test() => RoundTrip_GenericApi_ShouldProduceIdenticalObject();

	/// <summary>Runs the kit's <c>SerializeObject_NullValue_ShouldThrow</c> arm.</summary>
	[Fact]
	public void SerializeObject_NullValue_ShouldThrow_Test() => SerializeObject_NullValue_ShouldThrow();

	/// <summary>Runs the kit's <c>SerializeObject_NullType_ShouldThrow</c> arm.</summary>
	[Fact]
	public void SerializeObject_NullType_ShouldThrow_Test() => SerializeObject_NullType_ShouldThrow();

	/// <summary>Runs the kit's <c>DeserializeObject_NullType_ShouldThrow</c> arm.</summary>
	[Fact]
	public void DeserializeObject_NullType_ShouldThrow_Test() => DeserializeObject_NullType_ShouldThrow();

	/// <summary>Runs the kit's <c>Serialize_NullBufferWriter_ShouldThrow</c> arm.</summary>
	[Fact]
	public void Serialize_NullBufferWriter_ShouldThrow_Test() => Serialize_NullBufferWriter_ShouldThrow();

	/// <summary>Runs the kit's <c>Serialize_NullValue_GenericApi_ShouldThrowArgumentNullException</c> arm.</summary>
	[Fact]
	public void Serialize_NullValue_GenericApi_ShouldThrowArgumentNullException_Test() => Serialize_NullValue_GenericApi_ShouldThrowArgumentNullException();

	/// <summary>Fails if any kit arm has no forwarder above.</summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

}
