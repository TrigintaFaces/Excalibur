// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Conformance")]
public sealed class ProjectionPagingFallbackShould
{
    [Theory]
    [InlineData(3, 7, 9, 3)]
    [InlineData(5, 7, 0, 0)]
    public async Task PageBeyondTheDefaultQueryLimitWithAccurateTotals(int page, int size, int first, int last)
    {
        var store = new SearchLikeStore();
        var options = new QueryOptions(Skip: 999, Take: 1, OrderBy: "Rank", Descending: true);
        var result = await store.QueryPagedAsync(null, page, size, options, CancellationToken.None);
        result.TotalItems.ShouldBe(23);
        store.LastOptions.ShouldBe(options with { Skip = (page - 1) * size, Take = size });
        options.Skip.ShouldBe(999);
        options.Take.ShouldBe(1);
        if (first == 0) { result.Items.ShouldBeEmpty(); }
        else { result.Items.Select(x => x.Rank).ShouldBe(Enumerable.Range(last, first - last + 1).Reverse()); }
    }

    [Fact]
    public async Task RejectOverflowBeforeAnyProviderCall()
    {
        var store = new SearchLikeStore();
        await Should.ThrowAsync<OverflowException>(() => store.QueryPagedAsync(null, int.MaxValue, 2, null, CancellationToken.None));
        store.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PropagateCountFailureWithoutManufacturingAnEmptyPage()
    {
        var store = new SearchLikeStore { FailCount = true };
        await Should.ThrowAsync<InvalidOperationException>(() => store.QueryPagedAsync(null, 1, 7, null, CancellationToken.None));
        store.LastOptions.ShouldBeNull();
    }

    [Theory]
    [InlineData(23)]
    [InlineData(501)]
    public async Task ReturnAllDistinctValuesBeyondTheDefaultLimit(int rows)
    {
        var store = new SearchLikeStore { RowCount = rows };
        var values = await store.DistinctValuesAsync("Rank", null, CancellationToken.None);
        values.Count.ShouldBe(rows);
        store.LastOptions!.Take.ShouldBe(rows);
    }

    [Fact]
    public async Task RejectPartialDistinctResultsFromACappedProvider()
    {
        var store = new SearchLikeStore { RowCount = 501, ResultCap = 100 };
        await Should.ThrowAsync<InvalidOperationException>(() => store.DistinctValuesAsync("Rank", null, CancellationToken.None));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(2147483648L)]
    public async Task RejectUnmaterializableDistinctCountsBeforeQuery(long count)
    {
        var store = new SearchLikeStore { ReportedCount = count };
        await Should.ThrowAsync<InvalidOperationException>(() => store.DistinctValuesAsync("Rank", null, CancellationToken.None));
        store.LastOptions.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateDistinctPropertyAndAvoidAZeroLimitQuery()
    {
        var store = new SearchLikeStore { RowCount = 0 };
        await Should.ThrowAsync<ArgumentException>(() => store.DistinctValuesAsync("Missing", null, CancellationToken.None));
        store.Calls.ShouldBe(0);
        (await store.DistinctValuesAsync("Rank", null, CancellationToken.None)).ShouldBeEmpty();
        store.LastOptions.ShouldBeNull();
    }

    [Theory]
    [InlineData(long.MaxValue, 1, long.MaxValue)]
    [InlineData(long.MaxValue, 2, 4611686018427387904L)]
    [InlineData(9007199254740993L, 2, 4503599627370497L)]
    [InlineData(24L, 6, 4L)]
    [InlineData(0L, 6, 0L)]
    public void PreserveLargePageCountsExactly(long total, int size, long expected)
    {
        new PagedResult<Row>([], 1, size, total).TotalPages.ShouldBe(expected);
        new CursorPagedResult<Row>([], size, total).TotalPages.ShouldBe(expected);
    }

    private sealed record Row(int Rank);

    private sealed class SearchLikeStore : IProjectionStore<Row>
    {
        public int Calls { get; private set; }
        public bool FailCount { get; init; }
        public int RowCount { get; init; } = 23;
        public long? ReportedCount { get; init; }
        public int ResultCap { get; init; } = int.MaxValue;
        public QueryOptions? LastOptions { get; private set; }
        public Task<Row?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpsertAsync(string id, Row projection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
        {
            Calls++;
            return FailCount ? Task.FromException<long>(new InvalidOperationException("count failed")) : Task.FromResult(ReportedCount ?? RowCount);
        }
        public Task<IReadOnlyList<Row>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
        {
            Calls++;
            LastOptions = options;
            var live = Enumerable.Range(1, RowCount);
            if (options?.Descending == true) { live = live.Reverse(); }
            return Task.FromResult<IReadOnlyList<Row>>(live.Skip(options?.Skip ?? 0).Take(Math.Min(options?.Take ?? 10, ResultCap)).Select(x => new Row(x)).ToList());
        }
    }
}
