// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;

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

        services.TryAddScoped<ISubjectKeyManager, SubjectKeyManager>();
        services.TryAddScoped<IFieldEncryptor, FieldEncryptor>();
        services.TryAddScoped<SubjectFieldCryptor>();

        return services;
    }
}
