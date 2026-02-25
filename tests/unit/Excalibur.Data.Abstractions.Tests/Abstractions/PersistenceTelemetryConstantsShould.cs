// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class PersistenceTelemetryConstantsShould
{
	[Fact]
	public void HaveCorrectSourceName()
	{
		PersistenceTelemetryConstants.SourceName.ShouldBe("Excalibur.Data.Persistence");
	}

	[Fact]
	public void HaveCorrectVersion()
	{
		PersistenceTelemetryConstants.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void ProvideStaticActivitySource()
	{
		PersistenceTelemetryConstants.ActivitySource.ShouldNotBeNull();
		PersistenceTelemetryConstants.ActivitySource.Name.ShouldBe("Excalibur.Data.Persistence");
		PersistenceTelemetryConstants.ActivitySource.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void ProvideStaticMeter()
	{
		PersistenceTelemetryConstants.Meter.ShouldNotBeNull();
		PersistenceTelemetryConstants.Meter.Name.ShouldBe("Excalibur.Data.Persistence");
		PersistenceTelemetryConstants.Meter.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void HaveCorrectAttributeNames()
	{
		PersistenceTelemetryConstants.AttributeProviderName.ShouldBe("persistence.provider.name");
		PersistenceTelemetryConstants.AttributeProviderType.ShouldBe("persistence.provider.type");
		PersistenceTelemetryConstants.AttributeOperation.ShouldBe("persistence.operation");
		PersistenceTelemetryConstants.AttributeRequestType.ShouldBe("persistence.request.type");
	}

	[Fact]
	public void ReturnSameActivitySourceInstance()
	{
		var first = PersistenceTelemetryConstants.ActivitySource;
		var second = PersistenceTelemetryConstants.ActivitySource;
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void ReturnSameMeterInstance()
	{
		var first = PersistenceTelemetryConstants.Meter;
		var second = PersistenceTelemetryConstants.Meter;
		first.ShouldBeSameAs(second);
	}
}
