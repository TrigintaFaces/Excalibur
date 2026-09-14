using Excalibur.Data;

namespace Excalibur.Data.Tests.Core;

/// <summary>
/// The two proofs the precedent commit used, because "the encoding is the identity on ordinary ids"
/// is an assertion until an arm pins it.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TenantScopedKeyShould
{
	// Tenant "acme" reaching for a record whose id starts with "eu:", against tenant "acme:eu" owning a
	// record plainly called "order-42". Joined with a bare separator both render t:acme:eu:order-42, and
	// the first tenant's erase destroys the second tenant's history.
	private const string AttackerTenant = "acme";
	private const string AttackerSuppliedId = "eu:order-42";
	private const string VictimTenant = "acme:eu";
	private const string VictimId = "order-42";

	[Fact]
	public void Give_two_tenants_different_keys_for_the_colliding_pair()
	{
		var attacker = TenantScopedKey.Compose(AttackerTenant, AttackerSuppliedId);
		var victim = TenantScopedKey.Compose(VictimTenant, VictimId);

		attacker.ShouldNotBe(
			victim,
			"one tenant addressing another tenant's key is cross-tenant read, write and irreversible "
			+ "erasure through one ordinary string.");
	}

	[Theory]
	[InlineData("acme", "order-42")]
	[InlineData("tenant-1", "Order/2026/42")]
	[InlineData("a", "b")]
	public void Leave_an_ordinary_key_byte_identical_so_no_stored_record_is_orphaned(
		string tenantId,
		string id)
	{
		// This is what makes the change migration-free, and it is the whole reason '%' is escaped first.
		// A term containing neither ':' nor '%' encodes to itself, so every key already written under
		// the bare join is reproduced exactly.
		var composed = TenantScopedKey.Compose(tenantId, id);

		composed.ShouldBe(
			$"t:{tenantId}:{id}",
			"an encoding that moved ordinary keys would orphan every stored record on upgrade.");
	}

	[Fact]
	public void Leave_an_ordinary_multi_term_key_byte_identical_too()
	{
		// The three-term shape the Cosmos, DynamoDb and Firestore event stores use. Each term is escaped
		// separately and the separators between them stay raw, so this is identity as well -- passing
		// the tail as one pre-joined term would escape the inner separator and move every key.
		TenantScopedKey.Compose("acme", "Order", "42").ShouldBe("t:acme:Order:42");
	}

	[Fact]
	public void Not_let_the_escaping_introduce_a_collision_of_its_own()
	{
		// Why '%' is escaped FIRST. Escaping only ':' maps the distinct terms "a:b" and "a%3Ab" onto one
		// key -- a collision created by the fix.
		TenantScopedKey.Compose("a:b", "x")
			.ShouldNotBe(TenantScopedKey.Compose("a%3Ab", "x"));
	}

	[Theory]
	[InlineData("acme")]
	[InlineData("a:b")]
	[InlineData("a%3Ab")]
	[InlineData("100%")]
	[InlineData("::%::")]
	public void Round_trip_any_term_through_escape_and_unescape(string term)
	{
		TenantScopedKey.UnescapeSegment(TenantScopedKey.EscapeSegment(term)).ShouldBe(term);
	}

	[Theory]
	[InlineData("acme")]
	[InlineData("a:b")]
	[InlineData("a%3Ab")]
	[InlineData("100%")]
	[InlineData("%:%:%")]
	[InlineData(":")]
	[InlineData("%")]
	[InlineData("")]
	public void Encode_identically_to_the_ordered_two_pass_form_it_replaced(string term)
	{
		// The single-pass encoder exists so no later edit can swap the two replacements and silently
		// re-introduce the collision. This pins that the REWRITE changed no output: the correctly
		// ordered two-pass form is the reference, and any divergence would move stored keys.
		var reference = term
			.Replace("%", "%25", StringComparison.Ordinal)
			.Replace(":", "%3A", StringComparison.Ordinal);

		TenantScopedKey.EscapeSegment(term).ShouldBe(reference);
	}

	[Theory]
	[InlineData("acme")]
	[InlineData("order-42")]
	[InlineData("Order/2026/42")]
	public void Return_the_same_instance_when_nothing_needs_escaping(string term)
	{
		// The identity case is the common one on a hot key path, and it allocates nothing. Also pins
		// that characters OTHER than the separator and the escape are left alone -- a general-purpose
		// percent-encoder would move "Order/2026/42" and orphan every key already stored under it.
		ReferenceEquals(TenantScopedKey.EscapeSegment(term), term).ShouldBeTrue();
	}

	[Fact]
	public void Keep_supporting_a_tenant_id_that_contains_the_separator()
	{
		// A shipped conformance arm exercises tenant "a:b" as valid and supported. Rejecting the
		// separator would split the framework's own definition of a valid tenant id -- accepted by the
		// inbox, refused by the event store.
		_ = Should.NotThrow(() => TenantScopedKey.Compose("a:b", "order-42"));
	}
}
