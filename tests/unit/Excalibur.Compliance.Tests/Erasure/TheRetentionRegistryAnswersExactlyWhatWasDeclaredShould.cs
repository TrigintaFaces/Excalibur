// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The declared-retention lookup itself, against the real registry and the real startup validation.
/// </summary>
/// <remarks>
/// <para>
/// The unit is the aggregate type, WHOLE. An obligation to keep a record attaches to the record, so every
/// data subject named inside a retained type is covered by it — a statute requiring sales records to be
/// kept does not require the buyer and permit deleting the salesperson, because a partly-erased record has
/// no evidentiary value and that was the whole reason for keeping it.
/// </para>
/// <para>
/// <b>The consequence of getting the match wrong is not a missed lookup.</b> A declaration that does not
/// match the type the event store records is a legally-required record destroyed, silently, with a
/// certificate attesting a clean completion — so the matching rule is a guarantee rather than a detail.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TheRetentionRegistryAnswersExactlyWhatWasDeclaredShould
{
	private const string SalesRecord = "SalesRecord";

	// An attribute argument must be a compile-time constant, so the framework sentinel (a static
	// readonly field) cannot be written into [InlineData]. Pinned against the real one by its own arm.
	private const string UntenantedLiteral = "__untenanted__";

	/// <summary>SAFETY. A declared type is found, carrying everything it was declared with.</summary>
	[Fact]
	public void Find_a_declared_type()
	{
		var found = Registry(Valid(SalesRecord)).TryGetRetention(null, SalesRecord, out var retention);

		found.ShouldBeTrue();
		retention.ShouldNotBeNull();
		retention!.Basis.ShouldBe(LegalHoldBasis.LegalObligation);
		retention.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 6));
	}

	/// <summary>LIVENESS. A type nobody declared is not retained.</summary>
	[Fact]
	public void Not_find_a_type_nobody_declared()
	{
		Registry(Valid(SalesRecord)).TryGetRetention(null, "Customer", out _).ShouldBeFalse();
	}

	/// <summary>SAFETY. A declaration belonging to another tenant does not match.</summary>
	/// <remarks>
	/// RED input: drop the tenant from the lookup key. One tenant then inherits another's statute, which is
	/// over-retention — the Article 17 breach in the direction the mandatory period exists to prevent.
	/// </remarks>
	[Fact]
	public void Not_match_a_declaration_belonging_to_another_tenant()
	{
		var registry = Registry(Valid(SalesRecord) with { TenantId = "tenant-a" });

		registry.TryGetRetention("tenant-b", SalesRecord, out _).ShouldBeFalse();
		registry.TryGetRetention("tenant-a", SalesRecord, out _).ShouldBeTrue(
			"and the declaring tenant's own subjects must still be protected, or the arm above is "
			+ "satisfied by a registry that matches nobody");
	}

	/// <summary>
	/// SAFETY. Matching is ordinal, so a declaration in the wrong case is NOT a retention.
	/// </summary>
	/// <remarks>
	/// Ordinal is the right rule — a culture-sensitive comparison would make the outcome depend on the
	/// server's locale — but a consumer must know it, because the failure is a destroyed record rather than
	/// a lookup miss. This arm exists so the rule cannot be relaxed silently.
	/// </remarks>
	[Fact]
	public void Not_match_a_declaration_whose_case_differs_from_the_stored_type()
	{
		Registry(Valid("salesrecord")).TryGetRetention(null, SalesRecord, out _)
			.ShouldBeFalse(
				"the declaration must name the type exactly as the event store records it; anything else "
				+ "leaves the record unprotected while looking declared");
	}

	/// <summary>
	/// LIVENESS. The lookup is total: null, empty and whitespace answer "not retained" rather than throw.
	/// </summary>
	/// <remarks>
	/// It runs on the path that decides whether to destroy a record. An exception here turns an unknown
	/// aggregate type into a failed erasure rather than an erased one, which is the wrong direction and is
	/// not what the Try pattern's name promises.
	/// </remarks>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Answer_not_retained_for_a_blank_type_without_throwing(string? aggregateType)
	{
		var registry = Registry(Valid(SalesRecord));

		Should.NotThrow(() => registry.TryGetRetention(null, aggregateType, out _)).ShouldBeFalse();
	}

	/// <summary>SAFETY. Declared and the lookup agree in both directions.</summary>
	[Fact]
	public void Agree_with_its_own_declared_collection()
	{
		var registry = Registry(Valid(SalesRecord), Valid("Warranty"));

		registry.Declared.Count.ShouldBe(2);
		foreach (var declared in registry.Declared)
		{
			registry.TryGetRetention(declared.TenantId, declared.AggregateType, out var found)
				.ShouldBeTrue();
			found.ShouldBe(declared);
		}
	}

	/// <summary>SAFETY. An unset lawful basis is refused at startup rather than signed onto a certificate.</summary>
	/// <remarks>
	/// <c>required</c> cannot reach this: it binds the C# compiler, and a reflection binder, a deserializer
	/// or an out-of-range cast all produce a value this type never assigned. The default is
	/// Article 17(3)(a), freedom of expression — which would appear, on a signed erasure record, as the
	/// ground under which a tax record was kept. RED input: drop the <c>Enum.IsDefined</c> check.
	/// </remarks>
	[Fact]
	public void Refuse_a_declaration_whose_lawful_basis_is_not_a_defined_ground()
	{
		var declaration = Valid(SalesRecord) with { Basis = (LegalHoldBasis)int.MaxValue };

		Validate(declaration).Failed.ShouldBeTrue();
		Validate(declaration).FailureMessage.ShouldContain(nameof(ErasureRetention.Basis));
	}

	/// <summary>SAFETY. A declaration that names no aggregate type is refused at startup.</summary>
	[Fact]
	public void Refuse_a_declaration_that_names_no_aggregate_type()
	{
		Validate(Valid(SalesRecord) with { AggregateType = "  " }).Failed.ShouldBeTrue();
	}

	/// <summary>
	/// SAFETY. A declaration whose tenant is BLANK is refused at startup, so the normalisation downstream
	/// never has to decide what a declaration that says nothing means.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm exists because two protections were each other's coverage argument and neither could fail.
	/// Normalisation is safe to omit only if a blank tenant cannot reach the registry; a blank tenant is
	/// harmless only because normalisation would collapse it. Removing BOTH at once left 588 tests green.
	/// Pinning the refusal here makes the normalisation's "unreachable through the supported path" argument
	/// true AND checkable, rather than circular.
	/// </para>
	/// <para>
	/// The consumer case is not hypothetical: an empty string is the natural thing to write for a
	/// single-tenant deployment, and it is what the previously-nullable member encouraged. Such a
	/// declaration would match no erasure, so the retained aggregate is tombstoned — and its widened
	/// handle is now destroyed with it.
	/// </para>
	/// <para>
	/// RED input: drop the blank check from the validator. These declarations reach the registry, where
	/// normalisation silently turns them into the untenanted sentinel — which is the right answer for a
	/// single-tenant host and the WRONG one for a multi-tenant host that meant to name a tenant and failed.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Refuse_a_declaration_whose_tenant_is_blank(string tenantId)
	{
		var result = Validate(Valid(SalesRecord) with { TenantId = tenantId });

		result.Failed.ShouldBeTrue(
			"a blank tenant states neither a tenant nor untenantedness, and a reader cannot tell which was "
			+ "meant");
		result.FailureMessage.ShouldContain(
			nameof(ErasureRetention.TenantId),
			Case.Sensitive,
			"the message has to name the member, or a consumer cannot find what to fix at startup");
	}

	/// <summary>
	/// SAFETY, and it is the arm that makes declaration-side normalisation CHECKABLE rather than merely
	/// unreachable. A hand-built options instance carrying a blank tenant still resolves.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The validator refuses a blank tenant, so nothing blank reaches the registry through the supported
	/// path — which is exactly why removing declaration-side normalisation on its own leaves every arm
	/// green. That is a protection with no way to fail, and this arm gives it one by using the path the
	/// registry's own construction contract says must stay total: an options instance built by hand, which
	/// no validator has seen.
	/// </para>
	/// <para>
	/// Building the registry directly is deliberate here and wrong everywhere else in this file. Every other
	/// arm goes through the real registration so it binds what a consumer gets; this one has to bypass it,
	/// because the state under test is the one the registration refuses to produce.
	/// </para>
	/// <para>
	/// RED input: key the declaration on its tenant as written. The blank is stored as an empty string, the
	/// lookup normalises to the sentinel, and the two no longer meet — so the retention silently stops
	/// applying to the deployment that declared it.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Normalise_a_blank_tenant_on_a_hand_built_options_instance(string tenantId)
	{
		var options = new AggregateRetentionOptions();
		options.Aggregates.Add(Valid(SalesRecord) with { TenantId = tenantId });

		var registry = new ErasureRetentionRegistry(Options.Create(options));

		registry.TryGetRetention(null, SalesRecord, out _).ShouldBeTrue(
			"construction is total by contract, so a declaration the validator would have refused must "
			+ "still resolve rather than sit in the registry matching nothing");
	}

	/// <summary>
	/// SAFETY. One aggregate type declared twice under two spellings of the SAME tenant is refused AS A
	/// DUPLICATE, not merely as a blank.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The validator's duplicate rule and the registry's grouping are two predicates over one concept. While
	/// the validator keyed on the tenant AS WRITTEN and the registry grouped on the NORMALISED tenant, this
	/// pair passed the duplicate rule and then collapsed in the registry: <c>Declared</c> held ONE entry, the
	/// FIRST declared won, and the second was discarded with nothing reported. The basis and the period on a
	/// signed erasure record were therefore decided by the order two lines appear in a registration call
	/// — here, six years of tax-code retention in place of ten years of anti-money-laundering.
	/// </para>
	/// <para>
	/// Measured before the fix: the validator emitted exactly ONE failure, the blank-tenant message, and the
	/// duplicate rule never fired. That the pair was refused ANYWAY, by the blank rule, is why it had not
	/// shown up as a defect — and is precisely the circularity being removed: the duplicate rule was
	/// sound only for as long as some OTHER rule happened to reject the colliding spelling.
	/// </para>
	/// <para>
	/// RED input: key the duplicate check on the tenant as written. Both declarations are then distinct
	/// scopes, the duplicate message disappears, and the registry silently keeps the first.
	/// </para>
	/// </remarks>
	[Fact]
	public void Refuse_one_type_declared_twice_under_two_spellings_of_one_tenant()
	{
		var result = Validate(
			Valid(SalesRecord) with { TenantId = string.Empty },
			Valid(SalesRecord) with { TenantId = TenantScope.UntenantedSentinel });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(
			"a second time for the same tenant",
			Case.Sensitive,
			"the blank rule refuses this pair too, so asserting only that validation FAILED would pass "
			+ "with the duplicate rule still blind. The duplicate must be named");
	}

	/// <summary>SAFETY. A justification that only restates the ground establishes no obligation.</summary>
	[Theory]
	[InlineData("LegalObligation")]
	[InlineData("legal obligation")]
	[InlineData("Legal-Obligation.")]
	public void Refuse_a_justification_that_only_restates_the_basis(string justification)
	{
		Validate(Valid(SalesRecord) with { Justification = justification }).Failed.ShouldBeTrue();
	}

	/// <summary>LIVENESS. A well-formed declaration passes, so the arms above are not vacuous.</summary>
	[Fact]
	public void Accept_a_well_formed_declaration()
	{
		Validate(Valid(SalesRecord)).Succeeded.ShouldBeTrue();
	}

	/// <summary>LIVENESS. The same type declared for two different tenants is two declarations.</summary>
	/// <remarks>
	/// The duplicate check keys on (tenant, type). If it keyed on the type alone it would reject this, and
	/// a multi-tenant host whose tenants operate under different statutes could not express them.
	/// </remarks>
	[Fact]
	public void Accept_one_type_declared_for_two_tenants()
	{
		Validate(
				Valid(SalesRecord) with { TenantId = "tenant-a" },
				Valid(SalesRecord) with { TenantId = "tenant-b" })
			.Succeeded.ShouldBeTrue();
	}

	/// <summary>SAFETY. A period of zero or less is refused.</summary>
	[Fact]
	public void Refuse_a_period_that_is_not_greater_than_zero()
	{
		Validate(Valid(SalesRecord) with { RetentionPeriod = TimeSpan.Zero }).Failed.ShouldBeTrue();
	}

	/// <summary>SAFETY. One aggregate type in one tenant has one retention.</summary>
	[Fact]
	public void Refuse_a_second_declaration_of_the_same_type()
	{
		Validate(Valid(SalesRecord), Valid(SalesRecord)).Failed.ShouldBeTrue();
	}

	/// <summary>
	/// LIVENESS, end to end through the real registration. A host that declares a retention resolves a
	/// registry that answers for it.
	/// </summary>
	[Fact]
	public void Be_resolvable_from_the_registration_a_consumer_calls()
	{
		var services = new ServiceCollection();
		_ = services.AddErasureRetention(Valid(SalesRecord));

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IErasureRetentionRegistry>()
			.TryGetRetention(null, SalesRecord, out _).ShouldBeTrue();
	}

	/// <summary>
	/// The literal this file hands to <c>[InlineData]</c> IS the framework's untenanted sentinel.
	/// </summary>
	/// <remarks>
	/// An attribute argument must be a compile-time constant, and <c>TenantScope.UntenantedSentinel</c> is a
	/// static readonly field, so the arms below cannot reference it directly. That leaves a copied literal,
	/// which can drift from the real sentinel while every arm using it still passes — so the copy is
	/// pinned here instead of trusted. RED input: change the sentinel in the framework and this fails, rather
	/// than the retention arms quietly starting to assert nothing.
	/// </remarks>
	[Fact]
	public void Pin_the_sentinel_literal_its_arms_are_written_against() =>
		UntenantedLiteral.ShouldBe(
			TenantScope.UntenantedSentinel,
			"the arms below pass this literal as an erasure's tenant; if it stops being the sentinel they "
			+ "stop testing the case they name");

	/// <summary>
	/// SAFETY. A declaration naming the untenanted SENTINEL is found by an erasure that names NO tenant, so
	/// a single-tenant deployment cannot stop matching its own retentions over a spelling.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm a measurement forced. A single-tenant erasure request carries whatever the store
	/// stamped, and the store stamps the untenanted sentinel; a declaration is written by a human. Compared
	/// as written, a deployment would match its own retentions or not depending on which spelling of
	/// <em>untenanted</em> each side happened to use — and the failure is silent in the direction that
	/// destroys a legally-required record.
	/// </para>
	/// <para>
	/// RED inputs, each independently: compare the declared tenant as written instead of normalising it, and
	/// the null, empty and whitespace cases stop matching; normalise only the lookup side, and the sentinel
	/// case stops matching.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(UntenantedLiteral)]
	public void Match_an_untenanted_erasure_against_a_sentinel_declaration(string? erasureTenant) =>
		Registry(Valid(SalesRecord))
			.TryGetRetention(erasureTenant, SalesRecord, out _)
			.ShouldBeTrue(
				"every spelling of untenanted names one tenant, and a declaration naming it must be found "
				+ "by an erasure that spells it differently");

	/// <summary>
	/// SAFETY. One tenant's declaration does not answer for another's, which is the over-retention direction
	/// of the same obligation.
	/// </summary>
	[Fact]
	public void Not_match_one_tenant_declaration_against_another_tenant_erasure()
	{
		var registry = Registry(Valid(SalesRecord) with { TenantId = "tenant-a" });

		registry.TryGetRetention("tenant-a", SalesRecord, out _).ShouldBeTrue(
			"the declaring tenant's own erasure must find it, or the refusal below is satisfied by a "
			+ "registry that matches nothing at all");
		registry.TryGetRetention("tenant-b", SalesRecord, out _).ShouldBeFalse(
			"another tenant's declaration must not answer for this one — that is over-retention, the "
			+ "Article 17 breach in the other direction");
	}

	/// <summary>
	/// SAFETY for the WRITE path, which is tenant-blind: a type declared by ANY tenant is retained, because
	/// the key handle carries no tenant and the write cannot know whose retention will apply.
	/// </summary>
	/// <remarks>
	/// RED input: key this question on a tenant too. A write outside any tenant scope then answers "not
	/// retained" for a type someone declared, places the subject's fields under the plain subject handle,
	/// and the declaring tenant's erasure spares a widened handle nothing was ever written under.
	/// </remarks>
	[Fact]
	public void Report_a_type_retained_when_any_tenant_declared_it() =>
		Registry(Valid(SalesRecord) with { TenantId = "tenant-a" })
			.IsRetainedForAnyTenant(SalesRecord)
			.ShouldBeTrue(
				"the write path has no tenant, so a foreign tenant's declaration must still widen the "
				+ "handle; whose retention applies is the erasure's decision, not this one");

	/// <summary>
	/// LIVENESS, and it is what stops the arm above being satisfied by a registry reporting everything
	/// retained: an UNDECLARED type is not retained, so its values stay under the handle an erasure destroys
	/// without consulting any inventory.
	/// </summary>
	[Theory]
	[InlineData("Invoice")]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Report_an_undeclared_type_not_retained(string? aggregateType) =>
		Registry(Valid(SalesRecord) with { TenantId = "tenant-a" })
			.IsRetainedForAnyTenant(aggregateType)
			.ShouldBeFalse(
				"nothing is declared for this type, so widening its handle would make its erasure depend on "
				+ "the data inventory being complete, for no reason at all");

	private static ErasureRetention Valid(string aggregateType) => new()
	{
		AggregateType = aggregateType,
		TenantId = TenantScope.UntenantedSentinel,
		Basis = LegalHoldBasis.LegalObligation,
		Justification =
			"Vehicle sales records are kept for six years under the tax code's record-keeping requirement.",
		RetentionPeriod = TimeSpan.FromDays(365 * 6),
	};

	// Through the real registration, so the arms bind what a consumer actually gets rather than a
	// hand-built options instance the shipped path never produces.
	private static IErasureRetentionRegistry Registry(params ErasureRetention[] retentions)
	{
		var services = new ServiceCollection();
		_ = services.AddErasureRetention(retentions);

		return services.BuildServiceProvider().GetRequiredService<IErasureRetentionRegistry>();
	}

	// Through the real registration and the real options pipeline: resolving the value is what runs the
	// wired validator, and a rejected declaration surfaces exactly as it does at host start.
	private static ValidateOptionsResult Validate(params ErasureRetention[] retentions)
	{
		var services = new ServiceCollection();
		_ = services.AddErasureRetention(retentions);

		using var provider = services.BuildServiceProvider();

		try
		{
			_ = provider.GetRequiredService<IOptions<AggregateRetentionOptions>>().Value;

			return ValidateOptionsResult.Success;
		}
		catch (OptionsValidationException ex)
		{
			return ValidateOptionsResult.Fail(ex.Failures);
		}
	}
}
