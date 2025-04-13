using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public class ExcaliburHeaderNamesShould
{
	[Fact]
	public void SetCorrelationIdHeaderCorrectly()
	{
		// Arrange
		using var request = new HttpRequestMessage();

		// Act
		request.Headers.Add(ExcaliburHeaderNames.CorrelationId, "12345");

		// Assert
		request.Headers.Contains(ExcaliburHeaderNames.CorrelationId).ShouldBeTrue();
		request.Headers.GetValues(ExcaliburHeaderNames.CorrelationId).ShouldContain("12345");
	}

	[Fact]
	public void SetETagHeaderCorrectly()
	{
		// Arrange
		using var response = new HttpResponseMessage();

		// Act
		response.Headers.Add(ExcaliburHeaderNames.ETag, "\"etag-value\"");

		// Assert
		response.Headers.Contains(ExcaliburHeaderNames.ETag).ShouldBeTrue();
		response.Headers.GetValues(ExcaliburHeaderNames.ETag).ShouldContain("\"etag-value\"");
	}

	[Fact]
	public void SetTenantIdHeaderCorrectly()
	{
		// Arrange
		using var request = new HttpRequestMessage();

		// Act
		request.Headers.Add(ExcaliburHeaderNames.TenantId, "tenant-123");

		// Assert
		request.Headers.Contains(ExcaliburHeaderNames.TenantId).ShouldBeTrue();
		request.Headers.GetValues(ExcaliburHeaderNames.TenantId).ShouldContain("tenant-123");
	}

	[Fact]
	public void SetRaisedByHeaderCorrectly()
	{
		// Arrange
		using var request = new HttpRequestMessage();

		// Act
		request.Headers.Add(ExcaliburHeaderNames.RaisedBy, "user-123");

		// Assert
		request.Headers.Contains(ExcaliburHeaderNames.RaisedBy).ShouldBeTrue();
		request.Headers.GetValues(ExcaliburHeaderNames.RaisedBy).ShouldContain("user-123");
	}
}
