// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.EventStore;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventMetadataShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var metadata = new EventMetadata();

		metadata.EventType.ShouldBe(string.Empty);
		metadata.EventVersion.ShouldBe(0);
		metadata.UserId.ShouldBeNull();
		metadata.CorrelationId.ShouldBeNull();
		metadata.CausationId.ShouldBeNull();
		metadata.Properties.ShouldNotBeNull();
		metadata.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void SetAndGetProperties()
	{
		var now = DateTimeOffset.UtcNow;
		var metadata = new EventMetadata
		{
			EventType = "OrderCreated",
			EventVersion = 2,
			Timestamp = now,
			UserId = "user-123",
			CorrelationId = "corr-456",
			CausationId = "cause-789"
		};

		metadata.EventType.ShouldBe("OrderCreated");
		metadata.EventVersion.ShouldBe(2);
		metadata.Timestamp.ShouldBe(now);
		metadata.UserId.ShouldBe("user-123");
		metadata.CorrelationId.ShouldBe("corr-456");
		metadata.CausationId.ShouldBe("cause-789");
	}

	[Fact]
	public void AddCustomProperties()
	{
		var metadata = new EventMetadata();
		metadata.Properties["key1"] = "value1";
		metadata.Properties["key2"] = 42;
		metadata.Properties["key3"] = null;

		metadata.Properties.Count.ShouldBe(3);
		metadata.Properties["key1"].ShouldBe("value1");
		metadata.Properties["key2"].ShouldBe(42);
		metadata.Properties["key3"].ShouldBeNull();
	}
}
