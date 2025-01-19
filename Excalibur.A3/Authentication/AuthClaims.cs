using System.Diagnostics.CodeAnalysis;

namespace Excalibur.A3.Authentication;

/// <summary>
///     Provides constants for authentication claim types.
/// </summary>
public static class AuthClaims
{
	/// <summary>
	///     Represents the email claim type.
	/// </summary>
	public static readonly string Email = nameof(Email).ToUpperInvariant();

	/// <summary>
	///     Represents the family name claim type.
	/// </summary>
	[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Proper claim syntax")]
	public static readonly string Family_Name = nameof(Family_Name).ToUpperInvariant();

	/// <summary>
	///     Represents the given name claim type.
	/// </summary>
	[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Proper claim syntax")]
	public static readonly string Given_Name = nameof(Given_Name).ToUpperInvariant();

	/// <summary>
	///     Represents the name claim type.
	/// </summary>
	public static readonly string Name = nameof(Name).ToUpperInvariant();

	/// <summary>
	///     Represents the UPN (User Principal Name) claim type.
	/// </summary>
	public static readonly string Upn = nameof(Upn).ToUpperInvariant();
}
