// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Inbox;

/// <summary>
/// Extension methods for adding inbox (idempotency) middleware to the dispatch pipeline.
/// </summary>
public static class InboxPipelineExtensions
{
	/// <summary>
	/// Adds inbox middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The inbox middleware tracks processed messages to ensure idempotent handling.
	/// Duplicate messages are detected and short-circuited before reaching the handler.
	/// </para>
	/// <para>
	/// Inbox services (including an <c>IInboxStore</c> implementation) must be registered
	/// separately in the DI container. This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseInbox()               // Deduplicate before validation/processing
	///        .UseValidation()
	///        .UseTransaction();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseInbox(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<InboxMiddleware>();
	}

	/// <summary>
	/// Adds idempotency middleware to the dispatch pipeline.
	/// This is an alias for <see cref="UseInbox"/> -- both register the same
	/// <see cref="InboxMiddleware"/> which provides message deduplication.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	public static IDispatchBuilder UseIdempotency(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<InboxMiddleware>();
	}
}
