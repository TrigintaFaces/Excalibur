// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionGenerationReaderContractShould
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("\u00a0")]
    public async Task RejectAnUnverifiableScope(string? scope)
    {
        await using IProjectionGenerationReader<object> reader = new Reader(scope);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await reader.GetDestinationScopeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PreserveTheConfiguredScope()
    {
        const string scope = "postgres/Acme";
        await using IProjectionGenerationReader<object> reader = new Reader(scope);
        (await reader.GetDestinationScopeAsync(CancellationToken.None)).ShouldBe(scope);
        reader.DestinationScope.ShouldBe(scope);
    }

    private sealed class Reader(string? scope) : IProjectionGenerationReader<object>
    {
        public string? DestinationScope => scope;
        public Task<object?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<object>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
