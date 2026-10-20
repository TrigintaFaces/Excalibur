// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Data;

using FakeItEasy;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Pattern", "Regression")]
public sealed class DbConnectionRequestCancellationShould
{
	[Theory]
	[InlineData("synchronous")]
	[InlineData("faulted-task")]
	[InlineData("task-canceled-exception")]
	[InlineData("canceled-task")]
	public async Task PreserveRequestCancellationAndItsToken(string mode)
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		using var connection = A.Fake<IDbConnection>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		OperationCanceledException original = mode == "task-canceled-exception"
			? new TaskCanceledException("Driver cancellation", null, cancellation.Token)
			: new OperationCanceledException("Request cancellation", cancellation.Token);
		_ = A.CallTo(() => request.ResolveAsync).Returns(new Func<IDbConnection, Task<string>>(_ => mode switch
		{
			"synchronous" or "task-canceled-exception" => throw original,
			"canceled-task" => Task.FromCanceled<string>(cancellation.Token),
			_ => Task.FromException<string>(original),
		}));

		var operation = connection.ResolveAsync(request);
		OperationCanceledException? observed = null;
		try
		{
			_ = await operation;
		}
		catch (OperationCanceledException exception)
		{
			observed = exception;
		}
		var cancellationException = observed.ShouldNotBeNull("await must propagate the resolver's cancellation");

		operation.IsCanceled.ShouldBeTrue("request cancellation must remain cancellation rather than a wrapped fault");
		cancellationException.CancellationToken.ShouldBe(cancellation.Token);
		if (mode == "canceled-task")
		{
			cancellationException.ShouldBeOfType<TaskCanceledException>();
		}
		else
		{
			cancellationException.ShouldBeSameAs(original);
		}
	}

	[Theory]
	[InlineData("timeout")]
	[InlineData("io-with-cancellation-cause")]
	public async Task KeepInfrastructureFailuresFaultedAndWrapped(string kind)
	{
		using var connection = A.Fake<IDbConnection>();
		var request = A.Fake<IDataRequest<IDbConnection, string>>();
		Exception original = kind == "timeout"
			? new TimeoutException("Provider timeout")
			: new IOException("Storage failure", new OperationCanceledException());
		_ = A.CallTo(() => request.ResolveAsync).Returns(new Func<IDbConnection, Task<string>>(_ => Task.FromException<string>(original)));

		var operation = connection.ResolveAsync(request);
		var observed = await Should.ThrowAsync<OperationFailedException>(() => operation);

		operation.IsFaulted.ShouldBeTrue();
		operation.IsCanceled.ShouldBeFalse();
		observed.InnerException.ShouldBeSameAs(original);
	}
}
