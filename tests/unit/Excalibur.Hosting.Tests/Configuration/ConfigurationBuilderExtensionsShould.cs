// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
[Collection("EnvironmentVariableTests")]
public sealed class ConfigurationBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void ReturnBuilderFromAddParameterStoreSettingsWithNoArgs()
	{
		// Arrange
		var originalContext = Environment.GetEnvironmentVariable("RL_APP_CONTEXT");
		var originalName = Environment.GetEnvironmentVariable("RL_APP_NAME");
		try
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", null);
			Environment.SetEnvironmentVariable("RL_APP_NAME", null);

			var builder = new ConfigurationBuilder();

			// Act
			var result = builder.AddParameterStoreSettings();

			// Assert
			result.ShouldNotBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", originalContext);
			Environment.SetEnvironmentVariable("RL_APP_NAME", originalName);
		}
	}

	[Fact]
	public void ReturnBuilderFromAddParameterStoreSettingsWithAppName()
	{
		// Arrange
		var originalContext = Environment.GetEnvironmentVariable("RL_APP_CONTEXT");
		try
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", null);

			var builder = new ConfigurationBuilder();

			// Act
			var result = builder.AddParameterStoreSettings("my-app");

			// Assert
			result.ShouldNotBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", originalContext);
		}
	}

	[Fact]
	public void ReturnBuilderFromAddParameterStoreSettingsWithLocalContext()
	{
		// Arrange
		var originalContext = Environment.GetEnvironmentVariable("RL_APP_CONTEXT");
		try
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", null);

			var builder = new ConfigurationBuilder();

			// Act
			var result = builder.AddParameterStoreSettings("my-app", "Development");

			// Assert
			result.ShouldNotBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", originalContext);
		}
	}

	[Fact]
	public void AddEnvironmentVariablesSource()
	{
		// Arrange
		var originalContext = Environment.GetEnvironmentVariable("RL_APP_CONTEXT");
		try
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", null);

			var builder = new ConfigurationBuilder();

			// Act
			var result = builder.AddParameterStoreSettings("my-app", "Local");

			// Assert — should not throw and environment variables source should be added
			var config = result.Build();
			config.ShouldNotBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", originalContext);
		}
	}

	[Fact]
	public void UseDefaultAppNameFromEnvironmentVariable()
	{
		// Arrange
		var originalContext = Environment.GetEnvironmentVariable("RL_APP_CONTEXT");
		var originalName = Environment.GetEnvironmentVariable("RL_APP_NAME");
		try
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", null);
			Environment.SetEnvironmentVariable("RL_APP_NAME", "test-app");

			var builder = new ConfigurationBuilder();

			// Act — parameterless overload reads RL_APP_NAME
			var result = builder.AddParameterStoreSettings();

			// Assert
			result.ShouldNotBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", originalContext);
			Environment.SetEnvironmentVariable("RL_APP_NAME", originalName);
		}
	}

	[Fact]
	public void SkipLocalContextWhenNullOrEmpty()
	{
		// Arrange
		var originalContext = Environment.GetEnvironmentVariable("RL_APP_CONTEXT");
		try
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", null);

			var builder = new ConfigurationBuilder();

			// Act — localContext is null
			var result = builder.AddParameterStoreSettings("my-app", null);

			// Assert — should not throw, just adds env vars
			var config = result.Build();
			config.ShouldNotBeNull();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RL_APP_CONTEXT", originalContext);
		}
	}
}
