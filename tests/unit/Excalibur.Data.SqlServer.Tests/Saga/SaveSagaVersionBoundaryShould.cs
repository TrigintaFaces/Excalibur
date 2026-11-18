// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer.Requests;

namespace Excalibur.Data.Tests.Saga;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "STORE")]
public sealed class SaveSagaVersionBoundaryShould
{
	[Fact]
	public void Refuse_exhausted_version_instead_of_preparing_a_wrapped_token()
	{
		using var serializer = new DispatchJsonSerializer();
		var state = new VersionBoundaryState { Version = long.MaxValue };
		_ = Should.Throw<OverflowException>(() => new SaveSagaRequest<VersionBoundaryState>(
			state, serializer, "[dispatch].[sagas]", TenantScope.Scoped("tenantA"), CancellationToken.None));
		state.Version.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void Refuse_negative_version_instead_of_preparing_an_invalid_expected_token()
	{
		using var serializer = new DispatchJsonSerializer();
		var state = new VersionBoundaryState { Version = -1 };
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new SaveSagaRequest<VersionBoundaryState>(
			state, serializer, "[dispatch].[sagas]", TenantScope.Scoped("tenantA"), CancellationToken.None));
		state.Version.ShouldBe(-1);
	}

	[Fact]
	public void Prepare_zero_to_one_without_advancing_the_caller_before_a_successful_save()
	{
		using var serializer = new DispatchJsonSerializer();
		var state = new VersionBoundaryState { Version = 0 };
		var request = new SaveSagaRequest<VersionBoundaryState>(
			state, serializer, "[dispatch].[sagas]", TenantScope.Scoped("tenantA"), CancellationToken.None);
		var parameters = request.Command.Parameters.ShouldBeOfType<DynamicParameters>();
		parameters.Get<long>("ExpectedVersion").ShouldBe(0);
		parameters.Get<long>("NewVersion").ShouldBe(1);
		parameters.Get<string>("TenantId").ShouldBe("tenantA");
		state.Version.ShouldBe(0, "constructing a request is not a successful persistence operation");
	}

	[Fact]
	public void Prepare_the_last_representable_increment_without_advancing_the_caller()
	{
		using var serializer = new DispatchJsonSerializer();
		var state = new VersionBoundaryState { Version = long.MaxValue - 1 };
		var request = new SaveSagaRequest<VersionBoundaryState>(
			state, serializer, "[dispatch].[sagas]", TenantScope.Scoped("tenantA"), CancellationToken.None);
		var parameters = request.Command.Parameters.ShouldBeOfType<DynamicParameters>();
		parameters.Get<long>("ExpectedVersion").ShouldBe(long.MaxValue - 1);
		parameters.Get<long>("NewVersion").ShouldBe(long.MaxValue);
		state.Version.ShouldBe(long.MaxValue - 1, "request preparation must not claim persistence succeeded");
	}
	private sealed class VersionBoundaryState : SagaState
	{
	}
}
