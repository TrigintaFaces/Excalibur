// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.Workflows;

public sealed class WorkflowJobShould
{
	private readonly IWorkflow<string, int> _fakeWorkflow;
	private readonly WorkflowJob<IWorkflow<string, int>, string, int> _sut;

	public WorkflowJobShould()
	{
		_fakeWorkflow = A.Fake<IWorkflow<string, int>>();
		_sut = new WorkflowJob<IWorkflow<string, int>, string, int>(
			_fakeWorkflow,
			NullLogger<WorkflowJob<IWorkflow<string, int>, string, int>>.Instance);
	}

	[Fact]
	public void ThrowOnNullWorkflow()
	{
		Should.Throw<ArgumentNullException>(() =>
			new WorkflowJob<IWorkflow<string, int>, string, int>(
				null!,
				NullLogger<WorkflowJob<IWorkflow<string, int>, string, int>>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new WorkflowJob<IWorkflow<string, int>, string, int>(
				_fakeWorkflow,
				null!));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteWorkflowWithInputAndContext()
	{
		var successResult = WorkflowResultFactory.Success(42);
		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			"test-input",
			A<IWorkflowContext>._,
			A<CancellationToken>._))
			.Returns(successResult);

		var context = new WorkflowJobContext<string>("instance-001", "test-input", "corr-001");

		await _sut.ExecuteAsync(context, CancellationToken.None);

		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			"test-input",
			A<IWorkflowContext>.That.Matches(c => c.InstanceId == "instance-001"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassCorrelationIdToWorkflowContext()
	{
		var successResult = WorkflowResultFactory.Success(42);
		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			A<string>._,
			A<IWorkflowContext>._,
			A<CancellationToken>._))
			.Returns(successResult);

		var context = new WorkflowJobContext<string>("instance-002", "input", "my-correlation");

		await _sut.ExecuteAsync(context, CancellationToken.None);

		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			A<string>._,
			A<IWorkflowContext>.That.Matches(c => c.CorrelationId == "my-correlation"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RethrowExceptionFromWorkflow()
	{
		var exception = new InvalidOperationException("Workflow failed");
		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			A<string>._,
			A<IWorkflowContext>._,
			A<CancellationToken>._))
			.ThrowsAsync(exception);

		var context = new WorkflowJobContext<string>("instance-003", "input", null);

		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ExecuteAsync(context, CancellationToken.None));

		thrown.ShouldBeSameAs(exception);
	}

	[Fact]
	public async Task CompleteSuccessfullyWhenWorkflowReturnsFailureResult()
	{
		var failureResult = WorkflowResultFactory.Failure<int>(new InvalidOperationException("Business rule failed"));
		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			A<string>._,
			A<IWorkflowContext>._,
			A<CancellationToken>._))
			.Returns(failureResult);

		var context = new WorkflowJobContext<string>("instance-004", "input", null);

		// Should not throw — failure result is logged, not thrown
		await Should.NotThrowAsync(() =>
			_sut.ExecuteAsync(context, CancellationToken.None));
	}

	[Fact]
	public async Task PropagateCancellationToken()
	{
		using var cts = new CancellationTokenSource();
		var token = cts.Token;
		CancellationToken capturedToken = default;

		A.CallTo(() => _fakeWorkflow.ExecuteAsync(
			A<string>._,
			A<IWorkflowContext>._,
			A<CancellationToken>._))
			.Invokes((string _, IWorkflowContext _, CancellationToken ct) => capturedToken = ct)
			.Returns(WorkflowResultFactory.Success(1));

		var context = new WorkflowJobContext<string>("instance-005", "input", null);
		await _sut.ExecuteAsync(context, token);

		capturedToken.ShouldBe(token);
	}
}
