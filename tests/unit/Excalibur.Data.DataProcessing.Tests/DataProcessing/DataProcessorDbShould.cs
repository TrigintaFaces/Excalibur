// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessorDb"/> and <see cref="DataToProcessDb"/> adapters.
/// </summary>
[UnitTest]
public sealed class DataProcessorDbShould : UnitTestBase
{
	[Fact]
	public void DelegateConnection_ToUnderlyingDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var fakeConn = A.Fake<IDbConnection>();
		A.CallTo(() => fakeDb.Connection).Returns(fakeConn);

		var processorDb = new DataProcessorDb(fakeDb);

		// Act
		var connection = processorDb.Connection;

		// Assert
		connection.ShouldBeSameAs(fakeConn);
	}

	[Fact]
	public void DelegateOpen_ToUnderlyingDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var processorDb = new DataProcessorDb(fakeDb);

		// Act
		processorDb.Open();

		// Assert
		A.CallTo(() => fakeDb.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DelegateClose_ToUnderlyingDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var processorDb = new DataProcessorDb(fakeDb);

		// Act
		processorDb.Close();

		// Assert
		A.CallTo(() => fakeDb.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Throw_WhenDbIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DataProcessorDb(null!));
	}

	[Fact]
	public void ImplementIDataProcessorDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var processorDb = new DataProcessorDb(fakeDb);

		// Assert
		processorDb.ShouldBeAssignableTo<IDataProcessorDb>();
	}
}

/// <summary>
/// Unit tests for <see cref="DataToProcessDb"/> adapter.
/// </summary>
[UnitTest]
public sealed class DataToProcessDbShould : UnitTestBase
{
	[Fact]
	public void DelegateConnection_ToUnderlyingDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var fakeConn = A.Fake<IDbConnection>();
		A.CallTo(() => fakeDb.Connection).Returns(fakeConn);

		var toProcessDb = new DataToProcessDb(fakeDb);

		// Act
		var connection = toProcessDb.Connection;

		// Assert
		connection.ShouldBeSameAs(fakeConn);
	}

	[Fact]
	public void DelegateOpen_ToUnderlyingDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var toProcessDb = new DataToProcessDb(fakeDb);

		// Act
		toProcessDb.Open();

		// Assert
		A.CallTo(() => fakeDb.Open()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void DelegateClose_ToUnderlyingDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var toProcessDb = new DataToProcessDb(fakeDb);

		// Act
		toProcessDb.Close();

		// Assert
		A.CallTo(() => fakeDb.Close()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Throw_WhenDbIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DataToProcessDb(null!));
	}

	[Fact]
	public void ImplementIDataToProcessDb()
	{
		// Arrange
		var fakeDb = A.Fake<IDb>();
		var toProcessDb = new DataToProcessDb(fakeDb);

		// Assert
		toProcessDb.ShouldBeAssignableTo<IDataToProcessDb>();
	}
}
