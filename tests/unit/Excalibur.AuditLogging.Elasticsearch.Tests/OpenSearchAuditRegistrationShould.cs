// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.AuditLogging.OpenSearch;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.AuditLogging.Elasticsearch.Tests;

/// <summary>
/// Locks the OpenSearch audit registrations onto the typed clients they configure.
/// Both entry points added a second descriptor by implementation type, and that one wins on
/// resolve, so the exporter and sink a consumer actually got were built from the container's
/// plain HttpClient -- carrying neither the resilience handler nor the node-failover handler.
/// Every retry, timeout and multi-node setting the options advertise was inert as a result.
/// The typed client is configured with an infinite HttpClient timeout because the resilience
/// pipeline owns timeouts, so that value is the discriminator: a default 100-second timeout
/// means the handler chain was bypassed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class OpenSearchAuditRegistrationShould
{
	private static HttpClient HttpClientOf(object instance)
	{
		var field = instance.GetType()
			.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
			.SingleOrDefault(f => f.FieldType == typeof(HttpClient));

		field.ShouldNotBeNull(
			$"{instance.GetType().Name} no longer holds an HttpClient field; this lock needs updating.");

		return (HttpClient)field.GetValue(instance)!;
	}

	[Fact]
	public void GiveTheExporterTheConfiguredTypedClient()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOpenSearchAuditExporter(b => b
			.NodeUri(new Uri("https://localhost:9200"))
			.IndexName("audit"));

		using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = true });

		var exporter = provider.GetRequiredService<IAuditLogExporter>();

		HttpClientOf(exporter).Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
	}
}
