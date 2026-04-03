// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.OpenSearch;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class OpenSearchAuditSinkOptionsShould
{
	[Fact]
	public void HaveSensibleDefaults()
	{
		var options = new OpenSearchAuditSinkOptions();

		options.IndexPrefix.ShouldBe("dispatch-audit");
		options.RefreshPolicy.ShouldBe("false");
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.NodeUrls.ShouldBeNull();
		options.OpenSearchUrl.ShouldBeNull();
	}
}
