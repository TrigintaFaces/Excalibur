// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides cascade erasure capabilities that walk relationship graphs
/// to erase all related data subjects.
/// </summary>
/// <remarks>
/// <para>
/// Cascade erasure extends the standard erasure service by discovering
/// related data subjects through an <see cref="ICascadeRelationshipResolver"/>
/// and erasing their data as well. This supports GDPR scenarios where
/// erasure of a primary subject must propagate to related records.
/// </para>
/// </remarks>
public interface ICascadeErasureService
{
	/// <summary>
	/// Erases data for a subject and all related subjects discovered via relationship traversal.
	/// </summary>
	/// <param name="subjectId">The primary data subject identifier.</param>
	/// <param name="options">Options controlling cascade behavior.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the cascade erasure operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="subjectId"/> is null or whitespace.</exception>
	Task<CascadeErasureResult> EraseWithCascadeAsync(
		string subjectId,
		CascadeErasureOptions options,
		CancellationToken cancellationToken);
}

/// <summary>
/// Resolves relationships between data subjects for cascade erasure.
/// </summary>
public interface ICascadeRelationshipResolver
{
	/// <summary>
	/// Gets the identifiers of data subjects related to the specified subject.
	/// </summary>
	/// <param name="subjectId">The data subject identifier to find relationships for.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A collection of related subject identifiers.</returns>
	Task<IReadOnlyList<string>> GetRelatedSubjectsAsync(
		string subjectId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Options controlling cascade erasure behavior.
/// </summary>
public sealed class CascadeErasureOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to include related records in the erasure.
	/// Default: true.
	/// </summary>
	public bool IncludeRelatedRecords { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum depth of relationship traversal.
	/// Default: 3.
	/// </summary>
	public int RelationshipDepth { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether to perform a dry run without actually erasing data.
	/// Default: false.
	/// </summary>
	public bool DryRun { get; set; }
}

/// <summary>
/// Result of a cascade erasure operation.
/// </summary>
public sealed record CascadeErasureResult
{
	/// <summary>
	/// Gets a value indicating whether the cascade erasure completed successfully.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the primary subject identifier that was erased.
	/// </summary>
	public required string PrimarySubjectId { get; init; }

	/// <summary>
	/// Gets the total number of subjects erased (including primary).
	/// </summary>
	public int SubjectsErased { get; init; }

	/// <summary>
	/// Gets the identifiers of all related subjects that were erased.
	/// </summary>
	public IReadOnlyList<string> RelatedSubjectsErased { get; init; } = [];

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets a value indicating whether this was a dry run.
	/// </summary>
	public bool IsDryRun { get; init; }
}
