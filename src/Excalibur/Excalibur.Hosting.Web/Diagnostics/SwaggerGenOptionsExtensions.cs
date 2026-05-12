// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Extension methods for configuring Swagger generation options with problem details schema support.
/// </summary>
public static class SwaggerGenOptionsExtensions
{
	/// <summary>
	/// Adds a standardized OpenAPI schema definition for ProblemDetails responses to Swagger documentation.
	/// </summary>
	/// <param name="options"> The SwaggerGenOptions instance to configure. </param>
	public static void AddProblemDetailsSchema(this SwaggerGenOptions options) =>
		options.MapType<ProblemDetails>(static () => new OpenApiSchema
		{
			Type = JsonSchemaType.Object,
			Required = new HashSet<string>(StringComparer.Ordinal) { "title", "status", "traceId" },
			Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
			{
				["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A short, human-readable summary of the problem type." },
				["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32", Description = "The HTTP status code." },
				["detail"] =
					new OpenApiSchema { Type = JsonSchemaType.String, Description = "A human-readable explanation specific to this occurrence of the problem." },
				["instance"] =
					new OpenApiSchema
					{
						Type = JsonSchemaType.String,
						Format = "uri",
						Description = "A URI reference identifying this specific occurrence of the problem.",
					},
				["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "The trace identifier for correlating logs and diagnostics." },
				["exceptionId"] =
					new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid", Description = "A globally unique identifier for this exception instance." },
				["errorCode"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "Application-specific error code, if available." },
				["stack"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "The exception stack trace (only shown in development)." },
				["validationErrors"] = new OpenApiSchema
				{
					Type = JsonSchemaType.Array,
					Description = "List of validation errors returned for a failed validation.",
					Items = new OpenApiSchema
					{
						Type = JsonSchemaType.Object,
						Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
						{
							["propertyName"] = new OpenApiSchema { Type = JsonSchemaType.String },
							["errorMessage"] = new OpenApiSchema { Type = JsonSchemaType.String },
						},
					},
				},
			},
		});
}
