// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Verifies that <see cref="DataChangeEventProcessor.GetHandler"/> correctly translates
/// capture instance names to logical table names via <see cref="IDatabaseOptions.CaptureInstanceToTableNameMap"/>
/// before looking up the <see cref="IDataChangeHandler"/>.
/// </summary>
/// <remarks>
/// Sprint 816: CDC capture-instance → table-name translation fix.
/// The bug: CDC rows carry the capture instance name (e.g. "sales_Customers") but handlers
/// register with logical table names (e.g. "sales.Customers"). Without translation,
/// handler lookup throws <see cref="CdcMissingTableHandlerException"/>.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DataChangeEventProcessorHandlerLookupShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";

	/// <summary>
	/// Creates a <see cref="DataChangeEventProcessor"/> with the given <see cref="IDatabaseOptions"/>
	/// and handler registrations, suitable for testing <c>GetHandler</c> via reflection.
	/// </summary>
	private DataChangeEventProcessor CreateProcessor(
		IDatabaseOptions dbConfig,
		params IDataChangeHandler[] handlers)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IHostApplicationLifetime>());

		foreach (var handler in handlers)
		{
			services.AddSingleton(handler);
		}

		var provider = services.BuildServiceProvider();

		return new DataChangeEventProcessor(
			provider.GetRequiredService<IHostApplicationLifetime>(),
			dbConfig,
			new CdcRepository(new SqlConnection(TestConnectionString)),
			new SqlConnection(TestConnectionString),
			Options.Create(new SqlServerCdcStateStoreOptions()),
			provider,
			A.Fake<IDataAccessPolicyFactory>(),
			provider.GetRequiredService<ILogger<DataChangeEventProcessor>>());
	}

	/// <summary>
	/// Invokes the private <c>GetHandler(string)</c> method on the processor via reflection.
	/// </summary>
	private static IDataChangeHandler InvokeGetHandler(DataChangeEventProcessor processor, string tableName)
	{
		var method = typeof(DataChangeEventProcessor)
			.GetMethod("GetHandler", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("GetHandler method not found.");

		return (IDataChangeHandler)method.Invoke(processor, [tableName])!;
	}

	[Fact]
	public void ResolveHandler_WhenCaptureInstanceMatchesTableNameDirectly()
	{
		// Arrange — handler registered for "dbo.Orders", no translation needed
		var handler = A.Fake<IDataChangeHandler>();
		A.CallTo(() => handler.TableNames).Returns(["dbo.Orders"]);

		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo.Orders"]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap)
			.Returns(new Dictionary<string, string> { ["dbo.Orders"] = "dbo.Orders" }.AsReadOnly());
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);

		var processor = CreateProcessor(dbConfig, handler);

		// Act
		var result = InvokeGetHandler(processor, "dbo.Orders");

		// Assert
		result.ShouldBeSameAs(handler);
	}

	[Fact]
	public void ResolveHandler_WhenCaptureInstanceTranslatesToLogicalTableName()
	{
		// Arrange — handler registered for logical name "sales.Customers",
		// but CDC rows carry capture instance "sales_Customers"
		var handler = A.Fake<IDataChangeHandler>();
		A.CallTo(() => handler.TableNames).Returns(["sales.Customers"]);

		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["sales_Customers"]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap)
			.Returns(new Dictionary<string, string>
			{
				["sales_Customers"] = "sales.Customers"
			}.AsReadOnly());
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);

		var processor = CreateProcessor(dbConfig, handler);

		// Act — pass the capture instance name (what CDC rows carry)
		var result = InvokeGetHandler(processor, "sales_Customers");

		// Assert — should resolve to the handler registered for logical name
		result.ShouldBeSameAs(handler);
	}

	[Fact]
	public void ResolveHandler_WhenMultipleCaptureInstancesMapToDifferentTables()
	{
		// Arrange — two handlers, two capture instances with translation
		var ordersHandler = A.Fake<IDataChangeHandler>();
		A.CallTo(() => ordersHandler.TableNames).Returns(["dbo.Orders"]);

		var customersHandler = A.Fake<IDataChangeHandler>();
		A.CallTo(() => customersHandler.TableNames).Returns(["sales.Customers"]);

		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_Orders", "sales_Customers"]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap)
			.Returns(new Dictionary<string, string>
			{
				["dbo_Orders"] = "dbo.Orders",
				["sales_Customers"] = "sales.Customers"
			}.AsReadOnly());
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);

		var processor = CreateProcessor(dbConfig, ordersHandler, customersHandler);

		// Act & Assert — each capture instance resolves to its correct handler
		InvokeGetHandler(processor, "dbo_Orders").ShouldBeSameAs(ordersHandler);
		InvokeGetHandler(processor, "sales_Customers").ShouldBeSameAs(customersHandler);
	}

	[Fact]
	public void ThrowCdcMissingTableHandlerException_WhenNoTranslationAndNoHandlerFound()
	{
		// Arrange — capture instance "unknown_Table" has no mapping and no handler
		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_Orders"]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap)
			.Returns(new Dictionary<string, string>().AsReadOnly());
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);

		var processor = CreateProcessor(dbConfig);

		// Act & Assert
		var ex = Should.Throw<TargetInvocationException>(() =>
			InvokeGetHandler(processor, "unknown_Table"));

		ex.InnerException.ShouldBeOfType<CdcMissingTableHandlerException>();
	}

	[Fact]
	public void FallBackToDirectLookup_WhenCaptureInstanceNotInMap()
	{
		// Arrange — capture instance "dbo.Orders" is not in the map but handler
		// is registered with the same name (identity case, no translation needed)
		var handler = A.Fake<IDataChangeHandler>();
		A.CallTo(() => handler.TableNames).Returns(["dbo.Orders"]);

		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo.Orders"]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap)
			.Returns(new Dictionary<string, string>().AsReadOnly()); // Empty map
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);

		var processor = CreateProcessor(dbConfig, handler);

		// Act — direct name match should still work when map is empty
		var result = InvokeGetHandler(processor, "dbo.Orders");

		// Assert
		result.ShouldBeSameAs(handler);
	}

	[Fact]
	public void ThrowArgumentException_WhenTableNameIsNullOrWhitespace()
	{
		// Arrange
		var dbConfig = A.Fake<IDatabaseOptions>();
		A.CallTo(() => dbConfig.CaptureInstances).Returns([]);
		A.CallTo(() => dbConfig.CaptureInstanceToTableNameMap)
			.Returns(new Dictionary<string, string>().AsReadOnly());
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);

		var processor = CreateProcessor(dbConfig);

		// Act & Assert — null (ThrowIfNullOrWhiteSpace throws ArgumentNullException for null)
		var nullEx = Should.Throw<TargetInvocationException>(() =>
			InvokeGetHandler(processor, null!));
		nullEx.InnerException.ShouldBeOfType<ArgumentNullException>();

		// Act & Assert — whitespace (ThrowIfNullOrWhiteSpace throws ArgumentException for whitespace)
		var wsEx = Should.Throw<TargetInvocationException>(() =>
			InvokeGetHandler(processor, "  "));
		wsEx.InnerException.ShouldBeOfType<ArgumentException>();
	}
}
