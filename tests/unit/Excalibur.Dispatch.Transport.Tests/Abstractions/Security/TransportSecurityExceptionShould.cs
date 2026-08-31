// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Security;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.TransportAbstractions)]
public sealed class TransportSecurityExceptionShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var exception = new TransportSecurityException();

        exception.Message.ShouldBe("Transport security requirements were not met.");
        exception.InnerException!.ShouldBeNull();
        exception.TransportName.ShouldBeNull();
        exception.FailureReason.ShouldBe(TransportSecurityFailureReason.Unspecified);
    }

    [Fact]
    public void AllowCreationWithMessage()
    {
        var exception = new TransportSecurityException("TLS is required but not enabled");

        exception.Message.ShouldBe("TLS is required but not enabled");
        exception.InnerException!.ShouldBeNull();
    }

    [Fact]
    public void AllowCreationWithMessageAndInnerException()
    {
        var innerException = new System.Security.Authentication.AuthenticationException("Certificate validation failed");
        var exception = new TransportSecurityException("TLS handshake failed", innerException);

        exception.Message.ShouldBe("TLS handshake failed");
        exception.InnerException!.ShouldBe(innerException);
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
    public void AllowAllFailureReasonsToBeEnumerated()
    {
        var reasons = Enum.GetValues<TransportSecurityFailureReason>();

        reasons.Length.ShouldBe(2);
        reasons.ShouldContain(TransportSecurityFailureReason.Unspecified);
        reasons.ShouldContain(TransportSecurityFailureReason.TlsNotEnabled);
    }
}
