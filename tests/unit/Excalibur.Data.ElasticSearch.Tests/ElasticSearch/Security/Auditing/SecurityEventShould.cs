// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SecurityEventShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new SecurityEvent();

		sut.EventId.ShouldBe(Guid.Empty);
		sut.Timestamp.ShouldBe(default);
		sut.EventType.ShouldBe(string.Empty);
		sut.Severity.ShouldBe(string.Empty);
		sut.Source.ShouldBeNull();
		sut.UserId.ShouldBeNull();
		sut.SourceIpAddress.ShouldBeNull();
		sut.UserAgent.ShouldBeNull();
		sut.AdditionalData.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var id = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object> { ["key"] = "value" };

		var sut = new SecurityEvent
		{
			EventId = id,
			Timestamp = now,
			EventType = "Authentication",
			Severity = "High",
			Source = "SecurityMonitor",
			UserId = "admin",
			SourceIpAddress = "192.168.1.1",
			UserAgent = "Mozilla/5.0",
			AdditionalData = data,
		};

		sut.EventId.ShouldBe(id);
		sut.Timestamp.ShouldBe(now);
		sut.EventType.ShouldBe("Authentication");
		sut.Severity.ShouldBe("High");
		sut.Source.ShouldBe("SecurityMonitor");
		sut.UserId.ShouldBe("admin");
		sut.SourceIpAddress.ShouldBe("192.168.1.1");
		sut.UserAgent.ShouldBe("Mozilla/5.0");
		sut.AdditionalData.ShouldBeSameAs(data);
	}
}
