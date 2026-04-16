// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DynamoDb;
using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="SagaBuilderDynamoDbExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Phase C rewire: Updated from AddDynamoDbSagaStore to AddExcaliburSaga(saga =&gt; saga.UseDynamoDb(...)).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class DynamoDbSagaExtensionsShould
{
	#region UseDynamoDb Builder Tests

	[Fact]
	public void UseDynamoDb_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ISagaBuilder)null!).UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));
	}

	[Fact]
	public void UseDynamoDb_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburSaga(saga =>
				saga.UseDynamoDb((Action<IDynamoDBSagaBuilder>)null!)));
	}

	[Fact]
	public void UseDynamoDb_RegistersDynamoDbSagaStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburSaga(saga =>
			saga.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbSagaStore));
	}

	[Fact]
	public void UseDynamoDb_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		ISagaBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburSaga(saga =>
		{
			var result = saga.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000"));
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
		typeof(SagaBuilderDynamoDbExtensions).IsAbstract.ShouldBeTrue();
		typeof(SagaBuilderDynamoDbExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(SagaBuilderDynamoDbExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
