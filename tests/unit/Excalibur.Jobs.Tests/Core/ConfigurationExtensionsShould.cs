// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling

using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="ConfigurationExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class ConfigurationExtensionsShould
{
	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		// Arrange
		IConfiguration configuration = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			configuration.GetJobConfiguration<TestConfig>("Jobs:MyJob"));
	}

	[Fact]
	public void ThrowWhenSectionNameIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			config.GetJobConfiguration<TestConfig>(null!));
	}

	[Fact]
	public void ThrowInvalidOperationExceptionWhenSectionNotFound()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() =>
			config.GetJobConfiguration<TestConfig>("NonExistent:Section"));
		exception.Message.ShouldContain("NonExistent:Section");
	}

	[Fact]
	public void ReturnConfigurationWhenSectionExists()
	{
		// Arrange
		var inMemory = new Dictionary<string, string?>
		{
			["Jobs:MyJob:Name"] = "TestJobName",
			["Jobs:MyJob:Interval"] = "30",
		};
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(inMemory)
			.Build();

		// Act
		var result = config.GetJobConfiguration<TestConfig>("Jobs:MyJob");

		// Assert
		result.ShouldNotBeNull();
		result.Name.ShouldBe("TestJobName");
		result.Interval.ShouldBe(30);
	}

	private sealed class TestConfig
	{
		public string Name { get; set; } = string.Empty;
		public int Interval { get; set; }
	}
}
