// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

using Excalibur.Domain;
using Excalibur.Domain.Concurrency;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data;

/// <summary>
/// Provides extension methods for accessing contextual information from an <see cref="IActivityContext" /> instance.
/// </summary>
public static class ActivityContextExtensions
{
	/// <summary>
	/// Retrieves the application name from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns>
	/// The application name carried by the context, or an empty string when the context does not carry one. The context is
	/// the only source consulted: an ambient process-wide value is not substituted, because a caller cannot tell a value the
	/// context supplied from one it did not.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? ApplicationName(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Returns null when the context does not carry an application name, rather than the empty string
		// it used to fabricate. An empty string made "absent" indistinguishable from "present and empty",
		// so a caller could not tell a context that had never been populated from one deliberately
		// carrying no name.
		return context.GetValue<string?>(nameof(ApplicationName), defaultValue: null);
	}

	/// <summary>
	/// Retrieves the client address from the activity context, if available.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The client address, or <c> null </c> if not available. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? ClientAddress(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<IClientAddress>(nameof(ClientAddress))?.Value;
	}

	/// <summary>
	/// Retrieves the configuration from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The configuration instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static IConfiguration Configuration(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Stays non-nullable: unlike the accessors above, a missing configuration is not a legitimate
		// state a caller should branch on, it is a context that was never populated. So this follows
		// GetRequiredService rather than widening to IConfiguration? -- and it now says so, where before
		// Get<T>'s null-forgiving operator handed back a null that failed later, somewhere else.
		return context.Get<IConfiguration>(nameof(IConfiguration))
			?? throw new InvalidOperationException(
				"The activity context carries no IConfiguration. It is populated when the context is "
				+ "created for a request scope; a context built outside one has no configuration to read.");
	}

	/// <summary>
	/// Retrieves the correlation ID from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The correlation ID, or <see cref="Guid.Empty" /> if not set. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static Guid? CorrelationId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Returns null when the context carries no correlation id, rather than Guid.Empty. The sentinel
		// was ambiguous three ways: absent, present-but-unset, and a genuinely stored Guid.Empty all read
		// identically, and a correlation id is exactly the value a caller wants to know it does not have.
		return context.Get<ICorrelationId>(nameof(CorrelationId))?.Value;
	}

	/// <summary>
	/// Retrieves the incoming ETag from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The incoming ETag, or <c> null </c> if not set. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? ETag(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<IETag?>(nameof(ETag))?.IncomingValue;
	}

	/// <summary>
	/// Sets the outgoing ETag in the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <param name="newETag"> The new ETag to set. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static void ETag(this IActivityContext context, string? newETag)
	{
		ArgumentNullException.ThrowIfNull(context);

		var etag = context.Get<IETag?>(nameof(ETag));

		if (etag is not null)
		{
			etag.OutgoingValue = newETag;
		}
	}

	/// <summary>
	/// Retrieves the most recent ETag (outgoing if set, otherwise incoming) from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The latest ETag, or <c> null </c> if not set. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? LatestETag(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var etag = context.Get<IETag>(nameof(ETag));

		if (etag is null)
		{
			return null;
		}

		if (!string.IsNullOrEmpty(etag.OutgoingValue))
		{
			return etag.OutgoingValue;
		}

		return string.IsNullOrEmpty(etag.IncomingValue) ? null : etag.IncomingValue;
	}

	/// <summary>
	/// Retrieves a value of type <typeparamref name="T" /> from the activity context by its key.
	/// </summary>
	/// <typeparam name="T"> The type of the value to retrieve. </typeparam>
	/// <param name="context"> The activity context. </param>
	/// <param name="key"> The key associated with the value. </param>
	/// <returns> The value associated with the key, or <c> null </c> if not found. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static T? Get<T>(this IActivityContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);

		// The null-forgiving operator used to sit on the end of this line. It told the compiler that a
		// lookup into an optional key-value bag always produces a value, which is the one thing the bag
		// cannot promise: a missing key yields default(T), and for a reference type that is null. The
		// signature said T, the summary above said "or null if not found", and the documentation was the
		// half telling the truth. Consumers got a non-nullable contract that returned null.
		return context.GetValue(key, default(T));
	}

	/// <summary>
	/// Retrieves the service provider from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The service provider instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static IServiceProvider ServiceProvider(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Non-nullable for the same reason as Configuration above, and load-bearing for DomainDb below,
		// which resolves through it. Failing here names the missing thing; failing later named nothing.
		return context.Get<IServiceProvider>(nameof(IServiceProvider))
			?? throw new InvalidOperationException(
				"The activity context carries no IServiceProvider. It is populated when the context is "
				+ "created for a request scope; a context built outside one cannot resolve services.");
	}

	/// <summary>
	/// Retrieves the <see cref="IDomainDb" /> service from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The scoped <see cref="IDomainDb" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static IDomainDb DomainDb(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.ServiceProvider().GetRequiredService<IDomainDb>();
	}

	/// <summary>
	/// Retrieves the tenant ID from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The tenant ID. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? TenantId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// This was typed string and returned null whenever the key was absent, because Get<T> forced
		// non-null on a bag lookup. A tenant id that silently reads as non-null when it is missing is the
		// worst member of this set to get wrong: a caller that trusts the contract carries a null into a
		// tenant predicate instead of failing where the value should have been supplied.
		return context.Get<string>(nameof(TenantId));
	}
}
