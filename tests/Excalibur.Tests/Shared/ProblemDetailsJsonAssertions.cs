using System.Text.Json;

using Shouldly;

namespace Excalibur.Tests.Shared;

public static class ProblemDetailsJsonAssertions
{
	public static async Task<JsonElement> GetProblemDetailsAsync(this HttpResponseMessage response)
	{
		ArgumentNullException.ThrowIfNull(response, nameof(response));

		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
		return JsonSerializer.Deserialize<JsonElement>(content);
	}

	public static JsonElement ShouldContainProperty(this JsonElement element, string propertyName)
	{
		element.TryGetProperty(propertyName, out var property).ShouldBeTrue($"Expected property '{propertyName}' to exist.");
		return property;
	}

	public static void ShouldHaveErrorCode(this JsonElement element, string expectedCode)
	{
		var errorCode = element.ShouldContainProperty("errorCode").ToString();
		errorCode.ShouldBe(expectedCode);
	}

	public static void ShouldHaveValidationErrors(this JsonElement element)
	{
		element.TryGetProperty("validationErrors", out _).ShouldBeTrue("Expected 'validationErrors' property to exist.");
	}

	public static void ShouldHaveStackTrace(this JsonElement element)
	{
		element.TryGetProperty("stack", out _).ShouldBeTrue("Expected 'stack' property to exist.");
	}

	public static void ShouldHaveTraceId(this JsonElement element)
	{
		element.TryGetProperty("traceId", out var traceId).ShouldBeTrue();
		traceId.GetString().ShouldNotBeNullOrWhiteSpace();
	}

	public static void InstanceShouldStartWith(this JsonElement element, string expectedPrefix)
	{
		var instance = element.ShouldContainProperty("instance").GetString();
		instance.ShouldStartWith(expectedPrefix);
	}
}
