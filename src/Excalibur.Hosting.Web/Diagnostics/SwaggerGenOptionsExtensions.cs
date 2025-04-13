using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace Excalibur.Hosting.Web.Diagnostics;

public static class SwaggerGenOptionsExtensions
{
	public static void AddProblemDetailsSchema(this SwaggerGenOptions options)
	{
		options.MapType<ProblemDetails>(() => new OpenApiSchema
		{
			Type = "object",
			Required = new HashSet<string> { "title", "status", "traceId" },
			Properties = new Dictionary<string, OpenApiSchema>
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
						Description = "A URI reference identifying this specific occurrence of the problem."
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
						{
							["propertyName"] = new() { Type = "string" },
							["errorMessage"] = new() { Type = "string" }
						}
					}
				}
			}
		});
	}
}
