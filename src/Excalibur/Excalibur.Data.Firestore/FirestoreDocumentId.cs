using System.Linq;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Composes a Firestore document id from several caller-supplied terms without letting two distinct term
/// tuples render the same id.
/// </summary>
/// <remarks>
/// <para>
/// A Firestore document id may not contain "/" -- it is the path separator, so an id such as
/// "order-123/customer-456" is read as a nested collection path rather than as an id, and a write lands
/// somewhere the matching read never looks. Terms are caller data and may legally contain any character,
/// so they are escaped rather than rejected: every other provider accepts them.
/// </para>
/// <para>
/// The property that matters is injectivity. Joining raw terms with a separator that is itself legal
/// inside a term aliases distinct tuples onto one document: with "_" as the separator, ("a", "b_c") and
/// ("a_b", "c") both render "a_b_c". The document written second overwrites the first, and a read for
/// either returns whichever survived. Escaping the separator out of every term is what makes the join
/// injective, so the ambiguity becomes inexpressible rather than merely unlikely.
/// </para>
/// <para>
/// "%" is escaped FIRST, and that ordering is what makes the encoding reversible. Escaping only the
/// separator would map the distinct terms "a_b" and "a%5Fb" onto the same output -- a collision
/// introduced by the escaping itself.
/// </para>
/// </remarks>
internal static class FirestoreDocumentId
{
	/// <summary>
	/// The separator joining escaped terms. It cannot occur inside an escaped term, which is what makes
	/// <see cref="Compose" /> injective.
	/// </summary>
	private const string Separator = "_";

	/// <summary>
	/// Joins the supplied terms into a document id such that distinct term sequences produce distinct ids.
	/// </summary>
	/// <param name="terms">The terms to join, in order. Each may contain any character.</param>
	/// <returns>A Firestore-legal document id.</returns>
	public static string Compose(params string[] terms)
	{
		ArgumentNullException.ThrowIfNull(terms);

		return string.Join(Separator, terms.Select(static term => Escape(term ?? string.Empty)));
	}

	/// <summary>
	/// Escapes a single term so that it contains neither the path separator nor the id separator.
	/// </summary>
	/// <param name="value">The term to escape.</param>
	/// <returns>The escaped term.</returns>
	public static string Escape(string value) =>
		value.Replace("%", "%25", StringComparison.Ordinal)
			.Replace("/", "%2F", StringComparison.Ordinal)
			.Replace(Separator, "%5F", StringComparison.Ordinal);
}
