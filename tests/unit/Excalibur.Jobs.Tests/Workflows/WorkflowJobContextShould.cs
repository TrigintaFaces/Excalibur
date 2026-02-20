// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="WorkflowJobContext{TInput}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Workflows")]
public sealed class WorkflowJobContextShould
{
	[Fact]
	public void CreateWithInstanceIdAndInput()
	{
		// Arrange
		var instanceId = "instance-123";
		var input = "test-input";

		// Act
		var context = new WorkflowJobContext<string>(instanceId, input, null);

		// Assert
		context.InstanceId.ShouldBe(instanceId);
		context.Input.ShouldBe(input);
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CreateWithAllParameters()
	{
		// Arrange
		var instanceId = "instance-456";
		var input = new TestInput { Value = 42 };
		var correlationId = "correlation-789";

		// Act
		var context = new WorkflowJobContext<TestInput>(instanceId, input, correlationId);

		// Assert
		context.InstanceId.ShouldBe(instanceId);
		context.Input.ShouldBe(input);
		context.Input.Value.ShouldBe(42);
		context.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void ThrowOnNullInstanceId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new WorkflowJobContext<string>(null!, "input", null));
	}

	[Fact]
	public void AllowNullInput()
	{
		// Act
		var context = new WorkflowJobContext<string?>("instance", null, null);

		// Assert
		context.InstanceId.ShouldBe("instance");
		context.Input.ShouldBeNull();
	}

	[Fact]
	public void CreateWithIntInput()
	{
		// Act
		var context = new WorkflowJobContext<int>("instance", 100, null);

		// Assert
		context.Input.ShouldBe(100);
	}

	[Fact]
	public void CreateWithComplexInput()
	{
		// Arrange
		var input = new TestInput { Value = 999, Name = "Complex" };

		// Act
		var context = new WorkflowJobContext<TestInput>("instance", input, null);

		// Assert
		context.Input.ShouldNotBeNull();
		context.Input.Value.ShouldBe(999);
		context.Input.Name.ShouldBe("Complex");
	}

	[Fact]
	public void PreserveEmptyCorrelationId()
	{
		// Act
		var context = new WorkflowJobContext<string>("instance", "input", "");

		// Assert
		context.CorrelationId.ShouldBe("");
	}

	[Fact]
	public void CreateWithDefaultValueTypeInput()
	{
		// Act
		var context = new WorkflowJobContext<int>("instance", default, null);

		// Assert
		context.Input.ShouldBe(0);
	}

	private sealed class TestInput
	{
		public int Value { get; init; }
		public string Name { get; init; } = string.Empty;
	}
}
