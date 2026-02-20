// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.StreamingPull;

/// <summary>
/// Regression tests for S541.10 (bd-z52x2): TOCTOU race in StreamingPullStream.HasMessage().
/// Validates that _outstandingMessages uses ConcurrentDictionary for thread-safe access.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingPullStreamConcurrencyShould : UnitTestBase
{
	#region ConcurrentDictionary Verification (S541.10)

	[Fact]
	public void UsesConcurrentDictionaryForOutstandingMessages()
	{
		// _outstandingMessages must be ConcurrentDictionary (AD-541.2 — TOCTOU fix)
		var field = typeof(StreamingPullStream)
			.GetField("_outstandingMessages", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_outstandingMessages field must exist");
		field.FieldType.ShouldBe(
			typeof(ConcurrentDictionary<string, ReceivedMessage>),
			"_outstandingMessages must be ConcurrentDictionary for thread-safe HasMessage() access (TOCTOU fix)");
	}

	[Fact]
	public void NotUseDictionaryForOutstandingMessages()
	{
		// Negative check: ensure we didn't accidentally use Dictionary
		var field = typeof(StreamingPullStream)
			.GetField("_outstandingMessages", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field.FieldType.ShouldNotBe(
			typeof(Dictionary<string, ReceivedMessage>),
			"_outstandingMessages must NOT be Dictionary — use ConcurrentDictionary for thread safety");
	}

	#endregion

	#region HasMessage Thread Safety

	[Fact]
	public void HavePublicHasMessageMethod()
	{
		// HasMessage must be public for cross-stream message lookup
		var method = typeof(StreamingPullStream)
			.GetMethod("HasMessage", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull("HasMessage must be a public method");
		method.ReturnType.ShouldBe(typeof(bool));
		method.GetParameters().Length.ShouldBe(1);
		method.GetParameters()[0].ParameterType.ShouldBe(typeof(string));
	}

	#endregion

	#region Volatile Disposed Field

	[Fact]
	public void HaveVolatileDisposedField()
	{
		// _disposed must be volatile for thread-safe disposal checking
		var field = typeof(StreamingPullStream)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_disposed field must exist");

		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(
			typeof(System.Runtime.CompilerServices.IsVolatile),
			"_disposed must be volatile for thread-safe access");
	}

	#endregion

	#region Stream Lock Still Exists

	[Fact]
	public void RetainStreamLockField()
	{
		// _streamLock must still exist for write operations (Ack, Nack, ModifyAckDeadline)
		var field = typeof(StreamingPullStream)
			.GetField("_streamLock", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_streamLock must still exist for write serialization");
		field.FieldType.ShouldBe(typeof(SemaphoreSlim));
	}

	#endregion
}
