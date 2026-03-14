// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for <see cref="SchemaRegistrySchemaOptions"/>.
/// Verifies defaults and property assignment for schema-level settings.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SchemaRegistrySchemaOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new SchemaRegistrySchemaOptions();

		// Assert
		options.AutoRegisterSchemas.ShouldBeTrue();
		options.ValidateBeforeRegister.ShouldBeTrue();
		options.DefaultCompatibility.ShouldBe(CompatibilityMode.Backward);
		options.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.TopicName);
		options.CustomSubjectNameStrategyType.ShouldBeNull();
	}

	[Fact]
	public void AllowDisablingAutoRegistration()
	{
		// Arrange & Act
		var options = new SchemaRegistrySchemaOptions
		{
			AutoRegisterSchemas = false,
			ValidateBeforeRegister = false
		};

		// Assert
		options.AutoRegisterSchemas.ShouldBeFalse();
		options.ValidateBeforeRegister.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomCompatibilityMode()
	{
		// Arrange & Act
		var options = new SchemaRegistrySchemaOptions
		{
			DefaultCompatibility = CompatibilityMode.Full
		};

		// Assert
		options.DefaultCompatibility.ShouldBe(CompatibilityMode.Full);
	}

	[Fact]
	public void AllowCustomSubjectNameStrategy()
	{
		// Arrange & Act
		var options = new SchemaRegistrySchemaOptions
		{
			SubjectNameStrategy = SubjectNameStrategy.RecordName
		};

		// Assert
		options.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.RecordName);
	}

	[Fact]
	public void AllowCustomSubjectNameStrategyType()
	{
		// Arrange & Act
		var options = new SchemaRegistrySchemaOptions
		{
			CustomSubjectNameStrategyType = typeof(TopicNameStrategy)
		};

		// Assert
		options.CustomSubjectNameStrategyType.ShouldBe(typeof(TopicNameStrategy));
	}
}
