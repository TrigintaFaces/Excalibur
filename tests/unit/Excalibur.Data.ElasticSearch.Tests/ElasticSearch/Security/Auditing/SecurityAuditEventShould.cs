// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityAuditEventShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new SecurityAuditEvent();

		sut.EventId.ShouldBe(string.Empty);
		sut.Timestamp.ShouldBe(default);
		sut.EventType.ShouldBe(SecurityEventType.Authentication);
		sut.Severity.ShouldBe(SecurityEventSeverity.Low);
		sut.Source.ShouldBe(string.Empty);
		sut.UserId.ShouldBeNull();
		sut.SourceIpAddress.ShouldBeNull();
		sut.UserAgent.ShouldBeNull();
		sut.Details.ShouldNotBeNull();
		sut.Details.ShouldBeEmpty();
		sut.IntegrityHash.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var now = DateTimeOffset.UtcNow;
		var details = new Dictionary<string, object> { ["action"] = "login" };

		var sut = new SecurityAuditEvent
		{
			EventId = "evt-001",
			Timestamp = now,
			EventType = SecurityEventType.DataAccess,
			Severity = SecurityEventSeverity.High,
			Source = "IndexManager",
			UserId = "user-42",
			SourceIpAddress = "10.0.0.1",
			UserAgent = "ElasticClient/8.0",
			Details = details,
			IntegrityHash = "abc123hash",
		};

		sut.EventId.ShouldBe("evt-001");
		sut.Timestamp.ShouldBe(now);
		sut.EventType.ShouldBe(SecurityEventType.DataAccess);
		sut.Severity.ShouldBe(SecurityEventSeverity.High);
		sut.Source.ShouldBe("IndexManager");
		sut.UserId.ShouldBe("user-42");
		sut.SourceIpAddress.ShouldBe("10.0.0.1");
		sut.UserAgent.ShouldBe("ElasticClient/8.0");
		sut.Details.ShouldBeSameAs(details);
		sut.IntegrityHash.ShouldBe("abc123hash");
	}
}
