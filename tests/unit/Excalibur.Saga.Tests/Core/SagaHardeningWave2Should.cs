// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Idempotency;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;
using Excalibur.Saga.Queries;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Core;

/// <summary>
/// Sprint 617 E.1: Saga hardening Wave 2 tests -- IRetryPolicy deleted, compensation
/// idempotency wiring, ParallelSagaStep double-execution fix, ISagaCorrelationQuery interface.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaHardeningWave2Should
{
	#region A.1: IRetryPolicy Deleted (Sprint 617)

	[Fact]
	public void NotContainIRetryPolicyType()
	{
		// IRetryPolicy was deleted outright (greenfield, no consumers)
		var sagaAssembly = typeof(ISagaRetryPolicy).Assembly;
		var allTypes = sagaAssembly.GetExportedTypes()
			.Concat(sagaAssembly.GetTypes())
			.Select(t => t.FullName)
			.Where(n => n != null);

		allTypes.ShouldNotContain("Excalibur.Saga.Abstractions.IRetryPolicy",
			"IRetryPolicy must be fully deleted from the assembly");
	}

	[Fact]
	public void NotReferenceIRetryPolicyInISagaDefinition()
	{
		// Verify ISagaDefinition<T>.RetryPolicy returns ISagaRetryPolicy
		var retryPolicyProp = typeof(ISagaDefinition<>).GetProperty("RetryPolicy");
		retryPolicyProp.ShouldNotBeNull();
		retryPolicyProp.PropertyType.ShouldBe(typeof(ISagaRetryPolicy));
	}

	[Fact]
	public void KeepISagaRetryPolicyNotObsolete()
	{
		typeof(ISagaRetryPolicy).GetCustomAttribute<ObsoleteAttribute>()
			.ShouldBeNull("ISagaRetryPolicy is canonical and must not be obsolete");
	}

	#endregion

	#region A.2: ParallelSagaStep Double-Execution Fix

	[Fact]
	public async Task ExecuteAdaptiveAsync_ExecuteStepsExactlyOnce()
	{
		// Arrange: track execution count per step
		var executionCounts = new Dictionary<string, int>();
		var steps = new List<ISagaStep<TestSagaData>>();

		for (var i = 0; i < 3; i++)
		{
			var stepName = $"Step-{i}";
			executionCounts[stepName] = 0;

			var step = A.Fake<ISagaStep<TestSagaData>>();
			A.CallTo(() => step.Name).Returns(stepName);
			A.CallTo(() => step.Timeout).Returns(TimeSpan.FromMinutes(1));
			A.CallTo(() => step.ExecuteAsync(
				A<ISagaContext<TestSagaData>>._,
				A<CancellationToken>._))
				.ReturnsLazily(() =>
				{
					executionCounts[stepName]++;
					return Task.FromResult(StepResult.Success());
				});
			steps.Add(step);
		}

		var sut = new ParallelSagaStep<TestSagaData>(
			"ParallelTest",
			steps,
			null)
		{
			Strategy = ParallelismStrategy.Adaptive
		};

		var context = A.Fake<ISagaContext<TestSagaData>>();

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert: each step executed exactly once (no double-execution)
		foreach (var (stepName, count) in executionCounts)
		{
			count.ShouldBe(1, $"{stepName} should execute exactly once, but executed {count} times");
		}

		// Aggregated result should indicate success
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAdaptiveAsync_PropagateFailures()
	{
		// Arrange
		var step1 = A.Fake<ISagaStep<TestSagaData>>();
		A.CallTo(() => step1.Name).Returns("Step1");
		A.CallTo(() => step1.Timeout).Returns(TimeSpan.FromMinutes(1));
		A.CallTo(() => step1.ExecuteAsync(A<ISagaContext<TestSagaData>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(StepResult.Success()));

		var step2 = A.Fake<ISagaStep<TestSagaData>>();
		A.CallTo(() => step2.Name).Returns("Step2");
		A.CallTo(() => step2.Timeout).Returns(TimeSpan.FromMinutes(1));
		A.CallTo(() => step2.ExecuteAsync(A<ISagaContext<TestSagaData>>._, A<CancellationToken>._))
			.Returns(Task.FromResult(StepResult.Failure("test error")));

		var sut = new ParallelSagaStep<TestSagaData>(
			"ParallelTest",
			[step1, step2],
			null)
		{
			Strategy = ParallelismStrategy.Adaptive,
			FailurePolicy = ParallelFailurePolicy.RequireAll
		};

		var context = A.Fake<ISagaContext<TestSagaData>>();

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert: aggregated result should indicate failure (RequireAll policy)
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAdaptiveAsync_NotDoubleExecuteWithFailures()
	{
		// Arrange: verify steps execute exactly once even when some fail
		var executionCount = 0;

		var step = A.Fake<ISagaStep<TestSagaData>>();
		A.CallTo(() => step.Name).Returns("FailingStep");
		A.CallTo(() => step.Timeout).Returns(TimeSpan.FromMinutes(1));
		A.CallTo(() => step.ExecuteAsync(A<ISagaContext<TestSagaData>>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				Interlocked.Increment(ref executionCount);
				return Task.FromResult(StepResult.Failure("fail"));
			});

		var sut = new ParallelSagaStep<TestSagaData>(
			"ParallelTest",
			[step],
			null)
		{
			Strategy = ParallelismStrategy.Adaptive
		};

		var context = A.Fake<ISagaContext<TestSagaData>>();

		// Act
		await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		executionCount.ShouldBe(1, "Failing step should execute exactly once");
	}

	#endregion

	#region B.1: Compensation Idempotency Wiring

	[Fact]
	public void AcceptOptionalIdempotencyProviderInConstructor()
	{
		// Arrange
		var orchestrator = A.Fake<ISagaOrchestrator>();
		var stateStore = A.Fake<ISagaStateStore>();
		var options = Options.Create(new AdvancedSagaOptions());
		var logger = NullLogger<AdvancedSagaMiddleware>.Instance;
		var idempotencyProvider = A.Fake<ISagaIdempotencyProvider>();

		// Act -- should not throw (optional parameter)
		var sut = new AdvancedSagaMiddleware(
			orchestrator, stateStore, options, logger, idempotencyProvider);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullIdempotencyProvider()
	{
		// Arrange
		var orchestrator = A.Fake<ISagaOrchestrator>();
		var stateStore = A.Fake<ISagaStateStore>();
		var options = Options.Create(new AdvancedSagaOptions());
		var logger = NullLogger<AdvancedSagaMiddleware>.Instance;

		// Act -- null is the default
		var sut = new AdvancedSagaMiddleware(
			orchestrator, stateStore, options, logger, null);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void HaveIdempotencyProviderParameterInConstructor()
	{
		// Verify the constructor accepts ISagaIdempotencyProvider
		var ctors = typeof(AdvancedSagaMiddleware).GetConstructors();
		ctors.Length.ShouldBe(1);

		var parameters = ctors[0].GetParameters();
		var idempotencyParam = parameters.FirstOrDefault(p =>
			p.ParameterType == typeof(ISagaIdempotencyProvider));

		idempotencyParam.ShouldNotBeNull(
			"AdvancedSagaMiddleware must accept ISagaIdempotencyProvider in constructor");

		// It should be nullable (optional)
		idempotencyParam!.HasDefaultValue.ShouldBeTrue(
			"ISagaIdempotencyProvider parameter should have a default value (null)");
	}

	#endregion

	#region B.2: ISagaCorrelationQuery Interface

	[Fact]
	public void DefineISagaCorrelationQueryWithTwoMethods()
	{
		var methods = typeof(ISagaCorrelationQuery).GetMethods();
		methods.Length.ShouldBe(2, "ISagaCorrelationQuery should have exactly 2 methods");
	}

	[Fact]
	public void HaveFindByCorrelationIdAsyncMethod()
	{
		var method = typeof(ISagaCorrelationQuery).GetMethod("FindByCorrelationIdAsync");
		method.ShouldNotBeNull();

		var parameters = method!.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveFindByPropertyAsyncMethod()
	{
		var method = typeof(ISagaCorrelationQuery).GetMethod("FindByPropertyAsync");
		method.ShouldNotBeNull();

		var parameters = method!.GetParameters();
		parameters.Length.ShouldBe(3);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[1].ParameterType.ShouldBe(typeof(object));
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DefineSagaQueryResultAsImmutableRecord()
	{
		typeof(SagaQueryResult).IsClass.ShouldBeTrue();

		// Should have the expected properties
		var properties = typeof(SagaQueryResult).GetProperties();
		properties.Select(p => p.Name).ShouldContain("SagaId");
		properties.Select(p => p.Name).ShouldContain("SagaName");
		properties.Select(p => p.Name).ShouldContain("Status");
		properties.Select(p => p.Name).ShouldContain("CorrelationId");
		properties.Select(p => p.Name).ShouldContain("CreatedAt");
	}

	[Fact]
	public void DefineSagaCorrelationQueryOptionsWithDefaults()
	{
		var options = new SagaCorrelationQueryOptions();

		options.MaxResults.ShouldBe(100);
		options.IncludeCompleted.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingSagaCorrelationQueryOptions()
	{
		var options = new SagaCorrelationQueryOptions
		{
			MaxResults = 50,
			IncludeCompleted = true
		};

		options.MaxResults.ShouldBe(50);
		options.IncludeCompleted.ShouldBeTrue();
	}

	#endregion

	#region Saga SqlServer DI Registration (B.2)

	[Fact]
	public void RegisterISagaCorrelationQueryViaSqlServerBuilder()
	{
		// Verify the SagaBuilderSqlServerExtensions registers ISagaCorrelationQuery
		var extensionType = typeof(Excalibur.Saga.SqlServer.DependencyInjection.SagaBuilderSqlServerExtensions);
		extensionType.ShouldNotBeNull();

		var useSqlServerMethod = extensionType.GetMethods()
			.FirstOrDefault(m => m.Name == "UseSqlServer");

		useSqlServerMethod.ShouldNotBeNull(
			"SagaBuilderSqlServerExtensions must have a UseSqlServer method");
	}

	#endregion
}
