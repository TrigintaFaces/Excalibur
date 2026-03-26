// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Exceptions;

/// <summary>
/// Thrown when an inline projection fails after events have already been committed
/// to the event store. The events ARE committed -- do NOT retry <c>SaveAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// Recovery options:
/// <list type="number">
/// <item>Log the error and rely on async catch-up (if the projection also runs in async mode).</item>
/// <item>Call <c>IProjectionRecovery.ReapplyAsync&lt;T&gt;(aggregateId)</c> to re-apply
/// committed events to the failed projection without re-appending events.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class InlineProjectionException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InlineProjectionException"/> class.
	/// </summary>
	/// <param name="committedVersion">The aggregate version that was successfully committed.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="failedProjectionType">The CLR type of the projection that failed.</param>
	/// <param name="innerException">The original exception from the projection store.</param>
	public InlineProjectionException(
		long committedVersion,
		string aggregateId,
		string aggregateType,
		Type failedProjectionType,
		Exception innerException)
		: base(
			BuildMessage(
				committedVersion,
				aggregateId ?? throw new ArgumentNullException(nameof(aggregateId)),
				aggregateType ?? throw new ArgumentNullException(nameof(aggregateType)),
				failedProjectionType ?? throw new ArgumentNullException(nameof(failedProjectionType))),
			innerException)
	{
		CommittedVersion = committedVersion;
		AggregateId = aggregateId;
		AggregateType = aggregateType;
		FailedProjectionType = failedProjectionType;
	}

	private static string BuildMessage(
		long committedVersion, string aggregateId, string aggregateType, Type failedProjectionType) =>
		$"Inline projection '{failedProjectionType.Name}' failed for aggregate " +
		$"'{aggregateType}/{aggregateId}' at committed version {committedVersion}. " +
		"Events are committed -- do NOT retry SaveAsync. " +
		"Use IProjectionRecovery.ReapplyAsync to recover.";

	/// <summary>
	/// Gets the aggregate version that was successfully committed to the event store.
	/// </summary>
	/// <value>The committed version number.</value>
	public long CommittedVersion { get; }

	/// <summary>
	/// Gets the identifier of the aggregate whose projection failed.
	/// </summary>
	/// <value>The aggregate identifier.</value>
	public string AggregateId { get; }

	/// <summary>
	/// Gets the type name of the aggregate whose projection failed.
	/// </summary>
	/// <value>The aggregate type name.</value>
	public string AggregateType { get; }

	/// <summary>
	/// Gets the CLR type of the projection that failed to persist.
	/// </summary>
	/// <value>The failed projection type.</value>
	public Type FailedProjectionType { get; }
}
