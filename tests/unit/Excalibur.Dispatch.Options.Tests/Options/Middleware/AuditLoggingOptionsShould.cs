// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="AuditLoggingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class AuditLoggingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_LogMessagePayload_IsFalse()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.LogMessagePayload.ShouldBeFalse();
	}

	[Fact]
	public void Default_MaxPayloadSize_IsTenThousand()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.MaxPayloadSize.ShouldBe(10_000);
	}

	[Fact]
	public void Default_MaxPayloadDepth_IsFive()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.MaxPayloadDepth.ShouldBe(5);
	}

	[Fact]
	public void Default_UserIdExtractor_IsNull()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.UserIdExtractor.ShouldBeNull();
	}

	[Fact]
	public void Default_CorrelationIdExtractor_IsNull()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.CorrelationIdExtractor.ShouldBeNull();
	}

	[Fact]
	public void Default_PayloadFilter_IsNull()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.PayloadFilter.ShouldBeNull();
	}

	[Fact]
	public void Default_IncludeSensitiveData_IsFalse()
	{
		// Arrange & Act
		var options = new AuditLoggingOptions();

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void LogMessagePayload_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();

		// Act
		options.LogMessagePayload = true;

		// Assert
		options.LogMessagePayload.ShouldBeTrue();
	}

	[Fact]
	public void MaxPayloadSize_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();

		// Act
		options.MaxPayloadSize = 50_000;

		// Assert
		options.MaxPayloadSize.ShouldBe(50_000);
	}

	[Fact]
	public void MaxPayloadDepth_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();

		// Act
		options.MaxPayloadDepth = 10;

		// Assert
		options.MaxPayloadDepth.ShouldBe(10);
	}

	[Fact]
	public void UserIdExtractor_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();
		Func<IMessageContext, string?> extractor = ctx => "user-123";

		// Act
		options.UserIdExtractor = extractor;

		// Assert
		options.UserIdExtractor.ShouldBe(extractor);
	}

	[Fact]
	public void CorrelationIdExtractor_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();
		Func<IMessageContext, string?> extractor = ctx => "corr-456";

		// Act
		options.CorrelationIdExtractor = extractor;

		// Assert
		options.CorrelationIdExtractor.ShouldBe(extractor);
	}

	[Fact]
	public void PayloadFilter_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();
		Func<IDispatchMessage, bool> filter = msg => true;

		// Act
		options.PayloadFilter = filter;

		// Assert
		options.PayloadFilter.ShouldBe(filter);
	}

	[Fact]
	public void IncludeSensitiveData_CanBeSet()
	{
		// Arrange
		var options = new AuditLoggingOptions();

		// Act
		options.IncludeSensitiveData = true;

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		Func<IMessageContext, string?> userIdExtractor = ctx => "test-user";
		Func<IMessageContext, string?> correlationIdExtractor = ctx => "test-corr";
		Func<IDispatchMessage, bool> payloadFilter = msg => true;

		// Act
		var options = new AuditLoggingOptions
		{
			LogMessagePayload = true,
			MaxPayloadSize = 5000,
			MaxPayloadDepth = 3,
			UserIdExtractor = userIdExtractor,
			CorrelationIdExtractor = correlationIdExtractor,
			PayloadFilter = payloadFilter,
			IncludeSensitiveData = true,
		};

		// Assert
		options.LogMessagePayload.ShouldBeTrue();
		options.MaxPayloadSize.ShouldBe(5000);
		options.MaxPayloadDepth.ShouldBe(3);
		options.UserIdExtractor.ShouldBe(userIdExtractor);
		options.CorrelationIdExtractor.ShouldBe(correlationIdExtractor);
		options.PayloadFilter.ShouldBe(payloadFilter);
		options.IncludeSensitiveData.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForDebugging_LogsPayloads()
	{
		// Act
		var options = new AuditLoggingOptions
		{
			LogMessagePayload = true,
			MaxPayloadSize = 100_000,
			MaxPayloadDepth = 10,
		};

		// Assert
		options.LogMessagePayload.ShouldBeTrue();
		options.MaxPayloadSize.ShouldBeGreaterThan(10_000);
	}

	[Fact]
	public void Options_ForProduction_ExcludesSensitiveData()
	{
		// Act
		var options = new AuditLoggingOptions
		{
			LogMessagePayload = true,
			IncludeSensitiveData = false,
			MaxPayloadSize = 5000,
		};

		// Assert
		options.IncludeSensitiveData.ShouldBeFalse();
		options.MaxPayloadSize.ShouldBeLessThan(10_000);
	}

	#endregion
}
