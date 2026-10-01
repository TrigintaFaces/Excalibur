// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// Requires the key-management subsystem (<see cref="IKeyManagementProvider"/> +
    /// <see cref="IKeyManagementAdmin"/>) and a data-subject hasher (<see cref="Excalibur.Compliance.Erasure.IDataSubjectHasher"/>)
    /// to be registered — typically by the consumer's compliance-encryption and data-subject-hashing setup.
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
        // No tenant context is registered here, and none is needed: the key manager reads no tenant. The
        // handle it chooses is H(subject) or H(subject)-<type>, neither of which carries one.
        services.TryAddSingleton<IErasureRetentionRegistry, ErasureRetentionRegistry>();

        services.TryAddScoped<ISubjectKeyManager, SubjectKeyManager>();
        services.TryAddScoped<IFieldEncryptor, FieldEncryptor>();
        services.TryAddScoped<SubjectFieldCryptor>();

        return services;
    }
}
