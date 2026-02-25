// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="RequestLoggingOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify default values and configuration of request/response logging settings.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class RequestLoggingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultEnabled_ToFalse()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void DefaultLogRequestBody_ToFalse()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.LogRequestBody.ShouldBeFalse();
	}

	[Fact]
	public void DefaultLogResponseBody_ToFalse()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.LogResponseBody.ShouldBeFalse();
	}

	[Fact]
	public void DefaultLogFailuresOnly_ToTrue()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.LogFailuresOnly.ShouldBeTrue();
	}

	[Fact]
	public void DefaultMaxBodySizeBytes_To1024()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.MaxBodySizeBytes.ShouldBe(1024);
	}

	[Fact]
	public void DefaultSanitizeSensitiveData_ToTrue()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.SanitizeSensitiveData.ShouldBeTrue();
	}

	#endregion

	#region Property Configuration Tests

	[Fact]
	public void AllowEnabled_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { Enabled = true };

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowLogRequestBody_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { LogRequestBody = true };

		// Assert
		settings.LogRequestBody.ShouldBeTrue();
	}

	[Fact]
	public void AllowLogResponseBody_ToBeSetToTrue()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { LogResponseBody = true };

		// Assert
		settings.LogResponseBody.ShouldBeTrue();
	}

	[Fact]
	public void AllowLogFailuresOnly_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { LogFailuresOnly = false };

		// Assert
		settings.LogFailuresOnly.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomMaxBodySizeBytes()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { MaxBodySizeBytes = 4096 };

		// Assert
		settings.MaxBodySizeBytes.ShouldBe(4096);
	}

	[Fact]
	public void AllowSanitizeSensitiveData_ToBeSetToFalse()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { SanitizeSensitiveData = false };

		// Assert
		settings.SanitizeSensitiveData.ShouldBeFalse();
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var settings = new RequestLoggingOptions();

		// Assert
		settings.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_WithAllPropertiesConfigured()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions
		{
			Enabled = true,
			LogRequestBody = true,
			LogResponseBody = true,
			LogFailuresOnly = false,
			MaxBodySizeBytes = 8192,
			SanitizeSensitiveData = false
		};

		// Assert
		settings.Enabled.ShouldBeTrue();
		settings.LogRequestBody.ShouldBeTrue();
		settings.LogResponseBody.ShouldBeTrue();
		settings.LogFailuresOnly.ShouldBeFalse();
		settings.MaxBodySizeBytes.ShouldBe(8192);
		settings.SanitizeSensitiveData.ShouldBeFalse();
	}

	#endregion

	#region Integer Edge Cases

	[Fact]
	public void AllowZeroMaxBodySizeBytes()
	{
		// Arrange & Act
		var settings = new RequestLoggingOptions { MaxBodySizeBytes = 0 };

		// Assert
		settings.MaxBodySizeBytes.ShouldBe(0);
	}

	[Fact]
	public void AllowLargeMaxBodySizeBytes()
	{
		// Arrange
		const int largeSize = 1024 * 1024; // 1MB

		// Act
		var settings = new RequestLoggingOptions { MaxBodySizeBytes = largeSize };

		// Assert
		settings.MaxBodySizeBytes.ShouldBe(largeSize);
	}

	[Fact]
	public void AllowNegativeMaxBodySizeBytes()
	{
		// Note: This tests that the property accepts any int value;
		// validation should be done at configuration level if needed
		var settings = new RequestLoggingOptions { MaxBodySizeBytes = -1 };

		// Assert
		settings.MaxBodySizeBytes.ShouldBe(-1);
	}

	#endregion
}
