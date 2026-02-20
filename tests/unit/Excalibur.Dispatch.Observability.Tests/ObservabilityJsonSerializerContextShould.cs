// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Unit tests for <see cref="ObservabilityJsonSerializerContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Serialization")]
public sealed class ObservabilityJsonSerializerContextShould
{
	[Fact]
	public void InheritFromJsonSerializerContext()
	{
		var context = ObservabilityJsonSerializerContext.Default;
		context.ShouldBeAssignableTo<JsonSerializerContext>();
	}

	[Fact]
	public void ProvideDefaultInstance()
	{
		ObservabilityJsonSerializerContext.Default.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextSnapshotTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextSnapshot))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextMetricsSummaryTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextMetricsSummary))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextObservabilityOptionsTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextObservabilityOptions))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextTracingOptionsTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextTracingOptions))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextLimitsOptionsTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextLimitsOptions))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextFieldOptionsTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextFieldOptions))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextExportOptionsTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextExportOptions))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveContextAnomalyTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ContextAnomaly))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveObservabilityOptionsTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(ObservabilityOptions))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveAnomalyTypeEnumTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(AnomalyType))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDiagnosticSeverityEnumTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(DiagnosticSeverity))
			.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDictionaryStringObjectTypeInfo()
	{
		ObservabilityJsonSerializerContext.Default.GetTypeInfo(typeof(Dictionary<string, object>))
			.ShouldNotBeNull();
	}

	[Fact]
	public void UseCamelCasePropertyNaming()
	{
		// Verify the serializer context uses camelCase naming
		var options = ObservabilityJsonSerializerContext.Default.Options;
		options.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
	}

	[Fact]
	public void IgnoreNullValuesWhenWriting()
	{
		var options = ObservabilityJsonSerializerContext.Default.Options;
		options.DefaultIgnoreCondition.ShouldBe(JsonIgnoreCondition.WhenWritingNull);
	}

	[Fact]
	public void NotWriteIndented()
	{
		var options = ObservabilityJsonSerializerContext.Default.Options;
		options.WriteIndented.ShouldBeFalse();
	}
}
