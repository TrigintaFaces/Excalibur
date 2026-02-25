// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ConfluentSchemaRegistryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ConfluentSchemaRegistryOptions();

		// Assert
		options.Url.ShouldBe("http://localhost:8081");
		options.BasicAuthUserInfo.ShouldBeNull();
		options.MaxCachedSchemas.ShouldBe(1000);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableSslCertificateVerification.ShouldBeTrue();
		options.SslCaLocation.ShouldBeNull();
		options.SslKeyLocation.ShouldBeNull();
		options.SslCertificateLocation.ShouldBeNull();
		options.SslKeyPassword.ShouldBeNull();
		options.AutoRegisterSchemas.ShouldBeTrue();
		options.DefaultCompatibility.ShouldBe(CompatibilityMode.Backward);
		options.ValidateBeforeRegister.ShouldBeTrue();
		options.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.TopicName);
		options.CustomSubjectNameStrategyType.ShouldBeNull();
		options.CacheSchemas.ShouldBeTrue();
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
			EnableSslCertificateVerification = false,
			SslCaLocation = "/path/to/ca.crt",
			SslKeyLocation = "/path/to/key.pem",
			SslCertificateLocation = "/path/to/cert.pem",
			SslKeyPassword = "secret",
			AutoRegisterSchemas = false,
			DefaultCompatibility = CompatibilityMode.Full,
			ValidateBeforeRegister = false,
			SubjectNameStrategy = SubjectNameStrategy.RecordName,
			CacheSchemas = false,
		};

		// Assert
		options.Url.ShouldBe("https://registry.example.com:8081");
		options.BasicAuthUserInfo.ShouldBe("user:pass");
		options.MaxCachedSchemas.ShouldBe(500);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.EnableSslCertificateVerification.ShouldBeFalse();
		options.SslCaLocation.ShouldBe("/path/to/ca.crt");
		options.SslKeyLocation.ShouldBe("/path/to/key.pem");
		options.SslCertificateLocation.ShouldBe("/path/to/cert.pem");
		options.SslKeyPassword.ShouldBe("secret");
		options.AutoRegisterSchemas.ShouldBeFalse();
		options.DefaultCompatibility.ShouldBe(CompatibilityMode.Full);
		options.ValidateBeforeRegister.ShouldBeFalse();
		options.SubjectNameStrategy.ShouldBe(SubjectNameStrategy.RecordName);
		options.CacheSchemas.ShouldBeFalse();
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
			SubjectNameStrategy = SubjectNameStrategy.RecordName,
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
