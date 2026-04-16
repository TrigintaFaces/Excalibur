// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderDynamoDbExtensions" />.
/// </summary>
/// <remarks>
/// Phase C rewire: Updated from DynamoDbOutboxServiceCollectionExtensions to
/// OutboxBuilderDynamoDbExtensions.UseDynamoDb(Action&lt;IDynamoDBOutboxBuilder&gt;).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DynamoDbOutboxServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void UseDynamoDb_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb(db =>
		{
			db.ServiceUrl("http://localhost:8000");
		}));

		// Assert - Check services are registered
		services.Any(static sd =>
			sd.ServiceType == typeof(DynamoDbOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
		services.Any(static sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void UseDynamoDb_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb(db =>
		{
			db.ServiceUrl("http://localhost:8000")
			  .TableName("custom-table");
		}));

		// Assert - Check options configuration is registered
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<DynamoDbOutboxOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void UseDynamoDb_ThrowsOnNullBuilder()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			OutboxBuilderDynamoDbExtensions.UseDynamoDb(null!, db => { }));
	}

	[Fact]
	public void UseDynamoDb_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseDynamoDb((Action<Excalibur.Outbox.DynamoDb.IDynamoDBOutboxBuilder>)null!)));
	}

	[Fact]
	public void UseDynamoDb_ReturnsBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		Excalibur.Outbox.IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000"));
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void UseDynamoDb_InvokeConfigureDelegate()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox => outbox.UseDynamoDb(db =>
		{
			db.ServiceUrl("http://localhost:8000")
			  .TableName("test-outbox");
		}));

		// Assert -- resolve options to verify configure delegate was stored
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DynamoDbOutboxOptions>>().Value;
		options.TableName.ShouldBe("test-outbox");
	}
}
