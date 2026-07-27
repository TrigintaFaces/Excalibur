// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Dispatch.Tests.Transport.Grpc;

/// <summary>
/// DI-resolution regression lock for the keyed transport-adapter seam (bead <c>so2rhi</c>, and the
/// family guard for <c>jggd4u</c>/<c>oemmwy</c>). Proves the wired <see cref="ITransportAdapter"/> and the
/// keyed <see cref="ITransportSender"/> both <em>resolve</em> from a real container built by
/// <c>AddGrpcTransport(name, …)</c>. RED on the pre-fix implicit <c>TryAddSingleton&lt;GrpcTransportAdapter&gt;</c>
/// (ctor resolved an <strong>unkeyed</strong> sender that isn't registered → resolving the adapter throws);
/// GREEN on the factory fix that resolves <c>GetRequiredKeyedService&lt;ITransportSender&gt;(name)</c>.
/// This is the build-time guard the transport family was missing — it would have caught all three
/// keyed-registration defects.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportDiResolutionShould
{
    private const string TransportName = "t";

    [Fact]
    public async Task ResolveTransportAdapterFromKeyedRegistration()
    {
        await using var provider = BuildProvider();

        // Pre-fix this threw at registration (factory TryAddEnumerable) — and, before so2rhi, the adapter's
        // ctor resolved an unkeyed ITransportSender that AddGrpcTransport never registers.
        var adapter = provider.GetRequiredService<ITransportAdapter>();
        adapter.ShouldNotBeNull();
    }

    [Fact]
    public async Task ResolveKeyedTransportSender()
    {
        await using var provider = BuildProvider();

        var sender = provider.GetRequiredKeyedService<ITransportSender>(TransportName);
        sender.ShouldNotBeNull();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // adapter/sender factories require ILogger<T>
        services.AddGrpcTransport(TransportName, options => options.ServerAddress = "https://localhost:5001");
        return services.BuildServiceProvider();
    }
}
