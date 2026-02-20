// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityIncidentShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new SecurityIncident();

		sut.IncidentId.ShouldBe(Guid.Empty);
		sut.Timestamp.ShouldBe(default);
		sut.IncidentType.ShouldBe(string.Empty);
		sut.Severity.ShouldBe(string.Empty);
		sut.Description.ShouldBe(string.Empty);
		sut.AffectedUserId.ShouldBeNull();
		sut.SourceIpAddress.ShouldBeNull();
		sut.AffectedSystems.ShouldNotBeNull();
		sut.AffectedSystems.ShouldBeEmpty();
		sut.ResponseActions.ShouldNotBeNull();
		sut.ResponseActions.ShouldBeEmpty();
		sut.Resolution.ShouldBeNull();
		sut.AdditionalData.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var id = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object> { ["detail"] = "info" };

		var sut = new SecurityIncident
		{
			IncidentId = id,
			Timestamp = now,
			IncidentType = "BruteForce",
			Severity = "Critical",
			Description = "Multiple failed login attempts detected",
			AffectedUserId = "user-123",
			SourceIpAddress = "10.0.0.99",
			AffectedSystems = ["auth-service", "api-gateway"],
			ResponseActions = ["IP blocked", "User notified"],
			Resolution = "IP permanently banned",
			AdditionalData = data,
		};

		sut.IncidentId.ShouldBe(id);
		sut.Timestamp.ShouldBe(now);
		sut.IncidentType.ShouldBe("BruteForce");
		sut.Severity.ShouldBe("Critical");
		sut.Description.ShouldBe("Multiple failed login attempts detected");
		sut.AffectedUserId.ShouldBe("user-123");
		sut.SourceIpAddress.ShouldBe("10.0.0.99");
		sut.AffectedSystems.Count.ShouldBe(2);
		sut.ResponseActions.Count.ShouldBe(2);
		sut.Resolution.ShouldBe("IP permanently banned");
		sut.AdditionalData.ShouldBeSameAs(data);
	}
}
