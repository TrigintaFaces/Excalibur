// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using global::Avro;
using global::Avro.Specific;

using Excalibur.Dispatch.Serialization.Avro;

namespace Excalibur.Dispatch.Serialization.Tests.Conformance;

/// <summary>
/// ye7zf4: applies the shared <see cref="SerializerConformanceTestsBase"/> round-trip / idempotency /
/// empty / large-payload / thread-safety / null-input contract to <see cref="AvroSerializer"/> — previously
/// exempt from the shared contract. Uses a hand-rolled <see cref="ISpecificRecord"/> fixture (no generated
/// Avro types needed), mirroring <c>AvroSchemaSkewLockShould</c>.
/// </summary>
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test project")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test project")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class AvroConformanceShould : SerializerConformanceTestsBase
{
	protected override ISerializer CreateSerializer() => new AvroSerializer();

	protected override object CreateTestObject() => new AvroConformanceRecord
	{
		Name = "conformance-test",
		Value = 42,
		IsActive = true,
	};

	protected override object CreateEmptyTestObject() => new AvroConformanceRecord();

	protected override Type TestObjectType => typeof(AvroConformanceRecord);

	protected override void AssertObjectsEqual(object expected, object actual)
	{
		var e = (AvroConformanceRecord)expected;
		var a = (AvroConformanceRecord)actual;
		a.Name.ShouldBe(e.Name);
		a.Value.ShouldBe(e.Value);
		a.IsActive.ShouldBe(e.IsActive);
	}

	protected override void SerializeTyped(ISerializer serializer, object value, System.Buffers.IBufferWriter<byte> bufferWriter)
		=> serializer.Serialize((AvroConformanceRecord)value, bufferWriter);

	protected override object CreateLargeTestObject() => new AvroConformanceRecord
	{
		// A > 1MB string in the Avro string field exceeds the 1MB large-payload floor.
		Name = new string('x', 1_100_000),
		Value = int.MaxValue,
		IsActive = true,
	};

	// ── Minimal hand-rolled ISpecificRecord fixture (no generated Avro types needed) ──
	private sealed class AvroConformanceRecord : ISpecificRecord
	{
		private static readonly RecordSchema RecordSchema = (RecordSchema)Schema.Parse(
			"{\"type\":\"record\",\"name\":\"AvroConformanceRecord\",\"namespace\":\"conformance\",\"fields\":[" +
			"{\"name\":\"name\",\"type\":\"string\"}," +
			"{\"name\":\"value\",\"type\":\"int\"}," +
			"{\"name\":\"isActive\",\"type\":\"boolean\"}]}");

		public string Name { get; set; } = string.Empty;

		public int Value { get; set; }

		public bool IsActive { get; set; }

		public Schema Schema => RecordSchema;

		public object Get(int fieldPos) => fieldPos switch
		{
			0 => Name,
			1 => Value,
			2 => IsActive,
			_ => throw new AvroRuntimeException("bad field pos"),
		};

		public void Put(int fieldPos, object fieldValue)
		{
			switch (fieldPos)
			{
				case 0:
					Name = (string)fieldValue;
					break;
				case 1:
					Value = (int)fieldValue;
					break;
				case 2:
					IsActive = (bool)fieldValue;
					break;
				default:
					throw new AvroRuntimeException("bad field pos");
			}
		}
	}
}
