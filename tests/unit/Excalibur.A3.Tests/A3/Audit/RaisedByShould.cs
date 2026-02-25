// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.A3.Authentication;

using FakeItEasy;

namespace Excalibur.Tests.A3.Audit;

/// <summary>
/// Unit tests for <see cref="RaisedBy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Audit")]
public sealed class RaisedByShould : UnitTestBase
{
	[Fact]
	public void CreateWithDefaultValues()
	{
		// Arrange & Act
		var raisedBy = new RaisedBy();

		// Assert
		raisedBy.FullName.ShouldBeNull();
		raisedBy.Login.ShouldBeNull();
		raisedBy.FirstName.ShouldBeNull();
		raisedBy.LastName.ShouldBeNull();
		raisedBy.UserId.ShouldBeNull();
	}

	[Fact]
	public void CreateWithFullNameAndLogin()
	{
		// Arrange & Act
		var raisedBy = new RaisedBy("John Doe", "johndoe@example.com");

		// Assert
		raisedBy.FullName.ShouldBe("John Doe");
		raisedBy.Login.ShouldBe("johndoe@example.com");
	}

	[Fact]
	public void CreateFromAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		A.CallTo(() => authToken.FirstName).Returns("Jane");
		A.CallTo(() => authToken.LastName).Returns("Smith");
		A.CallTo(() => authToken.UserId).Returns("user-123");

		// Act
		var raisedBy = new RaisedBy(authToken);

		// Assert
		raisedBy.FirstName.ShouldBe("Jane");
		raisedBy.LastName.ShouldBe("Smith");
		raisedBy.UserId.ShouldBe("user-123");
	}

	[Fact]
	public void CreateFromNullAuthenticationToken()
	{
		// Arrange & Act
		var raisedBy = new RaisedBy(null);

		// Assert
		raisedBy.FirstName.ShouldBeNull();
		raisedBy.LastName.ShouldBeNull();
		raisedBy.UserId.ShouldBeNull();
	}

	[Fact]
	public void CreateFromAuthenticationTokenWithNullValues()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		A.CallTo(() => authToken.FirstName).Returns(null);
		A.CallTo(() => authToken.LastName).Returns(null);
		A.CallTo(() => authToken.UserId).Returns(null);

		// Act
		var raisedBy = new RaisedBy(authToken);

		// Assert
		raisedBy.FirstName.ShouldBeNull();
		raisedBy.LastName.ShouldBeNull();
		raisedBy.UserId.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var raisedBy1 = new RaisedBy("John Doe", "john@example.com");
		var raisedBy2 = new RaisedBy("John Doe", "john@example.com");

		// Assert
		raisedBy1.ShouldBe(raisedBy2);
		(raisedBy1 == raisedBy2).ShouldBeTrue();
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var raisedBy1 = new RaisedBy("John Doe", "john@example.com");
		var raisedBy2 = new RaisedBy("Jane Doe", "jane@example.com");

		// Assert
		raisedBy1.ShouldNotBe(raisedBy2);
		(raisedBy1 != raisedBy2).ShouldBeTrue();
	}

	[Fact]
	public void SupportWithExpression()
	{
		// Arrange
		var original = new RaisedBy("Original Name", "original@example.com");

		// Act
		var modified = original with { FullName = "Modified Name" };

		// Assert
		modified.FullName.ShouldBe("Modified Name");
		modified.Login.ShouldBe("original@example.com");
		original.FullName.ShouldBe("Original Name"); // Original unchanged
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var raisedBy = new RaisedBy
		{
			FullName = "Test User",
			Login = "testuser",
			FirstName = "Test",
			LastName = "User",
			UserId = "user-456",
		};

		// Assert
		raisedBy.FullName.ShouldBe("Test User");
		raisedBy.Login.ShouldBe("testuser");
		raisedBy.FirstName.ShouldBe("Test");
		raisedBy.LastName.ShouldBe("User");
		raisedBy.UserId.ShouldBe("user-456");
	}

	[Fact]
	public void HaveCorrectHashCode()
	{
		// Arrange
		var raisedBy1 = new RaisedBy("John Doe", "john@example.com");
		var raisedBy2 = new RaisedBy("John Doe", "john@example.com");

		// Assert
		raisedBy1.GetHashCode().ShouldBe(raisedBy2.GetHashCode());
	}

	[Fact]
	public void HaveDistinctHashCodeForDifferentValues()
	{
		// Arrange
		var raisedBy1 = new RaisedBy("John Doe", "john@example.com");
		var raisedBy2 = new RaisedBy("Jane Doe", "jane@example.com");

		// Assert - Different values should typically have different hash codes
		// (not guaranteed by contract, but usually true)
		raisedBy1.GetHashCode().ShouldNotBe(raisedBy2.GetHashCode());
	}

	[Fact]
	public void SupportToString()
	{
		// Arrange
		var raisedBy = new RaisedBy("John Doe", "john@example.com");

		// Act
		var str = raisedBy.ToString();

		// Assert
		str.ShouldContain("RaisedBy");
		str.ShouldContain("John Doe");
		str.ShouldContain("john@example.com");
	}

	[Fact]
	public void CreateWithEmptyStrings()
	{
		// Arrange & Act
		var raisedBy = new RaisedBy("", "");

		// Assert
		raisedBy.FullName.ShouldBe("");
		raisedBy.Login.ShouldBe("");
	}

	[Fact]
	public void PreserveFullNameAndLoginButNotOthersFromConstructor()
	{
		// Arrange & Act - Constructor with full name and login doesn't set other properties
		var raisedBy = new RaisedBy("Full Name", "login");

		// Assert
		raisedBy.FullName.ShouldBe("Full Name");
		raisedBy.Login.ShouldBe("login");
		raisedBy.FirstName.ShouldBeNull();
		raisedBy.LastName.ShouldBeNull();
		raisedBy.UserId.ShouldBeNull();
	}

	[Fact]
	public void PreserveAuthTokenPropertiesButNotOthersFromConstructor()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		A.CallTo(() => authToken.FirstName).Returns("First");
		A.CallTo(() => authToken.LastName).Returns("Last");
		A.CallTo(() => authToken.UserId).Returns("user-id");

		// Act
		var raisedBy = new RaisedBy(authToken);

		// Assert
		raisedBy.FirstName.ShouldBe("First");
		raisedBy.LastName.ShouldBe("Last");
		raisedBy.UserId.ShouldBe("user-id");
		raisedBy.FullName.ShouldBeNull();
		raisedBy.Login.ShouldBeNull();
	}
}
