// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Pins the erasure tombstone predicate. Every stream reader in the framework routes its
/// tombstone recognition through <see cref="ErasedEventMarker.IsErased"/>, so this is the single
/// point at which the recognition rule could drift for all of them at once.
/// </summary>
/// <remarks>
/// The rule that matters is exactness. A recognizer that is too loose would classify a genuinely
/// corrupt or unregistered event as an erasure and silently skip it, turning real data loss into a
/// non-event. A recognizer that is too tight would fail to recognize a real tombstone, which is the
/// permanent-wedge defect these readers were fixed for.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ErasedEventMarkerShould
{
	[Fact]
	public void RecognizeTheReservedMarker() =>
		ErasedEventMarker.IsErased(ErasedEventMarker.EventType).ShouldBeTrue();

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("$ERASED")]          // wrong case: ordinal comparison, never culture- or case-insensitive
	[InlineData("$erased ")]         // trailing whitespace
	[InlineData(" $erased")]         // leading whitespace
	[InlineData("$erased2")]
	[InlineData("erased")]
	[InlineData("$erasedEvent")]
	[InlineData("MyApp.Events.OrderPlaced")]
	[InlineData("MyApp.Events.OrderPlaced, MyApp, Version=1.0.0.0")]
	public void RejectAnythingThatIsNotExactlyTheMarker(string? eventType) =>
		ErasedEventMarker.IsErased(eventType).ShouldBeFalse(
			"only the exact reserved marker is an erasure; anything else is a real event type and a "
			+ "failure to resolve it is genuine corruption, never a licence to skip");
}
