// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Events;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.A3.Governance;

/// <summary>
/// Event-sourced aggregate representing an access review campaign.
/// </summary>
/// <remarks>
/// <para>
/// A campaign is a point-in-time snapshot of grants to review. Items are populated
/// at creation time from an <see cref="AccessReviewScope"/> query. New grants after
/// campaign creation are not reviewed until the next campaign.
/// </para>
/// <para>
/// State transitions: <c>Created</c> -> <c>InProgress</c> -> <c>Completed</c> | <c>Expired</c>.
/// </para>
/// </remarks>
internal sealed class AccessReviewCampaign : AggregateRoot, IAggregateRoot<AccessReviewCampaign, string>
{
	private readonly List<AccessReviewItem> _items = [];
	private readonly List<AccessReviewDecision> _decisions = [];

	/// <summary>
	/// Private constructor for event replay via static factory methods.
	/// </summary>
	private AccessReviewCampaign()
	{
	}

	/// <summary>
	/// Creates a new access review campaign.
	/// </summary>
	/// <param name="campaignId">Unique campaign identifier.</param>
	/// <param name="campaignName">Display name.</param>
	/// <param name="scope">The scope defining which grants to review.</param>
	/// <param name="createdBy">The actor creating the campaign.</param>
	/// <param name="startsAt">When the campaign starts.</param>
	/// <param name="expiresAt">When the campaign expires.</param>
	/// <param name="expiryPolicy">Policy to apply on expiry.</param>
	/// <param name="items">The grants to review (populated from scope query at creation).</param>
	public AccessReviewCampaign(
		string campaignId,
		string campaignName,
		AccessReviewScope scope,
		string createdBy,
		DateTimeOffset startsAt,
		DateTimeOffset expiresAt,
		AccessReviewExpiryPolicy expiryPolicy,
		IReadOnlyList<AccessReviewItem> items)
	{
		ArgumentException.ThrowIfNullOrEmpty(campaignId);
		ArgumentException.ThrowIfNullOrEmpty(campaignName);
		ArgumentNullException.ThrowIfNull(scope);
		ArgumentException.ThrowIfNullOrEmpty(createdBy);
		ArgumentNullException.ThrowIfNull(items);

		if (expiresAt <= startsAt)
		{
			throw new ArgumentException("ExpiresAt must be after StartsAt.", nameof(expiresAt));
		}

		RaiseEvent(new AccessReviewCampaignCreated
		{
			CampaignId = campaignId,
			CampaignName = campaignName,
			Scope = scope,
			CreatedBy = createdBy,
			StartsAt = startsAt,
			ExpiresAt = expiresAt,
			ExpiryPolicy = expiryPolicy,
			Items = items,
		});
	}

	/// <summary>
	/// Gets the campaign display name.
	/// </summary>
	public string CampaignName { get; private set; } = string.Empty;

	/// <summary>
	/// Gets the scope of grants being reviewed.
	/// </summary>
	public AccessReviewScope Scope { get; private set; } = new(AccessReviewScopeType.AllGrants, null);

	/// <summary>
	/// Gets the actor who created the campaign.
	/// </summary>
	public string CreatedBy { get; private set; } = string.Empty;

	/// <summary>
	/// Gets when the campaign starts.
	/// </summary>
	public DateTimeOffset StartsAt { get; private set; }

	/// <summary>
	/// Gets when the campaign expires.
	/// </summary>
	public DateTimeOffset ExpiresAt { get; private set; }

	/// <summary>
	/// Gets the policy applied on campaign expiry.
	/// </summary>
	public AccessReviewExpiryPolicy ExpiryPolicy { get; private set; }

	/// <summary>
	/// Gets the current lifecycle state.
	/// </summary>
	public AccessReviewState State { get; private set; }

	/// <summary>
	/// Gets the items (grants) to be reviewed.
	/// </summary>
	public IReadOnlyList<AccessReviewItem> Items => _items.AsReadOnly();

	/// <summary>
	/// Gets the decisions made so far.
	/// </summary>
	public IReadOnlyList<AccessReviewDecision> Decisions => _decisions.AsReadOnly();

	/// <summary>
	/// Creates a new instance with the specified identifier for event replay.
	/// </summary>
	/// <param name="id">The campaign identifier.</param>
	/// <returns>A new AccessReviewCampaign instance.</returns>
	public static AccessReviewCampaign Create(string id) => new() { Id = id };

	/// <summary>
	/// Rebuilds a campaign from a stream of historical events.
	/// </summary>
	/// <param name="id">The campaign identifier.</param>
	/// <param name="events">The stream of events to apply.</param>
	/// <returns>The campaign rebuilt from events.</returns>
	public static AccessReviewCampaign FromEvents(string id, IEnumerable<IDomainEvent> events)
	{
		var campaign = new AccessReviewCampaign { Id = id };
		campaign.LoadFromHistory(events);
		return campaign;
	}

	/// <summary>
	/// Starts the campaign, transitioning from <see cref="AccessReviewState.Created"/>
	/// to <see cref="AccessReviewState.InProgress"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the campaign is not in Created state.</exception>
	public void Start()
	{
		if (State != AccessReviewState.Created)
		{
			throw new InvalidOperationException(
				$"Cannot start campaign '{Id}': current state is {State}, expected {AccessReviewState.Created}.");
		}

		// We reuse the Created event's timestamp for state tracking;
		// the actual transition is just a state change. We model this
		// by re-raising the domain event pattern used for completions.
		// For Start, we simply need a state transition event.
		// Using a lightweight approach: the Created->InProgress transition
		// does not need a separate event because campaigns can auto-start
		// (AccessReviewOptions.AutoStartOnCreation). We model Start as
		// a completion-style event for audit trail.
		RaiseEvent(new AccessReviewCampaignStarted
		{
			CampaignId = Id,
		});
	}

