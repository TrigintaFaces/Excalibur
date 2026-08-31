// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Thrown by <see cref="IErasureStore.SaveRequestAsync"/> when — and only when — a request with the same
/// <see cref="ErasureRequest.RequestId"/> is already stored.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so that "the request is already on file" is <b>distinguishable by the caller</b> from
/// every other reason a save can fail. It is the difference between a caller correctly treating the write
/// as already done and a caller wrongly abandoning a write that never happened: an erasure request is a
/// data subject's exercise of a statutory right, and one dropped on a misread failure is not recoverable
/// by anything the caller does later.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/> because a duplicate insert genuinely is an
/// invalid operation on the store's current state, and because that keeps an existing broad
/// <c>catch (InvalidOperationException)</c> working. A caller that needs the duplicate signal specifically
/// must catch <b>this</b> type: the base type is also raised for unrelated conditions — a store whose
/// schema is absent, a disposed store, an unresolved ambient tenant — and treating any of those as
/// "already on file" silently loses the request.
/// </para>
/// <para>
/// Implementations raise this rather than the underlying database provider's own duplicate-key exception,
/// so a caller can handle the condition without referencing that provider or knowing its error codes. The
/// provider's exception is preserved as <see cref="Exception.InnerException"/> so the underlying cause
/// stays diagnosable.
/// </para>
/// </remarks>
public sealed class DuplicateErasureRequestException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateErasureRequestException"/> class.
	/// </summary>
	public DuplicateErasureRequestException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateErasureRequestException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public DuplicateErasureRequestException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateErasureRequestException"/> class with a message
	/// and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The underlying store failure that reported the duplicate, when there was one.</param>
	public DuplicateErasureRequestException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the request that is already stored.
	/// </summary>
	/// <value>
	/// The <see cref="ErasureRequest.RequestId"/> that was re-filed, or <see langword="null"/> when the
	/// exception was constructed without one.
	/// </value>
	public Guid? RequestId { get; init; }

	/// <summary>
	/// Creates an exception for a request identifier that is already stored.
	/// </summary>
	/// <param name="requestId">The request identifier that was re-filed.</param>
	/// <param name="innerException">The underlying store failure that reported the duplicate, if any.</param>
	/// <returns>The exception to throw.</returns>
	public static DuplicateErasureRequestException ForRequestId(Guid requestId, Exception? innerException = null) =>
		new($"An erasure request with id '{requestId}' already exists.", innerException)
		{
			RequestId = requestId,
		};
}
