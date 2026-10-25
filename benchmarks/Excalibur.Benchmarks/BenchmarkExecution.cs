// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Excalibur.Benchmarks;

internal static class BenchmarkExecution
{
	internal static int Run(Assembly assembly, string[] args) => Run(assembly, args, out _);

	/// <summary>Runs the benchmarks and reports WHY the run was rejected, if it was.</summary>
	/// <remarks>
	/// The diagnosis exists because the exit code alone is unusable as evidence. GetExitCode collapses
	/// eight distinct conditions into 0-or-1, so a failing benchmark gate told its caller only "should be
	/// 0" -- no benchmark name, no failing clause, nothing to act on. Three tests failed that way and none
	/// of them could say why.
	/// </remarks>
	internal static int Run(Assembly assembly, string[] args, out string diagnosis)
	{
		var summaries = BenchmarkSwitcher.FromAssembly(assembly).Run(args).ToArray();
		if (summaries.Length == 0 && args.Any(arg => arg is "--help" or "-h" or "--list" or "--info"))
		{
			diagnosis = "informational invocation; no run expected";
			return 0;
		}

		diagnosis = Describe(summaries);
		return GetExitCode(summaries);
	}

	internal static int GetExitCode(IEnumerable<Summary> summaries)
	{
		var results = summaries.ToArray();
		return results.Length > 0 && results.All(summary =>
			!summary.HasCriticalValidationErrors && summary.ValidationErrors.IsEmpty && !summary.Reports.IsEmpty &&
			summary.Reports.All(report => report.Success && report.GetResultRuns().Any() &&
				report.ExecuteResults.Count > 0 && report.ExecuteResults.All(execution =>
					execution.FoundExecutable && execution.ExitCode == 0 && execution.IsSuccess && execution.Errors.Count == 0)))
			? 0 : 1;
	}

	/// <summary>Names every clause of <see cref="GetExitCode"/> that a run failed, and for which report.</summary>
	/// <param name="summaries">The summaries to describe.</param>
	/// <returns>A human-readable account of why the run would be rejected, or that it is clean.</returns>
	internal static string Describe(IEnumerable<Summary> summaries)
	{
		var results = summaries.ToArray();
		if (results.Length == 0)
		{
			return "no summaries were produced: BenchmarkSwitcher matched no benchmark, or the host refused to run one";
		}

		var faults = new List<string>();
		foreach (var summary in results)
		{
			if (summary.HasCriticalValidationErrors)
			{
				faults.Add("critical validation errors");
			}

			foreach (var error in summary.ValidationErrors)
			{
				faults.Add($"validation{(error.IsCritical ? " (critical)" : string.Empty)}: {error.Message}");
			}

			if (summary.Reports.IsEmpty)
			{
				faults.Add("no reports");
				continue;
			}

			foreach (var report in summary.Reports)
			{
				var name = report.BenchmarkCase.Descriptor.WorkloadMethod.Name;
				if (!report.Success)
				{
					faults.Add($"{name}: report not successful");
				}

				if (!report.GetResultRuns().Any())
				{
					faults.Add($"{name}: no result runs");
				}

				if (report.ExecuteResults.Count == 0)
				{
					faults.Add($"{name}: no execute results");
				}

				foreach (var execution in report.ExecuteResults)
				{
					if (!execution.FoundExecutable)
					{
						faults.Add($"{name}: executable not found");
					}

					if (execution.ExitCode != 0)
					{
						faults.Add($"{name}: host exit code {execution.ExitCode}");
					}

					if (!execution.IsSuccess)
					{
						faults.Add($"{name}: execution not successful");
					}

					foreach (var error in execution.Errors)
					{
						faults.Add($"{name}: {error}");
					}
				}
			}
		}

		return faults.Count == 0
			? $"clean: {results.Length} summary(ies), {results.Sum(s => s.Reports.Length)} report(s)"
			: string.Join(" || ", faults);
	}
}
