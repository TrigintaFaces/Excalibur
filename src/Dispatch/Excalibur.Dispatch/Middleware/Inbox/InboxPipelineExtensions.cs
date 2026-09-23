// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

		DeclareInboxOperative(builder);
		RegisterMiddlewareDependencies(builder);

		return SeatMiddlewareOnce(builder);
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

		DeclareInboxOperative(builder);
		RegisterMiddlewareDependencies(builder);

		return SeatMiddlewareOnce(builder);
	}

	/// <summary>
	/// Records that the host wants the inbox operative, which is what placing it means.
	/// </summary>
	/// <remarks>
	/// <see cref="InboxMiddleware"/> returns to the next delegate untouched unless its options say the
	/// inbox is enabled, and that flag defaults to false. Placing the middleware was therefore not enough
	/// to deduplicate anything: a host had to ALSO select a mode, and nothing said so — so the samples and
	/// the XML docs both showed a bare <c>UseInbox()</c> call that suppressed no duplicate. Calling this
	/// method is the statement of intent, so it sets the flag rather than leaving the host to discover a
	/// second required call. A host that wants the store-backed mode over in-memory deduplication still
	/// says so with <c>WithInboxMode()</c>; that choice is unaffected.
	/// </remarks>
	private static void DeclareInboxOperative(IDispatchBuilder builder) =>
		builder.WithOptions(static options => options.Inbox.Enabled = true);

	/// <summary>
	/// Places <see cref="InboxMiddleware"/> in the pipeline, at most once per builder.
	/// </summary>
	/// <remarks>
	/// The inbox is a single pipeline stage and placing it twice is never correct: the second pass sees
	/// the message the first has just admitted, reads it as a duplicate and suppresses it, so the handler
	/// runs ZERO times and the message is lost silently. That is reachable by ordinary means — a host that
	/// takes the metapackage default and also copies the documented <c>UseInbox()</c> call out of the
	/// samples places it twice — so the seating is idempotent rather than relying on the host to place it
	/// exactly once. The existing registration is the ledger: the builder registers the middleware type
	/// when it seats it, and a middleware present in the container is placed in the pipeline whether it
	/// was seated here or resolved from DI.
	/// </remarks>
	private static IDispatchBuilder SeatMiddlewareOnce(IDispatchBuilder builder) =>
		builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(InboxMiddleware))
			? builder
			: builder.UseMiddleware<InboxMiddleware>();

	/// <summary>
	/// Registers the dependencies <see cref="InboxMiddleware"/> cannot be constructed without.
	/// </summary>
	/// <remarks>
	/// The middleware takes the deduplicator and the store as nullable parameters, but a nullable
	/// annotation does not make a constructor parameter optional to the container: without a registration
	/// the pipeline fails to build with "Unable to resolve service for type IInMemoryDeduplicator". The
	/// legacy infrastructure entry point registers it; the builder path did not, so placing the middleware
	/// through this method — the documented way to do it — produced a host that threw on its first dispatch.
	/// TryAdd, so a consumer's own deduplicator wins. The store stays the persistence package's to register:
	/// there is an in-box fallback for deduplication and none for durable storage, and a startup validator
	/// reports a durable inbox configured without one.
	/// </remarks>
	private static void RegisterMiddlewareDependencies(IDispatchBuilder builder)
	{
		builder.Services.TryAddSingleton<IInMemoryDeduplicator, InMemoryDeduplicator>();

		// The middleware reads IOptions<InboxConfigurationOptions>, but every route that configures the
		// inbox — WithInbox, WithInboxMode, and the metapackages' UseInbox option — writes into
		// DispatchOptions.Inbox, a DIFFERENT instance. Nothing bound the two, so the options system
		// satisfied the middleware with a default-constructed InboxConfigurationOptions whose Enabled is
		// false, and InvokeAsync returned straight to the next delegate. The middleware therefore
		// deduplicated nothing on EVERY path, the documented builder.UseInbox() one included: placing it
		// appeared to work, and an absence of duplicate suppression looks exactly like an absence of
		// duplicates. Projecting the nested instance rather than copying its properties keeps
		// DispatchOptions.Inbox the single source of truth, so a property added there cannot silently
		// fall out of the middleware's view the way Enabled did.
		builder.Services.TryAddSingleton<IOptions<InboxConfigurationOptions>>(static provider =>
			Microsoft.Extensions.Options.Options.Create(
				provider.GetRequiredService<IOptions<DispatchOptions>>().Value.Inbox));
	}
}
