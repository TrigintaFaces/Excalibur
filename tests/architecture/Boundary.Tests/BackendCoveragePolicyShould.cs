// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class BackendCoveragePolicyShould
{
    [Fact]
    public void FailNewAndStaleDebtIndependently()
    {
        var verdict = CoverageLedger.Evaluate(["Contract|New"], ["Contract|Old"], 14);
        verdict.Passed.ShouldBeFalse();
        verdict.NewContracts.ShouldBe(["Contract|New"]);
        verdict.StaleExceptions.ShouldBe(["Contract|Old"]);
    }

    [Fact]
    public void AcceptAnEmptyLedgerOnlyWhenTheMeasuredGapSetIsEmpty() =>
        CoverageLedger.Evaluate([], [], 14).Passed.ShouldBeTrue();

    [Theory]
    [InlineData("Contract|One\nContract|One")]
    [InlineData("Unqualified")]
    [InlineData("Contract|One\nContract|Two")]
    public void RefuseMalformedDuplicateOrExpandedDebt(string text) =>
        Should.Throw<InvalidOperationException>(() => CoverageLedger.Evaluate([], text.Split('\n'), 1));
}
