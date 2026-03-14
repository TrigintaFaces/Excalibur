// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Idempotency;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISagaIdempotencyProvider conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateProvider"/> to verify that
/// your saga idempotency provider conforms to the ISagaIdempotencyProvider contract.
/// </para>
/// <para>
/// The test kit verifies core idempotency operations including check, mark,
/// idempotent re-mark, and isolation behavior.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSagaIdempotencyConformanceTests : SagaIdempotencyConformanceTestKit
/// {
///     protected override ISagaIdempotencyProvider CreateProvider() =&gt;
///         new SqlServerSagaIdempotencyProvider(_connectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class SagaIdempotencyConformanceTestKit
{
	/// <summary>
	/// Creates a fresh saga idempotency provider instance for testing.
	/// </summary>
	/// <returns>An ISagaIdempotencyProvider implementation to test.</returns>
	protected abstract ISagaIdempotencyProvider CreateProvider();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates a unique saga ID for test isolation.
	/// </summary>
	/// <returns>A unique saga identifier string.</returns>
	protected virtual string GenerateSagaId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Generates a unique idempotency key for test isolation.
	/// </summary>
	/// <returns>A unique idempotency key string.</returns>
	protected virtual string GenerateIdempotencyKey() => $"msg-{Guid.NewGuid():N}";

	#region IsProcessed Tests

	/// <summary>
	/// Verifies that a never-marked key returns false.
	/// </summary>
	public virtual async Task IsProcessedAsync_UnknownKey_ShouldReturnFalse()
	{
		var provider = CreateProvider();
		var sagaId = GenerateSagaId();
		var key = GenerateIdempotencyKey();

		var result = await provider.IsProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"Expected IsProcessedAsync to return false for unknown key");
		}
	}

	/// <summary>
	/// Verifies that a marked key returns true.
	/// </summary>
	public virtual async Task IsProcessedAsync_MarkedKey_ShouldReturnTrue()
	{
		var provider = CreateProvider();
		var sagaId = GenerateSagaId();
		var key = GenerateIdempotencyKey();

		await provider.MarkProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		var result = await provider.IsProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"Expected IsProcessedAsync to return true for marked key");
		}
	}

	#endregion

	#region MarkProcessed Tests

	/// <summary>
	/// Verifies that marking a key as processed succeeds.
	/// </summary>
	public virtual async Task MarkProcessedAsync_NewKey_ShouldSucceed()
	{
		var provider = CreateProvider();
		var sagaId = GenerateSagaId();
		var key = GenerateIdempotencyKey();

		// Should not throw
		await provider.MarkProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		var isProcessed = await provider.IsProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		if (!isProcessed)
		{
			throw new TestFixtureAssertionException(
				"Expected key to be marked as processed after MarkProcessedAsync");
		}
	}

	/// <summary>
	/// Verifies that re-marking an already-processed key is idempotent (does not throw).
	/// </summary>
	public virtual async Task MarkProcessedAsync_AlreadyMarked_ShouldBeIdempotent()
	{
		var provider = CreateProvider();
		var sagaId = GenerateSagaId();
		var key = GenerateIdempotencyKey();

		await provider.MarkProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		// Second call should not throw (idempotent per interface contract)
		var exceptionThrown = false;
		try
		{
			await provider.MarkProcessedAsync(sagaId, key, CancellationToken.None)
				.ConfigureAwait(false);
		}
#pragma warning disable CA1031 // Do not catch general exception types
		catch (Exception)
		{
			exceptionThrown = true;
		}
#pragma warning restore CA1031

		if (exceptionThrown)
		{
			throw new TestFixtureAssertionException(
				"Expected MarkProcessedAsync to be idempotent (not throw on re-mark)");
		}

		// Key should still be processed
		var isProcessed = await provider.IsProcessedAsync(sagaId, key, CancellationToken.None)
			.ConfigureAwait(false);

		if (!isProcessed)
		{
			throw new TestFixtureAssertionException(
				"Expected key to remain processed after idempotent re-mark");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that keys are isolated by saga ID.
	/// </summary>
	public virtual async Task Keys_ShouldIsolateBySagaId()
	{
		var provider = CreateProvider();
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();
		var key = GenerateIdempotencyKey();

		// Mark key for saga1 only
		await provider.MarkProcessedAsync(sagaId1, key, CancellationToken.None)
			.ConfigureAwait(false);

		// Same key should NOT be processed for saga2
		var isProcessedForSaga2 = await provider.IsProcessedAsync(sagaId2, key, CancellationToken.None)
			.ConfigureAwait(false);

		if (isProcessedForSaga2)
		{
			throw new TestFixtureAssertionException(
				"Expected key to be isolated by saga ID (saga2 should not see saga1's key)");
		}

		// But should be processed for saga1
		var isProcessedForSaga1 = await provider.IsProcessedAsync(sagaId1, key, CancellationToken.None)
			.ConfigureAwait(false);

		if (!isProcessedForSaga1)
		{
			throw new TestFixtureAssertionException(
				"Expected key to be processed for saga1");
		}
	}

	/// <summary>
	/// Verifies that different keys within the same saga are independent.
	/// </summary>
	public virtual async Task DifferentKeys_SameSaga_ShouldBeIndependent()
	{
		var provider = CreateProvider();
		var sagaId = GenerateSagaId();
		var key1 = GenerateIdempotencyKey();
		var key2 = GenerateIdempotencyKey();

		// Mark only key1
		await provider.MarkProcessedAsync(sagaId, key1, CancellationToken.None)
			.ConfigureAwait(false);

		var isKey1Processed = await provider.IsProcessedAsync(sagaId, key1, CancellationToken.None)
			.ConfigureAwait(false);
		var isKey2Processed = await provider.IsProcessedAsync(sagaId, key2, CancellationToken.None)
			.ConfigureAwait(false);

		if (!isKey1Processed)
		{
			throw new TestFixtureAssertionException("Expected key1 to be processed");
		}

		if (isKey2Processed)
		{
			throw new TestFixtureAssertionException(
				"Expected key2 to NOT be processed (independent from key1)");
		}
	}

	/// <summary>
	/// Verifies that multiple keys can be marked for the same saga.
	/// </summary>
	public virtual async Task MultipleKeys_SameSaga_ShouldAllBeTracked()
	{
		var provider = CreateProvider();
		var sagaId = GenerateSagaId();
		var keys = new[]
		{
			GenerateIdempotencyKey(),
			GenerateIdempotencyKey(),
			GenerateIdempotencyKey()
		};

		foreach (var key in keys)
		{
			await provider.MarkProcessedAsync(sagaId, key, CancellationToken.None)
				.ConfigureAwait(false);
		}

		foreach (var key in keys)
		{
			var isProcessed = await provider.IsProcessedAsync(sagaId, key, CancellationToken.None)
				.ConfigureAwait(false);

			if (!isProcessed)
			{
				throw new TestFixtureAssertionException(
					$"Expected key '{key}' to be processed for saga '{sagaId}'");
			}
		}
	}

	#endregion
}
