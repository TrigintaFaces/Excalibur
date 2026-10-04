// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.SqlServer.Builders;

/// <summary>Capability eligibility follows the selected connection contract, without opening a connection.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Unit")]
public sealed class SqlServerAuthoritativeCapabilityShould
{
	private const string ConnectionString = "Server=localhost;Database=CapabilityTest;Integrated Security=true;TrustServerCertificate=true;";

	public static TheoryData<string, string> ConnectionSelections()
	{
		var data = new TheoryData<string, string>();
		var modes = new[] { "string", "named", "bound", "factory", "owned" };
		foreach (var first in modes)
		{
			foreach (var last in modes)
			{
				data.Add(first, last);
			}
		}
		return data;
	}

	[Theory]
	[MemberData(nameof(ConnectionSelections))]
	public void FollowTheLastConnectionSelection(string first, string last)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
			new Dictionary<string, string?>
			{
				["ConnectionStrings:Events"] = ConnectionString,
				["EventStorage:ConnectionString"] = ConnectionString,
			}).Build());
		var factoryResolutions = 0;
		Func<SqlConnection> ResolveFactory(IServiceProvider _)
		{
			factoryResolutions++;
			return () => throw new InvalidOperationException("Capability resolution must not open a connection.");
		}
		void Select(ISqlServerEventSourcingBuilder builder, string mode)
		{
			switch (mode)
			{
				case "string": builder.ConnectionString(ConnectionString); break;
				case "named": builder.ConnectionStringName("Events"); break;
				case "bound": builder.BindConfiguration("EventStorage"); break;
				case "factory": builder.ConnectionFactory(ResolveFactory); break;
				case "owned": builder.OwnedPrimaryConnectionFactory(ResolveFactory); break;
				default: throw new ArgumentOutOfRangeException(nameof(mode));
			}
		}
		new ExcaliburEventSourcingBuilder(services).UseSqlServer(builder =>
		{
			Select(builder, first);
			Select(builder, last);
		});
		using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var reader = store.GetService(typeof(IEventStoreAuthoritativeReader));
		if (last == "factory")
		{
			reader.ShouldBeNull();
		}
		else
		{
			reader.ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		}
		factoryResolutions.ShouldBe(last is "factory" or "owned" ? 1 : 0);
	}

	[Fact]
	public void ExposeOwnedConstructorsAndPreserveLegacyFactoryCapabilities()
	{
		var context = new Tenant();
		var logger = NullLogger<SqlServerEventStore>.Instance;
		var stores = new[]
		{
			new SqlServerEventStore(ConnectionString, logger, context),
			new SqlServerEventStore(ConnectionString, logger, context, null),
			new SqlServerEventStore(ConnectionString, logger, context, null, null),
			SqlServerEventStore.CreateWithOwnedPrimaryConnectionFactory(() => new SqlConnection(ConnectionString), logger, context),
		};
		foreach (var store in stores)
		{
			store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
			store.GetService(typeof(IEventStoreErasure)).ShouldBeSameAs(store);
			store.GetService(typeof(string)).ShouldBeNull();
			Should.Throw<ArgumentNullException>(() => store.GetService(null!));
			new DenyingDecorator(store).GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeNull();
		}
		var legacy = new SqlServerEventStore(() => new SqlConnection(ConnectionString), logger, context);
		legacy.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeNull();
		legacy.GetService(typeof(IEventStoreArchive)).ShouldBeSameAs(legacy);
	}

	[Fact]
	public void KeepExistingBuilderImplementationsCompatibleWithoutSilentlyGrantingOwnership()
	{
		var legacy = new LegacyBuilder();
		ISqlServerEventSourcingBuilder builder = legacy;
		Should.Throw<NotSupportedException>(() => builder.OwnedPrimaryConnectionFactory(_ => () => new SqlConnection()));
		legacy.FactoryCalls.ShouldBe(0, "the default interface method must not forward an ownership promise to an ordinary factory");
		builder.ConnectionFactory(_ => () => new SqlConnection());
		legacy.FactoryCalls.ShouldBe(1);
	}

	private sealed class Tenant : ITenantContext
	{
		public string? TenantId => "capability-tenant";
		public bool HasTenant => true;
	}

	private sealed class DenyingDecorator(IEventStore inner) : IsolatingEventStoreDecorator(inner);

	// Implements only the pre-existing interface surface. The new default method must remain usable
	// without requiring every third-party builder implementation to be rebuilt with an ownership opt-in.
	private sealed class LegacyBuilder : ISqlServerEventSourcingBuilder
	{
		public int FactoryCalls { get; private set; }
		public ISqlServerEventSourcingBuilder ConnectionString(string connectionString) => this;
		public ISqlServerEventSourcingBuilder ConnectionFactory(Func<IServiceProvider, Func<SqlConnection>> connectionFactory)
		{
			FactoryCalls++;
			return this;
		}
		public ISqlServerEventSourcingBuilder ConnectionStringName(string name) => this;
		public ISqlServerEventSourcingBuilder BindConfiguration(string sectionPath) => this;
		public ISqlServerEventSourcingBuilder EventStoreSchema(string schema) => this;
		public ISqlServerEventSourcingBuilder EventStoreTable(string tableName) => this;
		public ISqlServerEventSourcingBuilder SnapshotStoreSchema(string schema) => this;
		public ISqlServerEventSourcingBuilder SnapshotStoreTable(string tableName) => this;
		public ISqlServerEventSourcingBuilder UseMaterializedViewStore() => this;
		public ISqlServerEventSourcingBuilder UseMaterializedViewStore(string viewTableName, string positionTableName) => this;
		public ISqlServerEventSourcingBuilder EventTypeInfoResolver(System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver resolver) => this;
	}
}
