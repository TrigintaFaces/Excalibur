// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Transaction;

/// <summary>
/// Extension methods for adding transaction middleware to the dispatch pipeline.
/// </summary>
public static class TransactionPipelineExtensions
{
	/// <summary>
	/// Adds transaction middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The transaction middleware wraps downstream handlers in a transaction scope,
	/// ensuring that all operations within a single dispatch are committed or rolled
	/// back atomically.
	/// </para>
	/// <para>
	/// Transaction services must be registered separately in the DI container. This method
	/// only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseTransaction()         // Wrap handler execution in a transaction
	///        .UseOutbox();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseTransaction(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<TransactionMiddleware>();
	}
}
