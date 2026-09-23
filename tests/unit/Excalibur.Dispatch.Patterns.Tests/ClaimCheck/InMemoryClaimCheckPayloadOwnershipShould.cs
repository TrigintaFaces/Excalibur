// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// A stored claim-check payload is owned by the store, not shared with the caller on either boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> When compression was disabled, skipped or ineffective, the provider retained the
/// PRODUCER'S array and later handed that same array back to the consumer. Two independent ownership
/// breaks fell out of that: a producer mutating the buffer it had just stored changed what a later
/// retrieval returned, and a consumer mutating what it was given corrupted the stored payload for
/// everyone after it.
/// </para>
/// <para>
/// <b>WHY THIS IS THE ORDINARY PATH, NOT AN EDGE CASE.</b> The defaults skip compression below the size
/// threshold, so most payloads take the aliasing route. The contract says the original payload is
/// returned; with checksum validation on (also the default) the break surfaces as a corruption error on
/// a payload nothing corrupted, and with validation off it silently returns altered bytes.
/// </para>
/// <para>
/// <b>SCOPE, stated honestly:</b> this provider is advertised for development and testing, so this is a
/// correctness break in the contract rather than durable production data loss.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Patterns)]
public sealed class InMemoryClaimCheckPayloadOwnershipShould
{
	/// <summary>
	/// SAFETY, producer side. Mutating the buffer after handing it over must not change what was stored.
	/// </summary>
	[Fact]
	public async Task NotObserveAProducerMutatingTheArrayItAlreadyStored()
	{
		var provider = CreateProvider();
		var payload = Encoding.UTF8.GetBytes("the original payload");
		var expected = payload.AsSpan().ToArray();

		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		// The producer reuses its own buffer, which it is entitled to do: it handed over a value, not
		// custody of the array.
		payload[0] = (byte)'X';

		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);

		retrieved.ShouldBe(
			expected,
			"the store must have taken its own copy. Retaining the producer's array means a caller reusing "
			+ "its buffer silently rewrites stored data -- and with checksum validation on, the victim sees "
			+ "a corruption error on a payload nobody corrupted");
	}

	/// <summary>
	/// SAFETY, consumer side. The break most likely to be tripped, because mutating a buffer you were
	/// handed feels safe.
	/// </summary>
	[Fact]
	public async Task NotLetAConsumerMutatingItsResultCorruptTheStoredPayload()
	{
		var provider = CreateProvider();
		var payload = Encoding.UTF8.GetBytes("the original payload");
		var expected = payload.AsSpan().ToArray();

		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		var first = await provider.RetrieveAsync(reference, CancellationToken.None);
		first[0] = (byte)'X';

		var second = await provider.RetrieveAsync(reference, CancellationToken.None);

		second.ShouldBe(
			expected,
			"each retrieval must hand back its own copy. Returning the stored array lets one consumer's "
			+ "in-place edit change the payload every later consumer receives");
	}

	/// <summary>
	/// SAFETY. Two retrievals must not be the same object, which is the property both arms above rest on.
	/// </summary>
	[Fact]
	public async Task HandBackADistinctArrayOnEveryRetrieval()
	{
		var provider = CreateProvider();
		var payload = Encoding.UTF8.GetBytes("the original payload");

		var reference = await provider.StoreAsync(payload, CancellationToken.None);

		var first = await provider.RetrieveAsync(reference, CancellationToken.None);
		var second = await provider.RetrieveAsync(reference, CancellationToken.None);

		first.ShouldNotBeSameAs(second, "each caller owns what it is given");
		first.ShouldNotBeSameAs(payload, "and none of them is the producer's array");
	}

	/// <summary>
	/// LIVENESS, and the arm that stops the three above being satisfied by a provider that returns
	/// anything at all: the round-trip must still return the ORIGINAL BYTES, uncompressed and compressed.
	/// A defensive copy that copied the wrong thing would pass every safety arm here.
	/// </summary>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StillRoundTripThePayloadUnchanged(bool compressed)
	{
		// A large, highly compressible payload takes the compression path; a short one does not. The
		// compressed path already owned its buffer, so this arm proves the copy did not disturb it.
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = compressed;
			options.CompressionThreshold = 1024;
		});

		var payload = compressed
			? Encoding.UTF8.GetBytes(new string('A', 5000))
			: Encoding.UTF8.GetBytes("short");

		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);

		retrieved.ShouldBe(payload, "the contract promises the original payload back, byte for byte");
	}

	private static InMemoryClaimCheckProvider CreateProvider(Action<ClaimCheckOptions>? configure = null)
	{
		var options = new ClaimCheckOptions();
		configure?.Invoke(options);

		return new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options));
	}
}
