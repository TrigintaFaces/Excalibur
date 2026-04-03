// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Elasticsearch;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchAuditSinkOptionsShould
{
	[Fact]
	public void HaveSensibleDefaults()
	{
		var options = new ElasticsearchAuditSinkOptions();

		options.IndexPrefix.ShouldBe("dispatch-audit");
		options.RefreshPolicy.ShouldBe("false");
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.NodeUrls.ShouldBeNull();
		options.ElasticsearchUrl.ShouldBeNull();
		options.ApiKey.ShouldBeNull();
		options.ApplicationName.ShouldBeNull();
	}

	[Fact]
	public void AcceptNodeUrls()
	{
		var options = new ElasticsearchAuditSinkOptions
		{
			NodeUrls = ["https://node1:9200", "https://node2:9200"]
		};

		options.NodeUrls.Count.ShouldBe(2);
	}

	[Fact]
	public void AcceptSingleUrl()
	{
		var options = new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "https://es.example.com:9200"
		};

		options.ElasticsearchUrl.ShouldBe("https://es.example.com:9200");
	}
}
