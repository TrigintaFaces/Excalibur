// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Tests.Tenancy;

/// <summary>
/// <see cref="ITenantContext.HasTenant"/> and the conversion every tenant-partitioned store performs must
/// answer the same question, for every value an ambient scope can carry.
/// </summary>
/// <remarks>
/// <para>
/// The documented pattern is <em>test, then convert</em>: check the context reports a tenant, then convert
/// it to the scope a store binds. That pattern is only safe while the two agree. Where they disagree the
/// caller is handed an exception from the branch the predicate vouched for — the worst place to fail,
/// because nothing about the call site suggests it can.
/// </para>
/// <para>
/// This asserts the <em>relationship between the two abstractions</em> rather than either one's predicate,
/// and that is the point. A test that pins <c>HasTenant</c> to a particular spelling can only know the
/// divergences its author already knew about: the whitespace gap was found by inspection, and the
/// over-length gap — no whitespace involved — was sitting beside it unnoticed. A bi-implication over an
/// adversarial value set RED-detects both, and the next one nobody has thought of yet.
/// </para>
/// <para>
/// Mutations that turn this RED: widening the predicate back to <c>IsNullOrEmpty</c> (the blank values go
/// RED), and removing the length guard from the ambient ingress (the over-length value goes RED). Neither
/// arm alone is sufficient, and neither is a restatement of the implementation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Tenancy")]
[Trait("Priority", "1")]
public sealed class AmbientTenantContextAgreesWithTheConversionShould
{
	/// <summary>
	/// Every value an ambient scope can carry, including the ones that are legitimate but easy to mistake
	/// for faults: the reserved sentinel is a storage encoding a store hands straight back on read, and a
	/// blank term resolves to the untenanted partition rather than naming a tenant.
	/// </summary>
	public static TheoryData<string?, string> AmbientTerms() => new()
	{
		{ null, "no scope established" },
		{ "", "the empty string" },
		{ " ", "a single space" },
		{ "\t", "a tab" },
		{ "   ", "several spaces" },
		{ TenantScope.UntenantedSentinel, "the reserved untenanted sentinel" },
		{ "tenant-a", "an ordinary tenant id" },
		{ new string('a', TenantId.MaxLength), "an id exactly at the maximum length" },
	};

	[Theory]
	[MemberData(nameof(AmbientTerms))]
	public void AgreeWithTheConversion_ForEveryValueAnAmbientScopeCanCarry(string? ambientTerm, string because)
	{
		var context = new AmbientTenantContext();

		using (TenantContextHolder.BeginScope(ambientTerm))
		{
			var reportedATenant = context.HasTenant;
			var conversionSucceeded = TryConvert(context);

			conversionSucceeded.ShouldBe(
				reportedATenant,
				$"HasTenant and the conversion must agree for {because}. They disagree here, so a caller "
				+ "following the documented test-then-convert pattern is handed an exception from the "
				+ "branch HasTenant told them was safe");
		}
	}

	[Fact]
	public void RefuseAnOverLengthTenantAtEstablishment_RatherThanInSomeStoreLater()
	{
		var overLength = new string('a', TenantId.MaxLength + 1);

		var thrown = Should.Throw<ArgumentException>(() => TenantContextHolder.BeginScope(overLength));

		thrown.ParamName.ShouldBe(
			"tenantId",
			"the refusal names the argument the caller passed, because the caller is the only one who still "
			+ "knows where the id came from");

		TenantContextHolder.Current.ShouldBeNull(
			"a refused scope establishes nothing — a partially-applied ambient tenant would outlive the "
			+ "throw and silently partition the rest of the flow");
	}

	private static bool TryConvert(ITenantContext context)
	{
		try
		{
			_ = TenantScope.FromContext(context);
			return true;
		}
		catch (TenantRequiredException)
		{
			return false;
		}
		catch (ArgumentException)
		{
			return false;
		}
	}
}
