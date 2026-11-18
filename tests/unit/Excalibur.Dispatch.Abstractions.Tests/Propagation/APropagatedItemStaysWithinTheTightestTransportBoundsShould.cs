// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Abstractions.Tests.Propagation;

/// <summary>
/// Binds the three refusing bounds a propagated item is held to: the key's size, the value's size, and
/// how many items one context may carry.
/// </summary>
/// <remarks>
/// <para>
/// The bounds are the intersection of what every shipped transport accepts, and the tightest of them
/// states all three in BYTES. So every arm here is byte-measured, and the two discriminating pairs are
/// built to have the SAME CHARACTER COUNT on both sides of the bound: one member is accepted and the
/// other refused while a character-counting implementation cannot tell them apart. A suite of ASCII
/// strings passes against a character-counting implementation too, and therefore measures nothing about
/// the unit it was written for.
/// </para>
/// <para>
/// Each bound is bound on BOTH sides. An arm that only proves a refusal is satisfied by an
/// implementation that refuses everything, which is the cheapest way to hold a bound and the most
/// expensive way to be wrong — a consumer whose legal value is rejected has lost the capability
/// entirely. So exactly-at-the-limit is asserted ACCEPTED beside every one-byte-over refusal.
/// </para>
/// <para>
/// Nothing is truncated. A shortened value reads downstream as a whole one and nothing there can tell,
/// so the refusal arms assert both that the call throws AND that the set is unchanged afterwards — an
/// implementation that quietly stored a cut-down value would satisfy neither.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class APropagatedItemStaysWithinTheTightestTransportBoundsShould
{
	// U+3042 is three bytes in UTF-8. Every multi-byte arm below is built from it so the arithmetic
	// between characters and bytes is visible in the test rather than hidden in an encoder.
	private const char ThreeByteChar = 'あ';

	private const string SafeKey = "zzboundsprobe";
	private const string SafeValue = "probe-value";

	[Fact]
	public void AcceptAKeyOfExactlyTheByteLimit()
	{
		var key = new string('k', MessageContextPropagationExtensions.MaxPropagatedKeyBytes);
		var context = new TestMessageContext();

		context.SetPropagatedItem(key, SafeValue);

		Encoding.UTF8.GetByteCount(key)
			.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedKeyBytes);
		context.GetPropagatedItem(key).ShouldBe(SafeValue);
	}

	[Fact]
	public void RefuseAKeyOneByteOverTheLimit()
	{
		var key = new string('k', MessageContextPropagationExtensions.MaxPropagatedKeyBytes + 1);
		var context = new TestMessageContext();

		var thrown = Should.Throw<ArgumentException>(() => context.SetPropagatedItem(key, SafeValue));

		thrown.ParamName.ShouldBe("key");
		context.GetPropagatedItems().ShouldBeEmpty();
	}

	[Fact]
	public void AcceptAMultiByteKeyOfExactlyTheByteLimit()
	{
		// 85 three-byte characters plus one ASCII character: 86 characters, 256 bytes. The refusal arm
		// below carries the SAME character count and one more byte, so the pair cannot both pass a
		// character-counting implementation -- this is the half it would wrongly accept.
		var key = new string(ThreeByteChar, 85) + "k";
		var context = new TestMessageContext();

		context.SetPropagatedItem(key, SafeValue);

		key.Length.ShouldBe(86);
		Encoding.UTF8.GetByteCount(key)
			.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedKeyBytes);
		context.GetPropagatedItem(key).ShouldBe(SafeValue);
	}

	[Fact]
	public void RefuseAMultiByteKeyUnderTheCharacterLimitButOverTheByteLimit()
	{
		// 86 three-byte characters: 258 bytes, and far under 256 CHARACTERS. A character-counting
		// implementation accepts this and the broker then rejects the whole message.
		var key = new string(ThreeByteChar, 86);
		var context = new TestMessageContext();

		var thrown = Should.Throw<ArgumentException>(() => context.SetPropagatedItem(key, SafeValue));

		key.Length.ShouldBeLessThan(MessageContextPropagationExtensions.MaxPropagatedKeyBytes);
		Encoding.UTF8.GetByteCount(key)
			.ShouldBeGreaterThan(MessageContextPropagationExtensions.MaxPropagatedKeyBytes);
		thrown.ParamName.ShouldBe("key");
		context.GetPropagatedItems().ShouldBeEmpty();
	}

	[Fact]
	public void AcceptAValueOfExactlyTheByteLimit()
	{
		var value = new string('v', MessageContextPropagationExtensions.MaxPropagatedValueBytes);
		var context = new TestMessageContext();

		context.SetPropagatedItem(SafeKey, value);

		Encoding.UTF8.GetByteCount(value)
			.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedValueBytes);
		context.GetPropagatedItem(SafeKey).ShouldBe(value);
	}

	[Fact]
	public void RefuseAValueOneByteOverTheLimit()
	{
		var value = new string('v', MessageContextPropagationExtensions.MaxPropagatedValueBytes + 1);
		var context = new TestMessageContext();

		var thrown = Should.Throw<ArgumentException>(() => context.SetPropagatedItem(SafeKey, value));

		thrown.ParamName.ShouldBe("value");
		context.GetPropagatedItems().ShouldBeEmpty();
	}

	[Fact]
	public void AcceptAMultiByteValueOfExactlyTheByteLimit()
	{
		// 341 three-byte characters plus one ASCII character: 342 characters, 1024 bytes -- exactly the
		// limit. The refusal arm below is ALSO 342 characters and is 1026 bytes. The two differ only in
		// bytes, so a character-counting implementation must get one of them wrong.
		var value = new string(ThreeByteChar, 341) + "v";
		var context = new TestMessageContext();

		context.SetPropagatedItem(SafeKey, value);

		value.Length.ShouldBe(342);
		Encoding.UTF8.GetByteCount(value)
			.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedValueBytes);
		context.GetPropagatedItem(SafeKey).ShouldBe(value);
	}

	[Fact]
	public void RefuseAMultiByteValueUnderTheCharacterLimitButOverTheByteLimit()
	{
		// 342 three-byte characters: 1026 bytes, and a third of the character limit. This is the arm
		// that discriminates -- it is the same character count as the accepted value above.
		var value = new string(ThreeByteChar, 342);
		var context = new TestMessageContext();

		var thrown = Should.Throw<ArgumentException>(() => context.SetPropagatedItem(SafeKey, value));

		value.Length.ShouldBeLessThan(MessageContextPropagationExtensions.MaxPropagatedValueBytes);
		Encoding.UTF8.GetByteCount(value)
			.ShouldBeGreaterThan(MessageContextPropagationExtensions.MaxPropagatedValueBytes);
		thrown.ParamName.ShouldBe("value");
		context.GetPropagatedItems().ShouldBeEmpty();
	}

	[Fact]
	public void StoreAValueAtTheLimitByteForByteWithoutShorteningOrReEncodingIt()
	{
		// A value at the limit is legal, so it must come back out of the set identical. An
		// implementation that trimmed, normalised or re-encoded on the way in would read downstream as
		// the consumer's own value and nothing there could tell.
		var value = new string(ThreeByteChar, 340) + "ab";
		var context = new TestMessageContext();

		context.SetPropagatedItem(SafeKey, value);

		var stored = context.GetPropagatedItems()[SafeKey];
		stored.ShouldBe(value);
		stored.Length.ShouldBe(value.Length);
		Encoding.UTF8.GetByteCount(stored).ShouldBe(Encoding.UTF8.GetByteCount(value));
		Encoding.UTF8.GetBytes(stored).ShouldBe(Encoding.UTF8.GetBytes(value));
	}

	[Fact]
	public void AcceptTheItemThatBringsTheContextExactlyToTheItemLimit()
	{
		// Liveness for the count bound. A context that refuses its hundredth item satisfies every
		// refusal arm below while costing the consumer a legal item they are entitled to.
		var context = FillToItemLimit();

		context.GetPropagatedItems().Count.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedItems);
		context.GetPropagatedItem(KeyAt(MessageContextPropagationExtensions.MaxPropagatedItems - 1))
			.ShouldBe(SafeValue);
	}

	[Fact]
	public void RefuseANewKeyOnceTheContextCarriesTheItemLimit()
	{
		var context = FillToItemLimit();

		var thrown = Should.Throw<ArgumentException>(
			() => context.SetPropagatedItem("zzoneovertheline", SafeValue));

		thrown.ParamName.ShouldBe("key");
		context.GetPropagatedItems().Count.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedItems);
		context.GetPropagatedItem("zzoneovertheline").ShouldBeNull();
	}

	[Fact]
	public void AllowOverwritingAnExistingKeyAtTheItemLimit()
	{
		// Only a NEW key grows the set. A consumer correcting a value they already set is not changing
		// the count, so refusing them would be refusing a write that cannot breach the bound.
		var context = FillToItemLimit();
		var existing = KeyAt(0);

		context.SetPropagatedItem(existing, "corrected");

		context.GetPropagatedItem(existing).ShouldBe("corrected");
		context.GetPropagatedItems().Count.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedItems);
	}

	[Fact]
	public void StillRefuseANewKeyAfterAnOverwriteAtTheItemLimit()
	{
		// The other half of the exemption: an overwrite must not be read as having freed a slot.
		var context = FillToItemLimit();
		context.SetPropagatedItem(KeyAt(0), "corrected");

		_ = Should.Throw<ArgumentException>(() => context.SetPropagatedItem("zzafteroverwrite", SafeValue));

		context.GetPropagatedItems().Count.ShouldBe(MessageContextPropagationExtensions.MaxPropagatedItems);
	}

	[Fact]
	public void StillApplyTheSizeBoundsToAnOverwriteAtTheItemLimit()
	{
		// The count exemption is for the COUNT. An oversized value is oversized whether it is a new key
		// or a correction, because the broker sizes the header either way.
		var context = FillToItemLimit();
		var oversized = new string(ThreeByteChar, 342);

		var thrown = Should.Throw<ArgumentException>(() => context.SetPropagatedItem(KeyAt(0), oversized));

		thrown.ParamName.ShouldBe("value");
		context.GetPropagatedItem(KeyAt(0)).ShouldBe(SafeValue);
	}

	[Fact]
	public void LeaveAnEarlierValueIntactWhenAnOversizedOverwriteIsRefused()
	{
		// An implementation that removed the key before validating the replacement would destroy a value
		// the consumer had already set, on a call that threw.
		var context = new TestMessageContext();
		context.SetPropagatedItem(SafeKey, SafeValue);

		_ = Should.Throw<ArgumentException>(
			() => context.SetPropagatedItem(
				SafeKey,
				new string('v', MessageContextPropagationExtensions.MaxPropagatedValueBytes + 1)));

		context.GetPropagatedItem(SafeKey).ShouldBe(SafeValue);
		context.GetPropagatedItems().Count.ShouldBe(1);
	}

	[Fact]
	public void LeaveAContextUsableAfterARefusal()
	{
		// A refusal is loud and recoverable: it must not poison the context. A consumer who catches the
		// exception and supplies a shorter value gets their item.
		var context = new TestMessageContext();

		_ = Should.Throw<ArgumentException>(
			() => context.SetPropagatedItem(SafeKey, new string(ThreeByteChar, 342)));
		context.SetPropagatedItem(SafeKey, SafeValue);

		context.GetPropagatedItem(SafeKey).ShouldBe(SafeValue);
	}

	private static string KeyAt(int index) => $"zzcount{index:D3}";

	private static TestMessageContext FillToItemLimit()
	{
		var context = new TestMessageContext();
		for (var i = 0; i < MessageContextPropagationExtensions.MaxPropagatedItems; i++)
		{
			context.SetPropagatedItem(KeyAt(i), SafeValue);
		}

		return context;
	}
}
