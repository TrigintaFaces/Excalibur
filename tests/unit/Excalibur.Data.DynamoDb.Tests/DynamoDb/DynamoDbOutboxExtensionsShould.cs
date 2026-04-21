// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Outbox;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderDynamoDbExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Phase C rewire: Updated from AddDynamoDbOutboxStore to AddExcaliburOutbox(outbox =&gt; outbox.UseDynamoDb(...)).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class DynamoDbOutboxExtensionsShould
{
	#region UseDynamoDb Builder Tests

	[Fact]
	public void UseDynamoDb_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IOutboxBuilder)null!).UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));
	}

	[Fact]
	public void UseDynamoDb_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseDynamoDb((Action<IDynamoDBOutboxBuilder>)null!)));
	}

	[Fact]
	public void UseDynamoDb_RegistersICloudNativeOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox =>
			outbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICloudNativeOutboxStore));
	}

	[Fact]
	public void UseDynamoDb_RegistersDynamoDbOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburOutbox(outbox =>
			outbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(DynamoDbOutboxStore));
	}

	[Fact]
	public void UseDynamoDb_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburOutbox(outbox =>
		{
			var result = outbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000"));
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(OutboxBuilderDynamoDbExtensions).IsAbstract.ShouldBeTrue();
		typeof(OutboxBuilderDynamoDbExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(OutboxBuilderDynamoDbExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
