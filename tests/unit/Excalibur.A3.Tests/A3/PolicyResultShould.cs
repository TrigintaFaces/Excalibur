using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3;

/// <summary>
/// Unit tests for the <see cref="PolicyResult"/> value type.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms assert the DERIVATION of <see cref="PolicyResult.IsAuthorized"/>, which the previous
/// version could not.</b> It used to be a third independently settable flag, so every arm here set it by
/// hand and none of them bound it to anything: one arm asserted
/// <c>{ IsAuthorized = true } != { IsAuthorized = false }</c>, which is true of any two unrelated booleans
/// and said nothing about authorization, and two more set <c>IsAuthorized = true</c> alongside a grant
/// flag — demonstrating in a test the inconsistent state the type now makes unrepresentable. Authorization
/// is the existence of either grant; that is now computed, so these arms bind the rule rather than
/// restating an assignment.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class PolicyResultShould : UnitTestBase
{
	[Fact]
	public void TreatAResultWithNoGrantsAsUnauthorized()
	{
		var result = new PolicyResult();

		result.IsAuthorized.ShouldBeFalse();
		result.HasActivityGrant.ShouldBeFalse();
		result.HasResourceGrant.ShouldBeFalse();
	}

	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, true, true)]
	public void DeriveAuthorizationFromTheExistenceOfEitherGrant(
		bool hasActivityGrant, bool hasResourceGrant, bool expectedAuthorized)
	{
		// The whole contract over its whole input space -- four rows is exhaustive for two booleans, so
		// this is not a sample. A stored IsAuthorized could disagree with its own grants on any of these
		// rows and nothing would have rejected it.
		var result = new PolicyResult
		{
			HasActivityGrant = hasActivityGrant,
			HasResourceGrant = hasResourceGrant,
		};

		result.IsAuthorized.ShouldBe(expectedAuthorized);
	}

	[Fact]
	public void CarryTheTwoGrantFlagsIndependently()
	{
		var activityOnly = new PolicyResult { HasActivityGrant = true };
		var resourceOnly = new PolicyResult { HasResourceGrant = true };

		activityOnly.HasActivityGrant.ShouldBeTrue();
		activityOnly.HasResourceGrant.ShouldBeFalse();
		resourceOnly.HasResourceGrant.ShouldBeTrue();
		resourceOnly.HasActivityGrant.ShouldBeFalse();
	}

	[Fact]
	public void CompareEqualWhenBothGrantFlagsMatch()
	{
		var first = new PolicyResult { HasActivityGrant = true, HasResourceGrant = false };
		var second = new PolicyResult { HasActivityGrant = true, HasResourceGrant = false };

		first.ShouldBe(second);
		(first == second).ShouldBeTrue();
		(first != second).ShouldBeFalse();
		first.GetHashCode().ShouldBe(second.GetHashCode());
	}

	[Fact]
	public void CompareUnequalWhenAGrantFlagDiffers()
	{
		var granted = new PolicyResult { HasActivityGrant = true };
		var denied = new PolicyResult { HasActivityGrant = false };

		granted.ShouldNotBe(denied);
		(granted != denied).ShouldBeTrue();
	}

	[Fact]
	public void NotCompareEqualToSomethingThatIsNotAPolicyResult()
	{
		// Equality is hand-written rather than synthesized, so the object overload is code someone can get
		// wrong -- an `obj is PolicyResult` that was an unchecked cast would throw here instead.
		var result = new PolicyResult { HasActivityGrant = true };

		result.Equals("not a policy result").ShouldBeFalse();
		result.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void RecomputeAuthorizationOnACopyThatChangesAGrant()
	{
		// The derivation must survive `with`, which is where a cached or stored IsAuthorized would go
		// stale: the copy changes a grant, and the authorization verdict has to follow it.
		var denied = new PolicyResult();

		var granted = denied with { HasResourceGrant = true };

		denied.IsAuthorized.ShouldBeFalse();
		granted.IsAuthorized.ShouldBeTrue("a copy that adds a grant must become authorized");
	}

	[Fact]
	public void BeAValueTypeSoAnAuthorizationCheckNeedNotAllocate()
	{
		// Pins the decision the zero-allocation arms in AuthorizationPolicyWildcardPerformanceShould
		// depend on. This is a shape assertion and deliberately the weakest arm in the file -- the
		// BEHAVIOUR is bound there, by measuring bytes. It is here so that turning this back into a
		// reference type fails with a message naming the reason, rather than as an unexplained allocation
		// regression in another project.
		typeof(PolicyResult).IsValueType.ShouldBeTrue(
			"PolicyResult is returned by every HasGrant call on the dispatch path; as a reference type it "
			+ "cost one 24-byte allocation per authorization check, removed only when the JIT reached "
			+ "tier-1 and proved it did not escape");
	}
}
