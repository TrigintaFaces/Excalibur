// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using DispatchTransactionOptions = Excalibur.Dispatch.Options.Middleware.TransactionOptions;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="DispatchTransactionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TransactionOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchTransactionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_RequireTransactionByDefault_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchTransactionOptions();

		// Assert
		options.RequireTransactionByDefault.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableDistributedTransactions_IsFalse()
	{
		// Arrange & Act
		var options = new DispatchTransactionOptions();

		// Assert
		options.EnableDistributedTransactions.ShouldBeFalse();
	}

	[Fact]
	public void Default_DefaultIsolationLevel_IsReadCommitted()
	{
		// Arrange & Act
		var options = new DispatchTransactionOptions();

		// Assert
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void Default_DefaultTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new DispatchTransactionOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_BypassTransactionForTypes_IsNull()
	{
		// Arrange & Act
		var options = new DispatchTransactionOptions();

		// Assert
		options.BypassTransactionForTypes.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new DispatchTransactionOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RequireTransactionByDefault_CanBeSet()
	{
		// Arrange
		var options = new DispatchTransactionOptions();

		// Act
		options.RequireTransactionByDefault = false;

		// Assert
		options.RequireTransactionByDefault.ShouldBeFalse();
	}

	[Fact]
	public void EnableDistributedTransactions_CanBeSet()
	{
		// Arrange
		var options = new DispatchTransactionOptions();

		// Act
		options.EnableDistributedTransactions = true;

		// Assert
		options.EnableDistributedTransactions.ShouldBeTrue();
	}

	[Fact]
	public void DefaultIsolationLevel_CanBeSet()
	{
		// Arrange
		var options = new DispatchTransactionOptions();

		// Act
		options.DefaultIsolationLevel = IsolationLevel.Serializable;

		// Assert
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public void DefaultTimeout_CanBeSet()
	{
		// Arrange
		var options = new DispatchTransactionOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromMinutes(2);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void BypassTransactionForTypes_CanBeSet()
	{
		// Arrange
		var options = new DispatchTransactionOptions();
		var types = new[] { "ReadOnlyQuery", "CacheQuery" };

		// Act
		options.BypassTransactionForTypes = types;

		// Assert
		options.BypassTransactionForTypes.ShouldBe(types);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var bypassTypes = new[] { "QueryMessage" };

		// Act
		var options = new DispatchTransactionOptions
		{
			Enabled = false,
			RequireTransactionByDefault = false,
			EnableDistributedTransactions = true,
			DefaultIsolationLevel = IsolationLevel.RepeatableRead,
			DefaultTimeout = TimeSpan.FromMinutes(1),
			BypassTransactionForTypes = bypassTypes,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireTransactionByDefault.ShouldBeFalse();
		options.EnableDistributedTransactions.ShouldBeTrue();
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.RepeatableRead);
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		options.BypassTransactionForTypes.ShouldBe(bypassTypes);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighIsolation_UsesSerializable()
	{
		// Act
		var options = new DispatchTransactionOptions
		{
			DefaultIsolationLevel = IsolationLevel.Serializable,
			DefaultTimeout = TimeSpan.FromMinutes(5),
		};

		// Assert
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public void Options_ForDistributedSystem_EnablesDistributed()
	{
		// Act
		var options = new DispatchTransactionOptions
		{
			EnableDistributedTransactions = true,
			DefaultTimeout = TimeSpan.FromMinutes(2),
		};

		// Assert
		options.EnableDistributedTransactions.ShouldBeTrue();
		options.DefaultTimeout.ShouldBeGreaterThan(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Options_ForReadHeavyWorkload_BypassesQueries()
	{
		// Act
		var options = new DispatchTransactionOptions
		{
			BypassTransactionForTypes = new[] { "GetUserQuery", "ListOrdersQuery", "SearchProductsQuery" },
		};

		// Assert
		options.BypassTransactionForTypes.ShouldNotBeEmpty();
		options.BypassTransactionForTypes.Length.ShouldBe(3);
	}

	#endregion
}
