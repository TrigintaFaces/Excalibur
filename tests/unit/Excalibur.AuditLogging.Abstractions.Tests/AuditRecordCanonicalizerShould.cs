// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.AuditLogging;

using Shouldly;

using Xunit;

namespace Excalibur.AuditLogging.Abstractions.Tests;

/// <summary>
/// Locks for <see cref="AuditRecordCanonicalizer"/>.
/// </summary>
/// <remarks>
/// The MAC protects the bytes it is computed over, not the field structure those bytes came from. So the two
/// properties asserted here are the whole security argument for the canonical form: it must be
/// <b>deterministic</b> (or a genuine record fails its own verification) and <b>injective on field
/// boundaries</b> (or two different records share a MAC and one can be substituted for the other without
/// touching the tag). Neither is visible from reading a hex dump; both are cheap to assert.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditRecordCanonicalizerShould
{
	/// <summary>
	/// The injectivity property, stated as its own counterexample. Naive concatenation renders both of these
	/// field sequences as <c>"abc"</c>; length prefixing is what separates them. An attacker who can choose
	/// field contents chooses exactly this collision.
	/// </summary>
	[Fact]
	public void DistinguishFieldSequencesThatConcatenateIdentically()
	{
		var left = AuditRecordCanonicalizer.Canonicalize("a", "bc");
		var right = AuditRecordCanonicalizer.Canonicalize("ab", "c");

		left.ShouldNotBe(right);
	}

	/// <summary>
	/// An absent field and a present-but-empty one are different facts about a record — "no actor was
	/// recorded" versus "the actor was recorded as blank" — and must not share a canonical form.
	/// </summary>
	[Fact]
	public void DistinguishAnAbsentFieldFromAnEmptyOne()
	{
		// Written as an explicit array: a bare Canonicalize(null) binds to the params array itself, not to a
		// single absent field, and would assert nothing about field markers.
		var absent = AuditRecordCanonicalizer.Canonicalize(new string?[] { null });
		var empty = AuditRecordCanonicalizer.Canonicalize(string.Empty);

		absent.ShouldNotBe(empty);
	}

	/// <summary>
	/// Field order is part of the record's meaning: an actor and a subject swapped is a different audit
	/// entry, and must not verify against the original's tag.
	/// </summary>
	[Fact]
	public void DistinguishFieldOrder()
	{
		var forward = AuditRecordCanonicalizer.Canonicalize("actor", "subject");
		var reversed = AuditRecordCanonicalizer.Canonicalize("subject", "actor");

		forward.ShouldNotBe(reversed);
	}

	/// <summary>
	/// The liveness arm, and it is not a formality: every arm above is satisfied by a canonicalizer that
	/// returned random bytes, which would also make every genuine audit record fail verification.
	/// </summary>
	[Fact]
	public void ProduceByteIdenticalOutputForTheSameFields()
	{
		var first = AuditRecordCanonicalizer.Canonicalize("actor", null, "subject", string.Empty);
		var second = AuditRecordCanonicalizer.Canonicalize("actor", null, "subject", string.Empty);

		first.ShouldBe(second);
	}

	[Fact]
	public void LeadWithTheCanonicalVersionByte()
	{
		var canonical = AuditRecordCanonicalizer.Canonicalize("actor");

		canonical[0].ShouldBe(AuditRecordCanonicalizer.CanonicalVersion);
	}

	/// <summary>
	/// A record with no integrity-covered fields still canonicalizes to the version byte alone, so the
	/// format is defined at its own boundary rather than at whatever an empty loop happens to produce.
	/// </summary>
	[Fact]
	public void CanonicalizeAnEmptyFieldSetToTheVersionByteAlone()
	{
		var canonical = AuditRecordCanonicalizer.Canonicalize();

		canonical.Length.ShouldBe(1);
		canonical[0].ShouldBe(AuditRecordCanonicalizer.CanonicalVersion);
	}

	/// <summary>
	/// The length prefix counts UTF-8 <em>bytes</em>, not UTF-16 chars. Any field a person can type into an
	/// audit record — an accented name, a CJK subject, an emoji — has more bytes than chars, so a
	/// char-count prefix would mis-frame the field and corrupt every following one.
	/// </summary>
	[Theory]
	[InlineData("café")]
	[InlineData("日本語")]
	[InlineData("a\U0001F600b")]
	public void LengthPrefixInUtf8Bytes_NotCharacters(string field)
	{
		var canonical = AuditRecordCanonicalizer.Canonicalize(field);

		var expectedByteCount = Encoding.UTF8.GetByteCount(field);
		expectedByteCount.ShouldNotBe(field.Length, "the case must actually exercise a multi-byte field");

		// version(1) + marker(1) + length(4) + payload
		canonical.Length.ShouldBe(1 + 1 + 4 + expectedByteCount);

		var declaredLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(canonical.AsSpan(2, 4));
		declaredLength.ShouldBe(expectedByteCount);

		Encoding.UTF8.GetString(canonical.AsSpan(6)).ShouldBe(field);
	}

	/// <summary>
	/// A multi-byte field followed by another field is where a char-count length prefix does its real
	/// damage: it shifts the boundary and silently reinterprets the next field's bytes.
	/// </summary>
	[Fact]
	public void KeepFieldBoundariesIntactAfterAMultiByteField()
	{
		var withMultiByte = AuditRecordCanonicalizer.Canonicalize("café", "subject");
		var withAscii = AuditRecordCanonicalizer.Canonicalize("cafe", "subject");

		withMultiByte.ShouldNotBe(withAscii);
		withMultiByte.Length.ShouldBe(withAscii.Length + 1);
	}

	[Fact]
	public void Reject_ANullFieldArray()
		=> Should.Throw<ArgumentNullException>(() => AuditRecordCanonicalizer.Canonicalize((string?[])null!));
}
