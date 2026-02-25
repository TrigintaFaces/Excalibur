// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="WellKnownHeaderNames"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class WellKnownHeaderNamesShould
{
	[Fact]
	public void HaveCorrelationIdHeader()
	{
		// Assert
		WellKnownHeaderNames.CorrelationId.ShouldBe("X-Correlation-Id");
	}

	[Fact]
	public void HaveCausationIdHeader()
	{
		// Assert
		WellKnownHeaderNames.CausationId.ShouldBe("X-Causation-Id");
	}

	[Fact]
	public void HaveETagHeader()
	{
		// Assert
		WellKnownHeaderNames.ETag.ShouldBe("X-Etag");
	}

	[Fact]
	public void HaveTenantIdHeader()
	{
		// Assert
		WellKnownHeaderNames.TenantId.ShouldBe("X-Tenant-Id");
	}

	[Fact]
	public void HaveRaisedByHeader()
	{
		// Assert
		WellKnownHeaderNames.RaisedBy.ShouldBe("X-Raised-By");
	}

	[Fact]
	public void HaveAllHeadersWithXPrefix()
	{
		// Assert
		WellKnownHeaderNames.CorrelationId.ShouldStartWith("X-");
		WellKnownHeaderNames.CausationId.ShouldStartWith("X-");
		WellKnownHeaderNames.ETag.ShouldStartWith("X-");
		WellKnownHeaderNames.TenantId.ShouldStartWith("X-");
		WellKnownHeaderNames.RaisedBy.ShouldStartWith("X-");
	}

	[Fact]
	public void HaveNonNullValues()
	{
		// Assert
		WellKnownHeaderNames.CorrelationId.ShouldNotBeNull();
		WellKnownHeaderNames.CausationId.ShouldNotBeNull();
		WellKnownHeaderNames.ETag.ShouldNotBeNull();
		WellKnownHeaderNames.TenantId.ShouldNotBeNull();
		WellKnownHeaderNames.RaisedBy.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonEmptyValues()
	{
		// Assert
		WellKnownHeaderNames.CorrelationId.ShouldNotBeEmpty();
		WellKnownHeaderNames.CausationId.ShouldNotBeEmpty();
		WellKnownHeaderNames.ETag.ShouldNotBeEmpty();
		WellKnownHeaderNames.TenantId.ShouldNotBeEmpty();
		WellKnownHeaderNames.RaisedBy.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeStaticClass()
	{
		// Assert
		typeof(WellKnownHeaderNames).IsAbstract.ShouldBeTrue();
		typeof(WellKnownHeaderNames).IsSealed.ShouldBeTrue();
	}
}
