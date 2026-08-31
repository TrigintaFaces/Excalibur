// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

namespace Boundary.Tests;

/// <summary>
/// Structural guard on the tenant-scoping store seams: neither seam hands a call site an ambient
/// <c>ITenantContext</c> it could bind to a discard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both seams now close this hole by construction rather than by convention.</b> Each seam's factory
/// took the resolved <c>ITenantContext</c> as a SECOND lambda parameter, so a call site could write
/// <c>(sp, _) =&gt;</c> - compile, register, and still emit a marker attesting a discipline it did not
/// honour. Both factories now take <em>one</em> parameter, <c>Func&lt;IServiceProvider, TStore&gt;</c>;
/// there is no second formal parameter for a call site to bind to a discard, and each seam derives the
/// tenancy mechanism from the store TYPE's own constructors instead.
/// </para>
/// <para>
/// <b>This replaced a source scan, and the replacement is strictly stronger.</b> The projection seam was
/// previously policed by a regex over <c>src/</c> looking for a discarded second parameter. That
/// instrument could only see call sites inside this repository - the seam is public API, so a consumer's
/// discard was invisible to it - and it could go silently vacuous if the seam were renamed. Reflection
/// over the shipped signature answers the question for every caller that will ever exist, because the
/// shape a discard requires is no longer expressible.
/// </para>
/// <para>
/// The arms read the SHIPPED signature, not source text, so a future edit that reintroduces a second
/// delegate parameter is caught structurally rather than by hoping a pattern still matches.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class TenantScopedSeamContextNotDiscardedShould
{
    /// <summary>
    /// STRUCTURAL ARM (store seam). <c>AddTenantAwareStore</c>'s factory overload takes exactly one
    /// parameter - proving, by reflection on the shipped signature rather than by scanning for a pattern
    /// that can no longer occur, that a caller has no second formal parameter to bind to a discard.
    /// </summary>
    [Fact]
    public void TheStoreSeam_FactoryOverload_TakesExactlyOneParameter()
    {
        var storeFactoryParameter = SoleFactoryOverloadParameter("AddTenantAwareStore", totalParameters: 2);

        FactoryDelegateParameterCount(storeFactoryParameter).ShouldBe(
            1,
            "AddTenantAwareStore's factory delegate must take exactly ONE parameter (IServiceProvider). "
            + "A second parameter would reintroduce the shape where a call site can bind the ambient "
            + "ITenantContext the seam hands it to a discard, while still emitting a marker attesting "
            + "the store honours it.");
    }

    /// <summary>
    /// STRUCTURAL ARM (projection seam). <c>AddTenantScopedProjectionStore</c>'s factory takes exactly one
    /// parameter, for the same reason and by the same mechanism as the store seam above.
    /// </summary>
    /// <remarks>
    /// RED if a future edit re-adds <c>ITenantContext</c> to this factory's shape - the regression that
    /// left this seam, alone, policed by a source scan for as long as it kept the two-parameter form.
    /// </remarks>
    [Fact]
    public void TheProjectionSeam_Factory_TakesExactlyOneParameter()
    {
        var storeFactoryParameter = SoleFactoryOverloadParameter(
            "AddTenantScopedProjectionStore", totalParameters: 2);

        FactoryDelegateParameterCount(storeFactoryParameter).ShouldBe(
            1,
            "AddTenantScopedProjectionStore's factory delegate must take exactly ONE parameter "
            + "(IServiceProvider). A second parameter would let a call site bind the ambient "
            + "ITenantContext to a discard while the seam emits ITenantScopingCapability for the store's "
            + "family - a truthful-looking attestation of behaviour the store does not have.");
    }

    /// <summary>
    /// STRUCTURAL ARM (projection seam). The seam reads the STORE TYPE, which is what makes the emitted
    /// marker evidence rather than an assertion: the mechanism is derived from that type's constructors,
    /// so a store that does not take an <see cref="ITenantContext"/> cannot be registered through this
    /// verb at all.
    /// </summary>
    /// <remarks>
    /// Without this arm, the one-parameter factory alone would be satisfied by a seam that simply stopped
    /// asking about the tenant - a shape that is safe from discards and attests nothing.
    /// </remarks>
    [Fact]
    public void TheProjectionSeam_DerivesTheMechanismFromAStoreTypeParameter()
    {
        var seam = SoleFactoryOverload("AddTenantScopedProjectionStore", totalParameters: 2);

        seam.GetGenericArguments().Length.ShouldBe(
            3,
            "AddTenantScopedProjectionStore must take TService, TStore and TCapabilityFamily. Dropping "
            + "TStore would remove the seam's only evidence for the capability it emits, leaving the "
            + "marker resting on what the caller wrote rather than on what the store requires.");
    }

    /// <summary>
    /// NON-VACUITY SELF-TEST. The reflection the arms above rely on reports a two-parameter factory as
    /// two - so a green arm is a measurement, not a lookup that silently returns the expected number.
    /// </summary>
    [Fact]
    public void TheReflection_CountsATwoParameterFactoryAsTwo()
    {
        FactoryDelegateParameterCount(typeof(Func<IServiceProvider, ITenantContext, object>)).ShouldBe(
            2,
            "The instrument must be able to see the shape it exists to reject; if it cannot, the arms "
            + "above are green regardless of the seam's real signature.");
    }

    /// <summary>Resolves the single factory overload of <paramref name="methodName"/> on the seam type.</summary>
    private static MethodInfo SoleFactoryOverload(string methodName, int totalParameters)
    {
        var overloads = ResolveSeamType()
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == methodName)
            .Where(m => m.GetParameters().Length == totalParameters)
            .ToList();

        overloads.ShouldHaveSingleItem(
            $"Expected exactly one {methodName} overload taking a services parameter plus one factory "
            + "parameter. If this is not 1, the seam's overload set changed shape and this guard must be "
            + "revisited.");

        return overloads[0];
    }

    /// <summary>The factory parameter of that overload.</summary>
    private static ParameterInfo SoleFactoryOverloadParameter(string methodName, int totalParameters) =>
        SoleFactoryOverload(methodName, totalParameters).GetParameters()[totalParameters - 1];

    /// <summary>The arity of the delegate a factory parameter is typed as.</summary>
    private static int FactoryDelegateParameterCount(ParameterInfo storeFactoryParameter) =>
        FactoryDelegateParameterCount(storeFactoryParameter.ParameterType);

    /// <summary>The arity of a delegate type's Invoke.</summary>
    private static int FactoryDelegateParameterCount(Type delegateType)
    {
        var delegateInvoke = delegateType.GetMethod("Invoke");

        delegateInvoke.ShouldNotBeNull("the factory parameter must be a delegate type.");

        return delegateInvoke.GetParameters().Length;
    }

    /// <summary>Resolves the seam's declaring type by reflection over the shipped assembly.</summary>
    private static Type ResolveSeamType()
    {
        var seamType = typeof(ITenantContext).Assembly.GetType(
            "Microsoft.Extensions.DependencyInjection.TenantScopedStoreServiceCollectionExtensions");

        seamType.ShouldNotBeNull(
            "Expected TenantScopedStoreServiceCollectionExtensions in the Excalibur.Dispatch.Abstractions "
            + "assembly. If it moved or was renamed, repoint this guard rather than deleting it.");

        return seamType;
    }
}
