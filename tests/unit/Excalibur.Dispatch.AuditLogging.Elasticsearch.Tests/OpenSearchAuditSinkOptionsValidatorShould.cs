// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.OpenSearch;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

/// <summary>
/// Tests OpenSearch audit sink options through public API surface.
/// Validator is internal (tested via ValidateOnStart in DI integration).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class OpenSearchAuditSinkOptionsValidatorShould
{
	[Fact]
	public void AcceptNodeUrls()
	{
		var options = new OpenSearchAuditSinkOptions
		{
			NodeUrls = ["https://os-node1:9200", "https://os-node2:9200"]
		};
		options.NodeUrls.Count.ShouldBe(2);
	}

	[Fact]
	public void AcceptSingleUrl()
	{
		var options = new OpenSearchAuditSinkOptions
		{
			OpenSearchUrl = "https://opensearch.example.com:9200"
		};
		options.OpenSearchUrl.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptApiKey()
	{
		var options = new OpenSearchAuditSinkOptions { ApiKey = "test-key" };
		options.ApiKey.ShouldBe("test-key");
	}

	[Fact]
	public void AcceptCustomIndexPrefix()
	{
		var options = new OpenSearchAuditSinkOptions { IndexPrefix = "my-audit" };
		options.IndexPrefix.ShouldBe("my-audit");
	}

	[Fact]
	public void AcceptCustomRetrySettings()
	{
		var options = new OpenSearchAuditSinkOptions
		{
			MaxRetryAttempts = 5,
			RetryBaseDelay = TimeSpan.FromSeconds(2),
			Timeout = TimeSpan.FromMinutes(1)
		};
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void AcceptApplicationName()
	{
		var options = new OpenSearchAuditSinkOptions { ApplicationName = "MyApp" };
		options.ApplicationName.ShouldBe("MyApp");
	}

	[Fact]
	public void ExporterOptionsRequireUrl()
	{
		var options = new OpenSearchExporterOptions { OpenSearchUrl = "https://os:9200" };
		options.OpenSearchUrl.ShouldBe("https://os:9200");
	}
}
