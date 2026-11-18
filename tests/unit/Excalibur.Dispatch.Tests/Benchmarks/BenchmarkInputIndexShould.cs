// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Performance.Benchmarks;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Benchmarks")]
[Trait("Pattern", "Regression")]
public sealed class BenchmarkInputIndexShould
{
    [Theory]
    [InlineData(int.MinValue, "string")]
    [InlineData(int.MinValue + 1, "int")]
    [InlineData(-4, "string")]
    [InlineData(-3, "int")]
    [InlineData(-2, "payload")]
    [InlineData(-1, "guid")]
    [InlineData(0, "string")]
    [InlineData(1, "int")]
    [InlineData(2, "payload")]
    [InlineData(3, "guid")]
    [InlineData(4, "string")]
    [InlineData(int.MaxValue, "guid")]
    public void Select_the_expected_factory_at_signed_clock_boundaries(int tickCount, string expected)
    {
        string[] factories = ["string", "int", "payload", "guid"];
        var index = BenchmarkInputIndex.FromTickCount(tickCount, factories.Length);
        factories[index].ShouldBe(expected);
    }
}
