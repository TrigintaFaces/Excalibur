// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Net;

using Google;
using Google.Apis.Auth.OAuth2;

using Excalibur.EventSourcing.Gcs;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.TieredStorage;

[Trait("Category", "Integration")]
[Trait("Component", "TieredStorage")]
[Trait("Pattern", "EventSourcing")]
public sealed class GcsColdArchiveMigrationHttpShould
{
	// Python gzip.compress(b'123456789', mtime=0); CRC32C fixture is independent of the production helper.
	private static readonly byte[] Compressed = Convert.FromBase64String("H4sIAAAAAAAC/zM0MjYxNTO3sAQAJjn0ywkAAAA=");
	private const string Metadata = """{"bucket":"bucket","name":"key","generation":"42","size":"29","crc32c":"c3vXZg=="}""";

	[Theory]
	[InlineData("original")]
	[InlineData("no-media-checksum")]
	[InlineData("transformed")]
	[InlineData("same-length-corruption")]
	[InlineData("different-generation")]
	[InlineData("disappeared")]
	public async Task BindRawMediaToTheObservedMetadataThroughActualSdk(string scenario)
	{
		var requests = new ConcurrentQueue<(string Generation, string Condition, string Encoding, string Authorization)>();
		var builder = WebApplication.CreateBuilder();
		builder.Logging.ClearProviders();
		builder.WebHost.UseUrls("http://127.0.0.1:0");
		await using var server = builder.Build();
		server.Run(async context =>
		{
			if (context.Request.Query["alt"] != "media")
			{
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(Metadata, context.RequestAborted);
				return;
			}

			requests.Enqueue((context.Request.Query["generation"].ToString(), context.Request.Query["ifGenerationMatch"].ToString(),
				context.Request.Headers.AcceptEncoding.ToString(), context.Request.Headers.Authorization.ToString()));
			if (scenario == "disappeared")
			{
				context.Response.StatusCode = 404;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync("""{"error":{"code":404,"message":"Generation no longer exists"}}""", context.RequestAborted);
				return;
			}

			context.Response.Headers["x-goog-generation"] = scenario == "different-generation" ? "43" : "42";
			context.Response.Headers["x-goog-metageneration"] = "1";
			if (scenario == "original")
			{
				context.Response.Headers["x-goog-hash"] = "crc32c=c3vXZg==";
			}

			context.Response.ContentType = "application/octet-stream";
			if (scenario == "transformed")
			{
				context.Response.Headers["x-goog-stored-content-encoding"] = "gzip";
				await context.Response.Body.WriteAsync("123456789"u8.ToArray(), context.RequestAborted);
			}
			else
			{
				context.Response.Headers.ContentEncoding = "gzip";
				context.Response.ContentLength = Compressed.Length;
				var bytes = Compressed.ToArray();
				if (scenario == "same-length-corruption")
				{
					bytes[^1] ^= 1;
				}

				await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
			}
		});
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await server.StartAsync(timeout.Token);
		var rawBuilder = new GcsRawStorageClientBuilder
		{
			BaseUri = server.Urls.Single() + "/storage/v1/",
		};
		if (scenario == "original")
		{
			rawBuilder.Credential = GoogleCredential.FromAccessToken("integration-test-token");
		}
		else
		{
			rawBuilder.ApiKey = "integration-test-key";
		}

		using var client = rawBuilder.Build();
		var adapter = new GcsColdArchiveMigrationStorage(client, "bucket", "events");
		if (scenario == "disappeared")
		{
			var error = await Should.ThrowAsync<GoogleApiException>(() => adapter.ReadAsync("key", timeout.Token));
			error.HttpStatusCode.ShouldBe(HttpStatusCode.NotFound);
		}
		else if (scenario is "transformed" or "different-generation" or "same-length-corruption")
		{
			await Should.ThrowAsync<InvalidDataException>(() => adapter.ReadAsync("key", timeout.Token));
		}
		else
		{
			var archive = (await adapter.ReadAsync("key", timeout.Token)).ShouldNotBeNull();
			archive.CopyContent().ShouldBe(Compressed);
			archive.Revision.ShouldBe("42");
		}

		var request = requests.ShouldHaveSingleItem();
		request.Generation.ShouldBe("42");
		request.Condition.ShouldBe("42");
		request.Encoding.ShouldContain("gzip");
		if (scenario == "original")
		{
			request.Authorization.ShouldBe("Bearer integration-test-token");
		}
		await server.StopAsync(timeout.Token);
	}
}
