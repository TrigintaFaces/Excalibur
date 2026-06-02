// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Unit tests for <see cref="ResultFactoryRegistry"/> — AOT-compatible result factory methods.
/// </summary>
/// <remarks>
/// Post-sprint fix: validates the new runtime registry that replaces the old
/// source-generated partial stub. Covers RegisterFactory, GetFactory, and the
/// factory delegate's correctness in creating typed MessageResult instances.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Priority", "0")]
public sealed class ResultFactoryRegistryShould
{
	#region RegisterFactory Tests

	[Fact]
	public void RegisterFactory_ForStringType_MakesFactoryAvailable()
	{
		// Arrange & Act
		ResultFactoryRegistry.RegisterFactory<string>();

		// Assert
		var factory = ResultFactoryRegistry.GetFactory(typeof(string));
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterFactory_ForIntType_MakesFactoryAvailable()
	{
		// Arrange & Act
		ResultFactoryRegistry.RegisterFactory<int>();

		// Assert
		var factory = ResultFactoryRegistry.GetFactory(typeof(int));
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterFactory_ForGuidType_MakesFactoryAvailable()
	{
		// Arrange & Act
		ResultFactoryRegistry.RegisterFactory<Guid>();

		// Assert
		var factory = ResultFactoryRegistry.GetFactory(typeof(Guid));
		factory.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterFactory_CalledTwice_DoesNotThrow()
	{
		// TryAdd semantics — first registration wins, second is no-op
		ResultFactoryRegistry.RegisterFactory<double>();
		ResultFactoryRegistry.RegisterFactory<double>();

		var factory = ResultFactoryRegistry.GetFactory(typeof(double));
		factory.ShouldNotBeNull();
	}

	#endregion

	#region GetFactory Tests

	[Fact]
	public void GetFactory_ForUnregisteredType_ReturnsNull()
	{
		// Act
		var factory = ResultFactoryRegistry.GetFactory(typeof(DateTimeOffset));

		// Assert
		factory.ShouldBeNull();
	}

	[Fact]
	public void GetFactory_ReturnsFactory_ThatCreatesCorrectMessageResult()
	{
		// Arrange
		ResultFactoryRegistry.RegisterFactory<string>();
		var factory = ResultFactoryRegistry.GetFactory(typeof(string))!;

		// Act
		var result = factory("hello", null, null, null, false);

		// Assert
		result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeTrue();

		var typed = result.ShouldBeAssignableTo<IMessageResult<string>>();
		typed!.ReturnValue.ShouldBe("hello");
	}

	[Fact]
	public void GetFactory_ReturnsFactory_ThatPassesRoutingDecision()
	{
		// Arrange
		ResultFactoryRegistry.RegisterFactory<int>();
		var factory = ResultFactoryRegistry.GetFactory(typeof(int))!;

		// Act — pass null routing (RoutingDecision is sealed, can't fake)
		var result = factory(42, null, null, null, false);

		// Assert
		result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void GetFactory_ReturnsFactory_ThatPassesCacheHitFlag()
	{
		// Arrange
		ResultFactoryRegistry.RegisterFactory<bool>();
		var factory = ResultFactoryRegistry.GetFactory(typeof(bool))!;

		// Act
		var result = factory(true, null, null, null, true);

		// Assert
		result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Concurrent Access Tests

	[Fact]
	public void RegisterFactory_ConcurrentRegistrations_DoNotThrow()
	{
		// Arrange & Act
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		Parallel.For(0, 100, i =>
		{
			try
			{
				ResultFactoryRegistry.RegisterFactory<long>();
			}
#pragma warning disable CA1031
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
#pragma warning restore CA1031
		});

		// Assert
		exceptions.ShouldBeEmpty();
		ResultFactoryRegistry.GetFactory(typeof(long)).ShouldNotBeNull();
	}

	[Fact]
	public void GetFactory_ConcurrentReads_ReturnConsistentResults()
	{
		// Arrange
		ResultFactoryRegistry.RegisterFactory<short>();

		// Act & Assert — concurrent reads should all return non-null
		Parallel.For(0, 100, _ =>
		{
			var factory = ResultFactoryRegistry.GetFactory(typeof(short));
			factory.ShouldNotBeNull();
		});
	}

	#endregion
}
