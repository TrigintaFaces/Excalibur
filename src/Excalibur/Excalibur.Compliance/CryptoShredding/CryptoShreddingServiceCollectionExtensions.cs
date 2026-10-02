// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Erasure;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for per-subject crypto-shredding.
/// </summary>
public static class CryptoShreddingServiceCollectionExtensions
{
    /// <summary>
    /// Adds per-subject crypto-shredding: a <see cref="ISubjectKeyManager"/> that binds each data subject to
    /// a dedicated key over the registered key-management subsystem, so destroying a subject's key erases
    /// that subject's encrypted data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requires the key-management subsystem (<see cref="IKeyManagementProvider"/> +
    /// <see cref="IKeyManagementAdmin"/>) to be registered, by the consumer's compliance-encryption setup.
    /// </para>
    /// <para>
    /// The data-subject hasher this registration's own key manager depends on is registered here, so this call
    /// is sufficient on its own; the consumer still supplies the hashing pepper via
    /// <c>Configure&lt;DataSubjectHashingOptions&gt;</c> from a secret manager.
    /// </para>
    /// <para>
    /// A key-destruction ledger is also required, and is NOT registered here: it is supplied by an erasure
    /// store, or by <see cref="AddCryptoShreddingWithoutErasure" /> for a deployment that encrypts personal
    /// data at rest and never destroys a subject key. Registering neither is refused at start-up.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCryptoShredding(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // RESOLUTION MUST ALWAYS SUCCEED, because the alternative is not a failure but a DIFFERENT ANSWER.
        // An absent retention registry answers "nothing is retained" for every type, so a composition
        // missing it writes a retained subject's fields under the plain subject handle -- while a sibling
        // composition that has it writes under the widened one. The two name different keys, and a key
        // handle is the name of the thing an erasure destroys, so the stored record and the signed
        // certificate end up disagreeing with nothing to report it. An EMPTY registry is a value meaning
        // "none declared"; an ABSENT one is a second answer to the same question.
        //
        // A TENANT CONTEXT IS REGISTERED HERE, and this line replaces a comment asserting that none was
        // needed -- "the key manager reads no tenant. The handle it chooses is H(subject) or
        // H(subject)-<type>, neither of which carries one." That was true and it was the defect: a handle
        // constant in the tenant gave two tenants whose consumer-supplied data-subject identifiers coincided
        // ONE key, and either tenant's erasure destroyed it for both. The write path now binds the ambient
        // tenant into the handle, so it needs a context to read one from; AddDefaultTenantContext is
        // idempotent and yields the single-tenant identity when the host configures no multi-tenancy, which
        // is what makes this safe to call unconditionally.
        _ = services.AddDefaultTenantContext();

        services.TryAddSingleton<IErasureRetentionRegistry, ErasureRetentionRegistry>();

        // Same clause, one dependency over: SubjectKeyManager requires IDataSubjectHasher, and until this line
        // existed nothing on this path registered it -- so AddCryptoShredding() alone produced a container that
        // could not construct the key manager at all. It went unnoticed because every composition that worked
        // also added an erasure store, and the erasure registrations call AddDataSubjectHashing() for exactly
        // this reason. That call is idempotent by design and documented as belonging on every path that
        // resolves something which pseudonymizes a subject identifier; this is one of those paths.
        _ = services.AddDataSubjectHashing();

