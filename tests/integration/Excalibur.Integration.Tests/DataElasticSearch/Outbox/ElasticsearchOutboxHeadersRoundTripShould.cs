// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.DataElasticSearch.Outbox;

[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "Elasticsearch")]
public sealed class ElasticsearchOutboxHeadersRoundTripShould : ElasticsearchIntegrationTestBase
{
	[Fact]
	public async Task PreserveMessageHeadersExactly_OnPersistThenReloadOnAFreshStoreInstance()
	{
		ConnectionString.ShouldNotBeNullOrWhiteSpace(
			"a real Elasticsearch (TestContainers) is required -- this engage-test is never skipped");

		var indexName = $"{TestIndexPrefix}outbox-headers";
		CreatedIndices.Add(indexName);

		var options = Options.Create(new ElasticsearchOutboxOptions { IndexName = indexName });

		var headers = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["routing-key"] = "eu-west-1",
			["x-correlation"] = "corr-abc-123",
			["x-replay-count"] = "4",
			["x-source-system"] = "billing-service",
		};

		var message = new OutboundMessage(
			messageType: "Billing.InvoiceRaised",
			payload: [1, 2, 3, 4],
			destination: "invoices-topic",
			headers: headers)
		{
			CorrelationId = "corr-abc-123",
			TenantId = "tenant-42",
		};

		var writeStore = new ElasticsearchOutboxStore(
			Client, options, LoggerFactory.CreateLogger<ElasticsearchOutboxStore>());
		await writeStore.StageMessageAsync(message, CancellationToken.None);
		_ = await Client.Indices.RefreshAsync(indexName).ConfigureAwait(false);

		var readStore = new ElasticsearchOutboxStore(
			Client, options, LoggerFactory.CreateLogger<ElasticsearchOutboxStore>());
		var reloaded = (await readStore.GetUnsentMessagesAsync(batchSize: 100, CancellationToken.None)).ToList();

		var restored = reloaded.ShouldHaveSingleItem();
		restored.Id.ShouldBe(message.Id);

		restored.Headers.ShouldNotBeNull();
		restored.Headers.Count.ShouldBe(headers.Count, "every staged header must be reloaded -- none dropped");
		foreach (var (key, value) in headers)
		{
			restored.Headers.ShouldContainKey(key);
			restored.Headers[key]?.ToString().ShouldBe(value.ToString(),
				$"header '{key}' must round-trip with its exact value");
		}
	}
}
