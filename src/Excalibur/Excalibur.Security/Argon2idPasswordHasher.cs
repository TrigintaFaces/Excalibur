// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Copyright (c) TrigintaFaces. All rights reserved.

using System.Security.Cryptography;

using Excalibur.Security.Abstractions;

using Konscious.Security.Cryptography;

using Microsoft.Extensions.Options;

namespace Excalibur.Security;

/// <summary>
/// Password hasher implementation using Argon2id algorithm.
/// </summary>
/// <remarks>
/// Argon2id is the recommended choice for password hashing as it provides
/// resistance against both side-channel attacks (like Argon2d) and GPU-based
/// attacks (like Argon2i). This implementation follows OWASP Password Storage
/// Cheat Sheet recommendations.
/// </remarks>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
	private const string AlgorithmName = "argon2id";
	private const string ParameterMemorySize = "memorySize";
	private const string ParameterIterations = "iterations";
	private const string ParameterParallelism = "parallelism";
	private const string ParameterHashLength = "hashLength";

	private readonly Argon2Options _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="Argon2idPasswordHasher"/> class.
	/// </summary>
	/// <param name="options">The Argon2 configuration options.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
	public Argon2idPasswordHasher(IOptions<Argon2Options> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value ?? throw new ArgumentException(Resources.Argon2OptionsValueNull, nameof(options));
		ValidateOptions(_options);
	}

	/// <inheritdoc/>
	public Task<PasswordHashResult> HashPasswordAsync(
		string password,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(password);
		if (string.IsNullOrWhiteSpace(password))
		{
			throw new ArgumentException(Resources.Argon2PasswordEmptyOrWhitespace, nameof(password));
		}

		cancellationToken.ThrowIfCancellationRequested();

		// Generate cryptographically random salt
		var salt = RandomNumberGenerator.GetBytes(_options.SaltLength);

		// Compute hash
		var hash = ComputeHash(password, salt, _options);

		var result = new PasswordHashResult
		{
			Hash = Convert.ToBase64String(hash),
			Salt = Convert.ToBase64String(salt),
			Algorithm = AlgorithmName,
			Version = _options.Version,
			Parameters = new Dictionary<string, object>
			{
				[ParameterMemorySize] = _options.MemorySize,
				[ParameterIterations] = _options.Iterations,
				[ParameterParallelism] = _options.Parallelism,
				[ParameterHashLength] = _options.HashLength,
			},
		};

		return Task.FromResult(result);
	}

	/// <inheritdoc/>
	public Task<PasswordVerificationResult> VerifyPasswordAsync(
		string password,
		PasswordHashResult storedHash,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(password);
		ArgumentNullException.ThrowIfNull(storedHash);

		cancellationToken.ThrowIfCancellationRequested();

		// Empty or whitespace passwords always fail verification
		// This is a security best practice - empty passwords should never be valid
		if (string.IsNullOrWhiteSpace(password))
		{
			return Task.FromResult(PasswordVerificationResult.Failed);
		}

		// Verify algorithm matches
		if (!string.Equals(storedHash.Algorithm, AlgorithmName, StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult(PasswordVerificationResult.Failed);
		}

		// Extract parameters from stored hash
		var storedOptions = ExtractOptions(storedHash);

		// Decode salt and expected hash
		var salt = Convert.FromBase64String(storedHash.Salt);
		var expectedHash = Convert.FromBase64String(storedHash.Hash);

		// Compute hash with stored parameters
		var computedHash = ComputeHash(password, salt, storedOptions);

		// Use constant-time comparison to prevent timing attacks
		if (!CryptographicOperations.FixedTimeEquals(expectedHash, computedHash))
		{
			return Task.FromResult(PasswordVerificationResult.Failed);
		}

		// Check if rehashing is needed (parameters have changed)
		if (NeedsRehash(storedHash))
		{
			return Task.FromResult(PasswordVerificationResult.SuccessRehashNeeded);
		}

		return Task.FromResult(PasswordVerificationResult.Success);
	}

	private static byte[] ComputeHash(string password, byte[] salt, Argon2Options options)
	{
		using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
		{
			Salt = salt,
			DegreeOfParallelism = options.Parallelism,
			Iterations = options.Iterations,
			MemorySize = options.MemorySize,
		};

		return argon2.GetBytes(options.HashLength);
	}

	private static Argon2Options ExtractOptions(PasswordHashResult storedHash)
	{
		return new Argon2Options
		{
			MemorySize = GetParameterValue(storedHash.Parameters, ParameterMemorySize, 65536),
			Iterations = GetParameterValue(storedHash.Parameters, ParameterIterations, 4),
			Parallelism = GetParameterValue(storedHash.Parameters, ParameterParallelism, 4),
			HashLength = GetParameterValue(storedHash.Parameters, ParameterHashLength, 32),
			Version = storedHash.Version,
		};
	}

	private static T GetParameterValue<T>(IReadOnlyDictionary<string, object> parameters, string key, T defaultValue)
	{
		if (parameters.TryGetValue(key, out var value))
		{
			return value switch
			{
				T typed => typed,
				IConvertible convertible => (T)Convert.ChangeType(convertible, typeof(T),
					System.Globalization.CultureInfo.InvariantCulture),
				_ => defaultValue,
			};
		}

		return defaultValue;
	}

	private static void ValidateOptions(Argon2Options options)
	{
		if (options.MemorySize < 8192)
		{
			throw new ArgumentException(Resources.Argon2MemorySizeMinimum, nameof(options));
		}

		if (options.Iterations < 1)
		{
			throw new ArgumentException(Resources.Argon2IterationsMinimum, nameof(options));
		}

		if (options.Parallelism < 1)
		{
			throw new ArgumentException(Resources.Argon2ParallelismMinimum, nameof(options));
		}

		if (options.HashLength < 16)
		{
			throw new ArgumentException(Resources.Argon2HashLengthMinimum, nameof(options));
		}

		if (options.SaltLength < 16)
		{
			throw new ArgumentException(Resources.Argon2SaltLengthMinimum, nameof(options));
		}
	}

	private bool NeedsRehash(PasswordHashResult storedHash)
	{
		// Check version first
		if (storedHash.Version != _options.Version)
		{
			return true;
		}

		// Check if any parameters differ from current options
		var storedOptions = ExtractOptions(storedHash);

		return storedOptions.MemorySize != _options.MemorySize ||
			   storedOptions.Iterations != _options.Iterations ||
			   storedOptions.Parallelism != _options.Parallelism ||
			   storedOptions.HashLength != _options.HashLength;
	}
}
