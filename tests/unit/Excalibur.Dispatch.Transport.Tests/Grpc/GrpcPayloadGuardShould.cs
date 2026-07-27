// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask/Task

using System.Reflection;

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Author≠impl ingress payload-guard locks (bead ydvyi7) for BOTH gRPC ingress surfaces — the pull
/// <see cref="GrpcTransportReceiver"/> and the push <see cref="GrpcTransportSubscriber"/>.
/// </summary>
/// <remarks>
/// The guard is an allocation-DoS control: an over-limit inbound body is rejected via
/// <c>PayloadSizeGuard.EnsureWithinLimit(grpcMessage.Body.Length, _maxPayloadBytes)</c> inside the
/// per-message <c>MapToReceivedMessage</c> convert BEFORE the Base64 body is materialized
/// (<c>Convert.FromBase64String</c>) — it throws <c>PayloadTooLargeException</c>, which the receive/
/// subscribe loop catches to drop the poison message (never truncated, never surfaced).
/// <para>
/// Non-vacuous: RED against the missing-guard mutant (remove the <c>EnsureWithinLimit</c> line → the
/// oversized body is materialized/returned instead of rejected); GREEN with the guard wired on both
/// surfaces. A <see langword="null"/> limit opts out (unbounded); the boundary is inclusive (N bytes OK).
/// </para>
/// <para>
/// Seam note: the gRPC transport takes a sealed <see cref="GrpcChannel"/> and creates its
/// <c>CallInvoker</c> in the constructor, so the full <c>ReceiveAsync</c>/<c>SubscribeAsync</c> loop is
/// not unit-drivable without a live gRPC server (unlike the SQS <c>IAmazonSQS</c> / RabbitMQ
/// <c>IChannel</c> fakes). These locks therefore drive the smallest real seam that runs the guard — the
/// private convert method where <c>EnsureWithinLimit</c> executes — asserting the reject/accept/opt-out
/// differential directly at the guard's execution site.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcPayloadGuardShould
{
    private const int MaxPayloadBytes = 8;

    // Base64 wire strings whose .Length is what the guard measures.
    private const string OversizedBody = "0123456789ABCDEFGHIJ"; // 20 chars > 8 (rejected before decode)
    private const string AtLimitBody = "12345678";               // 8 chars == limit, valid Base64
    private const string OversizedValidBase64 = "AAAAAAAAAAAA";  // 12 chars > 8, valid Base64 (opt-out path decodes)

    private static IOptions<GrpcTransportOptions> Options(int? max) =>
        Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
        {
            ServerAddress = "https://localhost:5001",
            Destination = "test-destination",
            DeadlineSeconds = 10,
            MaxPayloadBytes = max,
        });

    private static GrpcReceivedMessage Message(string id, string body) =>
        new() { Id = id, Body = body, Source = "test-destination" };

    // Reflection-invoke the private per-message convert where the guard runs, unwrapping the guard throw.
    private static object InvokeMap(object instance, GrpcReceivedMessage message)
    {
        var method = instance.GetType()
            .GetMethod("MapToReceivedMessage", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().Name, "MapToReceivedMessage");
        try
        {
            return method.Invoke(instance, [message])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    // ---- Pull surface: GrpcTransportReceiver ----

    [Fact]
    public async Task Receiver_RejectOversized_BeforeMaterialization()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:5001");
        await using var receiver = new GrpcTransportReceiver(
            channel, Options(MaxPayloadBytes), NullLogger<GrpcTransportReceiver>.Instance);

        // Guard fails closed: over-limit body throws before Convert.FromBase64String.
        var ex = Should.Throw<Exception>(() => InvokeMap(receiver, Message("m1", OversizedBody)));
        ex.GetType().Name.ShouldBe("PayloadTooLargeException");
    }

    [Fact]
    public async Task Receiver_AcceptMessage_AtExactLimit()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:5001");
        await using var receiver = new GrpcTransportReceiver(
            channel, Options(MaxPayloadBytes), NullLogger<GrpcTransportReceiver>.Instance);

        var mapped = (TransportReceivedMessage)InvokeMap(receiver, Message("m3", AtLimitBody));

        // Inclusive boundary: exactly 8 bytes passes and is fully materialized.
        mapped.Id.ShouldBe("m3");
        mapped.Body.ShouldBe(Convert.FromBase64String(AtLimitBody));
    }

    [Fact]
    public async Task Receiver_NotEnforce_WhenLimitNull()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:5001");
        await using var receiver = new GrpcTransportReceiver(
            channel, Options(max: null), NullLogger<GrpcTransportReceiver>.Instance);

        // Opt-out: an oversized body is accepted (no ingress rejection) when the limit is null.
        var mapped = (TransportReceivedMessage)InvokeMap(receiver, Message("m4", OversizedValidBase64));

        mapped.Id.ShouldBe("m4");
        mapped.Body.ShouldBe(Convert.FromBase64String(OversizedValidBase64));
    }

    // ---- Push surface: GrpcTransportSubscriber ----

    [Fact]
    public async Task Subscriber_RejectOversized_BeforeMaterialization()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:5001");
        await using var subscriber = new GrpcTransportSubscriber(
            channel, Options(MaxPayloadBytes), NullLogger<GrpcTransportSubscriber>.Instance);

        var ex = Should.Throw<Exception>(() => InvokeMap(subscriber, Message("s1", OversizedBody)));
        ex.GetType().Name.ShouldBe("PayloadTooLargeException");
    }

    [Fact]
    public async Task Subscriber_NotEnforce_WhenLimitNull()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:5001");
        await using var subscriber = new GrpcTransportSubscriber(
            channel, Options(max: null), NullLogger<GrpcTransportSubscriber>.Instance);

        var mapped = (TransportReceivedMessage)InvokeMap(subscriber, Message("s3", OversizedValidBase64));

        mapped.Id.ShouldBe("s3");
        mapped.Body.ShouldBe(Convert.FromBase64String(OversizedValidBase64));
    }
}
