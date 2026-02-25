// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="ContractVersionCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class ContractVersionCheckOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_RequireExplicitVersions_IsFalse()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.RequireExplicitVersions.ShouldBeFalse();
	}

	[Fact]
	public void Default_FailOnIncompatibleVersions_IsTrue()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.FailOnIncompatibleVersions.ShouldBeTrue();
	}

	[Fact]
	public void Default_FailOnUnknownVersions_IsFalse()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.FailOnUnknownVersions.ShouldBeFalse();
	}

	[Fact]
	public void Default_RecordDeprecationMetrics_IsTrue()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.RecordDeprecationMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Default_Headers_VersionHeaderName_IsXMessageVersion()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Headers.VersionHeaderName.ShouldBe("X-Message-Version");
	}

	[Fact]
	public void Default_Headers_SchemaIdHeaderName_IsXSchemaMessageId()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Headers.SchemaIdHeaderName.ShouldBe("X-Schema-MessageId");
	}

	[Fact]
	public void Default_Headers_ProducerVersionHeaderName_IsXProducerVersion()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Headers.ProducerVersionHeaderName.ShouldBe("X-Producer-Version");
	}

	[Fact]
	public void Default_Headers_ProducerServiceHeaderName_IsXProducerService()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Headers.ProducerServiceHeaderName.ShouldBe("X-Producer-Service");
	}

	[Fact]
	public void Default_SupportedVersions_IsNull()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.SupportedVersions.ShouldBeNull();
	}

	[Fact]
	public void Default_BypassVersionCheckForTypes_IsNull()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.BypassVersionCheckForTypes.ShouldBeNull();
	}

	[Fact]
	public void Default_Headers_IsNotNull()
	{
		// Arrange & Act
		var options = new ContractVersionCheckOptions();

		// Assert
		options.Headers.ShouldNotBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new ContractVersionCheckOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RequireExplicitVersions_CanBeSet()
	{
		// Arrange
		var options = new ContractVersionCheckOptions();

		// Act
		options.RequireExplicitVersions = true;

		// Assert
		options.RequireExplicitVersions.ShouldBeTrue();
	}

	[Fact]
	public void SupportedVersions_CanBeSet()
	{
		// Arrange
		var options = new ContractVersionCheckOptions();

		// Act
		options.SupportedVersions = ["1.0", "1.1", "2.0"];

		// Assert
		_ = options.SupportedVersions.ShouldNotBeNull();
		options.SupportedVersions.Length.ShouldBe(3);
	}

	[Fact]
	public void BypassVersionCheckForTypes_CanBeSet()
	{
		// Arrange
		var options = new ContractVersionCheckOptions();

		// Act
		options.BypassVersionCheckForTypes = ["HealthCheck", "Ping"];

		// Assert
		_ = options.BypassVersionCheckForTypes.ShouldNotBeNull();
		options.BypassVersionCheckForTypes.Length.ShouldBe(2);
	}

	[Fact]
	public void Headers_CanBeReplaced()
	{
		// Arrange
		var options = new ContractVersionCheckOptions();

		// Act
		options.Headers = new VersionCheckHeaders { VersionHeaderName = "Custom" };

		// Assert
		options.Headers.VersionHeaderName.ShouldBe("Custom");
		options.Headers.SchemaIdHeaderName.ShouldBe("X-Schema-MessageId"); // default
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new ContractVersionCheckOptions
		{
			Enabled = false,
			RequireExplicitVersions = true,
			FailOnIncompatibleVersions = false,
			FailOnUnknownVersions = true,
			RecordDeprecationMetrics = false,
			Headers = new VersionCheckHeaders
			{
				VersionHeaderName = "Custom-Version",
				SchemaIdHeaderName = "Custom-Schema",
				ProducerVersionHeaderName = "Custom-Producer-Version",
				ProducerServiceHeaderName = "Custom-Producer-Service",
			},
			SupportedVersions = ["1.0"],
			BypassVersionCheckForTypes = ["Test"],
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireExplicitVersions.ShouldBeTrue();
		options.FailOnIncompatibleVersions.ShouldBeFalse();
		options.FailOnUnknownVersions.ShouldBeTrue();
		options.RecordDeprecationMetrics.ShouldBeFalse();
		options.Headers.VersionHeaderName.ShouldBe("Custom-Version");
		_ = options.SupportedVersions.ShouldNotBeNull();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForStrictVersioning_RequiresExplicitVersions()
	{
		// Act
		var options = new ContractVersionCheckOptions
		{
			RequireExplicitVersions = true,
			FailOnIncompatibleVersions = true,
			FailOnUnknownVersions = true,
			SupportedVersions = ["1.0", "1.1"],
		};

		// Assert
		options.RequireExplicitVersions.ShouldBeTrue();
		options.FailOnUnknownVersions.ShouldBeTrue();
		_ = options.SupportedVersions.ShouldNotBeNull();
	}

	[Fact]
	public void Options_ForFlexibleVersioning_AllowsUnknownVersions()
	{
		// Act
		var options = new ContractVersionCheckOptions
		{
			RequireExplicitVersions = false,
			FailOnIncompatibleVersions = false,
			FailOnUnknownVersions = false,
		};

		// Assert
		options.RequireExplicitVersions.ShouldBeFalse();
		options.FailOnUnknownVersions.ShouldBeFalse();
	}

	#endregion
}
