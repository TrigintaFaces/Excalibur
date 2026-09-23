namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Pins the properties the framework's key composition depends on, using inputs that CONTAIN the
/// delimiter and the escape character.
/// </summary>
/// <remarks>
/// <para>
/// An injectivity arm built on ordinary identifiers — tenant "acme", id "123" — passes against a bare
/// interpolation and therefore certifies nothing. Every arm below is chosen so that it goes RED against
/// the raw composer it replaced; <see cref="Fail_against_the_raw_composer_it_replaced" /> demonstrates
/// that directly rather than leaving it as a claim.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SegmentedKeyShould
{
	// Tenant "acme" reaching for a record whose id starts with "eu:", against tenant "acme:eu" owning a
	// record plainly called "order-42". Joined with a bare separator both render acme:eu:order-42.
	private const string AttackerTenant = "acme";
	private const string AttackerSuppliedId = "eu:order-42";
	private const string VictimTenant = "acme:eu";
	private const string VictimId = "order-42";

	/// <summary>The bare join this type exists to replace, kept so the arms can be shown RED against it.</summary>
	private static string RawCompose(params string[] segments) => string.Join(':', segments);

	[Fact]
	public void Give_two_identities_different_keys_for_the_colliding_pair()
	{
		var attacker = SegmentedKey.Compose(AttackerTenant, AttackerSuppliedId);
		var victim = SegmentedKey.Compose(VictimTenant, VictimId);

		attacker.ShouldNotBe(
			victim,
			"two distinct identity tuples addressing one key is cross-tenant read, write, cache service "
			+ "and authorization through one ordinary string.");
	}

	/// <summary>
	/// The arms above are only worth anything if they can fail. This pins that the colliding pair really
	/// does collide under the composer being replaced, so a future edit that quietly reverts the escaping
	/// cannot leave the suite green.
	/// </summary>
	[Fact]
	public void Fail_against_the_raw_composer_it_replaced()
	{
		var attacker = RawCompose(AttackerTenant, AttackerSuppliedId);
		var victim = RawCompose(VictimTenant, VictimId);

		attacker.ShouldBe(
			victim,
			"if these no longer collide the fixture has drifted and every injectivity arm in this class "
			+ "has stopped testing anything.");
	}

	[Theory]
	[InlineData("a:b", "t")]
	[InlineData("a", "b:t")]
	[InlineData("100%", "x")]
	[InlineData("a%3Ab", "t")]
	[InlineData("", "x")]
	[InlineData("x", "")]
	public void Round_trip_every_delimiter_bearing_term_losslessly(string first, string second)
	{
		var parts = SegmentedKey.Split(SegmentedKey.Compose(first, second), 2);

		parts[0].ShouldBe(first);
		parts[1].ShouldBe(second);
	}

	/// <summary>
	/// A literal "%3A" in a term must survive as those four characters and must NOT be read back as a
	/// separator. This is what forces '%' to be escaped before ':' in one direction and after it in the
	/// other; swapping either order makes this arm RED.
	/// </summary>
	[Fact]
	public void Not_mistake_a_literal_escape_sequence_for_a_separator()
	{
		var composed = SegmentedKey.Compose("a%3Ab", "t");

		SegmentedKey.Split(composed, 2)[0].ShouldBe("a%3Ab");

		SegmentedKey.Compose("a:b", "t").ShouldNotBe(
			composed,
			"escaping ':' without escaping '%' first maps the distinct terms 'a:b' and 'a%3Ab' onto one "
			+ "key -- a collision introduced by the escaping itself.");
	}

	[Theory]
	[InlineData("acme")]
	[InlineData("tenant-1")]
	[InlineData("Order/2026/42")]
	[InlineData("user@example.com")]
	[InlineData("a b")]
	[InlineData("a+b")]
	[InlineData("ünf")]
	public void Leave_an_ordinary_term_byte_identical_so_no_stored_key_is_orphaned(string term)
	{
		SegmentedKey.Escape(term).ShouldBeSameAs(
			term,
			"a term carrying neither ':' nor '%' must encode to itself, and without allocating: this is "
			+ "what makes the encoding adoptable on already-persisted keys.");
	}

	/// <summary>
	/// Records why the BCL escaper was not used, as an executable fact rather than a comment.
	/// </summary>
	/// <remarks>
	/// <see cref="Uri.EscapeDataString(string)" /> is injective and would otherwise be the correct
	/// standard-library choice. It is rejected because it percent-encodes everything outside the RFC 3986
	/// unreserved set, so it is not the identity on ordinary identifiers — adopting it would rewrite the
	/// bytes of already-stored keys. If a future runtime ever makes this arm fail, the BCL escaper has
	/// become a drop-in and this type should be reconsidered.
	/// </remarks>
	[Theory]
	[InlineData("user@example.com")]
	[InlineData("a b")]
	[InlineData("a+b")]
	public void Preserve_terms_that_the_BCL_escaper_would_rewrite(string term)
	{
		Uri.EscapeDataString(term).ShouldNotBe(
			term,
			"this term is the reason Uri.EscapeDataString cannot be used: it rewrites bytes that are "
			+ "already stored.");

		SegmentedKey.Escape(term).ShouldBe(term);
	}

	/// <summary>
	/// The split must preserve empty segments. <c>StringSplitOptions.RemoveEmptyEntries</c> shifts the
	/// remaining terms left, so a key whose first term is empty would promote the second into its place
	/// and address a different record entirely — a defect wholly independent of escaping.
	/// </summary>
	[Fact]
	public void Preserve_an_empty_segment_rather_than_shifting_the_rest_left()
	{
		var parts = SegmentedKey.Split(SegmentedKey.Compose(string.Empty, "acme", "order-42"), 3);

		parts.Length.ShouldBe(3);
		parts[0].ShouldBe(string.Empty);
		parts[1].ShouldBe("acme", "the tenant must not be promoted into the empty term's position.");
		parts[2].ShouldBe("order-42");
	}

	[Fact]
	public void Reject_a_key_carrying_more_segments_than_expected()
	{
		// The count-limited overload of string.Split would fold the surplus into the final element, which
		// then still holds a raw separator and decodes to a term nobody wrote.
		Should.Throw<ArgumentException>(() => SegmentedKey.Split("a:b:c", 2));
	}

	[Fact]
	public void Reject_a_key_carrying_fewer_segments_than_expected() =>
		Should.Throw<ArgumentException>(() => SegmentedKey.Split("a:b", 3));

	[Fact]
	public void Reject_a_single_segment_composition() =>
		Should.Throw<ArgumentException>(() => SegmentedKey.Compose(["only-one"]));

	[Fact]
	public void Reject_a_null_segment_rather_than_rendering_it_as_a_term() =>
		Should.Throw<ArgumentException>(() => SegmentedKey.Compose(["a", null!, "c"]));

	/// <summary>
	/// The four-term shape used by the grant stores and the activity-group documents, exercised with the
	/// delimiter in each position in turn.
	/// </summary>
	[Theory]
	[InlineData("u:x", "t", "g", "q")]
	[InlineData("u", "t:x", "g", "q")]
	[InlineData("u", "t", "g:x", "q")]
	[InlineData("u", "t", "g", "q:x")]
	public void Keep_a_four_term_grant_key_injective_wherever_the_delimiter_lands(
		string userId,
		string tenantId,
		string grantType,
		string qualifier)
	{
		var shifted = SegmentedKey.Compose(userId, tenantId, grantType, qualifier);
		var plain = SegmentedKey.Compose("u", "t", "g", "q");

		shifted.ShouldNotBe(plain);
		SegmentedKey.Split(shifted, 4).ShouldBe([userId, tenantId, grantType, qualifier]);
	}
}
