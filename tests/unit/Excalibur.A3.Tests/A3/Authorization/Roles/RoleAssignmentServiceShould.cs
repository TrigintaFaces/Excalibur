// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Roles.Commands;

namespace Excalibur.Tests.A3.Authorization.Roles;

/// <summary>
/// Unit tests for <see cref="RoleAssignmentService"/>: role validation
/// (exists, active) and grant key construction for revocation.
/// </summary>
/// <remarks>
/// Grant construction tests are deferred to integration tests (pw70h) because
/// the Grant aggregate requires ApplicationContext.ApplicationName which needs
/// full DI hosting infrastructure.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class RoleAssignmentServiceShould : UnitTestBase
{
	private readonly IRoleStore _roleStore = A.Fake<IRoleStore>();
	private readonly RoleAssignmentService _sut;

	public RoleAssignmentServiceShould()
	{
		_sut = new RoleAssignmentService(_roleStore);
	}

	private static AddRoleAssignmentCommand CreateCommand(
		string roleName = "Admin",
		string userId = "user-1") =>
		new(userId, "John Doe", roleName, "tenant-1", null, "admin");

	#region CreateRoleGrantAsync -- Validation

	[Fact]
	public async Task ThrowWhenRoleNotFound()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Missing", A<CancellationToken>._))
			.Returns((RoleSummary?)null);

		var command = CreateCommand(roleName: "Missing");

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.CreateRoleGrantAsync(command, CancellationToken.None));
		ex.Message.ShouldContain("not found");
	}

	[Fact]
	public async Task ThrowWhenRoleNotActive()
	{
		var inactiveRole = new RoleSummary("Admin", "Admin", null, "tenant-1",
			["GroupA"], [], RoleState.Inactive, DateTimeOffset.UtcNow);
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(inactiveRole);

		var command = CreateCommand();

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.CreateRoleGrantAsync(command, CancellationToken.None));
		ex.Message.ShouldContain("not active");
	}

	[Fact]
	public async Task ThrowWhenRoleDeprecated()
	{
		var deprecatedRole = new RoleSummary("Admin", "Admin", null, "tenant-1",
			["GroupA"], [], RoleState.Deprecated, DateTimeOffset.UtcNow);
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns(deprecatedRole);

		var command = CreateCommand();

		var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.CreateRoleGrantAsync(command, CancellationToken.None));
		ex.Message.ShouldContain("not active");
	}

	[Fact]
	public async Task ThrowWhenCommandIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.CreateRoleGrantAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task LookUpRoleByName()
	{
		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.Returns((RoleSummary?)null);

		var command = CreateCommand(roleName: "Admin");

		// Will throw because role not found, but verifies the lookup was called
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.CreateRoleGrantAsync(command, CancellationToken.None));

		A.CallTo(() => _roleStore.GetRoleAsync("Admin", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region BuildRoleGrantKey

	[Fact]
	public void BuildCorrectGrantKey()
	{
		var command = new RemoveRoleAssignmentCommand("user-1", "Admin", "tenant-1", "admin");

		var key = RoleAssignmentService.BuildRoleGrantKey(command);

		key.UserId.ShouldBe("user-1");
		key.Scope.TenantId.ShouldBe("tenant-1");
		key.Scope.GrantType.ShouldBe("Role");
		key.Scope.Qualifier.ShouldBe("Admin");
	}

	[Fact]
	public void ProduceConsistentKeyFormat()
	{
		var command = new RemoveRoleAssignmentCommand("user-1", "Admin", "tenant-1", "admin");
		var key = RoleAssignmentService.BuildRoleGrantKey(command);

		key.ToString().ShouldBe("user-1:tenant-1:Role:Admin");
	}

	[Fact]
	public void ThrowOnNullCommand_ForBuildKey()
	{
		Should.Throw<ArgumentNullException>(() =>
			RoleAssignmentService.BuildRoleGrantKey(null!));
	}

	#endregion

	#region RoleGrantType Constant

	[Fact]
	public void ExposeRoleGrantTypeConstant()
	{
		RoleAssignmentService.RoleGrantType.ShouldBe("Role");
	}

	#endregion
}
