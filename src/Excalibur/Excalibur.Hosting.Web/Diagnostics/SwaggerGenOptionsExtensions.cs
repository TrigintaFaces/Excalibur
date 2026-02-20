// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

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
			Type = "object",
			Required = new HashSet<string>(StringComparer.Ordinal) { "title", "status", "traceId" },
			Properties = new Dictionary<string, OpenApiSchema>
(StringComparer.Ordinal)
			{
				["title"] = new() { Type = "string", Description = "A short, human-readable summary of the problem type." },
				["status"] = new() { Type = "integer", Format = "int32", Description = "The HTTP status code." },
				["detail"] =
					new() { Type = "string", Description = "A human-readable explanation specific to this occurrence of the problem." },
				["instance"] =
					new()
					{
						Type = "string",
						Format = "uri",
						Description = "A URI reference identifying this specific occurrence of the problem.",
					},
				["traceId"] = new() { Type = "string", Description = "The trace identifier for correlating logs and diagnostics." },
				["exceptionId"] =
					new() { Type = "string", Format = "uuid", Description = "A globally unique identifier for this exception instance." },
				["errorCode"] = new() { Type = "integer", Description = "Application-specific error code, if available." },
				["stack"] = new() { Type = "string", Description = "The exception stack trace (only shown in development)." },
				["validationErrors"] = new()
				{
					Type = "array",
					Description = "List of validation errors returned for a failed validation.",
					Items = new OpenApiSchema
					{
						Type = "object",
						Properties = new Dictionary<string, OpenApiSchema>
(StringComparer.Ordinal)
						{
							["propertyName"] = new() { Type = "string" },
							["errorMessage"] = new() { Type = "string" },
						},
					},
				},
			},
		});
}
