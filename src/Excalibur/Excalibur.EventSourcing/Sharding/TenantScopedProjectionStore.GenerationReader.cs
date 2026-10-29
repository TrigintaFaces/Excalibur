// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing.Sharding;

public sealed partial class TenantScopedProjectionStore<TProjection>
{
    private sealed class GenerationReaderFactory(TenantScopedProjectionStore<TProjection> outer) : IProjectionGenerationReaderFactory<TProjection>
    {
        [RequiresUnreferencedCode("The conventional reader serializer requires reflection.")]
        [RequiresDynamicCode("The conventional reader serializer requires dynamic code.")]
        public IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity)
        {
            outer.RequireTenant();
            var factory = outer.Inner.GetService(typeof(IProjectionGenerationReaderFactory<TProjection>)) as IProjectionGenerationReaderFactory<TProjection>
                ?? throw new NotSupportedException("The current store does not provide generation readers.");
            return new GenerationReader(outer, factory.OpenReader(destinationIdentity));
        }
    }
    private sealed class GeneratedReaderFactory(TenantScopedProjectionStore<TProjection> outer) : IAotProjectionGenerationReaderFactory<TProjection>
    {
        public IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity, JsonTypeInfo<TProjection> typeInfo)
        {
            outer.RequireTenant();
            var factory = outer.Inner.GetService(typeof(IAotProjectionGenerationReaderFactory<TProjection>)) as IAotProjectionGenerationReaderFactory<TProjection>
                ?? throw new NotSupportedException("The current store does not provide generated generation readers.");
            return new GenerationReader(outer, factory.OpenReader(destinationIdentity, typeInfo));
        }
    }
    private sealed class GenerationReader(TenantScopedProjectionStore<TProjection> outer, IProjectionGenerationReader<TProjection> inner) : IProjectionGenerationReader<TProjection>
    {
        private readonly ProjectionTenantBoundary _boundary = Projections.ProjectionTenantValidation.Read(outer);
        private void ValidateBoundary()
        {
            outer.RequireTenant();
            if (Projections.ProjectionTenantValidation.Read(outer) != _boundary)
            {
                throw new InvalidOperationException("A generation reader cannot cross tenant execution contexts.");
            }
        }
        public string? DestinationScope { get { ValidateBoundary(); return inner.DestinationScope; } }
        public ValueTask<string> GetDestinationScopeAsync(CancellationToken cancellationToken)
        {
            ValidateBoundary();
            return inner.GetDestinationScopeAsync(cancellationToken);
        }
        public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            ValidateBoundary();
            return await inner.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
        public async Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
        {
            ValidateBoundary();
            return await inner.QueryAsync(filters, options, cancellationToken).ConfigureAwait(false);
        }
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
        {
            ValidateBoundary();
            return inner.CountAsync(filters, cancellationToken);
        }
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