	/// <summary>
	/// Records a reviewer's decision on a grant item.
	/// </summary>
	/// <param name="decision">The review decision.</param>
	/// <exception cref="InvalidOperationException">Thrown when the campaign is not InProgress,
	/// the item is not found, or the item already has a decision.</exception>
	public void RecordDecision(AccessReviewDecision decision)
	{
		ArgumentNullException.ThrowIfNull(decision);

		if (State != AccessReviewState.InProgress)
		{
			throw new InvalidOperationException(
				$"Cannot record decision on campaign '{Id}': current state is {State}, expected {AccessReviewState.InProgress}.");
		}

		var item = _items.Find(i =>
			string.Equals(i.GrantUserId, decision.GrantUserId, StringComparison.Ordinal) &&
			string.Equals(i.GrantScope, decision.GrantScope, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"Item not found in campaign '{Id}': user '{decision.GrantUserId}', scope '{decision.GrantScope}'.");

		if (item.CurrentDecision is not null)
		{
			throw new InvalidOperationException(
				$"Item already decided in campaign '{Id}': user '{decision.GrantUserId}', scope '{decision.GrantScope}'.");
		}

		RaiseEvent(new AccessReviewDecisionMade
		{
			CampaignId = Id,
			GrantUserId = decision.GrantUserId,
			GrantScope = decision.GrantScope,
			ReviewerId = decision.ReviewerId,
			Outcome = decision.Outcome,
			Justification = decision.Justification,
			DelegateToReviewerId = decision.DelegateToReviewerId,
		});
	}

	/// <summary>
	/// Completes the campaign when all items have been decided.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when not InProgress or undecided items remain.</exception>
	public void Complete()
	{
		if (State != AccessReviewState.InProgress)
		{
			throw new InvalidOperationException(
				$"Cannot complete campaign '{Id}': current state is {State}, expected {AccessReviewState.InProgress}.");
		}

		if (_items.Exists(i => i.CurrentDecision is null))
		{
			throw new InvalidOperationException(
				$"Cannot complete campaign '{Id}': undecided items remain.");
		}

		RaiseEvent(new AccessReviewCampaignCompleted
		{
			CampaignId = Id,
		});
	}

	/// <summary>
	/// Expires the campaign when the deadline passes.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the campaign is not InProgress.</exception>
	public void Expire()
	{
		if (State != AccessReviewState.InProgress)
		{
			throw new InvalidOperationException(
				$"Cannot expire campaign '{Id}': current state is {State}, expected {AccessReviewState.InProgress}.");
		}

		RaiseEvent(new AccessReviewCampaignExpired
		{
			CampaignId = Id,
			AppliedPolicy = ExpiryPolicy,
		});
	}

	/// <summary>
	/// Converts the aggregate to a read-model summary.
	/// </summary>
	internal AccessReviewCampaignSummary ToSummary() => new(
		CampaignId: Id,
		CampaignName: CampaignName,
		Scope: Scope,
		CreatedBy: CreatedBy,
		StartsAt: StartsAt,
		ExpiresAt: ExpiresAt,
		ExpiryPolicy: ExpiryPolicy,
		State: State,
		TotalItems: _items.Count,
		DecidedItems: _items.Count(i => i.CurrentDecision is not null));

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case AccessReviewCampaignCreated e: Apply(e); break;
			case AccessReviewCampaignStarted: ApplyStarted(); break;
			case AccessReviewDecisionMade e: Apply(e); break;
			case AccessReviewCampaignCompleted: ApplyCompleted(); break;
			case AccessReviewCampaignExpired: ApplyExpired(); break;
		}
	}

	private void Apply(AccessReviewCampaignCreated e)
	{
		Id = e.CampaignId;
		CampaignName = e.CampaignName;
		Scope = e.Scope;
		CreatedBy = e.CreatedBy;
		StartsAt = e.StartsAt;
		ExpiresAt = e.ExpiresAt;
		ExpiryPolicy = e.ExpiryPolicy;
		State = AccessReviewState.Created;
		_items.Clear();
		_items.AddRange(e.Items);
	}

	private void ApplyStarted()
	{
		State = AccessReviewState.InProgress;
	}

	private void Apply(AccessReviewDecisionMade e)
	{
		var index = _items.FindIndex(i =>
			string.Equals(i.GrantUserId, e.GrantUserId, StringComparison.Ordinal) &&
			string.Equals(i.GrantScope, e.GrantScope, StringComparison.Ordinal));

		if (index >= 0)
		{
			_items[index] = _items[index] with { CurrentDecision = e.Outcome };
		}

		_decisions.Add(new AccessReviewDecision(
			CampaignId: e.CampaignId,
			GrantUserId: e.GrantUserId,
			GrantScope: e.GrantScope,
			ReviewerId: e.ReviewerId,
			Outcome: e.Outcome,
			Justification: e.Justification,
			DecisionTimestamp: e.OccurredAt,
			DelegateToReviewerId: e.DelegateToReviewerId));
	}

	private void ApplyCompleted()
	{
		State = AccessReviewState.Completed;
	}

	private void ApplyExpired()
	{
		State = AccessReviewState.Expired;
	}
}
