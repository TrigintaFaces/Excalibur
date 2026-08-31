// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Xml.Linq;

namespace Boundary.Tests;

/// <summary>
/// A fenced block in the published docs that reads as verbatim framework output is a claim about the
/// shipped strings. When the strings are reworded and the block is not, the docs teach a consumer to
/// search their logs for text the framework never emits, and the divergence is invisible because
/// nothing compiles the documentation.
/// </summary>
/// <remarks>
/// This guard pins the direction that matters: the resource is the source of truth, and the published
/// block must contain what it says. It deliberately compares the literal, non-placeholder segments
/// rather than the whole format string, because the runtime substitutes the placeholders.
/// </remarks>
[Trait("Category", "Architecture")]
[Trait("Component", "Platform")]
public sealed class PublishedErrorTextMatchesShippedResourcesShould
{
	private const string ResxRelativePath = "src/Dispatch/Excalibur.Dispatch/Resources.resx";
	private const string DocRelativePath = "docs-site/docs/pipeline/profiles.md";

	private static string ReadRepoFile(string relativePath)
	{
		var path = Path.Combine(
			TestHelpers.GetRepositoryRoot(),
			relativePath.Replace('/', Path.DirectorySeparatorChar));
		File.Exists(path).ShouldBeTrue($"expected the file to exist: {relativePath}");
		return File.ReadAllText(path);
	}

	private static string ResourceValue(string name)
	{
		var resx = XDocument.Parse(ReadRepoFile(ResxRelativePath));
		var value = resx.Root?.Elements("data")
			.FirstOrDefault(d => (string?)d.Attribute("name") == name)
			?.Element("value")?.Value;

		value.ShouldNotBeNullOrWhiteSpace(
			$"the resource '{name}' is missing from {ResxRelativePath}; this guard cannot compare " +
			"against a string that is not there, and an absent resource is NOT a pass.");
		return value!;
	}

	/// <summary>
	/// Vacuity control. Both inputs must be real and non-trivial before any verdict below is read: an
	/// empty doc and an empty resource agree with each other perfectly.
	/// </summary>
	[Fact]
	public void ReadBothSourcesBeforeComparingThem()
	{
		ReadRepoFile(DocRelativePath).Length.ShouldBeGreaterThan(1_000);
		ResourceValue("PipelineBuilder_RequiredMiddlewareUnresolvedFormat").Length.ShouldBeGreaterThan(50);
	}

	[Fact]
	public void PublishTheHowToFixSentenceTheFrameworkActuallyEmits()
	{
		// The literal tail of the format string, after the last placeholder.
		var format = ResourceValue("PipelineBuilder_RequiredMiddlewareUnresolvedFormat");
		var literalTail = format[(format.LastIndexOf("{2}", StringComparison.Ordinal) + 3)..];

		ReadRepoFile(DocRelativePath).ShouldContain(
			literalTail,
			Case.Sensitive,
			$"{DocRelativePath} publishes a Build() failure block that reads as verbatim output. The " +
			$"shipped resource now says:\n\n{literalTail}\n\nUpdate the published block to match, or " +
			"stop presenting it as verbatim.");
	}

	[Fact]
	public void PublishTheUnregisteredMiddlewareReasonTheFrameworkActuallyEmits()
	{
		ReadRepoFile(DocRelativePath).ShouldContain(
			ResourceValue("PipelineBuilder_MiddlewareNotRegistered"),
			Case.Sensitive,
			$"{DocRelativePath} shows a per-entry reason that no longer matches the shipped string.");
	}
}
