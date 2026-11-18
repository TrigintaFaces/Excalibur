// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerCommonMetadataRegistrationShould
{
	private const string ConnectionString = "Server=localhost;Database=metadata-controls;Integrated Security=true;TrustServerCertificate=true";

	[Fact]
	public void UseTheExplicitCommonMetadataFactoryWhenConstructingAnOrdinaryStandaloneStore()
	{
		var calls = 0;
		var metadata = new OutboxMetadataSerialization(options => { calls++; return new CommonSqlMetadataContext(options); });
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOutboxMetadataSerialization(metadata);
		_ = services.AddSqlServerOutboxStore(options => options.ConnectionString = ConnectionString);
		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOutboxStore>();
		calls.ShouldBe(1, "metadata-only configuration must be consumed once by the actual constructed store");
	}

	[Fact]
	public async Task RejectUnregisteredHeaderMetadataBeforeOpeningTheConfiguredConnection()
	{
		var opened = 0;
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOutboxMetadataSerialization(new OutboxMetadataSerialization(options => new CommonSqlMetadataContext(options)));
		_ = services.AddSqlServerOutboxStore(_ => () =>
		{
			opened++;
			throw new InvalidOperationException("The test connection must not be requested for invalid generated metadata.");
		}, options => options.ConnectionString = ConnectionString);
		using var provider = services.BuildServiceProvider();
		var message = new OutboundMessage("type", [1], "destination", new Dictionary<string, object>
		{
			["unregistered"] = new CommonSqlUnknownHeader("hidden"),
		});
		_ = await Should.ThrowAsync<NotSupportedException>(async () =>
			await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false));
		opened.ShouldBe(0);
	}

	[Fact]
	public void PreserveOrdinaryStandaloneManagedRegistrationWithoutCommonWorkerOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerOutboxStore(options => options.ConnectionString = ConnectionString);
		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<IOutboxStore>();
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("sqlserver"));
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("default"));
		provider.GetService<OutboxOptions>().ShouldBeNull();
	}
}

internal sealed record CommonSqlUnknownHeader(string Value);

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal sealed partial class CommonSqlMetadataContext : JsonSerializerContext
{
}
