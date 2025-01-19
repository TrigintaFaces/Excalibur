using Medo;

namespace Excalibur.Extensions;

/// <summary>
///     Provides extension methods for generating and working with UUID v7 strings and GUIDs.
/// </summary>
public static class Uuid7Extensions
{
	/// <summary>
	///     Generates a new UUID v7 string in a compact 25-character format.
	/// </summary>
	/// <returns> A compact UUID v7 string representation. </returns>
	public static string GenerateString() => Uuid7.NewUuid7().ToId25String();

	/// <summary>
	///     Generates a new UUID v7 as a <see cref="Guid" /> object.
	/// </summary>
	/// <param name="matchGuidEndianness">
	///     If <c> true </c>, adjusts the endianess of the generated UUID so that its textual representation matches that of a
	///     <see cref="Guid" />. Defaults to <c> true </c>.
	/// </param>
	/// <returns> A <see cref="Guid" /> representation of the UUID v7. </returns>
	public static Guid GenerateGuid(bool matchGuidEndianness = true) => Uuid7.NewUuid7().ToGuid(matchGuidEndianness);
}
