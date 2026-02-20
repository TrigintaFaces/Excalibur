// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Security;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class TransportSecurityExceptionShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var exception = new TransportSecurityException();

        exception.Message.ShouldBe("Transport security requirements were not met.");
        exception.InnerException.ShouldBeNull();
        exception.TransportName.ShouldBeNull();
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.Unspecified);
    }

    [Fact]
    public void AllowCreationWithMessage()
    {
        var exception = new TransportSecurityException("TLS is required but not enabled");

        exception.Message.ShouldBe("TLS is required but not enabled");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void AllowCreationWithMessageAndInnerException()
    {
        var innerException = new System.Security.Authentication.AuthenticationException("Certificate validation failed");
        var exception = new TransportSecurityException("TLS handshake failed", innerException);

        exception.Message.ShouldBe("TLS handshake failed");
        exception.InnerException.ShouldBe(innerException);
    }

    [Theory]
    [InlineData("RabbitMQ")]
    [InlineData("Kafka")]
    [InlineData("AzureServiceBus")]
    [InlineData(null)]
    public void AllowSettingTransportName(string? transportName)
    {
        var exception = new TransportSecurityException("Security error")
        {
            TransportName = transportName
        };

        exception.TransportName.ShouldBe(transportName);
    }

    [Theory]
    [InlineData(TransportSecurityFailureReason.Unspecified)]
    [InlineData(TransportSecurityFailureReason.TlsNotEnabled)]
    [InlineData(TransportSecurityFailureReason.TlsVersionTooLow)]
    [InlineData(TransportSecurityFailureReason.CertificateValidationFailed)]
    [InlineData(TransportSecurityFailureReason.ClientCertificateFailed)]
    [InlineData(TransportSecurityFailureReason.ConnectionDowngraded)]
    public void AllowSettingFailureReason(TransportSecurityFailureReason reason)
    {
        var exception = new TransportSecurityException("Security error")
        {
            FailureReason = reason
        };

        exception.FailureReason.ShouldBe(reason);
    }

    [Fact]
    public void BeInvalidOperationException()
    {
        var exception = new TransportSecurityException();

        exception.ShouldBeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void AllowTlsNotEnabledConfiguration()
    {
        var exception = new TransportSecurityException("TLS is required but the connection is using plaintext")
        {
            TransportName = "RabbitMQ",
            FailureReason = TransportSecurityFailureReason.TlsNotEnabled
        };

        exception.Message.ShouldContain("TLS is required");
        exception.TransportName.ShouldBe("RabbitMQ");
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsNotEnabled);
    }

    [Fact]
    public void AllowTlsVersionTooLowConfiguration()
    {
        var exception = new TransportSecurityException("TLS 1.2 or higher is required, but TLS 1.0 was negotiated")
        {
            TransportName = "Kafka",
            FailureReason = TransportSecurityFailureReason.TlsVersionTooLow
        };

        exception.Message.ShouldContain("TLS");
        exception.TransportName.ShouldBe("Kafka");
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.TlsVersionTooLow);
    }

    [Fact]
    public void AllowCertificateValidationFailedConfiguration()
    {
        var innerException = new System.Security.Authentication.AuthenticationException("The remote certificate is invalid");
        var exception = new TransportSecurityException("Server certificate validation failed", innerException)
        {
            TransportName = "AzureServiceBus",
            FailureReason = TransportSecurityFailureReason.CertificateValidationFailed
        };

        exception.Message.ShouldContain("certificate validation");
        exception.InnerException.ShouldBe(innerException);
        exception.TransportName.ShouldBe("AzureServiceBus");
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.CertificateValidationFailed);
    }

    [Fact]
    public void AllowClientCertificateFailedConfiguration()
    {
        var exception = new TransportSecurityException("Client certificate (mTLS) authentication failed")
        {
            TransportName = "Kafka",
            FailureReason = TransportSecurityFailureReason.ClientCertificateFailed
        };

        exception.Message.ShouldContain("mTLS");
        exception.TransportName.ShouldBe("Kafka");
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.ClientCertificateFailed);
    }

    [Fact]
    public void AllowConnectionDowngradedConfiguration()
    {
        var exception = new TransportSecurityException("Connection was downgraded from TLS to plaintext")
        {
            TransportName = "RabbitMQ",
            FailureReason = TransportSecurityFailureReason.ConnectionDowngraded
        };

        exception.Message.ShouldContain("downgraded");
        exception.TransportName.ShouldBe("RabbitMQ");
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.ConnectionDowngraded);
    }

    [Fact]
    public void AllowAllFailureReasonsToBeEnumerated()
    {
        var reasons = Enum.GetValues<TransportSecurityFailureReason>();

        reasons.Length.ShouldBe(6);
        reasons.ShouldContain(TransportSecurityFailureReason.Unspecified);
        reasons.ShouldContain(TransportSecurityFailureReason.TlsNotEnabled);
        reasons.ShouldContain(TransportSecurityFailureReason.TlsVersionTooLow);
        reasons.ShouldContain(TransportSecurityFailureReason.CertificateValidationFailed);
        reasons.ShouldContain(TransportSecurityFailureReason.ClientCertificateFailed);
        reasons.ShouldContain(TransportSecurityFailureReason.ConnectionDowngraded);
    }
}
