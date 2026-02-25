// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsMessageAttributesShould
{
	[Fact]
	public void DefineCompressionConstant()
	{
		AwsSqsMessageAttributes.Compression.ShouldBe("dispatch-compression");
	}

	[Fact]
	public void DefineBodyEncodingConstant()
	{
		AwsSqsMessageAttributes.BodyEncoding.ShouldBe("dispatch-body-encoding");
	}

	[Fact]
	public void DefineBodyEncodingBase64Constant()
	{
		AwsSqsMessageAttributes.BodyEncodingBase64.ShouldBe("base64");
	}

	[Fact]
	public void DefineStringDataTypeConstant()
	{
		AwsSqsMessageAttributes.StringDataType.ShouldBe("String");
	}
}
