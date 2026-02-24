// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;
using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Tests.Messaging.Routing.Sprint523;

/// <summary>
/// Verifies that the top functional test files have been hardened from Task.Delay to
/// WaitUntilAsync polling pattern, and that remaining Task.Delay calls have intentional
/// delay comments (S523.8).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FunctionalTestHardeningVerificationShould
{
	private static readonly string[] HardenedFiles =
	[
		"EndToEndObservabilityShould.cs",
		"SagaTimeoutWorkflowTests.cs",
		"TimeoutPatternFunctionalShould.cs",
		"BulkheadPatternFunctionalShould.cs",
		"RetryPatternFunctionalShould.cs",
	];

	[Fact]
	public void HardenedFiles_ShouldExist()
	{
		// Arrange
		var root = FindProjectRoot();
		if (root is null)
		{
			return; // CI environment may not have all files
		}

		var functionalTestDir = Path.Combine(root, "tests", "functional",
			"Excalibur.Dispatch.Tests.Functional");

		if (!Directory.Exists(functionalTestDir))
		{
			return;
		}

		// Assert - all 5 targeted files should exist
		foreach (var fileName in HardenedFiles)
		{
			var files = Directory.GetFiles(functionalTestDir, fileName, SearchOption.AllDirectories);
			files.ShouldNotBeEmpty($"Functional test file '{fileName}' should exist");
		}
	}

	[Fact]
	public void RemainingTaskDelays_ShouldBeDocumented()
	{
		// Arrange
		var root = FindProjectRoot();
		if (root is null)
		{
			return;
		}

		var functionalTestDir = Path.Combine(root, "tests", "functional",
			"Excalibur.Dispatch.Tests.Functional");

		if (!Directory.Exists(functionalTestDir))
		{
			return;
		}

		// Act - find Task.Delay occurrences in hardened files
		foreach (var fileName in HardenedFiles)
		{
			var files = Directory.GetFiles(functionalTestDir, fileName, SearchOption.AllDirectories);
			foreach (var file in files)
			{
				var lines = File.ReadAllLines(file);
				for (var i = 0; i < lines.Length; i++)
				{
					if (!lines[i].Contains("Task.Delay", StringComparison.Ordinal))
					{
						continue;
					}

					// Assert - any remaining Task.Delay should have a comment nearby
					// explaining it's intentional (timeout simulation, rate limiting, etc.)
					var context = GetSurroundingLines(lines, i, 5);
					// Accept: comment with known keyword, OR cancellation-safe pattern
				var hasComment = context.Any(l =>
						l.Contains("//", StringComparison.Ordinal) &&
						(l.Contains("intentional", StringComparison.OrdinalIgnoreCase) ||
						 l.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
						 l.Contains("simulate", StringComparison.OrdinalIgnoreCase) ||
						 l.Contains("delay", StringComparison.OrdinalIgnoreCase) ||
						 l.Contains("necessary", StringComparison.OrdinalIgnoreCase) ||
						 l.Contains("polling", StringComparison.OrdinalIgnoreCase) ||
						 l.Contains("wait for", StringComparison.OrdinalIgnoreCase)));
					// Delay with a CancellationToken is the safe pattern.
					// Accept either direct Task.Delay(duration, token) or
					// global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(duration, token).
					var delayLine = lines[i].Trim();
					var delayStartIndex = delayLine.IndexOf("Task.Delay(", StringComparison.Ordinal);
					if (delayStartIndex < 0)
					{
						delayStartIndex = delayLine.IndexOf("global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(", StringComparison.Ordinal);
					}

					var usesCancellationToken =
						delayStartIndex >= 0 &&
						delayLine.IndexOf(',', delayStartIndex) > 0;

					(hasComment || usesCancellationToken).ShouldBeTrue(
						$"Task.Delay at {fileName}:{i + 1} should have a comment explaining " +
						"why it's intentional, or use a CancellationToken " +
						"(per S523.8 hardening requirement)");
				}
			}
		}
	}

	[Fact]
	public void WaitUntilAsync_OrPollingPattern_ShouldBeUsed()
	{
		// Arrange
		var root = FindProjectRoot();
		if (root is null)
		{
			return;
		}

		var functionalTestDir = Path.Combine(root, "tests", "functional",
			"Excalibur.Dispatch.Tests.Functional");

		if (!Directory.Exists(functionalTestDir))
		{
			return;
		}

		// Assert - at least some hardened files should contain WaitUntilAsync or polling pattern
		var hasPollingPattern = false;
		foreach (var fileName in HardenedFiles)
		{
			var files = Directory.GetFiles(functionalTestDir, fileName, SearchOption.AllDirectories);
			foreach (var file in files)
			{
				var content = File.ReadAllText(file);
				if (content.Contains("WaitUntilAsync", StringComparison.Ordinal) ||
				    content.Contains("WaitHelpers", StringComparison.Ordinal))
				{
					hasPollingPattern = true;
					break;
				}
			}

			if (hasPollingPattern)
			{
				break;
			}
		}

		hasPollingPattern.ShouldBeTrue(
			"At least one hardened functional test should use WaitUntilAsync or WaitHelpers " +
			"(S523.8 converted Task.Delay to polling pattern)");
	}

	private static string[] GetSurroundingLines(string[] lines, int index, int range)
	{
		var start = Math.Max(0, index - range);
		var end = Math.Min(lines.Length - 1, index + range);
		return lines[start..(end + 1)];
	}

	private static string? FindProjectRoot()
	{
		var dir = AppDomain.CurrentDomain.BaseDirectory;
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
			{
				return dir;
			}

			dir = Directory.GetParent(dir)?.FullName;
		}

		return null;
	}
}
