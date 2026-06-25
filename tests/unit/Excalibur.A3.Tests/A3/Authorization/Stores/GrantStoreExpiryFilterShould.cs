// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Stores.InMemory;

namespace Excalibur.Tests.A3.Authorization.Stores;

/// <summary>
/// Regression lock for <c>bd-9amsrw</c> (Sprint 847, Lane F2 — authz expiry enforcement, KEYSTONE): the
/// grant read path MUST be <b>default-secure</b> — an expired grant MUST NOT be returned by the default
/// <see cref="IGrantStore.GetAllGrantsAsync(string, System.Threading.CancellationToken)"/> overload, so it
/// can never influence an authorization or separation-of-duties decision.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (author ≠ impl, <c>pin-interface-seam-before-tests</c>),
/// bound to the settled seam (PlatformDeveloper): <see cref="Grant.IsExpired(System.DateTimeOffset)"/>
/// (pure, boundary <c>&lt;=</c>) + the default-secure 2-arg <c>GetAllGrantsAsync</c>.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the true pre-fix parent):</b> the discriminator binds the <b>stable 2-arg
/// API</b>, which existed pre-fix and returned <i>all</i> grants unfiltered. On pre-fix HEAD the seeded
/// expired grant IS returned, so the "active-only" assertion fails (RED); post-fix the default-secure
/// filter excludes it (GREEN). Determinism: expiry is seeded as a fixed past offset against the store's
/// (system) clock — a 5-minute-past <c>ExpiresOn</c> is unambiguously expired with no wall-clock race.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantStoreExpiryFilterShould
{
	private const string UserId = "user-9amsrw";
	private const string TenantId = "tenant-1";
	private const string GrantType = "activity";

	[Fact]
	public async Task ExcludeExpiredGrants_FromTheDefaultReadPath()
	{
		// Arrange — one expired and one active grant for the same user (distinct qualifiers → distinct keys).
		var store = new InMemoryGrantStore();
		var now = DateTimeOffset.UtcNow;

		var expiredGrant = NewGrant("expired-op", expiresOn: now.AddMinutes(-5));
		var activeGrant = NewGrant("active-op", expiresOn: now.AddYears(1));

		_ = await store.SaveGrantAsync(expiredGrant, CancellationToken.None);
		_ = await store.SaveGrantAsync(activeGrant, CancellationToken.None);

		// Act — the default-secure read path (the seam every authz/SoD decision consumes).
		var grants = await store.GetAllGrantsAsync(UserId, CancellationToken.None);

		// Assert — only the active grant is visible; the expired grant is inexpressible in a decision.
		// RED on pre-fix HEAD, where the 2-arg overload returned ALL grants (expired included).
		var qualifiers = grants.Select(g => g.Qualifier).ToList();
		qualifiers.ShouldContain("active-op");
		qualifiers.ShouldNotContain(
			"expired-op",
			customMessage: "An expired grant must never be returned by the default read path (default-secure).");
	}

	[Fact]
	public async Task StillReturnActiveGrants_NoOverBlocking()
	{
		// Arrange — a future-dated grant and a never-expiring (null ExpiresOn) grant; both are active.
		var store = new InMemoryGrantStore();
		var now = DateTimeOffset.UtcNow;

		_ = await store.SaveGrantAsync(NewGrant("future-op", expiresOn: now.AddDays(1)), CancellationToken.None);
		_ = await store.SaveGrantAsync(NewGrant("perpetual-op", expiresOn: null), CancellationToken.None);

		// Act
		var grants = await store.GetAllGrantsAsync(UserId, CancellationToken.None);

		// Assert — the default-secure filter must not over-block active grants (no regression).
		var qualifiers = grants.Select(g => g.Qualifier).ToList();
		qualifiers.ShouldContain("future-op");
		qualifiers.ShouldContain("perpetual-op");
	}

	private static Grant NewGrant(string qualifier, DateTimeOffset? expiresOn) => new(
		UserId,
		FullName: null,
		TenantId,
		GrantType,
		qualifier,
		expiresOn,
		GrantedBy: "admin",
		GrantedOn: DateTimeOffset.UtcNow.AddMinutes(-10));
}
