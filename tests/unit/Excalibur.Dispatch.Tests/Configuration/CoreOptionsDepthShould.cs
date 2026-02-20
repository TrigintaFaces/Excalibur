// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CoreOptionsDepthShould
{
	// --- CompressionOptions ---

	[Fact]
	public void CompressionOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CompressionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.CompressionType.ShouldBe(CompressionType.Gzip);
		options.CompressionLevel.ShouldBe(6);
		options.MinimumSizeThreshold.ShouldBe(1024);
	}

	[Fact]
	public void CompressionOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CompressionOptions
		{
			Enabled = true,
			CompressionType = CompressionType.Brotli,
			CompressionLevel = 9,
			MinimumSizeThreshold = 512,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.CompressionType.ShouldBe(CompressionType.Brotli);
		options.CompressionLevel.ShouldBe(9);
		options.MinimumSizeThreshold.ShouldBe(512);
	}

	// --- CompressionType enum ---

	[Fact]
	public void CompressionType_HasExpectedValues()
	{
		// Assert
		((int)CompressionType.None).ShouldBe(0);
		((int)CompressionType.Gzip).ShouldBe(1);
		((int)CompressionType.Deflate).ShouldBe(2);
		((int)CompressionType.Lz4).ShouldBe(3);
		((int)CompressionType.Brotli).ShouldBe(4);
	}

	[Fact]
	public void CompressionType_HasExpectedCount()
	{
		// Assert
		Enum.GetValues<CompressionType>().Length.ShouldBe(5);
	}

	// --- DispatchProfileOptions ---

	[Fact]
	public void DispatchProfileOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DispatchProfileOptions();

		// Assert
		options.ProfileName.ShouldBe(string.Empty);
	}

	[Fact]
	public void DispatchProfileOptions_ProfileName_IsSettable()
	{
		// Act
		var options = new DispatchProfileOptions { ProfileName = "production" };

		// Assert
		options.ProfileName.ShouldBe("production");
	}

	// --- EncryptionOptions ---

	[Fact]
	public void EncryptionOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new EncryptionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		options.Key.ShouldBeNull();
		options.KeyDerivation.ShouldBeNull();
		options.EnableKeyRotation.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionOptions_AllProperties_AreSettable()
	{
		// Arrange
		var key = new byte[] { 1, 2, 3, 4 };
		var kdf = new KeyDerivationOptions();

		// Act
		var options = new EncryptionOptions
		{
			Enabled = true,
			Algorithm = EncryptionAlgorithm.Aes128Gcm,
			Key = key,
			KeyDerivation = kdf,
			EnableKeyRotation = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes128Gcm);
		options.Key.ShouldBe(key);
		options.KeyDerivation.ShouldBe(kdf);
		options.EnableKeyRotation.ShouldBeTrue();
	}

	// --- EncryptionAlgorithm enum ---

	[Fact]
	public void EncryptionAlgorithm_HasExpectedValues()
	{
		// Assert
		((int)EncryptionAlgorithm.None).ShouldBe(0);
		((int)EncryptionAlgorithm.Aes128Gcm).ShouldBe(1);
		((int)EncryptionAlgorithm.Aes256Gcm).ShouldBe(2);
	}

	[Fact]
	public void EncryptionAlgorithm_HasExpectedCount()
	{
		// Assert
		Enum.GetValues<EncryptionAlgorithm>().Length.ShouldBe(3);
	}

	// --- HealthCheckOptions ---

	[Fact]
	public void HealthCheckOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new HealthCheckOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HealthCheckOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new HealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(5),
			Interval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
	}

	// --- InMemoryBusOptions ---

	[Fact]
	public void InMemoryBusOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InMemoryBusOptions();

		// Assert
		options.MaxQueueLength.ShouldBe(1000);
		options.PreserveOrder.ShouldBeTrue();
		options.ProcessingDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void InMemoryBusOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new InMemoryBusOptions
		{
			MaxQueueLength = 500,
			PreserveOrder = false,
			ProcessingDelay = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.MaxQueueLength.ShouldBe(500);
		options.PreserveOrder.ShouldBeFalse();
		options.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	// --- JsonSerializationOptions ---

	[Fact]
	public void JsonSerializationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new JsonSerializationOptions();

		// Assert
		options.JsonSerializerOptions.ShouldNotBeNull();
		options.PreserveReferences.ShouldBeFalse();
		options.MaxDepth.ShouldBe(64);
	}

	[Fact]
	public void JsonSerializationOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new JsonSerializationOptions
		{
			PreserveReferences = true,
			MaxDepth = 32,
		};

		// Assert
		options.PreserveReferences.ShouldBeTrue();
		options.MaxDepth.ShouldBe(32);
	}

	[Fact]
	public void JsonSerializationOptions_SetNull_CreatesNewInstance()
	{
		// Arrange
		var options = new JsonSerializationOptions();

		// Act
		options.JsonSerializerOptions = null!;

		// Assert
		options.JsonSerializerOptions.ShouldNotBeNull();
	}

	[Fact]
	public void JsonSerializationOptions_BuildJsonSerializerOptions_SetsMaxDepth()
	{
		// Arrange
		var options = new JsonSerializationOptions { MaxDepth = 16 };

		// Act
		var result = options.BuildJsonSerializerOptions();

		// Assert
		result.MaxDepth.ShouldBe(16);
	}

	[Fact]
	public void JsonSerializationOptions_BuildJsonSerializerOptions_SetsPreserveReferences()
	{
		// Arrange
		var options = new JsonSerializationOptions { PreserveReferences = true };

		// Act
		var result = options.BuildJsonSerializerOptions();

		// Assert
		result.ReferenceHandler.ShouldBe(ReferenceHandler.Preserve);
	}

	[Fact]
	public void JsonSerializationOptions_BuildJsonSerializerOptions_NoPreserveReferences_ByDefault()
	{
		// Arrange
		var options = new JsonSerializationOptions();

		// Act
		var result = options.BuildJsonSerializerOptions();

		// Assert
		result.ReferenceHandler.ShouldBeNull();
	}

	// --- KeyDerivationOptions ---

	[Fact]
	public void KeyDerivationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new KeyDerivationOptions();

		// Assert
		options.Password.ShouldBeNull();
		options.Salt.ShouldBeNull();
		options.Iterations.ShouldBe(100_000);
	}

	[Fact]
	public void KeyDerivationOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new KeyDerivationOptions
		{
			Password = "secret",
			Salt = [0x01, 0x02, 0x03],
			Iterations = 200_000,
		};

		// Assert
		options.Password.ShouldBe("secret");
		options.Salt.ShouldNotBeNull();
		options.Salt.Length.ShouldBe(3);
		options.Iterations.ShouldBe(200_000);
	}

	// --- MessageBusHealthCheckOptions ---

	[Fact]
	public void MessageBusHealthCheckOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MessageBusHealthCheckOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.Interval.ShouldBe(TimeSpan.FromSeconds(30));
		options.FailureThreshold.ShouldBe(3);
	}

	[Fact]
	public void MessageBusHealthCheckOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MessageBusHealthCheckOptions
		{
			Enabled = true,
			Timeout = TimeSpan.FromSeconds(5),
			Interval = TimeSpan.FromMinutes(1),
			FailureThreshold = 5,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.Interval.ShouldBe(TimeSpan.FromMinutes(1));
		options.FailureThreshold.ShouldBe(5);
	}

	// --- MiddlewareRegistrationOptions ---

	[Fact]
	public void MiddlewareRegistrationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MiddlewareRegistrationOptions();

		// Assert
		options.Registrations.ShouldNotBeNull();
		options.Registrations.ShouldBeEmpty();
	}

	// --- MultiTransportOptions ---

	[Fact]
	public void MultiTransportOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MultiTransportOptions();

		// Assert
		options.Transports.ShouldNotBeNull();
		options.Transports.ShouldBeEmpty();
		options.DefaultTransport.ShouldBeNull();
		options.EnableFailover.ShouldBeTrue();
	}

	[Fact]
	public void MultiTransportOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MultiTransportOptions
		{
			DefaultTransport = "rabbitmq",
			EnableFailover = false,
		};

		// Assert
		options.DefaultTransport.ShouldBe("rabbitmq");
		options.EnableFailover.ShouldBeFalse();
	}

	[Fact]
	public void MultiTransportOptions_Transports_CanAddEntries()
	{
		// Arrange
		var options = new MultiTransportOptions();

		// Act
		options.Transports["rabbitmq"] = new TransportConfiguration();

		// Assert
		options.Transports.Count.ShouldBe(1);
		options.Transports.ShouldContainKey("rabbitmq");
	}

	// --- TracingOptions ---

	[Fact]
	public void TracingOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new TracingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.SamplingRatio.ShouldBe(1.0);
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void TracingOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new TracingOptions
		{
			Enabled = true,
			SamplingRatio = 0.5,
			IncludeSensitiveData = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.SamplingRatio.ShouldBe(0.5);
		options.IncludeSensitiveData.ShouldBeTrue();
	}
}
