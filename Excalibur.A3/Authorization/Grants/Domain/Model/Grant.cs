using Excalibur.A3.Authorization.Events;
using Excalibur.Core;
using Excalibur.Domain.Model;

namespace Excalibur.A3.Authorization.Grants.Domain.Model;

/// <summary>
///     Represents a user permission or access grant with a specific scope and expiration.
/// </summary>
public sealed class Grant : AggregateRootBase
{
	/// <summary>
	///     Initializes a new instance of the <see cref="Grant" /> class.
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
	///     Initializes a new instance of the <see cref="Grant" /> class with a specified <see cref="GrantScope" />.
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

		UserId = userId;
		FullName = fullName;
		Scope = scope;
		ExpiresOn = expiresOn;
		GrantedBy = grantedBy;
		GrantedOn = DateTimeOffset.UtcNow;

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
	///     Initializes a new instance of the <see cref="Grant" /> class with additional information about the grant date.
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
		: this(userId, fullName, new GrantScope(tenantId, grantType, qualifier), expiresOn, grantedBy)
	{
		UserId = userId;
		FullName = fullName;
		Scope = new GrantScope(tenantId, grantType, qualifier);
		ExpiresOn = expiresOn;
		GrantedBy = grantedBy;
		GrantedOn = grantedOn;
	}

	/// <summary>
	///     Gets or sets the optional expiration date of the grant.
	/// </summary>
	public DateTimeOffset? ExpiresOn { get; internal set; }

	/// <summary>
	///     Gets or sets the full name of the user to whom the grant was issued.
	/// </summary>
	public string FullName { get; internal set; }

	/// <summary>
	///     Gets or sets the identifier of the entity or user who issued the grant.
	/// </summary>
	public string GrantedBy { get; internal set; }

	/// <summary>
	///     Gets or sets the date and time when the grant was issued.
	/// </summary>
	public DateTimeOffset GrantedOn { get; internal set; }

	/// <inheritdoc />
	public override string Key => $"{UserId}:{Scope}";

	/// <summary>
	///     Gets or sets the identifier of the entity or user who revoked the grant.
	/// </summary>
	public string? RevokedBy { get; internal set; }

	/// <summary>
	///     Gets or sets the date and time when the grant was revoked.
	/// </summary>
	public DateTimeOffset? RevokedOn { get; internal set; }

	/// <summary>
	///     Gets or sets the scope of the grant.
	/// </summary>
	public GrantScope Scope { get; internal set; }

	/// <summary>
	///     Gets or sets the ID of the user to whom the grant was issued.
	/// </summary>
	public string UserId { get; internal set; }

	/// <summary>
	///     Determines whether the grant is currently active.
	/// </summary>
	/// <returns> <c> true </c> if the grant is active; otherwise, <c> false </c>. </returns>
	public bool IsActive() => !(IsExpired() || IsRevoked());

	/// <summary>
	///     Determines whether the grant has expired.
	/// </summary>
	/// <returns> <c> true </c> if the grant has expired; otherwise, <c> false </c>. </returns>
	public bool IsExpired() => ExpiresOn.HasValue && ExpiresOn.Value <= DateTimeOffset.UtcNow;

	/// <summary>
	///     Determines whether the grant has been revoked.
	/// </summary>
	/// <returns> <c> true </c> if the grant has been revoked; otherwise, <c> false </c>. </returns>
	public bool IsRevoked() => RevokedOn.HasValue;

	/// <summary>
	///     Revokes the grant.
	/// </summary>
	/// <param name="revokedBy"> The identifier of the entity or user who revoked the grant. </param>
	public void Revoke(string revokedBy)
	{
		RevokedBy = revokedBy;
		RevokedOn = DateTimeOffset.UtcNow;

		RaiseEvent(new GrantRevoked(
			UserId,
			FullName,
			ApplicationContext.ApplicationName,
			Scope.TenantId,
			Scope.GrantType,
			Scope.Qualifier,
			ExpiresOn,
			RevokedBy,
			RevokedOn.Value));
	}
}
