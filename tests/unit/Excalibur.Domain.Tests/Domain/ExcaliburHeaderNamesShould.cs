// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="ExcaliburHeaderNames"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ExcaliburHeaderNamesShould
{
	[Fact]
	public void CorrelationId_HasCorrectValue()
	{
		// Assert
		ExcaliburHeaderNames.CorrelationId.ShouldBe("X-Correlation-Id");
	}

	[Fact]
	public void ETag_HasCorrectValue()
	{
		// Assert
		ExcaliburHeaderNames.ETag.ShouldBe("X-Etag");
	}

	[Fact]
	public void TenantId_HasCorrectValue()
	{
		// Assert
		ExcaliburHeaderNames.TenantId.ShouldBe("X-Tenant-Id");
	}

	[Fact]
	public void RaisedBy_HasCorrectValue()
	{
		// Assert
		ExcaliburHeaderNames.RaisedBy.ShouldBe("X-Raised-By");
	}

	[Fact]
	public void AllHeaders_StartWithXPrefix()
	{
		// Assert
		ExcaliburHeaderNames.CorrelationId.ShouldStartWith("X-");
		ExcaliburHeaderNames.ETag.ShouldStartWith("X-");
		ExcaliburHeaderNames.TenantId.ShouldStartWith("X-");
		ExcaliburHeaderNames.RaisedBy.ShouldStartWith("X-");
	}

	[Fact]
	public void AllHeaders_AreConstantStrings()
	{
		// These are compile-time constants
		const string correlationId = ExcaliburHeaderNames.CorrelationId;
		const string etag = ExcaliburHeaderNames.ETag;
		const string tenantId = ExcaliburHeaderNames.TenantId;
		const string raisedBy = ExcaliburHeaderNames.RaisedBy;

		// Assert that they're non-null and non-empty
		correlationId.ShouldNotBeNullOrEmpty();
		etag.ShouldNotBeNullOrEmpty();
		tenantId.ShouldNotBeNullOrEmpty();
		raisedBy.ShouldNotBeNullOrEmpty();
	}
}
