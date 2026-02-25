// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Middleware.Tests.Logging;

/// <summary>
/// Unit tests for LoggingMiddlewareOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoggingMiddlewareOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Information);
		options.FailureLevel.ShouldBe(LogLevel.Error);
		options.IncludePayload.ShouldBeFalse();
		options.IncludeTiming.ShouldBeTrue();
		options.LogStart.ShouldBeTrue();
		options.LogCompletion.ShouldBeTrue();
		options.ExcludeTypes.ShouldNotBeNull();
		options.ExcludeTypes.ShouldBeEmpty();
	}

	[Fact]
	public void SuccessLevel_CanBeChanged()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.SuccessLevel = LogLevel.Debug;

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Debug);
	}

	[Fact]
	public void FailureLevel_CanBeChanged()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.FailureLevel = LogLevel.Warning;

		// Assert
		options.FailureLevel.ShouldBe(LogLevel.Warning);
	}

	[Fact]
	public void IncludePayload_CanBeEnabled()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.IncludePayload = true;

		// Assert
		options.IncludePayload.ShouldBeTrue();
	}

	[Fact]
	public void IncludeTiming_CanBeDisabled()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.IncludeTiming = false;

		// Assert
		options.IncludeTiming.ShouldBeFalse();
	}

	[Fact]
	public void LogStart_CanBeDisabled()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.LogStart = false;

		// Assert
		options.LogStart.ShouldBeFalse();
	}

	[Fact]
	public void LogCompletion_CanBeDisabled()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.LogCompletion = false;

		// Assert
		options.LogCompletion.ShouldBeFalse();
	}

	[Fact]
	public void ExcludeTypes_CanAddTypes()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		_ = options.ExcludeTypes.Add(typeof(string));
		_ = options.ExcludeTypes.Add(typeof(int));

		// Assert
		options.ExcludeTypes.Count.ShouldBe(2);
		options.ExcludeTypes.ShouldContain(typeof(string));
		options.ExcludeTypes.ShouldContain(typeof(int));
	}

	[Fact]
	public void ExcludeTypes_CanCheckContains()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();
		_ = options.ExcludeTypes.Add(typeof(TimeoutException));

		// Act & Assert
		options.ExcludeTypes.Contains(typeof(TimeoutException)).ShouldBeTrue();
		options.ExcludeTypes.Contains(typeof(InvalidOperationException)).ShouldBeFalse();
	}
}
