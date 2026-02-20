// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Fluent builder interface for configuring in-memory CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures in-memory CDC options for testing scenarios.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.UseInMemory(mem =>
/// {
///     mem.ProcessorId("test-processor")
///        .BatchSize(10)
///        .PreserveHistory(true);
/// });
/// </code>
/// </example>
public interface IInMemoryCdcBuilder
{
	/// <summary>
	/// Sets the processor identifier for this CDC processor instance.
	/// </summary>
	/// <param name="processorId">The processor identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="processorId"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "inmemory-cdc".
	/// </para>
	/// </remarks>
	IInMemoryCdcBuilder ProcessorId(string processorId);

	/// <summary>
	/// Sets the batch size for CDC change processing.
	/// </summary>
	/// <param name="size">The batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="size"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 100.
	/// </para>
	/// </remarks>
	IInMemoryCdcBuilder BatchSize(int size);

	/// <summary>
	/// Sets whether to automatically flush changes after each batch.
	/// </summary>
	/// <param name="autoFlush">Whether to auto-flush.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is true.
	/// </para>
	/// </remarks>
	IInMemoryCdcBuilder AutoFlush(bool autoFlush = true);

	/// <summary>
	/// Sets whether to preserve changes in history after processing.
	/// </summary>
	/// <param name="preserveHistory">Whether to preserve history.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is false. Enable this to access processed changes via
	/// <see cref="IInMemoryCdcStore.GetHistory"/>.
	/// </para>
	/// </remarks>
	IInMemoryCdcBuilder PreserveHistory(bool preserveHistory = true);
}
