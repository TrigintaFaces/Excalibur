// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="DefaultClaimCheckNamingStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultClaimCheckNamingStrategyDepthShould
{
	[Fact]
	public void GenerateId_UsesDefaultPrefix()
	{
		var strategy = new DefaultClaimCheckNamingStrategy();
		var id = strategy.GenerateId();
		id.ShouldStartWith("cc-");
		id.Length.ShouldBeGreaterThan(3);
	}

	[Fact]
	public void GenerateId_UsesCustomPrefix()
	{
		var strategy = new DefaultClaimCheckNamingStrategy("custom-");
		var id = strategy.GenerateId();
		id.ShouldStartWith("custom-");
	}

	[Fact]
	public void GenerateId_ProducesUniqueIds()
	{
		var strategy = new DefaultClaimCheckNamingStrategy();
		var id1 = strategy.GenerateId();
		var id2 = strategy.GenerateId();
		id1.ShouldNotBe(id2);
	}

	[Fact]
	public void GenerateId_WithMetadata_StillGeneratesId()
	{
		var strategy = new DefaultClaimCheckNamingStrategy();
		var metadata = new ClaimCheckMetadata { MessageType = "Test" };
		var id = strategy.GenerateId(metadata);
		id.ShouldStartWith("cc-");
	}

	[Fact]
	public void GenerateStoragePath_IncludesDateAndId()
	{
		var strategy = new DefaultClaimCheckNamingStrategy();
		var path = strategy.GenerateStoragePath("test-id");
		var today = DateTimeOffset.UtcNow;
		path.ShouldContain(today.ToString("yyyy/MM/dd"));
		path.ShouldContain("test-id");
	}

	[Fact]
	public void GenerateStoragePath_WithMetadata_IncludesId()
	{
		var strategy = new DefaultClaimCheckNamingStrategy();
		var metadata = new ClaimCheckMetadata { ContentType = "application/json" };
		var path = strategy.GenerateStoragePath("cc-abc123", metadata);
		path.ShouldContain("cc-abc123");
	}
}
