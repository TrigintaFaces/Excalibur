// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="ICascadeErasureService"/> that walks relationship
/// graphs to erase data for a subject and all related subjects.
/// </summary>
public sealed partial class CascadeErasureService : ICascadeErasureService
{
	private readonly IErasureService _erasureService;
	private readonly ICascadeRelationshipResolver _relationshipResolver;
	private readonly ILogger<CascadeErasureService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CascadeErasureService"/> class.
	/// </summary>
	/// <param name="erasureService">The erasure service for performing individual erasures.</param>
	/// <param name="relationshipResolver">The resolver for discovering related subjects.</param>
	/// <param name="logger">The logger.</param>
	public CascadeErasureService(
		IErasureService erasureService,
		ICascadeRelationshipResolver relationshipResolver,
		ILogger<CascadeErasureService> logger)
	{
		_erasureService = erasureService ?? throw new ArgumentNullException(nameof(erasureService));
		_relationshipResolver = relationshipResolver ?? throw new ArgumentNullException(nameof(relationshipResolver));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<CascadeErasureResult> EraseWithCascadeAsync(
		string subjectId,
		CascadeErasureOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
		ArgumentNullException.ThrowIfNull(options);

		LogCascadeErasureStarted(subjectId, options.RelationshipDepth, options.DryRun);

		try
		{
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var erasedRelated = new List<string>();

			// Discover all related subjects via BFS up to the configured depth
			var allSubjects = new List<string> { subjectId };
			if (options.IncludeRelatedRecords)
			{
				await DiscoverRelatedSubjectsAsync(
					subjectId, options.RelationshipDepth, visited, allSubjects, cancellationToken)
					.ConfigureAwait(false);
			}

			if (options.DryRun)
			{
				LogCascadeErasureCompleted(subjectId, allSubjects.Count, isDryRun: true);
				return new CascadeErasureResult
				{
					Success = true,
					PrimarySubjectId = subjectId,
					SubjectsErased = allSubjects.Count,
					RelatedSubjectsErased = allSubjects.Where(s => !string.Equals(s, subjectId, StringComparison.OrdinalIgnoreCase)).ToList(),
					IsDryRun = true
				};
			}

			// Erase each subject
			foreach (var subject in allSubjects)
			{
				var request = new ErasureRequest
				{
					RequestId = Guid.NewGuid(),
					DataSubjectId = subject,
					IdType = DataSubjectIdType.UserId,
					Scope = ErasureScope.User,
					RequestedBy = "CascadeErasureService",
					LegalBasis = ErasureLegalBasis.DataSubjectRequest
				};

				await _erasureService.RequestErasureAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (!string.Equals(subject, subjectId, StringComparison.OrdinalIgnoreCase))
				{
					erasedRelated.Add(subject);
				}
			}

			LogCascadeErasureCompleted(subjectId, allSubjects.Count, isDryRun: false);

			return new CascadeErasureResult
			{
				Success = true,
				PrimarySubjectId = subjectId,
				SubjectsErased = allSubjects.Count,
				RelatedSubjectsErased = erasedRelated
			};
		}
		catch (Exception ex)
		{
			LogCascadeErasureFailed(subjectId, ex);
			return new CascadeErasureResult
			{
				Success = false,
				PrimarySubjectId = subjectId,
				ErrorMessage = ex.Message
			};
		}
	}

	private async Task DiscoverRelatedSubjectsAsync(
		string subjectId,
		int maxDepth,
		HashSet<string> visited,
		List<string> allSubjects,
		CancellationToken cancellationToken)
	{
		if (maxDepth <= 0 || !visited.Add(subjectId))
		{
			return;
		}

		var relatedSubjects = await _relationshipResolver.GetRelatedSubjectsAsync(
			subjectId, cancellationToken).ConfigureAwait(false);

		foreach (var related in relatedSubjects)
		{
			if (visited.Contains(related))
			{
				continue;
			}

			LogCascadeErasureRelatedSubjectDiscovered(subjectId, related);
			allSubjects.Add(related);

			await DiscoverRelatedSubjectsAsync(
				related, maxDepth - 1, visited, allSubjects, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	[LoggerMessage(
		ComplianceEventId.CascadeErasureStarted,
		LogLevel.Information,
		"Starting cascade erasure for subject {SubjectId} with depth {Depth}, dry run: {DryRun}")]
	private partial void LogCascadeErasureStarted(string subjectId, int depth, bool dryRun);

	[LoggerMessage(
		ComplianceEventId.CascadeErasureCompleted,
		LogLevel.Information,
		"Cascade erasure completed for subject {SubjectId}. Total subjects: {SubjectCount}, dry run: {IsDryRun}")]
	private partial void LogCascadeErasureCompleted(string subjectId, int subjectCount, bool isDryRun);

	[LoggerMessage(
		ComplianceEventId.CascadeErasureFailed,
		LogLevel.Error,
		"Cascade erasure failed for subject {SubjectId}")]
	private partial void LogCascadeErasureFailed(string subjectId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.CascadeErasureRelatedSubjectDiscovered,
		LogLevel.Debug,
		"Discovered related subject {RelatedSubjectId} from {ParentSubjectId}")]
	private partial void LogCascadeErasureRelatedSubjectDiscovered(string parentSubjectId, string relatedSubjectId);
}
