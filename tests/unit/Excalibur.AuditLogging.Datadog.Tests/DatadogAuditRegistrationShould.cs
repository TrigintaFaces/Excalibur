// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.AuditLogging.Datadog.Tests;

/// <summary>
/// Locks the Datadog audit exporter registration onto the typed client it configures.
/// The entry point added a second descriptor by implementation type, and that one wins on resolve,
/// so the exporter a consumer actually got was built from the container's plain HttpClient --
/// carrying none of the configuration AddHttpClient applied. Neither the resilience handler nor the retry and timeout settings it advertises reached it.
/// The HttpClient timeout is the discriminator: the plain client's default is 100 seconds.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DatadogAuditRegistrationShould
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
#pragma warning disable IL2026, IL3050
		_ = services.AddDatadogAuditExporter(dd => dd.ApiKey("test"));
#pragma warning restore IL2026, IL3050

		using var provider = services.BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = true });

		var exporter = provider.GetRequiredService<IAuditLogExporter>();

		HttpClientOf(exporter).Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
	}

	[Fact]
	public void RegisterTheExporterExactlyOnce()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
#pragma warning disable IL2026, IL3050
		_ = services.AddDatadogAuditExporter(dd => dd.ApiKey("test"));
#pragma warning restore IL2026, IL3050

		services.Count(d => d.ServiceType == typeof(IAuditLogExporter)).ShouldBe(1);
	}
}
