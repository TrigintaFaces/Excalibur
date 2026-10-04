// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Excalibur.Benchmarks;

internal static class BenchmarkExecution
{
	internal static int Run(Assembly assembly, string[] args)
	{
		var summaries = BenchmarkSwitcher.FromAssembly(assembly).Run(args).ToArray();
		if (summaries.Length == 0 && args.Any(arg => arg is "--help" or "-h" or "--list" or "--info"))
		{
			return 0;
		}

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
}
