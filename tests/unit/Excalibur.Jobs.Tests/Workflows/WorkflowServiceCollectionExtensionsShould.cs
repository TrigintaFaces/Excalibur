// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="WorkflowServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class WorkflowServiceCollectionExtensionsShould
{
	// --- AddWorkflows ---

	[Fact]
	public void AddWorkflowsThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddWorkflows());
	}

	[Fact]
	public void AddWorkflowsRegisterWorkflowContext()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddWorkflows();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IWorkflowContext) &&
			sd.ImplementationType == typeof(WorkflowContext) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddWorkflowsReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddWorkflows();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- AddWorkflow<TWorkflow, TInput, TOutput> ---

	[Fact]
	public void AddWorkflowThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddWorkflow<TestWorkflow, string, string>());
	}

	[Fact]
	public void AddWorkflowRegisterWithDefaultScopedLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddWorkflow<TestWorkflow, string, string>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestWorkflow) &&
			sd.Lifetime == ServiceLifetime.Scoped);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IWorkflow<string, string>) &&
			sd.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public void AddWorkflowRegisterWithCustomLifetime()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddWorkflow<TestWorkflow, string, string>(ServiceLifetime.Transient);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(TestWorkflow) &&
			sd.Lifetime == ServiceLifetime.Transient);
	}

	[Fact]
	public void AddWorkflowRegisterWorkflowJob()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddWorkflow<TestWorkflow, string, string>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(WorkflowJob<TestWorkflow, string, string>));
	}

	[Fact]
	public void AddWorkflowReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddWorkflow<TestWorkflow, string, string>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	// --- Test helpers ---

	private sealed class TestWorkflow : IWorkflow<string, string>
	{
		public Task<WorkflowResult<string>> ExecuteAsync(string input, IWorkflowContext context, CancellationToken cancellationToken)
			=> Task.FromResult(WorkflowResult<string>.Success(input));
	}
}
