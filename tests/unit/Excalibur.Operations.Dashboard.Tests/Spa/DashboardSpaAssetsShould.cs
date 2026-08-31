// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Operations.Dashboard.Spa;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

using Shouldly;

using Xunit;

namespace Excalibur.Operations.Dashboard.Tests.Spa;

/// <summary>
/// Locks for the embedded SPA serving path: what each response says about itself, and what a request for
/// something that is not there gets back.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DashboardSpaAssetsShould
{
	private const string ExpectedCsp =
		"default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
		"connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

	private static (DashboardSpaAssets Assets, DefaultHttpContext Context, MemoryStream Body) Arrange(
		params (string Path, string Content)[] files)
	{
		var provider = new InMemoryFileProvider(files);
		var context = new DefaultHttpContext();
		var body = new MemoryStream();
		context.Response.Body = body;

		return (new DashboardSpaAssets(provider), context, body);
	}

	private static string BodyText(MemoryStream body) => Encoding.UTF8.GetString(body.ToArray());

	// ---- assets -------------------------------------------------------------------------------------

	[Fact]
	public async Task ServeAHashedAssetWithItsContentAndContentType()
	{
		var (assets, context, body) = Arrange(("assets/index-DkR2b8Qa.js", "export const x = 1;"));

		await assets.ServeAssetAsync(context, "index-DkR2b8Qa.js");

		context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
		context.Response.ContentType.ShouldBe("text/javascript; charset=utf-8");
		BodyText(body).ShouldBe("export const x = 1;");
	}

	/// <summary>
	/// Hashed asset names change whenever their content does, so the response may be cached indefinitely.
	/// This is what keeps the dashboard from refetching its whole bundle on every load.
	/// </summary>
	[Fact]
	public async Task CacheAHashedAssetImmutably()
	{
		var (assets, context, _) = Arrange(("assets/app-abc123.css", "body{}"));

		await assets.ServeAssetAsync(context, "app-abc123.css");

		context.Response.Headers.CacheControl.ToString()
			.ShouldBe("public, max-age=31536000, immutable");
	}

	/// <summary>
	/// The dashboard exposes operational data, so its hardening headers are part of the contract rather than
	/// a deployment detail — an operator cannot add them from outside an embedded, self-served SPA.
	/// </summary>
	[Fact]
	public async Task SendTheHardeningHeadersOnAnAssetResponse()
	{
		var (assets, context, _) = Arrange(("assets/app-abc123.css", "body{}"));

		await assets.ServeAssetAsync(context, "app-abc123.css");

		context.Response.Headers["Content-Security-Policy"].ToString().ShouldBe(ExpectedCsp);
		context.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
		context.Response.Headers["Referrer-Policy"].ToString().ShouldBe("no-referrer");
	}

	/// <summary>
	/// The CSP admits no inline or eval'd script. That is the whole reason the build emits an external
	/// module script and an external stylesheet, and it is the property most easily lost to a later
	/// convenience.
	/// </summary>
	[Fact]
	public async Task SendAContentSecurityPolicyThatAdmitsNoInlineOrEvaluatedCode()
	{
		var (assets, context, _) = Arrange(("index.html", "<!doctype html>"));

		await assets.ServeIndexAsync(context);

		var csp = context.Response.Headers["Content-Security-Policy"].ToString();
		csp.ShouldNotContain("unsafe-inline");
		csp.ShouldNotContain("unsafe-eval");
		csp.ShouldContain("frame-ancestors 'none'");
		csp.ShouldContain("default-src 'none'");
	}

	/// <summary>
	/// A missing asset is a 404 and never the SPA document. Falling back here would serve HTML in answer to
	/// a request for a script — which, under <c>nosniff</c>, fails confusingly, and without it is worse.
	/// </summary>
	[Fact]
	public async Task Answer404ForAMissingAsset_WithoutFallingBackToTheIndexDocument()
	{
		var (assets, context, body) = Arrange(("index.html", "<!doctype html>"));

		await assets.ServeAssetAsync(context, "does-not-exist.js");

		context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
		BodyText(body).ShouldBeEmpty();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("nested/")]
	public async Task Answer404ForAPathThatNamesNoFile(string? path)
	{
		var (assets, context, body) = Arrange(("assets/nested/app.js", "x"));

		await assets.ServeAssetAsync(context, path);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
		BodyText(body).ShouldBeEmpty();
	}

	/// <summary>
	/// A directory is not a file. Serving one would produce a zero-length 200 that a client reads as a
	/// successfully-fetched empty asset.
	/// </summary>
	[Fact]
	public async Task Answer404ForADirectoryEntry()
	{
		var provider = new InMemoryFileProvider([]);
		provider.AddDirectory("assets/nested");
		var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

		await new DashboardSpaAssets(provider).ServeAssetAsync(context, "nested");

		context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
	}

	/// <summary>
	/// Assets are resolved under <c>assets/</c> and nowhere else, so a request cannot reach a sibling
	/// embedded resource by naming it directly.
	/// </summary>
	[Fact]
	public async Task ResolveAssetsOnlyBeneathTheAssetsDirectory()
	{
		var (assets, context, body) = Arrange(("index.html", "<!doctype html>"));

		await assets.ServeAssetAsync(context, "index.html");

		context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
		BodyText(body).ShouldBeEmpty();
	}

	// ---- index --------------------------------------------------------------------------------------

	[Fact]
	public async Task ServeTheIndexDocumentAsHtml()
	{
		var (assets, context, body) = Arrange(("index.html", "<!doctype html><title>ops</title>"));

		await assets.ServeIndexAsync(context);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
		context.Response.ContentType.ShouldBe("text/html; charset=utf-8");
		BodyText(body).ShouldBe("<!doctype html><title>ops</title>");
	}

	/// <summary>
	/// The entry document is the one file whose name never changes, so it must not be cached the way the
	/// hashed assets are — a deployed dashboard that keeps serving yesterday's document would keep
	/// requesting asset names that no longer exist.
	/// </summary>
	[Fact]
	public async Task ServeTheIndexDocumentWithoutCaching()
	{
		var (assets, context, _) = Arrange(("index.html", "<!doctype html>"));

		await assets.ServeIndexAsync(context);

		context.Response.Headers.CacheControl.ToString().ShouldBe("no-cache");
	}

	[Fact]
	public async Task Answer404WhenTheIndexDocumentIsAbsent()
	{
		var (assets, context, body) = Arrange();

		await assets.ServeIndexAsync(context);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
		BodyText(body).ShouldBeEmpty();
	}

	// ---- guards -------------------------------------------------------------------------------------

	[Fact]
	public void Reject_ANullFileProvider()
		=> Should.Throw<ArgumentNullException>(() => new DashboardSpaAssets(null!));

	[Fact]
	public async Task Reject_ANullContext()
	{
		var (assets, _, _) = Arrange(("index.html", "x"));

		_ = await Should.ThrowAsync<ArgumentNullException>(async () => await assets.ServeIndexAsync(null!));
		_ = await Should.ThrowAsync<ArgumentNullException>(async () => await assets.ServeAssetAsync(null!, "a.js"));
	}

	// ---- fake ---------------------------------------------------------------------------------------

	/// <summary>
	/// An in-memory <see cref="IFileProvider"/> standing in for the manifest-embedded one. It implements the
	/// interface directly rather than deriving from a framework provider, so the assertions bind
	/// <see cref="DashboardSpaAssets"/>'s own handling of exists/is-directory rather than a base class's.
	/// </summary>
	private sealed class InMemoryFileProvider : IFileProvider
	{
		private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
		private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

		public InMemoryFileProvider(IEnumerable<(string Path, string Content)> files)
		{
			foreach (var (path, content) in files)
			{
				_files[path] = Encoding.UTF8.GetBytes(content);
			}
		}

		public void AddDirectory(string path) => _directories.Add(path);

		public IFileInfo GetFileInfo(string subpath)
		{
			if (_files.TryGetValue(subpath, out var bytes))
			{
				return new InMemoryFileInfo(subpath, bytes, isDirectory: false);
			}

			return _directories.Contains(subpath)
				? new InMemoryFileInfo(subpath, [], isDirectory: true)
				: new NotFoundFileInfo(subpath);
		}

		public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

		public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

		private sealed class InMemoryFileInfo(string subpath, byte[] bytes, bool isDirectory) : IFileInfo
		{
			public bool Exists => true;

			public long Length => bytes.Length;

			public string? PhysicalPath => null;

			public string Name => subpath;

			public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

			public bool IsDirectory => isDirectory;

			public Stream CreateReadStream() => new MemoryStream(bytes, writable: false);
		}
	}
}
