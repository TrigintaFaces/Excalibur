// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="DispatchOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DispatchOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_DefaultTimeout_IsThirtySeconds()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_EnableCorrelation_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableCorrelation.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableStructuredLogging_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void Default_MaxConcurrency_IsProcessorCountTimesTwo()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
	}

	[Fact]
	public void Default_UseLightMode_IsFalse()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.UseLightMode.ShouldBeFalse();
	}

	[Fact]
	public void Default_DefaultRetryPolicy_IsNotNull()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		_ = options.CrossCutting.DefaultRetryPolicy.ShouldNotBeNull();
	}

	[Fact]
	public void Default_ValidateMessageSchemas_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.ValidateMessageSchemas.ShouldBeTrue();
	}

	[Fact]
	public void Default_MessageBufferSize_Is1024()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.MessageBufferSize.ShouldBe(1024);
	}

	[Fact]
	public void Default_EnableCacheMiddleware_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableCacheMiddleware.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableMultiTenancy_IsFalse()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableMultiTenancy.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnableVersioning_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableVersioning.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableAuthorization_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableAuthorization.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnableTransactions_IsFalse()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.Features.EnableTransactions.ShouldBeFalse();
	}

	[Fact]
	public void Default_EnablePipelineSynthesis_IsTrue()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		options.EnablePipelineSynthesis.ShouldBeTrue();
	}

	[Fact]
	public void Default_Inbox_IsNotNull()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		_ = options.Inbox.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Outbox_IsNotNull()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		_ = options.Outbox.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Consumer_IsNotNull()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		_ = options.Consumer.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Performance_IsNotNull()
	{
		// Arrange & Act
		var options = new DispatchOptions();

		// Assert
		_ = options.CrossCutting.Performance.ShouldNotBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultTimeout_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.DefaultTimeout = TimeSpan.FromMinutes(2);

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void EnableCorrelation_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.Features.EnableCorrelation = false;

		// Assert
		options.Features.EnableCorrelation.ShouldBeFalse();
	}

	[Fact]
	public void MaxConcurrency_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.MaxConcurrency = 100;

		// Assert
		options.MaxConcurrency.ShouldBe(100);
	}

	[Fact]
	public void UseLightMode_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.UseLightMode = true;

		// Assert
		options.UseLightMode.ShouldBeTrue();
	}

	[Fact]
	public void MessageBufferSize_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.MessageBufferSize = 4096;

		// Assert
		options.MessageBufferSize.ShouldBe(4096);
	}

	[Fact]
	public void EnableMultiTenancy_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.Features.EnableMultiTenancy = true;

		// Assert
		options.Features.EnableMultiTenancy.ShouldBeTrue();
	}

	[Fact]
	public void EnableTransactions_CanBeSet()
	{
		// Arrange
		var options = new DispatchOptions();

		// Act
		options.Features.EnableTransactions = true;

		// Assert
		options.Features.EnableTransactions.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsProperties()
	{
		// Act
		var options = new DispatchOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(5),
			MaxConcurrency = 50,
			UseLightMode = true,
			Features = { EnableCorrelation = false, EnableMetrics = false, EnableMultiTenancy = true, EnableTransactions = true },
		};

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.Features.EnableCorrelation.ShouldBeFalse();
		options.Features.EnableMetrics.ShouldBeFalse();
		options.MaxConcurrency.ShouldBe(50);
		options.UseLightMode.ShouldBeTrue();
		options.Features.EnableMultiTenancy.ShouldBeTrue();
		options.Features.EnableTransactions.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForProduction_HasAllFeaturesEnabled()
	{
		// Act
		var options = new DispatchOptions
		{
			Features = { EnableMetrics = true, EnableStructuredLogging = true, EnableAuthorization = true, EnableVersioning = true },
		};

		// Assert
		options.Features.EnableMetrics.ShouldBeTrue();
		options.Features.EnableStructuredLogging.ShouldBeTrue();
		options.Features.EnableAuthorization.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForLightweight_HasMinimalFeatures()
	{
		// Act
		var options = new DispatchOptions
		{
			UseLightMode = true,
			Features = { EnableMetrics = false, EnableAuthorization = false, EnableTransactions = false, EnableCacheMiddleware = false },
		};

		// Assert
		options.UseLightMode.ShouldBeTrue();
		options.Features.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForMultiTenant_HasTenancyEnabled()
	{
		// Act
		var options = new DispatchOptions
		{
			Features = { EnableMultiTenancy = true, EnableAuthorization = true },
		};

		// Assert
		options.Features.EnableMultiTenancy.ShouldBeTrue();
		options.Features.EnableAuthorization.ShouldBeTrue();
	}

	#endregion
}
