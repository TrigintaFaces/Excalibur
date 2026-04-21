// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ConfluentSchemaRegistryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ConfluentSchemaRegistryOptions();

		// Assert - root properties
		options.Url.ShouldBe("http://localhost:8081");
		options.BasicAuthUserInfo.ShouldBeNull();
		options.MaxCachedSchemas.ShouldBe(1000);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.CacheSchemas.ShouldBeTrue();

		// Assert - SSL sub-options
		options.Ssl.ShouldNotBeNull();
		options.Ssl.EnableSslCertificateVerification.ShouldBeTrue();
		options.Ssl.SslCaLocation.ShouldBeNull();
		options.Ssl.SslKeyLocation.ShouldBeNull();
		options.Ssl.SslCertificateLocation.ShouldBeNull();
		options.Ssl.SslKeyPassword.ShouldBeNull();

		// Assert - Schema sub-options
		options.Schema.ShouldNotBeNull();
		options.Schema.AutoRegisterSchemas.ShouldBeTrue();
		options.Schema.DefaultCompatibility.ShouldBe(CompatibilityMode.Backward);
		options.Schema.ValidateBeforeRegister.ShouldBeTrue();
		options.Schema.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.TopicName);
		options.Schema.CustomSubjectNameStrategyType.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new ConfluentSchemaRegistryOptions
		{
			Url = "https://registry.example.com:8081",
			BasicAuthUserInfo = "user:pass",
			MaxCachedSchemas = 500,
			RequestTimeout = TimeSpan.FromSeconds(10),
			CacheSchemas = false,
			Ssl = new SchemaRegistrySslOptions
			{
				EnableSslCertificateVerification = false,
				SslCaLocation = "/path/to/ca.crt",
				SslKeyLocation = "/path/to/key.pem",
				SslCertificateLocation = "/path/to/cert.pem",
				SslKeyPassword = "secret",
			},
			Schema = new SchemaRegistrySchemaOptions
			{
				AutoRegisterSchemas = false,
				DefaultCompatibility = CompatibilityMode.Full,
				ValidateBeforeRegister = false,
				SubjectNameStrategy = SubjectNameStrategy.RecordName,
			},
		};

		// Assert - root
		options.Url.ShouldBe("https://registry.example.com:8081");
		options.BasicAuthUserInfo.ShouldBe("user:pass");
		options.MaxCachedSchemas.ShouldBe(500);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.CacheSchemas.ShouldBeFalse();

		// Assert - SSL
		options.Ssl.EnableSslCertificateVerification.ShouldBeFalse();
		options.Ssl.SslCaLocation.ShouldBe("/path/to/ca.crt");
		options.Ssl.SslKeyLocation.ShouldBe("/path/to/key.pem");
		options.Ssl.SslCertificateLocation.ShouldBe("/path/to/cert.pem");
		options.Ssl.SslKeyPassword.ShouldBe("secret");

		// Assert - Schema
		options.Schema.AutoRegisterSchemas.ShouldBeFalse();
		options.Schema.DefaultCompatibility.ShouldBe(CompatibilityMode.Full);
		options.Schema.ValidateBeforeRegister.ShouldBeFalse();
		options.Schema.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.RecordName);
	}

	[Fact]
	public void CreateTopicNameStrategyByDefault()
	{
		// Arrange
		var options = new ConfluentSchemaRegistryOptions();

		// Act
#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode
		var strategy = options.CreateSubjectNameStrategy();
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		strategy.ShouldBeOfType<TopicNameStrategy>();
	}

	[Fact]
	public void CreateRecordNameStrategyWhenConfigured()
	{
		// Arrange
		var options = new ConfluentSchemaRegistryOptions
		{
			Schema = new SchemaRegistrySchemaOptions
			{
				SubjectNameStrategy = SubjectNameStrategy.RecordName,
			},
		};

		// Act
#pragma warning disable IL2026
#pragma warning disable IL3050
		var strategy = options.CreateSubjectNameStrategy();
#pragma warning restore IL3050
#pragma warning restore IL2026

		// Assert
		strategy.ShouldBeOfType<RecordNameStrategy>();
	}
}
