// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerGeneratedMetadataConstructionShould
{
	private const string ConnectionString = "Server=localhost;Database=metadata-controls;Integrated Security=true;TrustServerCertificate=true";

	[Fact]
	public void ConstructOneWriterThroughThePublicStandaloneSafeOverloadWithoutAddingWorkers()
	{
		var calls = 0;
		var metadata = new OutboxMetadataSerialization(options => { calls++; return new SqlConstructionMetadataContext(options); });
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerOutboxStore(metadata, options => options.ConnectionString = ConnectionString);
		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredService<IOutboxStore>();
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("sqlserver"));
		store.ShouldBeSameAs(provider.GetRequiredKeyedService<IOutboxStore>("default"));
		store.ShouldBeSameAs(provider.GetRequiredService<ITransactionalOutboxWriter>());
		provider.GetServices<IHostedService>().ShouldBeEmpty();
		provider.GetService<OutboxOptions>().ShouldBeNull();
		calls.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void PreferTheCapturedExplicitInstanceWithoutChangingACommonDefault(bool defaultFirst)
	{
		var defaultCalls = 0;
		var explicitCalls = 0;
		var common = new OutboxMetadataSerialization(options => { defaultCalls++; return new SqlConstructionMetadataContext(options); });
		var selected = new OutboxMetadataSerialization(options => { explicitCalls++; return new SqlConstructionMetadataContext(options); });
		var services = new ServiceCollection();
		_ = services.AddLogging();
		if (defaultFirst) { services.AddOutboxMetadataSerialization(common); }
		_ = services.AddSqlServerOutboxStore(selected, options => options.ConnectionString = ConnectionString);
		if (!defaultFirst) { services.AddOutboxMetadataSerialization(common); }
		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOutboxStore>();
		provider.GetRequiredService<OutboxMetadataSerialization>().ShouldBeSameAs(common);
		explicitCalls.ShouldBe(1);
		defaultCalls.ShouldBe(0);
	}

	[Fact]
	public void KeepSeparateContainersIndependentWithoutClaimingNamedStoresInOneContainer()
	{
		var optionsSeen = new List<JsonSerializerOptions>();
		var metadata = new OutboxMetadataSerialization(options => { optionsSeen.Add(options); return new SqlConstructionMetadataContext(options); });
		var first = new ServiceCollection();
		var second = new ServiceCollection();
		_ = first.AddLogging();
		_ = second.AddLogging();
		_ = first.AddSqlServerOutboxStore(metadata, options => options.ConnectionString = ConnectionString);
		_ = second.AddSqlServerOutboxStore(metadata, options => options.ConnectionString = ConnectionString);
		using var firstProvider = first.BuildServiceProvider();
		using var secondProvider = second.BuildServiceProvider();
		_ = firstProvider.GetRequiredService<IOutboxStore>();
		_ = secondProvider.GetRequiredService<IOutboxStore>();
		optionsSeen.Count.ShouldBe(2);
		optionsSeen[0].ShouldNotBeSameAs(optionsSeen[1]);
	}

	[Fact]
	public async Task FailMissingMetadataBeforeInvokingAConsumerConnectionFactory()
	{
		var opened = 0;
		var metadata = new OutboxMetadataSerialization(options => new SqlConstructionMetadataContext(options));
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerOutboxStore(_ => () =>
		{
			opened++;
			throw new InvalidOperationException("No database should be opened for invalid metadata.");
		}, metadata, options => options.ConnectionString = ConnectionString);
		using var provider = services.BuildServiceProvider();
		var message = new OutboundMessage("type", [1, 2, 255], "destination", new Dictionary<string, object>
		{
			["missing"] = new SqlConstructionUnknownHeader("hidden"),
		});
		_ = await Should.ThrowAsync<NotSupportedException>(async () =>
			await provider.GetRequiredService<IOutboxStore>().StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false));
		opened.ShouldBe(0);
	}

	[Fact]
	public void RejectAContextDefaultAtConstructionRatherThanTheFirstWrite()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSqlServerOutboxStore(new OutboxMetadataSerialization(_ => SqlConstructionMetadataContext.Default),
			options => options.ConnectionString = ConnectionString);
		using var provider = services.BuildServiceProvider();
		_ = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IOutboxStore>());
	}

	[Fact]
	public void ConstructThePrimaryOutboxBuilderThroughTheExplicitMetadataOverload()
	{
		var calls = 0;
		var metadata = new OutboxMetadataSerialization(options => { calls++; return new SqlConstructionMetadataContext(options); });
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
			outbox.UseSqlServer(metadata, sql => sql.ConnectionString(ConnectionString))));
		using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IOutboxStore>();
		calls.ShouldBe(1);
	}

	[Fact]
	public void RejectNullExplicitMetadataWithoutInvokingOptionsConfiguration()
	{
		var configured = false;
		_ = Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqlServerOutboxStore((OutboxMetadataSerialization)null!, _ => configured = true));
		configured.ShouldBeFalse();
	}
}

internal sealed record SqlConstructionUnknownHeader(string Value);

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal sealed partial class SqlConstructionMetadataContext : JsonSerializerContext
{
}
