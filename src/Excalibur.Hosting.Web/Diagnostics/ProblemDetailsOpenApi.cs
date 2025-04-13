namespace Excalibur.Hosting.Web.Diagnostics;

public static class ProblemDetailsOpenApi
{
	public static string GetYamlPath() =>
		Path.Combine(AppContext.BaseDirectory, "openapi/problem-details.openapi.yaml");
}
