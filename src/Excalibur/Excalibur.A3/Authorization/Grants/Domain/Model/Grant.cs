// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Events;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain;
using Excalibur.Domain.Model;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a user permission or access grant with a specific scope and expiration.
/// </summary>
/// <remarks>
/// <para>
/// The Grant aggregate uses a composite key in the format "{UserId}:{Scope}" where
/// Scope is "{TenantId}:{GrantType}:{Qualifier}".
/// </para>
/// <para>
/// This aggregate implements <see cref="IAggregateRoot{TAggregate, TKey}"/> to support
/// static factory methods for repository integration with <see cref="IEventSourcedRepository{TAggregate}"/>.
/// </para>
/// </remarks>
public class Grant : AggregateRoot, IAggregateRoot<Grant, string>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Grant" /> class.
	/// </summary>
	/// <param name="userId"> The ID of the user to whom the grant is issued. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of grant issued. </param>
	/// <param name="qualifier"> Additional qualifier for the grant type. </param>
	/// <param name="expiresOn"> The optional expiration date of the grant. </param>
	/// <param name="grantedBy"> The identifier of the entity or user who issued the grant. </param>
	public Grant(
		string userId,
		string fullName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy)
		: this(userId, fullName, new GrantScope(tenantId, grantType, qualifier), expiresOn, grantedBy)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Grant" /> class with a specified <see cref="GrantScope" />.
	/// </summary>
	/// <param name="userId"> The ID of the user to whom the grant is issued. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="scope"> The scope of the grant. </param>
	/// <param name="expiresOn"> The optional expiration date of the grant. </param>
	/// <param name="grantedBy"> The identifier of the entity or user who issued the grant. </param>
	public Grant(
		string userId,
		string fullName,
		GrantScope scope,
		DateTimeOffset? expiresOn,
		string grantedBy)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);
		ArgumentException.ThrowIfNullOrEmpty(fullName);
		ArgumentNullException.ThrowIfNull(scope);

		if (expiresOn.HasValue && expiresOn.Value.ToUniversalTime() <= DateTimeOffset.UtcNow)
		{
			throw new ArgumentException($"The {nameof(expiresOn)} argument cannot be in the past.");
		}

		ArgumentException.ThrowIfNullOrEmpty(grantedBy);

		RaiseEvent(new GrantAdded(
			userId,
			fullName,
			ApplicationContext.ApplicationName,
			scope.TenantId,
			scope.GrantType,
			scope.Qualifier,
			expiresOn,
			grantedBy,
			GrantedOn));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Grant" /> class with additional information about the grant date.
	/// </summary>
	/// <param name="userId"> The ID of the user to whom the grant is issued. </param>
	/// <param name="fullName"> The full name of the user. </param>
	/// <param name="tenantId"> The tenant ID associated with the grant. </param>
	/// <param name="grantType"> The type of grant issued. </param>
	/// <param name="qualifier"> Additional qualifier for the grant type. </param>
	/// <param name="expiresOn"> The optional expiration date of the grant. </param>
	/// <param name="grantedBy"> The identifier of the entity or user who issued the grant. </param>
	/// <param name="grantedOn"> The date and time when the grant was issued. </param>
	public Grant(
		string userId,
		string fullName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		DateTimeOffset grantedOn)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);
		ArgumentException.ThrowIfNullOrEmpty(fullName);

		if (expiresOn.HasValue && expiresOn.Value.ToUniversalTime() <= DateTimeOffset.UtcNow)
		{
			throw new ArgumentException($"The {nameof(expiresOn)} argument cannot be in the past.");
		}

		ArgumentException.ThrowIfNullOrEmpty(grantedBy);

		RaiseEvent(new GrantAdded(
			userId,
			fullName,
			ApplicationContext.ApplicationName,
			tenantId,
			grantType,
			qualifier,
			expiresOn,
			grantedBy,
			grantedOn));
	}

	/// <summary>
	/// Private constructor for event replay via static factory methods.
	/// </summary>
	private Grant()
	{
	}

	/// <summary>
	/// Gets the optional expiration date of the grant.
	/// </summary>
	/// <value>The expiration date, or <see langword="null"/> if the grant does not expire.</value>
	public DateTimeOffset? ExpiresOn { get; internal set; }

	/// <summary>
	/// Gets the full name of the user to whom the grant was issued.
	/// </summary>
	/// <value>The full name of the user, or <see langword="null"/> if not available.</value>
	public string? FullName { get; internal set; }

	/// <summary>
	/// Gets the identifier of the entity or user who issued the grant.
	/// </summary>
	/// <value>The identifier of the entity or user, or <see langword="null"/> if not available.</value>
	public string? GrantedBy { get; internal set; }

	/// <summary>
	/// Gets the date and time when the grant was issued.
	/// </summary>
	/// <value>The date and time when the grant was issued.</value>
	public DateTimeOffset GrantedOn { get; internal set; }

	/// <summary>
	/// Gets the identifier of the entity or user who revoked the grant.
	/// </summary>
	/// <value>The identifier of the entity or user, or <see langword="null"/> if the grant has not been revoked.</value>
	public string? RevokedBy { get; internal set; }

	/// <summary>
	/// Gets the date and time when the grant was revoked.
	/// </summary>
	/// <value>The date and time when the grant was revoked, or <see langword="null"/> if not revoked.</value>
	public DateTimeOffset? RevokedOn { get; internal set; }

	/// <summary>
	/// Gets the scope of the grant.
	/// </summary>
	/// <value>The scope of the grant, or <see langword="null"/> if not available.</value>
	public GrantScope? Scope { get; internal set; }

	/// <summary>
	/// Gets the ID of the user to whom the grant was issued.
	/// </summary>
	/// <value>The ID of the user, or <see langword="null"/> if not available.</value>
	public string? UserId { get; internal set; }

	/// <summary>
	/// Creates a new Grant instance with the specified identifier.
	/// </summary>
	/// <param name="id">The composite key in format "{UserId}:{Scope}".</param>
	/// <returns>A new Grant instance.</returns>
	public static Grant Create(string id) => new() { Id = id };

	/// <summary>
	/// Rebuilds a Grant from a stream of historical events.
	/// </summary>
	/// <param name="id">The composite key in format "{UserId}:{Scope}".</param>
	/// <param name="events">The stream of events to apply.</param>
	/// <returns>The Grant rebuilt from the events.</returns>
	public static Grant FromEvents(string id, IEnumerable<IDomainEvent> events)
	{
		var grant = new Grant { Id = id };
		grant.LoadFromHistory(events);
		return grant;
	}

	/// <summary>
	/// Determines whether the grant is currently active.
	/// </summary>
	/// <returns> <c> true </c> if the grant is active; otherwise, <c> false </c>. </returns>
	public bool IsActive() => !(IsExpired() || IsRevoked());

	/// <summary>
	/// Determines whether the grant has expired.
	/// </summary>
	/// <returns> <c> true </c> if the grant has expired; otherwise, <c> false </c>. </returns>
	public bool IsExpired() => ExpiresOn <= DateTimeOffset.UtcNow;

	/// <summary>
	/// Determines whether the grant has been revoked.
	/// </summary>
	/// <returns> <c> true </c> if the grant has been revoked; otherwise, <c> false </c>. </returns>
	public bool IsRevoked() => RevokedOn.HasValue;

	/// <summary>
	/// Revokes the grant.
	/// </summary>
	/// <param name="revokedBy"> The identifier of the entity or user who revoked the grant. </param>
	public void Revoke(string revokedBy) =>
		RaiseEvent(new GrantRevoked
		{
			UserId = UserId ?? string.Empty,
			FullName = FullName ?? string.Empty,
			ApplicationName = ApplicationContext.ApplicationName,
			TenantId = Scope?.TenantId ?? string.Empty,
			GrantType = Scope?.GrantType ?? string.Empty,
			Qualifier = Scope?.Qualifier ?? string.Empty,
			ExpiresOn = ExpiresOn,
			RevokedBy = revokedBy,
			RevokedOn = DateTimeOffset.UtcNow,
		});

	/// <inheritdoc />
	protected override void ApplyEventInternal(IDomainEvent @event)
	{
		switch (@event)
		{
			case IGrantAdded added:
				UserId = added.UserId;
				FullName = added.FullName;
				Scope = new GrantScope(added.TenantId, added.GrantType, added.Qualifier);
				ExpiresOn = added.ExpiresOn;
				GrantedBy = added.GrantedBy;
				GrantedOn = added.GrantedOn;
				// Set the composite Id from event data
				Id = $"{added.UserId}:{Scope}";
				break;

			case IGrantRevoked revoked:
				RevokedBy = revoked.RevokedBy;
				RevokedOn = revoked.RevokedOn;
				break;
		}
	}
}
