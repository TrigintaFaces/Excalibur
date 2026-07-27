// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

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
					// A dry run performs no erasure; report the planned subjects as scheduled.
					SubjectsScheduled = allSubjects.Count,
					RelatedSubjectsErased = allSubjects.Where(s => !string.Equals(s, subjectId, StringComparison.OrdinalIgnoreCase)).ToList(),
					IsDryRun = true
				};
			}

			// Erase each subject, inspecting the per-subject outcome rather than assuming success.
			// RequestErasureAsync schedules execution (it does not erase synchronously) and can report
			// Scheduled / BlockedByLegalHold / Failed — a blocked or failed subject must NOT be counted
			// as erased, and must surface so callers do not act on a false completion.
			var scheduledCount = 0;
			var erasedCount = 0;
			var blocked = new List<string>();
			var failed = new List<string>();

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

				var outcome = await _erasureService.RequestErasureAsync(request, cancellationToken)
					.ConfigureAwait(false);

				var isPrimary = string.Equals(subject, subjectId, StringComparison.OrdinalIgnoreCase);

				switch (outcome.Status)
				{
					case ErasureRequestStatus.Completed:
					case ErasureRequestStatus.PartiallyCompleted:
						erasedCount++;
						if (!isPrimary)
						{
							erasedRelated.Add(subject);
						}

						break;

					case ErasureRequestStatus.Pending:
					case ErasureRequestStatus.Scheduled:
					case ErasureRequestStatus.InProgress:
						scheduledCount++;
						if (!isPrimary)
						{
							erasedRelated.Add(subject);
						}

						break;

					case ErasureRequestStatus.BlockedByLegalHold:
						blocked.Add(subject);
						LogCascadeErasureSubjectBlocked(subjectId, subject);
						break;

					default:
						// Failed, Cancelled, or any other non-accepting status: not erased.
						failed.Add(subject);
						LogCascadeErasureSubjectFailed(subjectId, subject, outcome.Status.ToString());
						break;
				}
			}

			LogCascadeErasureCompleted(subjectId, allSubjects.Count, isDryRun: false);

			return new CascadeErasureResult
			{
				// Success only when no subject was blocked or failed — never mask an incomplete cascade.
				Success = blocked.Count == 0 && failed.Count == 0,
				PrimarySubjectId = subjectId,
				SubjectsErased = erasedCount,
				SubjectsScheduled = scheduledCount,
				RelatedSubjectsErased = erasedRelated,
				BlockedSubjects = blocked,
				FailedSubjects = failed
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

	[LoggerMessage(
		ComplianceEventId.CascadeErasureSubjectBlocked,
		LogLevel.Warning,
		"Cascade erasure for {PrimarySubjectId} was blocked for subject {SubjectId} (e.g. legal hold); not erased")]
	private partial void LogCascadeErasureSubjectBlocked(string primarySubjectId, string subjectId);

	[LoggerMessage(
		ComplianceEventId.CascadeErasureSubjectFailed,
		LogLevel.Error,
		"Cascade erasure for {PrimarySubjectId} did not erase subject {SubjectId} (status {Status})")]
	private partial void LogCascadeErasureSubjectFailed(string primarySubjectId, string subjectId, string status);
}
