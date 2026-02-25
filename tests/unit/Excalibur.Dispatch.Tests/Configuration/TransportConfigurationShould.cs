// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportConfigurationShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var config = new TransportConfiguration();

		// Assert
		config.Name.ShouldBe(string.Empty);
		config.Enabled.ShouldBeTrue();
		config.Priority.ShouldBe(100);
	}

	[Fact]
	public void Name_CanBeSet()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Name = "kafka-primary";

		// Assert
		config.Name.ShouldBe("kafka-primary");
	}

	[Fact]
	public void Enabled_CanBeDisabled()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Enabled = false;

		// Assert
		config.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange
		var config = new TransportConfiguration();

		// Act
		config.Priority = 50;

		// Assert
		config.Priority.ShouldBe(50);
	}
}
