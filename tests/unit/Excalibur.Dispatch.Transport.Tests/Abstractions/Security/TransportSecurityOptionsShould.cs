// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Security;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class TransportSecurityOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new TransportSecurityOptions();

        options.RequireTls.ShouldBeTrue();
        options.ValidateServerCertificate.ShouldBeTrue();
        options.MinimumTlsVersion.ShouldBe(TlsVersion.Tls12);
        options.ClientCertificatePath.ShouldBeNull();
        options.ClientCertificateKeyPath.ShouldBeNull();
        options.ClientCertificatePassword.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingRequireTls(bool require)
    {
        var options = new TransportSecurityOptions { RequireTls = require };

        options.RequireTls.ShouldBe(require);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingValidateServerCertificate(bool validate)
    {
        var options = new TransportSecurityOptions { ValidateServerCertificate = validate };

        options.ValidateServerCertificate.ShouldBe(validate);
    }

    [Theory]
    [InlineData(TlsVersion.Tls10)]
    [InlineData(TlsVersion.Tls11)]
    [InlineData(TlsVersion.Tls12)]
    [InlineData(TlsVersion.Tls13)]
    public void AllowSettingMinimumTlsVersion(TlsVersion version)
    {
        var options = new TransportSecurityOptions { MinimumTlsVersion = version };

        options.MinimumTlsVersion.ShouldBe(version);
    }

    [Theory]
    [InlineData("/path/to/client.crt")]
    [InlineData("C:\\certs\\client.pfx")]
    [InlineData(null)]
    public void AllowSettingClientCertificatePath(string? path)
    {
        var options = new TransportSecurityOptions { ClientCertificatePath = path };

        options.ClientCertificatePath.ShouldBe(path);
    }

    [Theory]
    [InlineData("/path/to/client.key")]
    [InlineData("C:\\certs\\client.key")]
    [InlineData(null)]
    public void AllowSettingClientCertificateKeyPath(string? path)
    {
        var options = new TransportSecurityOptions { ClientCertificateKeyPath = path };

        options.ClientCertificateKeyPath.ShouldBe(path);
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("")]
    [InlineData(null)]
    public void AllowSettingClientCertificatePassword(string? password)
    {
        var options = new TransportSecurityOptions { ClientCertificatePassword = password };

        options.ClientCertificatePassword.ShouldBe(password);
    }

    [Fact]
    public void AllowMtlsConfiguration()
    {
        var options = new TransportSecurityOptions
        {
            RequireTls = true,
            ValidateServerCertificate = true,
            MinimumTlsVersion = TlsVersion.Tls13,
            ClientCertificatePath = "/certs/client.crt",
            ClientCertificateKeyPath = "/certs/client.key",
            ClientCertificatePassword = "secret"
        };

        options.RequireTls.ShouldBeTrue();
        options.ValidateServerCertificate.ShouldBeTrue();
        options.MinimumTlsVersion.ShouldBe(TlsVersion.Tls13);
        options.ClientCertificatePath.ShouldBe("/certs/client.crt");
        options.ClientCertificateKeyPath.ShouldBe("/certs/client.key");
        options.ClientCertificatePassword.ShouldBe("secret");
    }

    [Fact]
    public void AllowDevelopmentConfiguration()
    {
        // Development/testing configuration that disables security checks
        var options = new TransportSecurityOptions
        {
            RequireTls = false,
            ValidateServerCertificate = false,
            MinimumTlsVersion = TlsVersion.Tls10
        };

        options.RequireTls.ShouldBeFalse();
        options.ValidateServerCertificate.ShouldBeFalse();
        options.MinimumTlsVersion.ShouldBe(TlsVersion.Tls10);
    }

    [Fact]
    public void AllowProductionConfiguration()
    {
        // Production-ready configuration with all security enabled
        var options = new TransportSecurityOptions
        {
            RequireTls = true,
            ValidateServerCertificate = true,
            MinimumTlsVersion = TlsVersion.Tls12
        };

        options.RequireTls.ShouldBeTrue();
        options.ValidateServerCertificate.ShouldBeTrue();
        options.MinimumTlsVersion.ShouldBe(TlsVersion.Tls12);
    }

    [Fact]
    public void AllowKafkaStyleMtlsConfiguration()
    {
        // Kafka mTLS configuration pattern
        var options = new TransportSecurityOptions
        {
            RequireTls = true,
            ValidateServerCertificate = true,
            MinimumTlsVersion = TlsVersion.Tls12,
            ClientCertificatePath = "/var/private/ssl/client.pem",
            ClientCertificateKeyPath = "/var/private/ssl/client.key"
        };

        options.ClientCertificatePath.ShouldBe("/var/private/ssl/client.pem");
        options.ClientCertificateKeyPath.ShouldBe("/var/private/ssl/client.key");
        options.ClientCertificatePassword.ShouldBeNull();
    }

    [Fact]
    public void AllowRabbitMqStyleMtlsConfiguration()
    {
        // RabbitMQ mTLS configuration pattern
        var options = new TransportSecurityOptions
        {
            RequireTls = true,
            ValidateServerCertificate = true,
            MinimumTlsVersion = TlsVersion.Tls12,
            ClientCertificatePath = "/path/to/client.pfx",
            ClientCertificatePassword = "pfx-password"
        };

        options.ClientCertificatePath.ShouldBe("/path/to/client.pfx");
        options.ClientCertificateKeyPath.ShouldBeNull();
        options.ClientCertificatePassword.ShouldBe("pfx-password");
    }

    [Fact]
    public void AllowTlsVersionEnumValues()
    {
        var versions = Enum.GetValues<TlsVersion>();

        versions.Length.ShouldBe(4);
        versions.ShouldContain(TlsVersion.Tls10);
        versions.ShouldContain(TlsVersion.Tls11);
        versions.ShouldContain(TlsVersion.Tls12);
        versions.ShouldContain(TlsVersion.Tls13);
    }

    [Fact]
    public void HaveCorrectTlsVersionValues()
    {
        ((int)TlsVersion.Tls10).ShouldBe(0);
        ((int)TlsVersion.Tls11).ShouldBe(1);
        ((int)TlsVersion.Tls12).ShouldBe(2);
        ((int)TlsVersion.Tls13).ShouldBe(3);
    }
}
