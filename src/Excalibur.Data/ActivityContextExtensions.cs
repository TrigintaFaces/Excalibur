using Excalibur.Domain;

namespace Excalibur.Data;

/// <summary>
///     Extension methods for <see cref="IActivityContext" /> to provide easier access to commonly used services.
/// </summary>
public static class ActivityContextExtensions
{
	/// <summary>
	///     Retrieves an <see cref="IDomainDb" /> instance from the activity context.
	/// </summary>
	/// <param name="context"> The activity context to retrieve the service from. </param>
	/// <returns> The <see cref="IDomainDb" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="context" /> is <c> null </c>. </exception>
	/// <exception cref="KeyNotFoundException"> Thrown if <see cref="IDomainDb" /> is not found in the context. </exception>
	public static IDomainDb DomainDb(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context, nameof(context));

		return context.Get<IDomainDb>(nameof(IDomainDb));
	}
}
