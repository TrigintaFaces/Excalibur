// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// Runs an operation at most once per stable identity, returning the original durable receipt on a retry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this interface exists.</b> The implementation is constructed by the consumer, in their own
/// command pipeline, and called on their request path — so without a seam there is nothing to substitute
/// in a test and nothing to wrap. A consumer wanting to assert "a retry with the same key did not append
/// twice" had to reach a real SQL Server to do it, which is the wrong cost for a unit test of their own
/// handler. It also lets a consumer decorate the call (logging, metrics, a tenant guard) without
/// reimplementing the receipt protocol.
/// </para>
/// <para>
/// <b>What it guarantees, in falsifiable terms.</b> For a stable <c>scope</c> plus <c>operationKey</c>
/// and an immutable <c>fingerprint</c>, SQL commits at most one event append together with one durable
/// receipt. A replay returns the original aggregate identity, version and global positions WITHOUT
/// executing the action again. This is one committed append per operation identity — it is NOT
/// exactly-once execution of arbitrary side effects, and an operation that appends no events has no
/// receipt.
/// </para>
/// <para>
/// <b>What it does not cover.</b> Independent business commits, multiple aggregate saves in one
/// operation, and effects outside the SQL transaction are all outside this contract. A caller that
/// invents a fresh operation key for a retry gets a second execution, correctly: the key is the identity.
/// </para>
/// </remarks>
public interface ISqlServerOperationExecutor
{
	/// <summary>Runs a single-append operation, or returns its original durable receipt without running it.</summary>
	/// <param name="scope">A stable authenticated caller and operation namespace, or CDC source incarnation.</param>
	/// <param name="operationKey">The caller's stable retry key. Compared ordinally.</param>
	/// <param name="fingerprint">SHA-256 of immutable input, including target and original preconditions.</param>
	/// <param name="action">The handler. At most one nonempty event append is supported.</param>
	/// <param name="cancellationToken">Cancels this attempt; cancellation does not prove rollback.</param>
	/// <returns>The outcome, carrying the receipt when the operation appended events.</returns>
	Task<SqlServerOperationResult> ExecuteAsync(
		string scope,
		string operationKey,
		ReadOnlyMemory<byte> fingerprint,
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken);

	/// <summary>Binds this target to a source incarnation, refusing a source whose history was replaced.</summary>
	/// <param name="source">The configured source identity.</param>
	/// <param name="sourceIdentity">The source's database and capture identity, excluding values that advance normally.</param>
	/// <param name="cancellationToken">Cancels this attempt.</param>
	/// <returns>A task that completes when the binding is established or confirmed unchanged.</returns>
	/// <exception cref="SqlServerSourceChangedException">
	/// The bound source database, recovery fork or capture generation changed. Reconcile or rebuild target
	/// state before rebinding; restarting does not accept the new history.
	/// </exception>
	Task BindSourceAsync(string source, ReadOnlyMemory<byte> sourceIdentity, CancellationToken cancellationToken);
}
