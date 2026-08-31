// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Operations.Dashboard.Spa;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests.Spa;

/// <summary>
/// Locks for the embedded SPA's content-type table.
/// </summary>
/// <remarks>
/// A wrong content type on this path is not cosmetic. The dashboard is served with
/// <c>X-Content-Type-Options: nosniff</c>, so a browser will not rescue a mislabelled response: a stylesheet
/// or module script announced as <c>application/octet-stream</c> is refused outright and the page renders
/// unstyled or not at all.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DashboardSpaContentTypesShould
{
	[Theory]
	[InlineData("index.html", "text/html; charset=utf-8")]
	[InlineData("app.js", "text/javascript; charset=utf-8")]
	[InlineData("app.mjs", "text/javascript; charset=utf-8")]
	[InlineData("app.css", "text/css; charset=utf-8")]
	[InlineData("data.json", "application/json; charset=utf-8")]
	[InlineData("site.webmanifest", "application/manifest+json; charset=utf-8")]
	[InlineData("icon.svg", "image/svg+xml")]
	[InlineData("icon.png", "image/png")]
	[InlineData("favicon.ico", "image/x-icon")]
	[InlineData("font.woff2", "font/woff2")]
	[InlineData("font.woff", "font/woff")]
	[InlineData("app.js.map", "application/json; charset=utf-8")]
	[InlineData("robots.txt", "text/plain; charset=utf-8")]
	public void MapEachAssetExtensionTheBuildEmits(string path, string expected)
		=> DashboardSpaContentTypes.ForPath(path).ShouldBe(expected);

	/// <summary>
	/// Extension casing is not something the serving path controls — it comes from whatever the build
	/// emitted and whatever the client asked for.
	/// </summary>
	[Theory]
	[InlineData("APP.CSS")]
	[InlineData("App.Css")]
	[InlineData("app.cSs")]
	public void MatchExtensionsWithoutRegardToCase(string path)
		=> DashboardSpaContentTypes.ForPath(path).ShouldBe("text/css; charset=utf-8");

	/// <summary>
	/// The safety arm: anything not on the list is declared as opaque bytes rather than guessed at. Guessing
	/// here is how a served file gets interpreted as something more dangerous than it is.
	/// </summary>
	[Theory]
	[InlineData("archive.zip")]
	[InlineData("script.php")]
	[InlineData("page.htm")]
	[InlineData("noextension")]
	[InlineData("")]
	[InlineData("trailing.")]
	public void FallBackToOpaqueBytesForAnythingUnrecognised(string path)
		=> DashboardSpaContentTypes.ForPath(path).ShouldBe("application/octet-stream");

	/// <summary>
	/// Only the final segment's extension counts. A directory containing a dot must not lend its suffix to a
	/// file that has none.
	/// </summary>
	[Fact]
	public void IgnoreADotInADirectorySegment()
		=> DashboardSpaContentTypes.ForPath("v1.2/bundle").ShouldBe("application/octet-stream");

	/// <summary>
	/// The hashed file names the build actually emits carry two dots. This is the real shape of an asset
	/// request, and it must resolve on the last extension rather than the hash.
	/// </summary>
	[Fact]
	public void ResolveHashedAssetNamesOnTheirFinalExtension()
		=> DashboardSpaContentTypes.ForPath("index-DkR2b8Qa.js").ShouldBe("text/javascript; charset=utf-8");
}
