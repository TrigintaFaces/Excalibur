// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Serialization;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Locks that an activity-group grant still authorizes after the policy document has been through the
/// CACHE SERIALIZER — the path a second request takes.
/// </summary>
/// <remarks>
/// <para>
/// The policy provider caches the activity-group document and reads it back on the next request within
/// the TTL. This document was once declared <c>IDictionary&lt;string, object&gt;</c>, and a declared
/// <see langword="object" /> is the one shape that cannot survive a JSON round trip intact — the
/// deserializer materialises it as a <c>JsonElement</c>, which satisfied no collection test, so every
/// activity-group grant was silently denied on the second request. No exception, no log.
/// </para>
/// <para>
/// <b>The document is now typed end to end, so that defect is no longer expressible.</b> This arm is what
/// keeps it that way: a future change that re-widens any hop back to <see langword="object" /> compiles
/// fine and goes red here, because the round trip is the only place the widening shows.
/// </para>
/// <para>
/// The serializer is the PRODUCTION one, configured exactly as the provider configures it. A stand-in
/// would prove nothing: the whole failure was a property of how that specific serializer materialises a
/// declared <see langword="object" />.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicySurvivesTheCacheRoundTripShould
{
	private const string TenantId = "tenant-1";
	private const string GroupName = "Finance";
	private const string Activity = "CreatePayment";

	/// <summary>
	/// THE LOCK: a grant that authorizes on the cache-miss path must still authorize after the document
	/// has been cached and read back.
	/// </summary>
	/// <remarks>
	/// A user whose access depends on which side of a cache expiry their request lands is not authorized —
	/// they are intermittently authorized, which is the same defect wearing a clock.
	/// </remarks>
	[Fact]
	public void AuthorizeAfterThePolicyDocumentHasBeenThroughTheCacheSerializer()
	{
		var document = ActivityGroupDocument();

		PolicyOver(document).IsAuthorized(Activity, resourceId: null).ShouldBeTrue(
			"control: the document authorizes BEFORE the round-trip, so a failure below is the round-trip");

		var restored = RoundTrip(document);

		PolicyOver(restored).IsAuthorized(Activity, resourceId: null).ShouldBeTrue(
			"the same grant, the same activity, the same group — only a cache write and read in between. "
			+ "A grant that stops authorizing because the document was cached denies a user who is "
			+ "entitled, and denies them silently");
	}

	/// <summary>
	/// CONTROL: the round-trip itself is not destroying the document wholesale.
	/// </summary>
	/// <remarks>
	/// Without this, a failure above is ambiguous between "the value's shape changed" and "the serializer
	/// dropped the entry". This pins it to the shape by proving the key survives.
	/// </remarks>
	[Fact]
	public void PreserveTheGroupKeyAcrossTheRoundTrip()
	{
		var restored = RoundTrip(ActivityGroupDocument());

		restored.ShouldContainKey(
			SegmentedKey.Compose(TenantId, GroupName),
			"if the key were lost the membership failure would be about the entry, not about its value");
	}

	#region Helpers

	/// <summary>
	/// The document exactly as the policy-data seam produces it: activity names boxed as
	/// <see langword="object" />.
	/// </summary>
	private static Dictionary<string, IReadOnlyCollection<string>> ActivityGroupDocument() =>
		new(StringComparer.Ordinal)
		{
			[SegmentedKey.Compose(TenantId, GroupName)] = new List<string> { Activity },
		};

	/// <summary>
	/// Writes and reads the document through the PRODUCTION cache serializer, configured as the policy
	/// provider configures it.
	/// </summary>
	private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> RoundTrip(IReadOnlyDictionary<string, IReadOnlyCollection<string>> document)
	{
		using var serializer = new DispatchJsonSerializer(options =>
		{
			var defaults = DispatchJsonSerializerOptions.Default;
			options.PropertyNamingPolicy = defaults.PropertyNamingPolicy;
			options.DefaultIgnoreCondition = defaults.DefaultIgnoreCondition;
			options.WriteIndented = defaults.WriteIndented;
		});

		var json = serializer.Serialize<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(document);

		return serializer.Deserialize<Dictionary<string, IReadOnlyCollection<string>>>(json)
			?? throw new InvalidOperationException("the cache serializer returned no document");
	}

	private static AuthorizationPolicy PolicyOver(IReadOnlyDictionary<string, IReadOnlyCollection<string>> activityGroups)
	{
		var grants = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[SegmentedKey.Compose(TenantId, GrantType.ActivityGroup, GroupName)] = new object(),
		};

		var tenantContext = A.Fake<ITenantContext>();
		_ = A.CallTo(() => tenantContext.TenantId).Returns(TenantId);

		return new AuthorizationPolicy(grants, activityGroups, tenantContext, "user-1");
	}

	#endregion
}
