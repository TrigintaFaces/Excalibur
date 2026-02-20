// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SigningModelsCoverageShould
{
    [Fact]
    public void SigningContextHaveCorrectDefaults()
    {
        // Act
        var context = new SigningContext();

        // Assert
        context.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
        context.KeyId.ShouldBeNull();
        context.TenantId.ShouldBeNull();
        context.IncludeTimestamp.ShouldBeTrue();
        context.Format.ShouldBe(SignatureFormat.Base64);
        context.Purpose.ShouldBeNull();
        context.Metadata.ShouldNotBeNull();
        context.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public void SigningContextSetAllProperties()
    {
        // Act
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA512,
            KeyId = "key-1",
            TenantId = "tenant-1",
            IncludeTimestamp = false,
            Format = SignatureFormat.Hex,
            Purpose = "testing",
        };
        context.Metadata["key"] = "value";

        // Assert
        context.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
        context.KeyId.ShouldBe("key-1");
        context.TenantId.ShouldBe("tenant-1");
        context.IncludeTimestamp.ShouldBeFalse();
        context.Format.ShouldBe(SignatureFormat.Hex);
        context.Purpose.ShouldBe("testing");
        context.Metadata["key"].ShouldBe("value");
    }

    [Fact]
    public void SignedMessageSetAllProperties()
    {
        // Act
        var now = DateTimeOffset.UtcNow;
        var msg = new SignedMessage
        {
            Content = "test-content",
            Signature = "test-sig",
            Algorithm = SigningAlgorithm.HMACSHA512,
            KeyId = "key-2",
            SignedAt = now,
            Metadata = { ["custom"] = "value" },
        };

        // Assert
        msg.Content.ShouldBe("test-content");
        msg.Signature.ShouldBe("test-sig");
        msg.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
        msg.KeyId.ShouldBe("key-2");
        msg.SignedAt.ShouldBe(now);
        msg.Metadata["custom"].ShouldBe("value");
    }

    [Fact]
    public void SigningOptionsHaveCorrectDefaults()
    {
        // Act
        var options = new SigningOptions();

        // Assert
        options.Enabled.ShouldBeTrue();
        options.DefaultAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
        options.DefaultKeyId.ShouldBeNull();
        options.MaxSignatureAgeMinutes.ShouldBe(5);
        options.IncludeTimestampByDefault.ShouldBeTrue();
        options.KeyRotationIntervalDays.ShouldBe(30);
        options.TenantAlgorithms.ShouldNotBeNull();
        options.TenantAlgorithms.Count.ShouldBe(0);
    }

    [Fact]
    public void SigningOptionsSetAllProperties()
    {
        // Act
        var options = new SigningOptions
        {
            Enabled = false,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA512,
            DefaultKeyId = "custom-key",
            MaxSignatureAgeMinutes = 10,
            IncludeTimestampByDefault = false,
            KeyRotationIntervalDays = 60,
        };
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.HMACSHA512;

        // Assert
        options.Enabled.ShouldBeFalse();
        options.DefaultAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA512);
        options.DefaultKeyId.ShouldBe("custom-key");
        options.MaxSignatureAgeMinutes.ShouldBe(10);
        options.IncludeTimestampByDefault.ShouldBeFalse();
        options.KeyRotationIntervalDays.ShouldBe(60);
        options.TenantAlgorithms["tenant-a"].ShouldBe(SigningAlgorithm.HMACSHA512);
    }

    [Fact]
    public void SigningExceptionConstructors()
    {
        // Parameterless
        var ex1 = new SigningException();
        ex1.Message.ShouldNotBeNull();

        // Message only
        var ex2 = new SigningException("test message");
        ex2.Message.ShouldBe("test message");

        // Message + inner exception
        var inner = new InvalidOperationException("inner");
        var ex3 = new SigningException("outer", inner);
        ex3.Message.ShouldBe("outer");
        ex3.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void VerificationExceptionConstructors()
    {
        // Parameterless
        var ex1 = new VerificationException();
        ex1.Message.ShouldNotBeNull();

        // Message only
        var ex2 = new VerificationException("test message");
        ex2.Message.ShouldBe("test message");

        // Message + inner exception
        var inner = new InvalidOperationException("inner");
        var ex3 = new VerificationException("outer", inner);
        ex3.Message.ShouldBe("outer");
        ex3.InnerException.ShouldBe(inner);
    }

    [Theory]
    [InlineData(SigningAlgorithm.Unknown, 0)]
    [InlineData(SigningAlgorithm.HMACSHA256, 1)]
    [InlineData(SigningAlgorithm.HMACSHA512, 2)]
    [InlineData(SigningAlgorithm.RSASHA256, 3)]
    [InlineData(SigningAlgorithm.RSAPSSSHA256, 4)]
    [InlineData(SigningAlgorithm.ECDSASHA256, 5)]
    [InlineData(SigningAlgorithm.Ed25519, 6)]
    public void SigningAlgorithmEnumValues(SigningAlgorithm algorithm, int expectedValue)
    {
        ((int)algorithm).ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData(SignatureFormat.Base64, 0)]
    [InlineData(SignatureFormat.Hex, 1)]
    [InlineData(SignatureFormat.Binary, 2)]
    public void SignatureFormatEnumValues(SignatureFormat format, int expectedValue)
    {
        ((int)format).ShouldBe(expectedValue);
    }
}
