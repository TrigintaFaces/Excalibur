using Excalibur.Core;
using Excalibur.Core.Concurrency;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Domain;

/// <summary>
///     Provides extension methods for accessing contextual information from an <see cref="IActivityContext" /> instance.
/// </summary>
public static class ActivityContextExtensions
{
	/// <summary>
	///     Retrieves the application name from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The application name. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string ApplicationName(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get(nameof(ApplicationName), ApplicationContext.ApplicationName);
	}

	/// <summary>
	///     Retrieves the client address from the activity context, if available.
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
	///     Retrieves the configuration from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The configuration instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static IConfiguration Configuration(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<IConfiguration>(nameof(IConfiguration));
	}

	/// <summary>
	///     Retrieves the correlation ID from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The correlation ID, or <see cref="Guid.Empty" /> if not set. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static Guid CorrelationId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<ICorrelationId>(nameof(CorrelationId))?.Value ?? Guid.Empty;
	}

	/// <summary>
	///     Retrieves the incoming ETag from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The incoming ETag, or <c> null </c> if not set. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? ETag(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<IETag>(nameof(ETag))?.IncomingValue;
	}

	/// <summary>
	///     Sets the outgoing ETag in the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <param name="newETag"> The new ETag to set. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static void ETag(this IActivityContext context, string? newETag)
	{
		ArgumentNullException.ThrowIfNull(context);

		var etag = context.Get<IETag>(nameof(ETag));

		etag.OutgoingValue = newETag;
	}

	/// <summary>
	///     Retrieves the most recent ETag (outgoing if set, otherwise incoming) from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The latest ETag, or <c> null </c> if not set. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string? LatestETag(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var eTag = (IETag?)context.Get<IETag>(nameof(ETag));

		return eTag != null ? string.IsNullOrEmpty(eTag.OutgoingValue) ? eTag.IncomingValue : eTag.OutgoingValue : null;
	}

	/// <summary>
	///     Retrieves a value of type <typeparamref name="T" /> from the activity context by its key.
	/// </summary>
	/// <typeparam name="T"> The type of the value to retrieve. </typeparam>
	/// <param name="context"> The activity context. </param>
	/// <param name="key"> The key associated with the value. </param>
	/// <returns> The value associated with the key, or <c> null </c> if not found. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static T Get<T>(this IActivityContext context, string key)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get(key, default(T))!;
	}

	/// <summary>
	///     Retrieves the service provider from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The service provider instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static IServiceProvider ServiceProvider(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<IServiceProvider>(nameof(IServiceProvider));
	}

	/// <summary>
	///     Retrieves the tenant ID from the activity context.
	/// </summary>
	/// <param name="context"> The activity context. </param>
	/// <returns> The tenant ID. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	public static string TenantId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.Get<ITenantId>(nameof(TenantId)).Value;
	}
}
