// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Excalibur.Saga.Models;

/// <summary>
/// Represents the persistent state of a saga.
/// </summary>
public class SagaState
{
	/// <summary>
	/// Gets or sets the saga identifier.
	/// </summary>
	/// <value>the saga identifier.</value>
	public string SagaId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga name.
	/// </summary>
	/// <value>the saga name.</value>
	public string SagaName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga version.
	/// </summary>
	/// <value>the saga version.</value>
	public string Version { get; set; } = "1.0";

	/// <summary>
	/// Gets or sets the correlation ID.
	/// </summary>
	/// <value>the correlation ID.</value>
	public string CorrelationId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current status.
	/// </summary>
	/// <value>the current status.</value>
	public SagaStatus Status { get; set; }

	/// <summary>
	/// Gets or sets the current step index.
	/// </summary>
	/// <value>the current step index.</value>
	public int CurrentStepIndex { get; set; }

	/// <summary>
	/// Gets or sets the serialized saga data.
	/// </summary>
	/// <value>the serialized saga data.</value>
	public string DataJson { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the data type name.
	/// </summary>
	/// <value>the data type name.</value>
	public string DataType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the saga started.
	/// </summary>
	/// <value>when the saga started.</value>
	public DateTime StartedAt { get; set; }

	/// <summary>
	/// Gets or sets when the saga completed.
	/// </summary>
	/// <value>when the saga completed.</value>
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the last update time.
	/// </summary>
	/// <value>the last update time.</value>
	public DateTime LastUpdatedAt { get; set; }

	/// <summary>
	/// Gets or sets the error message.
	/// </summary>
	/// <value>the error message.</value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets the step execution history.
	/// </summary>
	/// <value>the step execution history.</value>
	public IList<StepExecutionRecord> StepHistory { get; } = [];

	/// <summary>
	/// Gets custom metadata.
	/// </summary>
	/// <value>custom metadata.</value>
	public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Deserializes the saga data.
	/// </summary>
	/// <typeparam name="TData"> The data type. </typeparam>
	/// <returns> The deserialized data. </returns>
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public TData? GetData<TData>()
		where TData : class
		=>
			string.IsNullOrEmpty(DataJson)
				? null
				: JsonSerializer.Deserialize<TData>(DataJson);

	/// <summary>
	/// Serializes and sets the saga data.
	/// </summary>
	/// <typeparam name="TData"> The data type. </typeparam>
	/// <param name="data"> The data to serialize. </param>
	[RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("JSON serialization may require dynamic code generation which is not compatible with AOT compilation.")]
	public void SetData<TData>(TData data)
		where TData : class
	{
		DataJson = JsonSerializer.Serialize(data);
		DataType = typeof(TData).AssemblyQualifiedName ?? typeof(TData).FullName ?? string.Empty;
	}
}

