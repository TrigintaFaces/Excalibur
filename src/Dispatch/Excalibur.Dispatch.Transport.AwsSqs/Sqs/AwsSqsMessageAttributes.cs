// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.Transport.Aws;

internal static class AwsSqsMessageAttributes
{
	public const string Compression = "dispatch-compression";
	public const string BodyEncoding = "dispatch-body-encoding";
	public const string BodyEncodingBase64 = "base64";
	public const string StringDataType = "String";
}
