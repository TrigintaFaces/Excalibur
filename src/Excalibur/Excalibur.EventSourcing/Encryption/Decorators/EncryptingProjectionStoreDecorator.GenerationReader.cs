// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing.Encryption.Decorators;

public sealed partial class EncryptingProjectionStoreDecorator<TProjection>
{
    private sealed class GenerationReaderFactory(EncryptingProjectionStoreDecorator<TProjection> outer) : IProjectionGenerationReaderFactory<TProjection>
    {
        [RequiresUnreferencedCode("The conventional reader serializer requires reflection.")]
        [RequiresDynamicCode("The conventional reader serializer requires dynamic code.")]
        public IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity)
        {
            
            var factory = outer.Inner.GetService(typeof(IProjectionGenerationReaderFactory<TProjection>)) as IProjectionGenerationReaderFactory<TProjection>
                ?? throw new NotSupportedException("The current store does not provide generation readers.");
            return new GenerationReader(outer, factory.OpenReader(destinationIdentity));
        }
    }
    private sealed class GenerationReader(EncryptingProjectionStoreDecorator<TProjection> outer, IProjectionGenerationReader<TProjection> inner) : IProjectionGenerationReader<TProjection>
    {
        
        public string? DestinationScope => inner.DestinationScope;
        public ValueTask<string> GetDestinationScopeAsync(CancellationToken cancellationToken)
        {
            
            return inner.GetDestinationScopeAsync(cancellationToken);
        }
        public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            
            var value = await inner.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return value is null ? null : await outer.DecryptProjectionAsync(value, cancellationToken).ConfigureAwait(false);
        }
        public async Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
        {
            
            return await outer.DecryptAllAsync(await inner.QueryAsync(filters, options, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
        {
            
            return inner.CountAsync(filters, cancellationToken);
        }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
