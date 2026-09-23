// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Exception thrown when a message could not be settled with the broker.
/// </summary>
/// <remarks>
/// <para>
/// Settlement is the acknowledgement or rejection that tells the broker what became of a received
/// message. When it fails, the broker's view of the message is unchanged — so the outcome the caller
/// asked for did not happen, and the message is usually still owned by the broker.
/// </para>
/// <para>
/// This exception exists so that failure is not reported as success. A settle call that swallowed its
/// error returned normally, telling the caller the message was settled when it was not, and the
/// redelivery that followed appeared to the consumer as an unexplained duplicate.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/>, which is the type the transport receivers
/// already throw from this position, so existing <c>catch (InvalidOperationException)</c> handlers keep
/// working. Catch this type instead to distinguish a settlement failure from an unrelated invalid
/// operation — handler code frequently sits in the same <c>try</c> block, and the base type cannot tell
/// the two apart.
/// </para>
/// <para>
/// Implementations are required to keep provider-specific exceptions off this boundary: a broker
/// client's own exception type travels as <see cref="Exception.InnerException"/>, so a consumer writing
/// portable code does not have to reference a specific broker's package to catch a settlement failure.
/// </para>
/// <para>
/// <b>Known gap.</b> That requirement is newly stated, and not every shipped transport has been brought
/// to it yet. Several receivers still let their broker client's exception escape from
/// <c>AcknowledgeAsync</c> and <c>RejectAsync</c>, and a settlement that times out surfaces as an
/// <see cref="OperationCanceledException"/> rather than as this type. Until each transport is converted,
/// portable code that must not fault should catch <see cref="InvalidOperationException"/> — this type's
/// base — and treat an unrecognised exception from a settlement call as a settlement of unknown outcome:
/// assume the message may be redelivered.
/// </para>
/// </remarks>
public sealed class TransportSettlementException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSettlementException"/> class.
	/// </summary>
	public TransportSettlementException()
		: base("The message could not be settled with the broker.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSettlementException"/> class
	/// with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public TransportSettlementException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportSettlementException"/> class
	/// with a specified error message and a reference to the inner exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public TransportSettlementException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the transport where the settlement failed, such as <c>IbmMq</c> or <c>Mqtt</c>.
	/// </summary>
	/// <remarks>
	/// This identifies the transport family, not the name a connection was registered under in
	/// dependency injection. A host wiring more than one transport uses it to tell which one failed.
	/// </remarks>
	public string? TransportName { get; init; }

	/// <summary>
	/// Gets whether the broker is expected to redeliver the message after this failure.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the discriminator that makes the exception actionable. "The settle failed" on its own
	/// does not tell a caller what to do; whether the message is coming back does. See
	/// <see cref="TransportRedeliveryExpectation"/> for what each value obliges the caller to do.
	/// </para>
	/// <para>
	/// It is <see langword="required"/> so that a transport must state it. Were it optional, a transport
	/// that genuinely could not tell and a transport whose author did not fill it in would produce
	/// identical exceptions, and a caller could not distinguish a measured
	/// <see cref="TransportRedeliveryExpectation.Unspecified"/> from an omission.
	/// </para>
	/// <para>
	/// Two of the three values are actionable and the third aliases one of them: treat
	/// <see cref="TransportRedeliveryExpectation.Unspecified"/> exactly as
	/// <see cref="TransportRedeliveryExpectation.Expected"/>, because assuming the work may arrive again
	/// is the safe direction. A <c>switch</c> over this type needs two branches, not three.
	/// </para>
	/// </remarks>
	public required TransportRedeliveryExpectation RedeliveryExpectation { get; init; }

	/// <summary>
	/// Gets whether retrying the settlement itself could succeed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Separate from <see cref="RedeliveryExpectation"/>, which says what becomes of the <i>message</i>.
	/// This says what becomes of the <i>call</i>. A caller implementing the obvious policy — the
	/// settlement failed, so try it again — loops forever against a failure that can never succeed, so
	/// the transport has to say which kind it was.
	/// </para>
	/// <para>
	/// It is <see langword="required"/> for the same reason as
	/// <see cref="RedeliveryExpectation"/>: a transport that cannot tell must say
	/// <see cref="SettlementRetryability.Unspecified"/> deliberately rather than inherit it by omission.
	/// </para>
	/// </remarks>
	public required SettlementRetryability Retryability { get; init; }
}

/// <summary>
/// Specifies whether retrying a failed settlement could succeed.
/// </summary>
/// <remarks>
/// The numbering is not contiguous. Values are never reassigned, because a stored or logged number
/// would silently come to mean something else.
/// </remarks>
public enum SettlementRetryability
{
	/// <summary>
	/// The transport could not determine whether a retry would succeed.
	/// </summary>
	/// <remarks>
	/// A real outcome, not a missing value. Treat it as <see cref="Permanent"/> for control-flow
	/// purposes — bounding the attempts is safe, whereas assuming a retry will eventually work is the
	/// case that loops forever.
	/// </remarks>
	Unspecified = 0,

	/// <summary>
	/// The failure was transient and the same settlement call may succeed if repeated.
	/// </summary>
	/// <remarks>
	/// A broker that was briefly unreachable, for example. The caller still owes the settlement, so
	/// abandoning it leaves the message outstanding.
	/// </remarks>
	Retryable = 1,

	/// <summary>
	/// The settlement can never succeed and repeating it will fail the same way.
	/// </summary>
	/// <remarks>
	/// The state the call depended on is gone — a closed connection whose unit of work no longer exists,
	/// or a claim that a newer owner has taken over. Retrying wastes attempts and hides the outcome;
	/// read <see cref="TransportSettlementException.RedeliveryExpectation"/> to learn what happens to the
	/// message instead.
	/// </remarks>
	Permanent = 2,
}

/// <summary>
/// Specifies whether a message is expected to be redelivered after a failed settlement.
/// </summary>
/// <remarks>
/// The numbering is not contiguous. Values are never reassigned, because a stored or logged number
/// would silently come to mean something else.
/// </remarks>
public enum TransportRedeliveryExpectation
{
	/// <summary>
	/// The transport could not determine whether the message will be redelivered.
	/// </summary>
	/// <remarks>
	/// This is a real outcome and not a missing value: some brokers do not report enough about a failed
	/// settle to say. Treat it as <see cref="Expected"/> for safety — assume the work may arrive again.
	/// </remarks>
	Unspecified = 0,

	/// <summary>
	/// The broker still owns the message and is expected to deliver it again to the consumer group.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The handler's work may therefore run a second time, so it must be idempotent. This is the usual
	/// outcome: an acknowledgement that did not reach the broker leaves the message outstanding, and a
	/// rejection that failed to suppress redelivery leaves it queued.
	/// </para>
	/// <para>
	/// <b>It promises redelivery to the consumer GROUP, not to this instance.</b> On a partitioned or
	/// group-based transport the message may be delivered to a different member, in a different process.
	/// A caller must not read this as "I will see this message again" — that is a property of process
	/// identity that no correct consumer may depend on, and on several transports it is simply unknowable.
	/// What is promised is that the work is not lost.
	/// </para>
	/// </remarks>
	Expected = 1,

	/// <summary>
	/// The message will not be delivered again despite the failure.
	/// </summary>
	/// <remarks>
	/// The settle call failed after the broker had already released the message, so retrying the
	/// settlement achieves nothing and the work will not arrive again. If the handler's effect must not
	/// be lost, the caller is responsible for recording it.
	/// </remarks>
	NotExpected = 2,
}
