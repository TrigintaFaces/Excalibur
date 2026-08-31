// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class CloudEventContentTypeShould
{
	[Theory]
	[InlineData("application/json")]
	[InlineData("APPLICATION/JSON")]
	[InlineData("Application/Json; charset=utf-8")]
	[InlineData("application/cloudevents+json")]
	[InlineData("APPLICATION/CLOUDEVENTS+JSON")]
	[InlineData("Application/CloudEvents+JSON")]
	[InlineData("application/cloudevents+json; charset=utf-8")]
	[InlineData("  application/cloudevents+json  ")]
	public void TreatEveryJsonMediaTypeSpellingAsJson(string contentType) =>
		CloudEventContentType.IsJson(contentType).ShouldBeTrue();

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("text/plain")]
	[InlineData("TEXT/PLAIN; charset=utf-8")]
	[InlineData("application/octet-stream")]
	[InlineData("application/x-base64")]
	[InlineData("application/xml")]
	[InlineData("application/jsonish")]
	public void NotTreatNonJsonMediaTypesAsJson(string? contentType) =>
		CloudEventContentType.IsJson(contentType).ShouldBeFalse();

	[Theory]
	[InlineData("application/cloudevents+json")]
	[InlineData("APPLICATION/CLOUDEVENTS+JSON; charset=utf-8")]
	[InlineData("application/cloudevents-batch+json")]
	public void RecogniseStructuredCloudEventMediaTypes(string contentType) =>
		CloudEventContentType.IsStructured(contentType).ShouldBeTrue();

	[Theory]
	[InlineData(null)]
	[InlineData("application/json")]
	[InlineData("text/plain")]
	[InlineData("application/octet-stream")]
	public void NotRecogniseNonCloudEventMediaTypesAsStructured(string? contentType) =>
		CloudEventContentType.IsStructured(contentType).ShouldBeFalse();

	[Theory]
	[InlineData("application/octet-stream", "application/octet-stream")]
	[InlineData("APPLICATION/OCTET-STREAM", "application/octet-stream")]
	[InlineData("application/octet-stream; charset=utf-8", "application/octet-stream")]
	[InlineData("application/x-base64", "application/x-base64")]
	public void CompareMediaTypesIgnoringCaseAndParameters(string contentType, string expected) =>
		CloudEventContentType.Is(contentType, expected).ShouldBeTrue();

	[Theory]
	[InlineData("application/json", "application/octet-stream")]
	[InlineData(null, "application/octet-stream")]
	[InlineData("text/plain", "application/x-base64")]
	public void NotMatchADifferentMediaType(string? contentType, string expected) =>
		CloudEventContentType.Is(contentType, expected).ShouldBeFalse();

	[Theory]
	[InlineData("application/cloudevents+json; charset=utf-8", "application/cloudevents+json")]
	[InlineData("  text/plain ; charset=utf-8 ", "text/plain")]
	[InlineData("application/json", "application/json")]
	public void StripParametersFromTheMediaType(string contentType, string expected) =>
		CloudEventContentType.MediaType(contentType).ShouldBe(expected);

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ReportNoMediaTypeWhenTheHeaderIsAbsent(string? contentType) =>
		CloudEventContentType.MediaType(contentType).ShouldBeNull();
}
