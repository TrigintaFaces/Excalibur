// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="DefaultClaimCheckNamingStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
[Trait("Feature", "ClaimCheck")]
public sealed class DefaultClaimCheckNamingStrategyShould
{
	[Fact]
	public void GenerateId_WithDefaultPrefix()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();

		// Act
		var id = strategy.GenerateId();

		// Assert
		id.ShouldStartWith("cc-");
		id.Length.ShouldBeGreaterThan(3);
	}

	[Fact]
	public void GenerateId_WithCustomPrefix()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy("claim-");

		// Act
		var id = strategy.GenerateId();

		// Assert
		id.ShouldStartWith("claim-");
	}

	[Fact]
	public void GenerateId_WithEmptyPrefix()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy("");

		// Act
		var id = strategy.GenerateId();

		// Assert
		id.ShouldNotBeNullOrEmpty();
		id.Length.ShouldBe(32); // GUID without hyphens
	}

	[Fact]
	public void GenerateUniqueIds()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();

		// Act
		var id1 = strategy.GenerateId();
		var id2 = strategy.GenerateId();
		var id3 = strategy.GenerateId();

		// Assert
		id1.ShouldNotBe(id2);
		id2.ShouldNotBe(id3);
		id1.ShouldNotBe(id3);
	}

	[Fact]
	public void GenerateId_WithMetadata()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();
		var metadata = new ClaimCheckMetadata { MessageType = "TestMessage" };

		// Act
		var id = strategy.GenerateId(metadata);

		// Assert
		id.ShouldStartWith("cc-");
	}

	[Fact]
	public void GenerateStoragePath_WithClaimCheckId()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();
		var claimCheckId = "cc-abc123";

		// Act
		var path = strategy.GenerateStoragePath(claimCheckId);

		// Assert
		path.ShouldContain(claimCheckId);
		path.ShouldContain("/");
	}

	[Fact]
	public void GenerateStoragePath_IncludesDateComponents()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();
		var claimCheckId = "cc-test";
		var now = DateTimeOffset.UtcNow;

		// Act
		var path = strategy.GenerateStoragePath(claimCheckId);

		// Assert
		path.ShouldContain(now.ToString("yyyy"));
		path.ShouldContain(now.ToString("MM"));
		path.ShouldContain(now.ToString("dd"));
	}

	[Fact]
	public void GenerateStoragePath_HasHierarchicalStructure()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();
		var claimCheckId = "cc-test123";

		// Act
		var path = strategy.GenerateStoragePath(claimCheckId);

		// Assert
		var parts = path.Split('/');
		parts.Length.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void GenerateStoragePath_WithMetadata()
	{
		// Arrange
		var strategy = new DefaultClaimCheckNamingStrategy();
		var claimCheckId = "cc-meta";
		var metadata = new ClaimCheckMetadata { MessageType = "TestMessage" };

		// Act
		var path = strategy.GenerateStoragePath(claimCheckId, metadata);

		// Assert
		path.ShouldContain(claimCheckId);
	}
}
