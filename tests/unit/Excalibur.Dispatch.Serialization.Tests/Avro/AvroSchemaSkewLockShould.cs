// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using global::Avro;
using global::Avro.Specific;

using Excalibur.Dispatch.Serialization.Avro;

namespace Excalibur.Dispatch.Serialization.Tests.Avro;

/// <summary>
/// Author≠impl regression lock for S852 · <c>a9adcc</c> (AC-F4 — Avro fail-closed, no silent corruption).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (FrontendDeveloper) against committed mainline (the
/// fail-closed floor, commit <c>cfff44a29</c>). <see cref="AvroSerializer"/> frames every payload with the
/// Avro single-object-encoding header (<c>0xC3 0x01</c> + 8-byte little-endian
/// <c>SchemaNormalization.ParsingFingerprint64</c> of the writer schema); on read it validates the
/// fingerprint and throws <see cref="SchemaMismatchException"/> on a writer/reader skew or a missing header,
/// rather than positionally mis-decoding into garbage.
/// </para>
/// <para>
/// <b>RED mutant (non-vacuity):</b> against pre-<c>cfff44a29</c> mainline (no header +
/// <c>SpecificDatumReader(reader, reader)</c> with no fingerprint validation), the skew facts do NOT throw
/// <see cref="SchemaMismatchException"/> — they silently mis-decode or throw a generic
/// <c>SerializationException</c>. Both code paths (<see cref="AvroSerializer.DeserializeObject"/> and the
/// generic <see cref="AvroSerializer.Deserialize{T}"/>) are covered (F-5 both-code-paths).
/// </para>
/// </remarks>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class AvroSchemaSkewLockShould
{
	// Two record schemas that differ by one field ⇒ different ParsingFingerprint64 ⇒ a writer/reader skew.
	private const string V1Json =
		"{\"type\":\"record\",\"name\":\"Foo\",\"namespace\":\"x\",\"fields\":[{\"name\":\"a\",\"type\":\"int\"}]}";
	private const string V2Json =
		"{\"type\":\"record\",\"name\":\"Foo\",\"namespace\":\"x\",\"fields\":[{\"name\":\"a\",\"type\":\"int\"},{\"name\":\"b\",\"type\":\"int\",\"default\":0}]}";

	private static readonly RecordSchema SchemaV1 = (RecordSchema)Schema.Parse(V1Json);
	private static readonly RecordSchema SchemaV2 = (RecordSchema)Schema.Parse(V2Json);

	[Fact]
	public void FrameWithSingleObjectHeader_AndRoundTripSameSchema()
	{
		var serializer = new AvroSerializer();

		var bytes = serializer.SerializeObject(new FooV1 { A = 42 }, typeof(FooV1));

		// The single-object-encoding marker proves the writer-schema fingerprint is framed (not a bare payload).
		bytes.Length.ShouldBeGreaterThan(2);
		bytes[0].ShouldBe((byte)0xC3);
		bytes[1].ShouldBe((byte)0x01);

		var roundTripped = (FooV1)serializer.DeserializeObject(bytes, typeof(FooV1));
		roundTripped.A.ShouldBe(42);
	}

	[Fact]
	public void Throw_OnWriterReaderSkew_DeserializeObjectPath()
	{
		var serializer = new AvroSerializer();
		var bytes = serializer.SerializeObject(new FooV1 { A = 42 }, typeof(FooV1));

		// Reader schema (FooV2) differs from the framed writer schema (FooV1) ⇒ fail-closed, never positional-decode.
		_ = Should.Throw<SchemaMismatchException>(() => serializer.DeserializeObject(bytes, typeof(FooV2)));
	}

	[Fact]
	public void Throw_OnWriterReaderSkew_GenericDeserializePath()
	{
		var serializer = new AvroSerializer();
		var bytes = serializer.SerializeObject(new FooV1 { A = 42 }, typeof(FooV1));

		// Same skew via the generic Deserialize<T> entry point (F-5 both-code-paths).
		_ = Should.Throw<SchemaMismatchException>(() => serializer.Deserialize<FooV2>(bytes));
	}

	[Fact]
	public void Throw_OnMissingHeader_UnframedBytes()
	{
		var serializer = new AvroSerializer();

		// Bytes that lack the single-object-encoding header must be rejected, not positionally decoded.
		_ = Should.Throw<SchemaMismatchException>(() => serializer.DeserializeObject(new byte[] { 1, 2, 3 }, typeof(FooV1)));
		_ = Should.Throw<SchemaMismatchException>(() => serializer.Deserialize<FooV1>(new byte[] { 1, 2, 3 }));
	}

	// ── Minimal hand-rolled ISpecificRecord fixtures (no generated Avro types needed) ──

	private sealed class FooV1 : ISpecificRecord
	{
		public int A { get; set; }

		public Schema Schema => SchemaV1;

		public object Get(int fieldPos) => fieldPos == 0 ? A : throw new AvroRuntimeException("bad field pos");

		public void Put(int fieldPos, object fieldValue)
		{
			if (fieldPos == 0)
			{
				A = (int)fieldValue;
			}
		}
	}

	private sealed class FooV2 : ISpecificRecord
	{
		public int A { get; set; }
		public int B { get; set; }

		public Schema Schema => SchemaV2;

		public object Get(int fieldPos) => fieldPos switch
		{
			0 => A,
			1 => B,
			_ => throw new AvroRuntimeException("bad field pos"),
		};

		public void Put(int fieldPos, object fieldValue)
		{
			switch (fieldPos)
			{
				case 0:
					A = (int)fieldValue;
					break;
				case 1:
					B = (int)fieldValue;
					break;
				default:
					throw new AvroRuntimeException("bad field pos");
			}
		}
	}
}
