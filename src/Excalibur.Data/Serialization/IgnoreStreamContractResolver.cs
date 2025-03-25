using System.Reflection;

using JsonNet.ContractResolvers;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Excalibur.Data.Serialization;

/// <summary>
///     A custom contract resolver that ignores properties of type <see cref="Stream" /> during JSON serialization.
/// </summary>
/// <remarks>
///     This class extends <see cref="PrivateSetterContractResolver" /> to provide additional customization for JSON serialization. It
///     excludes all properties that are assignable to the <see cref="Stream" /> type, ensuring such properties are not serialized. This is
///     particularly useful when serializing objects that contain streams, such as file handlers or data streams, which are not serializable.
/// </remarks>
internal sealed class IgnoreStreamContractResolver : PrivateSetterContractResolver
{
	/// <summary>
	///     Creates a JSON property definition for the given member.
	/// </summary>
	/// <param name="member"> The member for which the JSON property is being created. </param>
	/// <param name="memberSerialization"> The member serialization options. </param>
	/// <returns>
	///     A <see cref="JsonProperty" /> instance representing the JSON property for the given member. If the member's type is assignable
	///     to <see cref="Stream" />, the property is configured to be ignored.
	/// </returns>
	protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
	{
		var prop = base.CreateProperty(member, memberSerialization);

		if (typeof(Stream).IsAssignableFrom(prop.PropertyType))
		{
			prop.Ignored = true;
		}

		return prop;
	}
}
