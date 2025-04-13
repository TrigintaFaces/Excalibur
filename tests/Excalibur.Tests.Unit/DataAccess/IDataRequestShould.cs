using System.Data;

using Dapper;

using Excalibur.DataAccess;
using Excalibur.Tests.Shared;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class IDataRequestShould
{
	[Fact]
	public void ProvideAccessToCommandDefinition()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		var command = new CommandDefinition("SELECT 1");
		_ = A.CallTo(() => request.Command).Returns(command);

		// Act
		var result = request.Command;

		// Assert
		result.ShouldBe(command);
	}

	[Fact]
	public void ProvideAccessToParameters()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		var parameters = new DynamicParameters();
		parameters.Add("@Param1", "Value1");
		_ = A.CallTo(() => request.Parameters).Returns(parameters);

		// Act
		var result = request.Parameters;

		// Assert
		result.ShouldBe(parameters);
		result.ParameterNames.ShouldContain("Param1");
	}

	[Fact]
	public void AllowSettingParameters()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		var parameters = new DynamicParameters();
		parameters.Add("@Param1", "Value1");

		// Act
		request.Parameters = parameters; // Directly set property on fake

		// Assert
		_ = A.CallToSet(() => request.Parameters).To(parameters).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProvideResolveAsyncFunction()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		var connection = A.Fake<IDbConnection>();
		var expectedResult = "Test Result";
		_ = A.CallTo(() => request.ResolveAsync(A<IDbConnection>._)).Returns(Task.FromResult(expectedResult));

		// Act
		var resolveFunc = request.ResolveAsync;
		var result = await resolveFunc(connection).ConfigureAwait(true);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public void SupportGenericTypeParameters()
	{
		// Arrange & Act
		var stringRequest = A.Fake<IDataRequest<IDbConnection, string>>();
		var intRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		var boolRequest = A.Fake<IDataRequest<IDbConnection, bool>>();

		// Assert
		_ = stringRequest.ShouldBeAssignableTo<IDataRequest<IDbConnection, string>>();
		_ = intRequest.ShouldBeAssignableTo<IDataRequest<IDbConnection, int>>();
		_ = boolRequest.ShouldBeAssignableTo<IDataRequest<IDbConnection, bool>>();
	}

	[Fact]
	public void SupportDifferentConnectionTypes()
	{
		// Arrange & Act
		var dbConnectionRequest = A.Fake<IDataRequest<IDbConnection, string>>();
		var customConnectionRequest = A.Fake<IDataRequest<GetUserByIdRequest, string>>();

		// Assert
		_ = dbConnectionRequest.ShouldBeAssignableTo<IDataRequest<IDbConnection, string>>();
		_ = customConnectionRequest.ShouldBeAssignableTo<IDataRequest<GetUserByIdRequest, string>>();
	}
}
