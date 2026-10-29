// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing.Sharding;

internal sealed partial class TenantRoutingProjectionStore<TProjection>
{
    private sealed class GenerationReaderFactory(TenantRoutingProjectionStore<TProjection> outer) : IProjectionGenerationReaderFactory<TProjection>
    {
        [RequiresUnreferencedCode("The conventional reader serializer requires reflection.")]
        [RequiresDynamicCode("The conventional reader serializer requires dynamic code.")]
        public IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity)
        {
            
            var factory = outer.ResolveStore().GetService(typeof(IProjectionGenerationReaderFactory<TProjection>)) as IProjectionGenerationReaderFactory<TProjection>
                ?? throw new NotSupportedException("The current store does not provide generation readers.");
            return new GenerationReader(outer, factory.OpenReader(destinationIdentity));
        }
    }
    private sealed class GeneratedReaderFactory(TenantRoutingProjectionStore<TProjection> outer) : IAotProjectionGenerationReaderFactory<TProjection>
    {
        public IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity, JsonTypeInfo<TProjection> typeInfo)
        {
            
            var factory = outer.ResolveStore().GetService(typeof(IAotProjectionGenerationReaderFactory<TProjection>)) as IAotProjectionGenerationReaderFactory<TProjection>
                ?? throw new NotSupportedException("The current store does not provide generated generation readers.");
            return new GenerationReader(outer, factory.OpenReader(destinationIdentity, typeInfo));
        }
    }
    private sealed class GenerationReader(TenantRoutingProjectionStore<TProjection> outer, IProjectionGenerationReader<TProjection> inner) : IProjectionGenerationReader<TProjection>
    {
        private readonly string tenant = Excalibur.Dispatch.TenantScope.FromContext(outer._tenantContext).TenantId;
        public string? DestinationScope
        {
            get
            {
                if (!string.Equals(Excalibur.Dispatch.TenantScope.FromContext(outer._tenantContext).TenantId, tenant, StringComparison.Ordinal)) { throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts."); }
                return inner.DestinationScope;
            }
        }
        public ValueTask<string> GetDestinationScopeAsync(CancellationToken cancellationToken)
        {
            if (!string.Equals(Excalibur.Dispatch.TenantScope.FromContext(outer._tenantContext).TenantId, tenant, StringComparison.Ordinal)) { throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts."); }
            return inner.GetDestinationScopeAsync(cancellationToken);
        }
        public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            if (!string.Equals(Excalibur.Dispatch.TenantScope.FromContext(outer._tenantContext).TenantId, tenant, StringComparison.Ordinal)) { throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts."); }
            return await inner.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
        public async Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
        {
            if (!string.Equals(Excalibur.Dispatch.TenantScope.FromContext(outer._tenantContext).TenantId, tenant, StringComparison.Ordinal)) { throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts."); }
            return await inner.QueryAsync(filters, options, cancellationToken).ConfigureAwait(false);
        }
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
        {
            if (!string.Equals(Excalibur.Dispatch.TenantScope.FromContext(outer._tenantContext).TenantId, tenant, StringComparison.Ordinal)) { throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts."); }
            return inner.CountAsync(filters, cancellationToken);
        }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
