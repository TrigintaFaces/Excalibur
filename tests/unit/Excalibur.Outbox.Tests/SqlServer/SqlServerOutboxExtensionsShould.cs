// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
public sealed class SqlServerOutboxExtensionsShould : UnitTestBase
{
	[Fact]
	public async Task AddSqlServerOutboxStore_WithFactoryProvider_ResolvesStoreWithoutOpeningConnection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var providerFactoryCallCount = 0;
		var connectionFactoryCallCount = 0;
		services.AddSqlServerOutboxStore(
			_ =>
			{
				providerFactoryCallCount++;
				return () =>
				{
					connectionFactoryCallCount++;
					throw new InvalidOperationException("Connection factory should not be called in this test.");
				};
			},
			options => options.ConnectionString = "Server=localhost;Database=Messaging;");
		var provider = services.BuildServiceProvider();

		// Act
		var store = provider.GetRequiredService<SqlServerOutboxStore>();
		var exception = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.StageMessageAsync(null!, CancellationToken.None));

		// Assert
		exception.ParamName.ShouldBe("message");
		providerFactoryCallCount.ShouldBe(1);
		connectionFactoryCallCount.ShouldBe(0);
	}

	[Fact]
	public void AddSqlServerDeadLetterQueue_WithConnectionString_RegistersQueueAndOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string connectionString = "Server=localhost;Database=DeadLetters;";
		services.AddSqlServerDeadLetterQueue(connectionString);
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<SqlServerDeadLetterQueueOptions>>();
		var queue = provider.GetRequiredService<IDeadLetterQueue>();

		// Assert
		options.Value.ConnectionString.ShouldBe(connectionString);
		queue.ShouldBeOfType<SqlServerDeadLetterQueue>();
	}

	[Fact]
	public void UseSqlServerOutboxStore_OnDispatchBuilder_RegistersOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string connectionString = "Server=localhost;Database=DispatchOutbox;";
		services.AddDispatch(builder => builder.UseSqlServerOutboxStore(connectionString));
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		var store = provider.GetRequiredService<IOutboxStore>();

		// Assert
		options.Value.ConnectionString.ShouldBe(connectionString);
		store.ShouldBeOfType<SqlServerOutboxStore>();
	}

	[Fact]
	public void UseSqlServerDeadLetterQueue_OnDispatchBuilder_RegistersDeadLetterQueue()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		const string connectionString = "Server=localhost;Database=DispatchDeadLetters;";
		services.AddDispatch(builder => builder.UseSqlServerDeadLetterQueue(connectionString));
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<SqlServerDeadLetterQueueOptions>>();
		var queue = provider.GetRequiredService<IDeadLetterQueue>();

		// Assert
		options.Value.ConnectionString.ShouldBe(connectionString);
		queue.ShouldBeOfType<SqlServerDeadLetterQueue>();
	}
}
