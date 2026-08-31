// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Locks the sync payload parsing. These read a real JSON body through the service's own
/// deserialization path, so they fail if that path stops producing populated objects — the failure
/// mode a compile-time-only check cannot see.
/// </summary>
// ApplicationContext.Init clears process-wide state, so every class that calls it shares one
// collection. Without this the class ran in parallel with the others and wiped the keys they had
// just installed, which surfaces as an InvalidConfigurationException in whichever class lost the race.
[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ActivityGroupServiceShould : IDisposable
{
	private const string ActivityGroupsPayload = """
		[
		  {
		    "TenantId": "acme",
		    "Name": "Billing",
		    "Description": "Billing operations",
		    "Activities": [
		      { "ApplicationName": "Portal", "ActivityName": "Invoice.Read" }
		    ]
		  }
		]
		""";

	private const string GrantsPayload = """
		[
		  {
		    "TenantId": "acme",
		    "ActivityGroupName": "Billing",
		    "ExpiresOn": "2030-01-02T03:04:05+00:00",
		    "UserId": "user-1"
		  }
		]
		""";

	private readonly IActivityGroupStore _groupStore = A.Fake<IActivityGroupStore>();
	private readonly IActivityGroupGrantStore _grantStore = A.Fake<IActivityGroupGrantStore>();

	public ActivityGroupServiceShould() =>
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
			[nameof(AuthorizationCacheKey)] = "testapp",
		});

	public void Dispose() => ApplicationContext.Reset();

	[Fact]
	public async Task Populate_every_activity_group_field_from_the_sync_payload()
	{
		var sut = CreateService(ActivityGroupsPayload);

		await sut.SyncActivityGroupsAsync(CancellationToken.None);

		A.CallTo(() => _groupStore.CreateActivityGroupAsync(
				"acme",
				"Billing",
				"Invoice.Read",
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Populate_every_grant_field_from_the_sync_payload()
	{
		var sut = CreateService(GrantsPayload);

		await sut.SyncActivityGroupGrantsAsync("user-1", CancellationToken.None);

		A.CallTo(() => _grantStore.InsertActivityGroupGrantAsync(
				"user-1",
				"user-1",
				"acme",
				GrantType.ActivityGroup,
				"Billing",
				new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
				A<string>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Reject_a_payload_entry_that_omits_the_tenant()
	{
		var sut = CreateService(ActivityGroupsPayload.Replace("\"TenantId\": \"acme\"", "\"TenantId\": null", StringComparison.Ordinal));

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.SyncActivityGroupsAsync(CancellationToken.None));
	}

	private ActivityGroupService CreateService(string body)
	{
		var httpClient = new HttpClient(new StubHandler(body)) { BaseAddress = new Uri("https://authorization.test/") };
		var correlationId = A.Fake<ICorrelationId>();
		var token = A.Fake<IAuthenticationToken>();
		var cache = A.Fake<IDistributedCache>();

		A.CallTo(() => _grantStore.GetDistinctActivityGroupGrantUserIdsAsync(A<string>._, A<CancellationToken>._))
			.Returns<IReadOnlyList<string>>([]);

		return new ActivityGroupService(
			httpClient,
			correlationId,
			token,
			_groupStore,
			_grantStore,
			NullLogger<ActivityGroupService>.Instance,
			cache);
	}

	private sealed class StubHandler(string body) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			});
	}
}
