using Excalibur.Domain;

namespace Excalibur.A3;

/// <summary>
///     Provides extension methods for accessing data from an <see cref="IActivityContext" /> instance.
/// </summary>
public static class ActivityContextExtensions
{
	/// <summary>
	///     Retrieves the current <see cref="IAccessToken" /> from the activity context, if available.
	/// </summary>
	/// <param name="context"> The activity context to retrieve the access token from. </param>
	/// <returns> The <see cref="IAccessToken" /> instance if present in the context; otherwise, <c> null </c>. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="context" /> is <c> null </c>. </exception>
	public static IAccessToken? AccessToken(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context, nameof(context));

		return context.Get<IAccessToken?>(nameof(AccessToken));
	}
}
