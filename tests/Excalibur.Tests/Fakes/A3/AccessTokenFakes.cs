using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.Tests.Shared;

using FakeItEasy;

namespace Excalibur.Tests.Fakes.A3;

public static class AccessTokenFakes
{
	public static IAccessToken AccessToken => AccessTokenFake.Value;

	private static Lazy<IAccessToken> AccessTokenFake { get; } = new(() =>
	{
		var fake = A.Fake<IAccessToken>();

		_ = A.CallTo(() => fake.AuthenticationState).Returns(AuthenticationState.Authenticated);
		_ = A.CallTo(() => fake.IsAuthorized(A<string>._, A<string>._)).Returns(true);
		_ = A.CallTo(() => fake.Login).Returns(WellKnownId.LocalUser);
		_ = A.CallTo(() => fake.TenantId).Returns(WellKnownId.TestTenant);
		_ = A.CallTo(() => fake.UserId).Returns(WellKnownId.LocalUser);
		_ = A.CallTo(() => fake.FullName).Returns("Fake User");

		return fake;
	});
}
