// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
namespace Excalibur.EventSourcing.Tests.Benchmarks;
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class BenchmarkCsvReportsShould
{
    [Theory]
    [InlineData("NA")]
    [InlineData("1.25 ms")]
    public void ReadActualMeanAfterQuotedCommaInArguments(string mean)
    {
        using var reader = new StringReader($"Method,Arguments,InvocationCount,Mean\nLoad,\"-m:1,/p:BuildExamplesAndTests=true\",1,{mean}\n");
        BenchmarkCsvReports.ReadMeans(reader).ShouldHaveSingleItem().ShouldBe(mean);
    }
    [Theory]
    [InlineData("Method,Error\nLoad,NA\n")]
    [InlineData("Mean,Mean\nNA,NA\n")]
    [InlineData("Method,Mean\nLoad\n")]
    [InlineData("Method,Mean\nLoad,\n")]
    [InlineData("Method,Mean\nLoad,NA,extra\n")]
    [InlineData("Method,Mean\n")]
    [InlineData("")]
    public void RejectIncompleteOrAmbiguousResults(string csv)
    {
        using var reader = new StringReader(csv);
        Should.Throw<InvalidDataException>(() => BenchmarkCsvReports.ReadMeans(reader));
    }
    [Theory]
    [InlineData("1.25 ms", true)]
    [InlineData("1,234.5 μs", true)]
    [InlineData("NA", false)]
    [InlineData("", false)]
    [InlineData("garbage", false)]
    [InlineData("NaN ms", false)]
    [InlineData("Infinity ms", false)]
    [InlineData("0 ms", false)]
    [InlineData("-1 ms", false)]
    [InlineData("1 parsecs", false)]
    public void RequireFinitePositiveDuration(string mean, bool expected) =>
        BenchmarkCsvReports.IsPositiveFiniteDuration(mean).ShouldBe(expected);
    [Fact]
    public void ReadEscapedQuotesAndEmbeddedNewline()
    {
        using var reader = new StringReader("Arguments,Mean\n\"a,\"\"b\"\"\nc\",\"1,234.5 μs\"\n");
        BenchmarkCsvReports.ReadMeans(reader).ShouldHaveSingleItem().ShouldBe("1,234.5 μs");
    }
    [Fact]
    public void RejectMalformedQuotedField()
    {
        using var reader = new StringReader("Arguments,Mean\n\"unterminated,NA");
        Should.Throw<Microsoft.VisualBasic.FileIO.MalformedLineException>(() => BenchmarkCsvReports.ReadMeans(reader));
    }
}
