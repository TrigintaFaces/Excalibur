// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Benchmarks;
using Excalibur.Benchmarks.EventSourcing;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Collection("BenchmarkValidationLifecycle")]
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerBenchmarkConfigurationShould
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FailSetupForMissingOrBlankSqlConfiguration(string? configuration)
    {
        var previous = Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");
        try
        {
            Environment.SetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING", configuration);
            var benchmark = new SqlServerEventStoreBenchmarks();

            var error = await Should.ThrowAsync<InvalidOperationException>(benchmark.GlobalSetup);
            error.Message.ShouldContain("BENCHMARK_SQL_CONNECTIONSTRING");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING", previous);
        }
    }

    [Fact]
    public void RejectActualRunnerResultsWithoutSqlConfiguration()
    {
        var previous = Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING");
        try
        {
            Environment.SetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING", null);
            var artifacts = Path.Combine(Path.GetTempPath(), "sxl9s4-bdn", Guid.NewGuid().ToString("N"));
            BenchmarkExecution.Run(typeof(SqlServerEventStoreBenchmarks).Assembly, [
                "--filter", "*SqlServerEventStoreBenchmarks*", "--artifacts", artifacts,
                "--warmupCount", "1", "--iterationCount", "1",
            ]).ShouldBe(1);

            var reports = Directory.GetFiles(Path.Combine(artifacts, "results"), "*-report.csv");
            reports.ShouldNotBeEmpty();
            foreach (var report in reports)
            {
                // BDN may retain named failure placeholders; none may carry a numeric mean.
                var rows = File.ReadAllLines(report);
                rows.Length.ShouldBeGreaterThan(1);
                var meanIndex = Array.IndexOf(rows[0].Split(','), "Mean");
                meanIndex.ShouldBeGreaterThanOrEqualTo(0);
                foreach (var row in rows.Skip(1))
                {
                    row.Split(',')[meanIndex].ShouldBe("NA");
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING", previous);
        }
    }
}
