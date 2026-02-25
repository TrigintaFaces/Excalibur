// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Hosting.AzureFunctions;

/// <summary>
/// Builder for configuring parallel step groups in saga orchestrations.
/// </summary>
/// <typeparam name="TSagaInput"> The saga input type. </typeparam>
/// <typeparam name="TSagaOutput"> The saga output type. </typeparam>
public sealed class SagaParallelStepGroupBuilder<TSagaInput, TSagaOutput>
{
	private readonly List<ISagaStepDefinition> _steps = [];
	private readonly string _groupName;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaParallelStepGroupBuilder{TSagaInput, TSagaOutput}" /> class.
	/// </summary>
	/// <param name="groupName"> The group name. </param>
	internal SagaParallelStepGroupBuilder(string groupName) => _groupName = groupName;

	/// <summary>
	/// Adds a step to the parallel group.
	/// </summary>
	/// <typeparam name="TStepInput"> The step input type. </typeparam>
	/// <typeparam name="TStepOutput"> The step output type. </typeparam>
	/// <param name="stepName"> The step name. </param>
	/// <param name="configure"> The step configuration function. </param>
	/// <returns> The parallel step group builder for chaining. </returns>
	public SagaParallelStepGroupBuilder<TSagaInput, TSagaOutput> AddStep<TStepInput, TStepOutput>(
		string stepName,
		Action<SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var stepBuilder = new SagaStepBuilder<TSagaInput, TStepInput, TStepOutput>(stepName);
		configure(stepBuilder);

		var stepDefinition = stepBuilder.Build();
		_steps.Add(stepDefinition);

		return this;
	}

	/// <summary>
	/// Builds the parallel step group definition.
	/// </summary>
	/// <returns> The parallel step group definition. </returns>
	internal SagaParallelStepGroupDefinition<TSagaInput> Build() => new(_groupName, _steps);
}
