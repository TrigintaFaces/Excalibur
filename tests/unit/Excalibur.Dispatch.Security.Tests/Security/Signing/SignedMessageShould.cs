// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="SignedMessage"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SignedMessageShould
{
	[Fact]
	public void RequireContent()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test-content",
			Signature = "abc123",
		};

		// Assert
		message.Content.ShouldBe("test-content");
	}

	[Fact]
	public void RequireSignature()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test-content",
			Signature = "abc123signature",
		};

		// Assert
		message.Signature.ShouldBe("abc123signature");
	}

	[Fact]
	public void HaveUnknownAlgorithm_ByDefault()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Assert
		message.Algorithm.ShouldBe(SigningAlgorithm.Unknown);
	}

	[Fact]
	public void HaveNullKeyId_ByDefault()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Assert
		message.KeyId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultSignedAt_ByDefault()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Assert
		message.SignedAt.ShouldBe(default);
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Assert
		message.Metadata.ShouldNotBeNull();
		message.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingContent()
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "initial",
			Signature = "sig",
		};

		// Act
		message.Content = "updated-content";

		// Assert
		message.Content.ShouldBe("updated-content");
	}

	[Fact]
	public void AllowSettingSignature()
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "content",
			Signature = "initial-sig",
		};

		// Act
		message.Signature = "updated-signature";

		// Assert
		message.Signature.ShouldBe("updated-signature");
	}

	[Theory]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.HMACSHA512)]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.Ed25519)]
	public void AllowSettingAlgorithm(SigningAlgorithm algorithm)
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Act
		message.Algorithm = algorithm;

		// Assert
		message.Algorithm.ShouldBe(algorithm);
	}

	[Fact]
	public void AllowSettingKeyId()
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Act
		message.KeyId = "key-2024-01";

		// Assert
		message.KeyId.ShouldBe("key-2024-01");
	}

	[Fact]
	public void AllowSettingSignedAt()
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};
		var signedAt = DateTimeOffset.UtcNow;

		// Act
		message.SignedAt = signedAt;

		// Assert
		message.SignedAt.ShouldBe(signedAt);
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Act
		message.Metadata["version"] = "1.0";
		message.Metadata["source"] = "api";

		// Assert
		message.Metadata.Count.ShouldBe(2);
		message.Metadata["version"].ShouldBe("1.0");
		message.Metadata["source"].ShouldBe("api");
	}

	[Fact]
	public void AllowInitializingMetadataWithValues()
	{
		// Arrange & Act
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
			Metadata = new Dictionary<string, string>
			{
				["app"] = "excalibur",
				["env"] = "production",
			},
		};

		// Assert
		message.Metadata.Count.ShouldBe(2);
		message.Metadata["app"].ShouldBe("excalibur");
		message.Metadata["env"].ShouldBe("production");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var signedAt = DateTimeOffset.UtcNow;

		// Act
		var message = new SignedMessage
		{
			Content = "{\"type\":\"command\",\"data\":\"payload\"}",
			Signature = "base64signaturestring==",
			Algorithm = SigningAlgorithm.HMACSHA256,
			KeyId = "hmac-key-v2",
			SignedAt = signedAt,
			Metadata = new Dictionary<string, string>
			{
				["tenant"] = "tenant-123",
			},
		};

		// Assert
		message.Content.ShouldBe("{\"type\":\"command\",\"data\":\"payload\"}");
		message.Signature.ShouldBe("base64signaturestring==");
		message.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
		message.KeyId.ShouldBe("hmac-key-v2");
		message.SignedAt.ShouldBe(signedAt);
		message.Metadata["tenant"].ShouldBe("tenant-123");
	}

	[Fact]
	public void MetadataUseOrdinalStringComparison()
	{
		// Arrange
		var message = new SignedMessage
		{
			Content = "test",
			Signature = "sig",
		};

		// Act
		message.Metadata["Key"] = "value";

		// Assert - case-sensitive (Ordinal comparison)
		message.Metadata.ContainsKey("Key").ShouldBeTrue();
		message.Metadata.ContainsKey("key").ShouldBeFalse();
		message.Metadata.ContainsKey("KEY").ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(SignedMessage).IsSealed.ShouldBeTrue();
	}
}
