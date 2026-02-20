// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="LoggingMiddlewareOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LoggingMiddlewareOptionsShould
{
	[Fact]
	public void HaveInformationAsDefaultSuccessLevel()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Information);
	}

	[Fact]
	public void HaveErrorAsDefaultFailureLevel()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.FailureLevel.ShouldBe(LogLevel.Error);
	}

	[Fact]
	public void HaveIncludePayloadDisabledByDefault()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.IncludePayload.ShouldBeFalse();
	}

	[Fact]
	public void HaveIncludeTimingEnabledByDefault()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.IncludeTiming.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyExcludeTypesByDefault()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.ExcludeTypes.ShouldNotBeNull();
		options.ExcludeTypes.ShouldBeEmpty();
	}

	[Fact]
	public void HaveLogStartEnabledByDefault()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.LogStart.ShouldBeTrue();
	}

	[Fact]
	public void HaveLogCompletionEnabledByDefault()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions();

		// Assert
		options.LogCompletion.ShouldBeTrue();
	}

	[Theory]
	[InlineData(LogLevel.Trace)]
	[InlineData(LogLevel.Debug)]
	[InlineData(LogLevel.Information)]
	[InlineData(LogLevel.Warning)]
	[InlineData(LogLevel.Error)]
	[InlineData(LogLevel.Critical)]
	[InlineData(LogLevel.None)]
	public void AllowSettingSuccessLevel(LogLevel level)
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.SuccessLevel = level;

		// Assert
		options.SuccessLevel.ShouldBe(level);
	}

	[Theory]
	[InlineData(LogLevel.Trace)]
	[InlineData(LogLevel.Debug)]
	[InlineData(LogLevel.Information)]
	[InlineData(LogLevel.Warning)]
	[InlineData(LogLevel.Error)]
	[InlineData(LogLevel.Critical)]
	[InlineData(LogLevel.None)]
	public void AllowSettingFailureLevel(LogLevel level)
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.FailureLevel = level;

		// Assert
		options.FailureLevel.ShouldBe(level);
	}

	[Fact]
	public void AllowSettingIncludePayload()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.IncludePayload = true;

		// Assert
		options.IncludePayload.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingIncludeTiming()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.IncludeTiming = false;

		// Assert
		options.IncludeTiming.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingLogStart()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.LogStart = false;

		// Assert
		options.LogStart.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingLogCompletion()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.LogCompletion = false;

		// Assert
		options.LogCompletion.ShouldBeFalse();
	}

	[Fact]
	public void AllowAddingTypesToExcludeTypes()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act
		options.ExcludeTypes.Add(typeof(string));
		options.ExcludeTypes.Add(typeof(int));

		// Assert
		options.ExcludeTypes.Count.ShouldBe(2);
		options.ExcludeTypes.ShouldContain(typeof(string));
		options.ExcludeTypes.ShouldContain(typeof(int));
	}

	[Fact]
	public void PreventDuplicateTypesInExcludeTypes()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();

		// Act - Add same type twice
		options.ExcludeTypes.Add(typeof(string));
		options.ExcludeTypes.Add(typeof(string));

		// Assert - HashSet prevents duplicates
		options.ExcludeTypes.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var options = new LoggingMiddlewareOptions
		{
			SuccessLevel = LogLevel.Debug,
			FailureLevel = LogLevel.Critical,
			IncludePayload = true,
			IncludeTiming = false,
			LogStart = false,
			LogCompletion = true,
		};

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Debug);
		options.FailureLevel.ShouldBe(LogLevel.Critical);
		options.IncludePayload.ShouldBeTrue();
		options.IncludeTiming.ShouldBeFalse();
		options.LogStart.ShouldBeFalse();
		options.LogCompletion.ShouldBeTrue();
	}

	[Fact]
	public void SupportClearingExcludeTypes()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();
		options.ExcludeTypes.Add(typeof(string));
		options.ExcludeTypes.Add(typeof(int));

		// Act
		options.ExcludeTypes.Clear();

		// Assert
		options.ExcludeTypes.ShouldBeEmpty();
	}

	[Fact]
	public void SupportRemovingFromExcludeTypes()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();
		options.ExcludeTypes.Add(typeof(string));
		options.ExcludeTypes.Add(typeof(int));

		// Act
		var removed = options.ExcludeTypes.Remove(typeof(string));

		// Assert
		removed.ShouldBeTrue();
		options.ExcludeTypes.Count.ShouldBe(1);
		options.ExcludeTypes.ShouldContain(typeof(int));
		options.ExcludeTypes.ShouldNotContain(typeof(string));
	}

	[Fact]
	public void ReturnFalseWhenRemovingNonExistentType()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();
		options.ExcludeTypes.Add(typeof(string));

		// Act
		var removed = options.ExcludeTypes.Remove(typeof(int));

		// Assert
		removed.ShouldBeFalse();
		options.ExcludeTypes.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportCheckingContainsInExcludeTypes()
	{
		// Arrange
		var options = new LoggingMiddlewareOptions();
		options.ExcludeTypes.Add(typeof(string));

		// Act & Assert
		options.ExcludeTypes.Contains(typeof(string)).ShouldBeTrue();
		options.ExcludeTypes.Contains(typeof(int)).ShouldBeFalse();
	}

	[Fact]
	public void SimulateTypicalProductionConfiguration()
	{
		// Arrange & Act - Typical production: less verbose, no payload, timing enabled
		var options = new LoggingMiddlewareOptions
		{
			SuccessLevel = LogLevel.Debug, // Less verbose in production
			FailureLevel = LogLevel.Error,
			IncludePayload = false, // Security: don't log sensitive data
			IncludeTiming = true,
			LogStart = false, // Reduce log volume
			LogCompletion = true,
		};

		// Add health check types to exclude
		options.ExcludeTypes.Add(typeof(object)); // Placeholder for health check type

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Debug);
		options.IncludePayload.ShouldBeFalse();
		options.LogStart.ShouldBeFalse();
		options.ExcludeTypes.Count.ShouldBe(1);
	}

	[Fact]
	public void SimulateTypicalDevelopmentConfiguration()
	{
		// Arrange & Act - Typical development: verbose, payload included for debugging
		var options = new LoggingMiddlewareOptions
		{
			SuccessLevel = LogLevel.Information,
			FailureLevel = LogLevel.Warning, // Lower level to catch issues early
			IncludePayload = true, // Helpful for debugging
			IncludeTiming = true,
			LogStart = true,
			LogCompletion = true,
		};

		// Assert
		options.SuccessLevel.ShouldBe(LogLevel.Information);
		options.IncludePayload.ShouldBeTrue();
		options.LogStart.ShouldBeTrue();
		options.ExcludeTypes.ShouldBeEmpty();
	}
}