        // SINGLETON, and these three used to be SCOPED, which is what stopped a host starting at all.
        //
        // Nothing here holds per-request state. Each of the three carries only readonly fields and no
        // disposal, and EVERYTHING BENEATH THEM IS ALREADY A SINGLETON -- the key-management provider and
        // admin, the data-subject hasher, the retention registry, the destruction ledger, the provider
        // registry. They were scoped by convention, not because anything in the graph was request-bound.
        //
        // The convention had a cost that only showed up at a second door. A singleton that is decorated --
        // the event store, for instance -- builds its decorator from the ROOT provider, and a decorator
        // factory that resolves a SCOPED collaborator throws there under scope validation, which is on by
        // default in Development. So a composition wiring at-rest encryption could not start, and the
        // failure named SubjectFieldCryptor, a type the consumer never asked for. Where scope validation is
        // off it did not throw: the singleton captured one scope's instance for the process lifetime
        // instead, which is the same defect without the diagnosis.
        //
        // FieldEncryptor depends on ITenantContext, and that is safe BY PUBLISHED CONTRACT rather than by
        // luck: every tenant context this framework registers is a singleton that reads the ambient tenant
        // on each access, so a singleton may depend on one. A consumer who registers a request-bound
        // ITenantContext of their own breaks that contract, and the multi-tenancy documentation states it.
        services.TryAddSingleton<ISubjectKeyManager, SubjectKeyManager>();
        services.TryAddSingleton<IFieldEncryptor, FieldEncryptor>();
        services.TryAddSingleton<SubjectFieldCryptor>();

        // FAIL AT START RATHER THAN ON THE FIRST REQUEST. FieldEncryptor requires IKeyDestructionLedger, and
        // every registration of one hangs off an erasure store -- so a composition with crypto-shredding and
        // no erasure store cannot construct the field encryptor. Because the encryptor is SCOPED, that
        // surfaces per-request, as a dependency-injection error naming a type the consumer never asked for.
        // Both descriptors are registered deliberately: the hosted service places the check in a host's
        // startup pipeline, and the prerequisite validator lets the same check run for a consumer who builds
        // a provider and calls ValidateStartupGates without ever starting a host.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, CryptoShreddingLedgerWiringValidator>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, CryptoShreddingLedgerWiringValidator>());

        return services;
    }

    /// <summary>
    /// Declares that this deployment encrypts personal data but performs NO erasure, and registers the
    /// destruction ledger that follows: one holding no rows, so every key generation reports as not destroyed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For encryption at rest without the GDPR erasure subsystem -- an encrypted event store, inbox or outbox
    /// in a deployment that never destroys a subject key. Reads decrypt normally and no tombstone is ever
    /// produced, because nothing was ever destroyed. That is the true answer here, not a placeholder.
    /// </para>
    /// <para>
    /// <b>Call this INSTEAD of <see cref="AddCryptoShredding" />, and never alongside an erasure store.</b> If
    /// this deployment erases, register an erasure store: it supplies the real ledger, and that is the only
    /// one that can report an erasure. Registering both is <b>refused at start-up</b> rather than left to
    /// registration order -- every ledger registration in this framework is a <c>TryAdd</c>, so whichever call
    /// ran first would win silently, and in one of the two orders that is an always-false ledger standing in
    /// front of a real erasure store: the deployment erases, nothing ever tombstones, and no component
    /// reports a problem.
    /// </para>
    /// <para>
    /// Separate call rather than a default, deliberately. An always-false ledger cannot fabricate an erasure,
    /// which is the safe direction -- and that is exactly why defaulting to one would be wrong: a deployment
    /// that DOES erase, but whose erasure store was never registered, would silently never tombstone anything
    /// and read its own erased subjects back in the clear, with nothing to report it. Naming the choice keeps
    /// it visible in the consumer's registration code instead of inferred from an absence.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCryptoShreddingWithoutErasure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddCryptoShredding();

        // Registered under its own concrete type as well, so the start-up guard can PROBE that this choice was
        // made without resolving anything. The guard needs to distinguish "the no-erasure ledger is in play"
        // from "some ledger is in play", and the IKeyDestructionLedger descriptor alone cannot say which.
        services.TryAddSingleton<NoErasureKeyDestructionLedger>();
        services.TryAddSingleton<IKeyDestructionLedger>(
            static sp => sp.GetRequiredService<NoErasureKeyDestructionLedger>());

        return services;
    }
}
