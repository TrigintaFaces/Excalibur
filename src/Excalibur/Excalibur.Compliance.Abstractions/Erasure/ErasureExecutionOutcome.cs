// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// What an erasure execution attempt actually did — which is five distinguishable outcomes, not two.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <see langword="bool"/> could not say "refused", so every refusal was reported as a FAILURE.</b>
/// The result type carried one <c>Success</c> flag and four named factories, which is an enumeration
/// written longhand; the state that could not be expressed is the one that arrives most often. Five of
/// the executor's seven failure returns were not failures at all: a request that does not exist, a
/// request another executor already owns, a request whose status makes it ineligible, and — the one a
/// regulator would ask about — a request a LEGAL HOLD has lawfully DEFERRED under Article 17(3).
/// </para>
/// <para>
/// <b>Why that mattered to a consumer and not only to us.</b> The concrete erasure service is public and
/// its execution entry point is shipped, so a consumer who asks for an erasure of a data subject under a
/// legal hold was told the erasure FAILED. Nothing failed: the law required the data kept, the framework
/// correctly withheld the destruction, and the only dishonest part was the answer.
/// </para>
/// <para>
/// <b>Zero is deliberately not a success state.</b> An unassigned field, a <c>default</c> value or a
/// result that was never set reads as <see cref="NotDetermined"/>, which no caller may treat as an
/// outcome. The alternative — making a success the zero value — turns "nobody decided" into "it worked".
/// </para>
/// </remarks>
public enum ErasureExecutionOutcome
{
	/// <summary>
	/// Nothing decided this. The attempt must not be treated as having succeeded, failed or been refused.
	/// </summary>
	/// <remarks>
	/// Reachable only from a <c>default</c> value, so a caller that sees it has been handed a result
	/// nobody produced. It exists to make that case nameable rather than to be returned.
	/// </remarks>
	NotDetermined = 0,

	/// <summary>
	/// The erasure ran and covered everything it was asked to cover.
	/// </summary>
	Succeeded = 1,

	/// <summary>
	/// The erasure ran and some part of it did not complete; what did complete is durable.
	/// </summary>
	/// <remarks>
	/// A genuine partial outcome rather than a refusal: destruction HAPPENED, and the counts on the result
	/// say how much. Re-running it is the remedy.
	/// </remarks>
	PartiallyCompleted = 2,

	/// <summary>
	/// The erasure covered everything it discovered, but writes arrived concurrently and are not covered.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="PartiallyCompleted"/> because the remedy differs: nothing was lost and
	/// erasing again once the writes have stopped completes it.
	/// </remarks>
	CompletedExceptConcurrentWrites = 3,

	/// <summary>
	/// This executor declined to perform the erasure. NOTHING WAS DESTROYED and the request is unchanged.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A refusal is not a failure, and the difference is load-bearing rather than cosmetic.</b> A
	/// failure means the erasure was attempted and did not work, so retrying it is right and a fault
	/// budget should be spent. A refusal means it was never attempted, because performing it would have
	/// been wrong or impossible — so spending a retry budget on it converts a correct withholding into a
	/// terminal non-erasure, which is its own regulator's finding.
	/// </para>
	/// <para>
	/// The reason is on <c>ErrorMessage</c>. A legal hold deferring an erasure is a refusal; so is a
	/// request another executor already owns, one whose status makes it ineligible, and one that does not
	/// exist. A caller that must distinguish them reads the message; a caller deciding whether to RETRY
	/// needs only this value, which is why there is one refusal state and not four.
	/// </para>
	/// </remarks>
	Refused = 4,

	/// <summary>
	/// The erasure was attempted and failed. Retrying it is the appropriate response.
	/// </summary>
	Failed = 5,
}
