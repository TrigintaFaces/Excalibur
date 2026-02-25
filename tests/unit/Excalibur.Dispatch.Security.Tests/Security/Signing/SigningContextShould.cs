// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="SigningContext"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SigningContextShould
{
	[Fact]
	public void HaveHMACSHA256Algorithm_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
	}

	[Fact]
	public void HaveNullKeyId_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.KeyId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullTenantId_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.TenantId.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.Metadata.ShouldNotBeNull();
		context.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void HaveTrueIncludeTimestamp_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.IncludeTimestamp.ShouldBeTrue();
	}

	[Fact]
	public void HaveBase64Format_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.Format.ShouldBe(SignatureFormat.Base64);
	}

	[Fact]
	public void HaveNullPurpose_ByDefault()
	{
		// Arrange & Act
		var context = new SigningContext();

		// Assert
		context.Purpose.ShouldBeNull();
	}

	[Theory]
	[InlineData(SigningAlgorithm.Unknown)]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.HMACSHA512)]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.Ed25519)]
	public void AllowSettingAlgorithm(SigningAlgorithm algorithm)
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.Algorithm = algorithm;

		// Assert
		context.Algorithm.ShouldBe(algorithm);
	}

	[Fact]
	public void AllowSettingKeyId()
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.KeyId = "my-signing-key";

		// Assert
		context.KeyId.ShouldBe("my-signing-key");
	}

	[Fact]
	public void AllowSettingTenantId()
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.TenantId = "tenant-abc";

		// Assert
		context.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.Metadata["operation"] = "create-order";
		context.Metadata["region"] = "us-east-1";

		// Assert
		context.Metadata.Count.ShouldBe(2);
		context.Metadata["operation"].ShouldBe("create-order");
		context.Metadata["region"].ShouldBe("us-east-1");
	}

	[Fact]
	public void AllowSettingIncludeTimestamp()
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.IncludeTimestamp = false;

		// Assert
		context.IncludeTimestamp.ShouldBeFalse();
	}

	[Theory]
	[InlineData(SignatureFormat.Base64)]
	[InlineData(SignatureFormat.Hex)]
	[InlineData(SignatureFormat.Binary)]
	public void AllowSettingFormat(SignatureFormat format)
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.Format = format;

		// Assert
		context.Format.ShouldBe(format);
	}

	[Fact]
	public void AllowSettingPurpose()
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.Purpose = "message-authentication";

		// Assert
		context.Purpose.ShouldBe("message-authentication");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.RSASHA256,
			KeyId = "rsa-key-prod",
			TenantId = "enterprise-tenant",
			IncludeTimestamp = true,
			Format = SignatureFormat.Hex,
			Purpose = "api-request-signing",
			Metadata = new Dictionary<string, string>
			{
				["service"] = "payment-service",
			},
		};

		// Assert
		context.Algorithm.ShouldBe(SigningAlgorithm.RSASHA256);
		context.KeyId.ShouldBe("rsa-key-prod");
		context.TenantId.ShouldBe("enterprise-tenant");
		context.IncludeTimestamp.ShouldBeTrue();
		context.Format.ShouldBe(SignatureFormat.Hex);
		context.Purpose.ShouldBe("api-request-signing");
		context.Metadata["service"].ShouldBe("payment-service");
	}

	[Fact]
	public void MetadataUseOrdinalStringComparison()
	{
		// Arrange
		var context = new SigningContext();

		// Act
		context.Metadata["AppId"] = "test-app";

		// Assert - case-sensitive (Ordinal comparison)
		context.Metadata.ContainsKey("AppId").ShouldBeTrue();
		context.Metadata.ContainsKey("appid").ShouldBeFalse();
		context.Metadata.ContainsKey("APPID").ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(SigningContext).IsSealed.ShouldBeTrue();
	}
}
