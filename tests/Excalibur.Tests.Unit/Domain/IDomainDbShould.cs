using System.Data;

using Excalibur.DataAccess;
using Excalibur.Domain;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Domain;

public class IDomainDbShould
{
	[Fact]
	public void InheritFromIDb()
	{
		// Arrange
		var domainDb = A.Fake<IDomainDb>();

		// Act & Assert
		_ = domainDb.ShouldBeAssignableTo<IDb>();
	}

	[Fact]
	public void ExposeConnectionProperty()
	{
		// Arrange
		var domainDb = A.Fake<IDomainDb>();
		var connection = A.Fake<IDbConnection>();
		_ = A.CallTo(() => domainDb.Connection).Returns(connection);

		// Act
		var result = domainDb.Connection;

		// Assert
		result.ShouldBe(connection);
	}

	[Fact]
	public void SupportOpenMethod()
	{
		// Arrange
		var domainDb = A.Fake<IDomainDb>();

		// Act
		domainDb.Open();

		// Assert
		_ = A.CallTo(() => domainDb.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SupportCloseMethod()
	{
		// Arrange
		var domainDb = A.Fake<IDomainDb>();

		// Act
		domainDb.Close();

		// Assert
		_ = A.CallTo(() => domainDb.Close()).MustHaveHappenedOnceExactly();
	}

	// Note: Removed tests for IDisposable since the IDomainDb interface doesn't implement IDisposable
	// Implementation classes might implement IDisposable, but that's not a requirement of the interface
}
