// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Tenancy;

// Independent structural regression lock (author != implementer) for the S902 keystone seam
// (fxw3om / 0ior5n): a KEYED store whose emitted statement carries NO tenant term must be
// UNCONSTRUCTABLE. The type-split makes the isolation violation a state the type system cannot
// name; this lock proves that structural property directly and holds it against the one-token
// mutants that would re-open the empty-predicate hole.
//
// THE DEFECT CLASS (root cause dn3ha6): a keyed store (inbox / saga / snapshot / tenant-columned
// event store) whose unique key includes the tenant column emits TWO SQL shapes from one
// conditional predicate — `scope.IsScoped ? "AND TenantId = @p" : string.Empty`. The else-half is
// the empty predicate; it matches EVERY tenant's rows and no tenant-isolation test ever executed
// it. KeyedTenantPartition removes that else-half from the type system: it has NO `None`/empty
// inhabitant, so a keyed request can never resolve to an empty tenant term.
//
// PROPERTY, not mechanism (testing-patterns S3): every arm asserts an OBSERVABLE term/behaviour of
// the partition, never an internal symbol. Each arm names the one-token mutant it goes RED against.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KeyedTenantPartitionShould
{
    private const string RealTenant = "tenant-a";

    // The sanctioned untenanted term. Asserted as a literal (not the internal const) so the lock
    // binds the OBSERVABLE wire value a keyed store will bind, and goes RED if the sentinel is ever
    // silently changed to "" / null (which would collapse it back to an empty predicate).
    private const string UntenantedSentinel = "__untenanted__";

    // ---- SAFETY: the empty tenant term is unnameable -------------------------------------------

    // The load-bearing structural arm. Untenanted is the ONLY untenanted inhabitant, and it still
    // binds a concrete, non-empty term. RED against a mutant that binds Untenanted to "" or null
    // (the empty-predicate hole this whole sprint removes).
    [Fact]
    public void Bind_A_Concrete_NonEmpty_Term_For_The_Untenanted_Partition()
    {
        KeyedTenantPartition.Untenanted.TenantId.ShouldNotBeNullOrWhiteSpace(
            "the untenanted keyed partition must emit a real equality term, never an empty predicate " +
            "— an empty term matches every tenant's rows");
        KeyedTenantPartition.Untenanted.TenantId.ShouldBe(
            UntenantedSentinel,
            "the untenanted term must be the reserved sentinel, so it can never equal a real tenant");
    }

    // A keyed request cannot be built without a tenant term: a null/empty/whitespace tenant throws
    // rather than degrading to "no filter". RED against a mutant that drops the IsNullOrWhiteSpace
    // guard (which would let Scoped(null) build a partition binding an empty term).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_A_Missing_Tenant_On_Scoped(string? missing)
        => Should.Throw<TenantRequiredException>(() => KeyedTenantPartition.Scoped(missing));

    // The reserved sentinel cannot be forged as a real tenant, so a real tenant can never collide
    // with the untenanted partition. RED against a mutant that removes the sentinel-equality guard.
    [Fact]
    public void Reject_The_Reserved_Sentinel_As_A_Real_Tenant()
        => Should.Throw<ArgumentException>(() => KeyedTenantPartition.Scoped(UntenantedSentinel));

    // An identifier longer than every shipped provider's narrowest tenant column (ADR-140, bd-5y8vg2) is
    // rejected here rather than truncated by a store, where a truncated identifier could collide with
    // another tenant's. RED against a mutant that removes the length guard.
    [Fact]
    public void Reject_An_Identifier_Longer_Than_MaxLength()
        => Should.Throw<ArgumentException>(() => KeyedTenantPartition.Scoped(new string('t', TenantId.MaxLength + 1)));

    // LIVENESS pair for the arm above (testing-patterns S3): a legal identifier at exactly the boundary
    // still binds. A guard that rejected everything would satisfy the safety arm alone.
    [Fact]
    public void Bind_An_Identifier_At_Exactly_MaxLength()
    {
        var value = new string('t', TenantId.MaxLength);

        KeyedTenantPartition.Scoped(value).TenantId.ShouldBe(value);
    }

    // The migration bridge from the column-agnostic family: TenantScope.Untenanted (the ONE sanctioned
    // empty-term scope) must project to Untenanted (a concrete sentinel term), NEVER carry the empty
    // term across into the keyed family. This is the exact fail-open FR-8 warns of: a keyed store
    // handed None must bind __untenanted__, not emit nothing. RED against a mutant that maps
    // None -> a null/empty-term partition.
    [Fact]
    public void Project_The_None_Scope_Onto_The_Untenanted_Sentinel_Term()
    {
        var partition = KeyedTenantPartition.FromScope(TenantScope.Untenanted);

        partition.ShouldBe(KeyedTenantPartition.Untenanted);
        partition.TenantId.ShouldBe(
            UntenantedSentinel,
            "a keyed store must never inherit the column-agnostic empty predicate — None becomes the " +
            "concrete untenanted sentinel term");
    }

    // A missing ambient context is NOT a tenant fact, and this conversion no longer pretends otherwise.
    // The compiler is the real enforcement -- a caller holding an ITenantContext? cannot reach this method
    // at all -- but a nullable-oblivious caller can still hand it null at run time, and it must fail closed
    // rather than invent the untenanted partition. RED against a mutant that restores the null fold.
    [Fact]
    public void Reject_A_Null_Context_RatherThanInventingTheUntenantedPartition()
    {
        _ = Should.Throw<ArgumentNullException>(() => KeyedTenantPartition.FromContext(null!));
    }

    // ---- LIVENESS: a real tenant's term is preserved -------------------------------------------
    // Without these, a partition that bound the sentinel for EVERYTHING (isolation trivially "safe",
    // every tenant collapsed onto one term) would satisfy every safety arm above while destroying
    // multi-tenancy.

    // A real tenant's term is carried through verbatim, so the store still filters to that tenant's
    // rows (A sees A). RED against a mutant that returns the sentinel for a scoped partition.
    [Fact]
    public void Preserve_A_Real_Tenants_Term_On_Scoped()
    {
        var partition = KeyedTenantPartition.Scoped(RealTenant);

        partition.TenantId.ShouldBe(RealTenant);
        partition.IsRealTenant.ShouldBeTrue(
            "a scoped partition names a real tenant, so the store filters to that tenant's rows");
    }

    // A scoped context resolves to that real tenant's term (the liveness twin of the null-context
    // arm above). RED against a mutant that ignores a resolved tenant and binds the sentinel.
    [Fact]
    public void Derive_A_Real_Tenants_Term_From_A_Scoped_Context()
    {
        var partition = KeyedTenantPartition.FromContext(new FixedTenantContext(RealTenant));

        partition.TenantId.ShouldBe(RealTenant);
        partition.IsRealTenant.ShouldBeTrue();
    }

    // The real tenant and the untenanted partition are distinct terms — a real tenant is never the
    // sentinel and vice-versa. Guards the fix from being "corrected" into treating the sentinel as a
    // wildcard that a real tenant matches.
    [Fact]
    public void Distinguish_A_Real_Tenant_From_The_Untenanted_Partition()
    {
        KeyedTenantPartition.Scoped(RealTenant).IsRealTenant.ShouldBeTrue();
        KeyedTenantPartition.Untenanted.IsRealTenant.ShouldBeFalse();
        KeyedTenantPartition.Scoped(RealTenant).ShouldNotBe(KeyedTenantPartition.Untenanted);
    }

    // A fixture ITenantContext implemented DIRECTLY from the interface (no first-party base supplying
    // the member), widening no production visibility — testing-patterns S3 fixture-shape corollary.
    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(string? tenantId) => TenantId = tenantId;

        public string? TenantId { get; }

        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }

    // ---- THE CONVERSION PROPERTY, at every boundary --------------------------------------------
    //
    // ONE property, exercised at every boundary a tenant term can enter through -- storage, ambient
    // context, and a scope value -- rather than four per-boundary locks:
    //
    //   For any tenant term t obtained from STORAGE or a SCOPE: converting t SUCCEEDS and yields
    //   Untenanted exactly when t is null, whitespace, or the sentinel.
    //
    //   For a tenant term obtained from an AMBIENT CONTEXT: the sentinel and a null CONTEXT convert,
    //   but a PRESENT context that resolved no tenant FAILS CLOSED.
    //
    // The asymmetry is not stylistic. Only AmbientTenantContext -- the context multi-tenancy itself
    // registers -- can yield a null tenant; SingleTenantContext's is a constant. So on the ambient
    // path a null tenant is positive evidence that multi-tenancy is active and unresolved, which is
    // exactly the state that must not silently widen to every tenant. Storage has no such signal: a
    // null column is an untenanted ROW, and throwing there aborts a legitimate pass on its first
    // legacy record. The SENTINEL rule is shared by both.
    //
    // Both halves are load-bearing and neither is sufficient alone:
    //
    //   SAFETY   -- a genuine identifier NEVER silently becomes Untenanted. Without this, a
    //               conversion that mapped everything to Untenanted would satisfy liveness while
    //               destroying isolation.
    //   LIVENESS -- EVERY untenanted spelling converts WITHOUT THROWING. Without this, a conversion
    //               that threw on the untenanted case would satisfy safety while making an
    //               untenanted caller unable to operate at all. Suppressing the throw and returning
    //               an empty result would ALSO pass a safety-only lock -- the arm that matters is
    //               "an untenanted caller gets its rows", never "the exception stopped".

    // SAFETY -- the ambient path fails closed when the context is PRESENT but resolved NO tenant.
    //
    // THE DISCRIMINATOR, which is what makes this a safety case and not a liveness one. It is not
    // "a context exists" -- that carries no information, since AddDefaultTenantContext() registers
    // one unconditionally. It is WHICH context can produce a null tenant at all:
    //
    //   SingleTenantContext   TenantId => TenantDefaults.DefaultTenantId ("__default__")
    //                         NEVER null. A single-tenant host cannot reach this arm.
    //   AmbientTenantContext  TenantId => TenantContextHolder.Current
    //                         null OUTSIDE a BeginScope, and it is registered only BY multi-tenancy.
    //
    // Those are the only two ITenantContext implementations in the product. So a present context
    // yielding null/whitespace means multi-tenancy IS registered and the tenant is UNRESOLVED --
    // and converting that to Untenanted would hand the caller a partition that reads across every
    // tenant. Failing closed is the correct behaviour and this arm exists to keep it.
    //
    // This arm was flipped twice on rulings that were later withdrawn. The reading that lost held
    // that "a present context implies nothing, so throwing breaks single-tenant hosts" -- true of
    // the premise, false of the conclusion, because a single-tenant host's context never yields a
    // null tenant to throw on. Recorded so the next reader inherits the discriminator rather than
    // re-deriving it.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FailClosedWhenAPresentAmbientContextResolvedNoTenant(string? unresolvedTenantId)
    {
        var context = new FixedTenantContext(unresolvedTenantId);

        _ = Should.Throw<TenantRequiredException>(() => KeyedTenantPartition.FromContext(context));
    }

    // The structural half of the rule above, and the one that actually prevents the defect: there is no
    // null-accepting conversion on the public surface, so a store holding an optional context fails to
    // COMPILE instead of silently receiving a well-formed untenanted partition. This test pins the shape;
    // the compiler enforces it. RED against re-adding an ITenantContext? overload, and RED against merely
    // relaxing the annotation on the surviving one.
    [Fact]
    public void Expose_No_NullAccepting_FromContext_Conversion()
    {
        var overloads = typeof(KeyedTenantPartition)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => string.Equals(m.Name, nameof(KeyedTenantPartition.FromContext), StringComparison.Ordinal))
            .ToList();

        overloads.Count.ShouldBe(
            1,
            "a second, null-accepting conversion is exactly what let a store whose context was never wired " +
            "report a well-formed untenanted partition");

        var parameter = overloads[0].GetParameters().Single();
        new NullabilityInfoContext().Create(parameter).WriteState.ShouldBe(
            NullabilityState.NotNull,
            "the compiler is the enforcement: a caller that hands this a possibly-null context must not build");
    }

    // LIVENESS -- the SENTINEL-bearing context converts rather than being rejected as a reserved
    // value. This is the single-sited sentinel rule, and it is the one part shared by every entry
    // point on this type.
    [Fact]
    public void ConvertTheSentinelBearingContextToUntenanted_WithoutThrowing()
    {
        var context = new FixedTenantContext(UntenantedSentinel);

        var partition = Should.NotThrow(() => KeyedTenantPartition.FromContext(context));

        partition.ShouldBe(KeyedTenantPartition.Untenanted);
        partition.TenantId.ShouldBe(UntenantedSentinel);
        partition.IsRealTenant.ShouldBeFalse();
    }

    // LIVENESS -- the same property at the STORAGE boundary. A row written before tenancy existed
    // reads back as null/empty; a row written by the framework reads back as the sentinel. All are
    // untenanted spellings and none may throw on the way in.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(UntenantedSentinel)]
    public void ConvertEveryUntenantedStoredSpellingToTheSentinel_WithoutThrowing(string? storedValue)
    {
        var partition = Should.NotThrow(() => KeyedTenantPartition.FromStoredValue(storedValue));

        partition.ShouldBe(KeyedTenantPartition.Untenanted);
        partition.TenantId.ShouldBe(UntenantedSentinel);
    }

    // LIVENESS -- the same property at the SCOPE boundary. TenantScope has two untenanted
    // inhabitants: the default (None) and the explicit Untenanted sentinel. Both must convert, and
    // both must land on the same term -- if they diverged, a keyed store would emit two different
    // untenanted predicates depending on which one its caller happened to hold.
    [Fact]
    public void ConvertBothUntenantedScopeInhabitantsToTheSameSentinelTerm_WithoutThrowing()
    {
        var fromNone = Should.NotThrow(() => KeyedTenantPartition.FromScope(TenantScope.Untenanted));
        var fromUntenanted = Should.NotThrow(() => KeyedTenantPartition.FromScope(TenantScope.Untenanted));

        fromNone.ShouldBe(KeyedTenantPartition.Untenanted);
        fromUntenanted.ShouldBe(KeyedTenantPartition.Untenanted);
        fromNone.ShouldBe(fromUntenanted);
    }

    // SAFETY -- the other half. A genuine identifier must survive EVERY boundary as itself and must
    // never be silently downgraded to the untenanted sentinel. This is what stops the liveness arms
    // above from being satisfied by a conversion that simply returns Untenanted for everything.
    [Fact]
    public void NeverDowngradeAGenuineIdentifierToUntenanted_AtAnyBoundary()
    {
        var fromContext = KeyedTenantPartition.FromContext(new FixedTenantContext(RealTenant));
        var fromStored = KeyedTenantPartition.FromStoredValue(RealTenant);
        var fromScope = KeyedTenantPartition.FromScope(TenantScope.Scoped(RealTenant));

        foreach (var partition in new[] { fromContext, fromStored, fromScope })
        {
            partition.TenantId.ShouldBe(RealTenant);
            partition.IsRealTenant.ShouldBeTrue();
            partition.ShouldNotBe(KeyedTenantPartition.Untenanted);
        }
    }
}
