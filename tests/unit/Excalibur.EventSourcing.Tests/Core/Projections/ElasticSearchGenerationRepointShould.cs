// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json;
using Excalibur.Data.ElasticSearch.IndexManagement;
namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ElasticSearchGenerationRepointShould
{
    [Fact]
    public async Task PreserveConfigurationAndConditionTheRemovalOnTheExpectedTarget()
    {
        using var filter = JsonDocument.Parse("{\"term\":{\"tenant\":\"a\"}}");
        var manager = A.Fake<IIndexAliasManager>();
        A.CallTo(() => manager.GetAliasesAsync("read", A<CancellationToken>._)).Returns(
            [new AliasDefinition { AliasName = "read", Indices = ["old"], FilterJson = filter.RootElement,
                IndexRouting = "a", SearchRouting = "a,b", IsWriteIndex = false, IsHidden = true }]);
        AliasOperation[]? captured = null;
        A.CallTo(() => manager.UpdateAliasesAsync(A<IEnumerable<AliasOperation>>._, A<CancellationToken>._))
            .Invokes((IEnumerable<AliasOperation> operations, CancellationToken _) => captured = operations.ToArray())
            .Returns(true);
        await new AliasGenerationRepoint(manager, "read").RepointAsync("elasticsearch/old", "elasticsearch/new", CancellationToken.None);
        var operations = captured.ShouldNotBeNull();
        operations.Length.ShouldBe(2);
        operations[0].OperationType.ShouldBe(AliasOperationType.Remove);
        operations[0].IndexName.ShouldBe("old");
        operations[0].MustExist.ShouldBeTrue();
        operations[1].IndexName.ShouldBe("new");
        using var config = JsonDocument.Parse(operations[1].ConfigurationJson!.Value.GetRawText());
        config.RootElement.GetProperty("filter").GetProperty("term").GetProperty("tenant").GetString().ShouldBe("a");
        config.RootElement.GetProperty("index_routing").GetString().ShouldBe("a");
        config.RootElement.GetProperty("search_routing").GetString().ShouldBe("a,b");
        config.RootElement.GetProperty("is_write_index").GetBoolean().ShouldBeFalse();
        config.RootElement.GetProperty("is_hidden").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseUnknownOrAmbiguousAliasState(bool ambiguous)
    {
        var manager = A.Fake<IIndexAliasManager>();
        A.CallTo(() => manager.GetAliasesAsync("read", A<CancellationToken>._)).Returns(ambiguous
            ? [new AliasDefinition { AliasName = "read", Indices = ["old", "other"] }]
            : Array.Empty<AliasDefinition>());
        await Should.ThrowAsync<InvalidOperationException>(() => new AliasGenerationRepoint(manager, "read")
            .RepointAsync("elasticsearch/old", "elasticsearch/new", CancellationToken.None));
        A.CallTo(() => manager.UpdateAliasesAsync(A<IEnumerable<AliasOperation>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PropagateLookupFailureWithoutUpdatingTheAlias()
    {
        var manager = A.Fake<IIndexAliasManager>();
        A.CallTo(() => manager.GetAliasesAsync("read", A<CancellationToken>._)).ThrowsAsync(new HttpRequestException("unavailable"));
        await Should.ThrowAsync<HttpRequestException>(() => new AliasGenerationRepoint(manager, "read")
            .RepointAsync("elasticsearch/old", "elasticsearch/new", CancellationToken.None));
        A.CallTo(() => manager.UpdateAliasesAsync(A<IEnumerable<AliasOperation>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ReconcileAnAlreadyCompletedSwapWithoutWritingAgain()
    {
        var manager = A.Fake<IIndexAliasManager>();
        A.CallTo(() => manager.GetAliasesAsync("read", A<CancellationToken>._)).Returns(
            [new AliasDefinition { AliasName = "read", Indices = ["new"] }]);
        var repoint = new AliasGenerationRepoint(manager, "read");
        await repoint.RepointAsync("elasticsearch/old", "elasticsearch/new", CancellationToken.None);
        (await repoint.GetActiveDestinationAsync(CancellationToken.None)).ShouldBe("elasticsearch/new");
        A.CallTo(() => manager.UpdateAliasesAsync(A<IEnumerable<AliasOperation>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
