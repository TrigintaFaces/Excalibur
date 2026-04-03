// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Elasticsearch;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchAuditSinkOptionsValidatorShould
{
	private readonly ElasticsearchAuditSinkOptionsValidator _validator = new();

	[Fact]
	public void PassWithValidNodeUrls()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			NodeUrls = ["https://node1:9200", "https://node2:9200"]
		});
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassWithValidSingleUrl()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "https://es.example.com:9200"
		});
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenNoUrlProvided()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions());
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("NodeUrls");
	}

	[Fact]
	public void FailWithInvalidNodeUrl()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			NodeUrls = ["not-a-url"]
		});
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("not a valid");
	}

	[Fact]
	public void FailWithEmptyNodeUrl()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			NodeUrls = [""]
		});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWithInvalidSingleUrl()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "not-a-url"
		});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWithNegativeRetryAttempts()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "https://es:9200",
			MaxRetryAttempts = -1
		});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWithNegativeRetryDelay()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "https://es:9200",
			RetryBaseDelay = TimeSpan.FromSeconds(-1)
		});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWithZeroTimeout()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "https://es:9200",
			Timeout = TimeSpan.Zero
		});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWithEmptyIndexPrefix()
	{
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			ElasticsearchUrl = "https://es:9200",
			IndexPrefix = ""
		});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void PreferNodeUrlsOverSingleUrl()
	{
		// Both set -- should pass (NodeUrls takes precedence)
		var result = _validator.Validate(null, new ElasticsearchAuditSinkOptions
		{
			NodeUrls = ["https://node1:9200"],
			ElasticsearchUrl = "https://single:9200"
		});
		result.Succeeded.ShouldBeTrue();
	}
}
