// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsMessageBodyCodecShould
{
	[Fact]
	public void ReturnFalseWhenMessageAttributesAreNull()
	{
		// Arrange
		var message = new Message { MessageAttributes = null! };

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenMessageAttributesAreEmpty()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>(),
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenCompressionAttributeMissing()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-body-encoding"] = new() { StringValue = "base64", DataType = "String" },
			},
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenEncodingAttributeMissing()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = "Gzip", DataType = "String" },
			},
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenEncodingIsNotBase64()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = "Gzip", DataType = "String" },
				["dispatch-body-encoding"] = new() { StringValue = "utf8", DataType = "String" },
			},
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenCompressionAlgorithmIsNone()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = "None", DataType = "String" },
				["dispatch-body-encoding"] = new() { StringValue = "base64", DataType = "String" },
			},
			Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenCompressionAlgorithmIsInvalid()
	{
		// Arrange
		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = "InvalidAlgo", DataType = "String" },
				["dispatch-body-encoding"] = new() { StringValue = "base64", DataType = "String" },
			},
			Body = Convert.ToBase64String(Encoding.UTF8.GetBytes("test")),
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeFalse();
		body.ShouldBeEmpty();
		algorithm.ShouldBeNull();
	}

	[Theory]
	[InlineData(CompressionAlgorithm.Gzip)]
	[InlineData(CompressionAlgorithm.Deflate)]
	[InlineData(CompressionAlgorithm.Brotli)]
	public void DecodeCompressedBase64Body(CompressionAlgorithm compressionAlgorithm)
	{
		// Arrange — compress test payload and base64-encode it
		var originalText = "Hello compressed world!";
		var originalBytes = Encoding.UTF8.GetBytes(originalText);
		var compressed = AwsSqsCompression.Compress(originalBytes, compressionAlgorithm);
		var base64Body = Convert.ToBase64String(compressed);

		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = compressionAlgorithm.ToString(), DataType = "String" },
				["dispatch-body-encoding"] = new() { StringValue = "base64", DataType = "String" },
			},
			Body = base64Body,
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeTrue();
		body.ShouldBe(originalBytes);
		algorithm.ShouldBe(compressionAlgorithm);
	}

	[Fact]
	public void HandleEmptyBodyWhenCompressed()
	{
		// Arrange
		var compressed = AwsSqsCompression.Compress([], CompressionAlgorithm.Gzip);
		var base64Body = Convert.ToBase64String(compressed);

		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = "Gzip", DataType = "String" },
				["dispatch-body-encoding"] = new() { StringValue = "base64", DataType = "String" },
			},
			Body = base64Body,
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out _);

		// Assert
		result.ShouldBeTrue();
		body.ShouldBeEmpty();
	}

	[Fact]
	public void ParseCompressionAlgorithmCaseInsensitively()
	{
		// Arrange
		var originalBytes = Encoding.UTF8.GetBytes("test");
		var compressed = AwsSqsCompression.Compress(originalBytes, CompressionAlgorithm.Gzip);

		var message = new Message
		{
			MessageAttributes = new Dictionary<string, MessageAttributeValue>
			{
				["dispatch-compression"] = new() { StringValue = "gzip", DataType = "String" }, // lowercase
				["dispatch-body-encoding"] = new() { StringValue = "BASE64", DataType = "String" }, // uppercase
			},
			Body = Convert.ToBase64String(compressed),
		};

		// Act
		var result = AwsSqsMessageBodyCodec.TryDecodeBody(message, out var body, out var algorithm);

		// Assert
		result.ShouldBeTrue();
		body.ShouldBe(originalBytes);
		algorithm.ShouldBe(CompressionAlgorithm.Gzip);
	}
}
