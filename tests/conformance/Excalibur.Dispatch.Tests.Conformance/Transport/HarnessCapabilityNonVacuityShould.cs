// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CloudNative.CloudEvents;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Non-vacuity gate for the urttf7 transport-conformance capability seam (AC-U1..U4). Each capability
/// assertion (header-surfacing, CloudEvents binding, ack/nack redelivery, filtering) MUST be GREEN against
/// the <see cref="ConformingInMemoryTransport" /> and RED against the <see cref="NonConformingInMemoryTransport" />.
/// This proves the seam exposes <b>real, RED-able</b> assertions — closing the htcbgu false-conformance hole
/// where a capability fact would pass even for a transport with zero support — before the per-transport
/// conversions (bd-1rbj0a / bd-5dox7c / bd-jj4hx4 / bd-liyait) build on it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HarnessCapabilityNonVacuityShould
{
	// ---- AC-U1 : header-surfacing (bd-liyait) ----

	/// <summary>AC-U1: a header round-trip assertion passes against a header-preserving transport.</summary>
	[Fact]
	public async Task Surface_Carrier_Headers_Against_A_Conforming_Transport() =>
		await AssertHeaderRoundTripAsync(new ConformingInMemoryTransport());

	/// <summary>
	/// AC-U1 / AC-U4: the same header round-trip assertion is RED against a transport that discards headers
	/// (no false conformance).
	/// </summary>
	[Fact]
	public async Task Fail_The_Header_Assertion_Against_A_Header_Discarding_Transport() =>
		await ShouldFailConformanceAsync(() => AssertHeaderRoundTripAsync(new NonConformingInMemoryTransport()));

	// ---- AC-U2 : CloudEvents binding (bd-jj4hx4) ----

	/// <summary>AC-U2: a CloudEvents semantic-equality round-trip passes against a CE-binding transport.</summary>
	[Fact]
	public async Task RoundTrip_CloudEvents_Against_A_Conforming_Transport() =>
		await AssertCloudEventRoundTripAsync(new ConformingInMemoryTransport());

	/// <summary>
	/// AC-U2 / AC-U4: the CloudEvents round-trip is RED against a zero-CE transport double (the htcbgu defect
	/// — a POCO round-trip would otherwise pass for a transport with no CloudEvents support).
	/// </summary>
	[Fact]
	public async Task Fail_The_CloudEvents_Assertion_Against_A_Zero_CE_Transport() =>
		await ShouldFailConformanceAsync(() => AssertCloudEventRoundTripAsync(new NonConformingInMemoryTransport()));

	// ---- AC-U3 : ack/nack redelivery (bd-5dox7c) ----

	/// <summary>AC-U3: a nack'd message is redelivered by a transport that supports at-least-once.</summary>
	[Fact]
	public async Task Redeliver_A_Nacked_Message_Against_A_Conforming_Transport() =>
		await AssertNackRedeliversAsync(new ConformingInMemoryTransport());

	/// <summary>
	/// AC-U3 / AC-U4: a single send/receive double that never redelivers FAILS the at-least-once assertion.
	/// </summary>
	[Fact]
	public async Task Fail_The_Redelivery_Assertion_Against_A_Non_Redelivering_Transport() =>
		await ShouldFailConformanceAsync(() => AssertNackRedeliversAsync(new NonConformingInMemoryTransport()));

	// ---- AC-F1 (proven at the seam) : filtering (bd-1rbj0a) ----

	/// <summary>AC-F1: filtering returns only the matching message against a filtering transport.</summary>
	[Fact]
	public async Task Filter_To_The_Matching_Message_Against_A_Conforming_Transport() =>
		await AssertFilteringAsync(new ConformingInMemoryTransport());

	/// <summary>
	/// AC-F1 / AC-U4: a transport that ignores the filter FAILS the filtering assertion (it returns a
	/// non-matching message).
	/// </summary>
	[Fact]
	public async Task Fail_The_Filtering_Assertion_Against_A_Non_Filtering_Transport() =>
		await ShouldFailConformanceAsync(() => AssertFilteringAsync(new NonConformingInMemoryTransport()));

	/// <summary>
	/// Runs a capability conformance assertion and requires it to be RED (throw <see cref="ShouldAssertException" />).
	/// This is the non-vacuity gate: a capability fact that cannot fail against a non-conforming transport is
	/// vacuous. (We catch Shouldly's own assertion exception explicitly rather than via
	/// <c>Should.ThrowAsync&lt;ShouldAssertException&gt;</c>, which special-cases its own type.)
	/// </summary>
	private static async Task ShouldFailConformanceAsync(Func<Task> conformanceAssertion)
	{
		var wentRed = false;
		try
		{
			await conformanceAssertion();
		}
		catch (ShouldAssertException)
		{
			wentRed = true;
		}

		wentRed.ShouldBeTrue(
			"The capability conformance assertion MUST be RED against a non-conforming transport double "
			+ "(non-vacuity gate AC-U4) — a fact that cannot fail proves nothing.");
	}

	// ---- Capability assertions (the seam's real, RED-able checks) ----

	private static async Task AssertHeaderRoundTripAsync(ITransportConformanceCapabilities transport)
	{
		transport.Capabilities.HasFlag(TransportCapability.HeaderSurfacing).ShouldBeTrue();

		var headers = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["x-trace-id"] = "trace-abc-123",
			["x-tenant"] = "tenant-42",
		};
		var message = NewMessage();

		await transport.SendWithHeadersAsync(message, headers, CancellationToken.None);
		var received = await transport.ReceiveWithContextAsync<TestMessage>(CancellationToken.None);

		_ = received.ShouldNotBeNull();
		received.Headers.ShouldContainKey("x-trace-id");
		received.Headers["x-trace-id"].ShouldBe("trace-abc-123");
		received.Headers["x-tenant"].ShouldBe("tenant-42");
	}

	private static async Task AssertCloudEventRoundTripAsync(ITransportConformanceCapabilities transport)
	{
		transport.Capabilities.HasFlag(TransportCapability.CloudEventsBinding).ShouldBeTrue();

		var sent = new CloudEvent
		{
			Id = Guid.NewGuid().ToString(),
			Source = new Uri("https://example.com/conformance"),
			Type = "com.example.test.roundtrip",
			DataContentType = "application/json",
			Subject = "test/subject",
			Time = DateTimeOffset.UtcNow,
			Data = "payload-42",
		};

		await transport.SendCloudEventAsync(sent, CloudEventBinding.Binary, CancellationToken.None);
		var received = await transport.ReceiveCloudEventAsync(CloudEventBinding.Binary, CancellationToken.None);

		_ = received.ShouldNotBeNull();
		received.Id.ShouldBe(sent.Id);
		received.Source.ShouldBe(sent.Source);
		received.Type.ShouldBe(sent.Type);
		received.Subject.ShouldBe(sent.Subject);
		_ = received.Time.ShouldNotBeNull();
		received.Time.Value.ShouldBe(sent.Time!.Value, TimeSpan.FromMilliseconds(1));
		received.DataContentType.ShouldBe(sent.DataContentType);
	}

	private static async Task AssertNackRedeliversAsync(ITransportConformanceCapabilities transport)
	{
		transport.Capabilities.HasFlag(TransportCapability.AckNackRedelivery).ShouldBeTrue();

		var message = NewMessage();
		await transport.SendWithHeadersAsync(
			message,
			new Dictionary<string, string>(StringComparer.Ordinal),
			CancellationToken.None);

		var first = await transport.ReceiveWithContextAsync<TestMessage>(CancellationToken.None);
		_ = first.ShouldNotBeNull();
		first.Body!.Id.ShouldBe(message.Id);

		// Nack → the transport MUST redeliver the same message.
		await first.RejectAsync(CancellationToken.None);

		var redelivered = await transport.ReceiveWithContextAsync<TestMessage>(CancellationToken.None);
		_ = redelivered.ShouldNotBeNull();
		redelivered.Body!.Id.ShouldBe(message.Id);
	}

	private static async Task AssertFilteringAsync(ITransportConformanceCapabilities transport)
	{
		transport.Capabilities.HasFlag(TransportCapability.Filtering).ShouldBeTrue();

		var keep = NewMessage("keep");
		var drop = NewMessage("drop");

		// The non-matching message is sent FIRST so a transport that ignores the filter returns it (RED).
		await transport.SendFilterableAsync(
			drop,
			new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "drop" },
			CancellationToken.None);
		await transport.SendFilterableAsync(
			keep,
			new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "keep" },
			CancellationToken.None);

		var received = await transport.ReceiveMatchingAsync<TestMessage>(
			new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "keep" },
			CancellationToken.None);

		_ = received.ShouldNotBeNull();
		received.Body!.Content.ShouldBe("keep");
	}

	private static TestMessage NewMessage(string content = "conformance") => new()
	{
		Id = Guid.NewGuid().ToString(),
		Content = content,
		Timestamp = DateTimeOffset.UtcNow,
	};
}
