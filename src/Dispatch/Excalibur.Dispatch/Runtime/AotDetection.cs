// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Runtime;

/// <summary>
/// Provides detection capabilities for AOT (Ahead-of-Time) compilation.
/// </summary>
public static class AotDetection
{
	/// <summary>
	/// Gets a value indicating whether the application is running with AOT compilation.
	/// </summary>
	/// <value>The current <see cref="IsAotCompiled"/> value.</value>
	public static bool IsAotCompiled { get; }

#if AOT_ENABLED
		= true;
#endif

	/// <summary>
	/// Gets a value indicating whether dynamic code generation is supported.
	/// </summary>
	/// <remarks> This will be false in AOT-compiled applications and true in JIT environments. </remarks>
	/// <value>The current <see cref="IsDynamicCodeSupported"/> value.</value>
	public static bool IsDynamicCodeSupported { get; } = RuntimeFeature.IsDynamicCodeSupported;

	/// <summary>
	/// Gets a value indicating whether the runtime supports runtime code generation.
	/// </summary>
	/// <remarks> This includes features like Reflection.Emit, runtime type generation, etc. </remarks>
	/// <value>The current <see cref="IsRuntimeCodeGenerationSupported"/> value.</value>
	public static bool IsRuntimeCodeGenerationSupported => IsDynamicCodeSupported;

	/// <summary>
	/// Ensures that the code path is compatible with AOT compilation.
	/// </summary>
	/// <param name="message"> The message to include if running in AOT mode when not expected. </param>
	/// <exception cref="PlatformNotSupportedException"> Thrown when running in AOT mode but AOT is not expected. </exception>
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "This method is specifically for AOT detection")]
	public static void EnsureNotAot(string message = "This operation requires JIT compilation and is not supported in AOT mode.")
	{
		if (IsAotCompiled)
		{
			throw new PlatformNotSupportedException(message);
		}
	}

	/// <summary>
	/// Ensures that the code path is AOT-compatible.
	/// </summary>
	/// <param name="message"> The message to include if not running in AOT mode when expected. </param>
	/// <exception cref="InvalidOperationException"> Thrown when not running in AOT mode but AOT is expected. </exception>
	public static void EnsureAot(string message = "This operation requires AOT compilation.")
	{
		if (!IsAotCompiled)
		{
			throw new InvalidOperationException(message);
		}
	}

	/// <summary>
	/// Executes different code paths based on AOT compilation status.
	/// </summary>
	/// <typeparam name="T"> The return type. </typeparam>
	/// <param name="aotPath"> The function to execute in AOT mode. </param>
	/// <param name="jitPath"> The function to execute in JIT mode. </param>
	/// <returns> The result from the appropriate code path. </returns>
	public static T Execute<T>(Func<T> aotPath, Func<T> jitPath)
	{
		ArgumentNullException.ThrowIfNull(aotPath);
		ArgumentNullException.ThrowIfNull(jitPath);

		return IsAotCompiled ? aotPath() : jitPath();
	}

	/// <summary>
	/// Executes different code paths based on AOT compilation status.
	/// </summary>
	/// <param name="aotPath"> The action to execute in AOT mode. </param>
	/// <param name="jitPath"> The action to execute in JIT mode. </param>
	public static void Execute(Action aotPath, Action jitPath)
	{
		ArgumentNullException.ThrowIfNull(aotPath);
		ArgumentNullException.ThrowIfNull(jitPath);

		if (IsAotCompiled)
		{
			aotPath();
		}
		else
		{
			jitPath();
		}
	}

	/// <summary>
	/// Executes different async code paths based on AOT compilation status.
	/// </summary>
	/// <typeparam name="T"> The return type. </typeparam>
	/// <param name="aotPath"> The async function to execute in AOT mode. </param>
	/// <param name="jitPath"> The async function to execute in JIT mode. </param>
	/// <returns> The result from the appropriate code path. </returns>
	public static Task<T> ExecuteAsync<T>(Func<Task<T>> aotPath, Func<Task<T>> jitPath)
	{
		ArgumentNullException.ThrowIfNull(aotPath);
		ArgumentNullException.ThrowIfNull(jitPath);

		return IsAotCompiled ? aotPath() : jitPath();
	}

	/// <summary>
	/// Gets diagnostic information about the runtime environment.
	/// </summary>
	/// <returns> A string containing runtime diagnostic information. </returns>
	public static string GetDiagnosticInfo() =>
		$"""
		 AOT Detection Diagnostics:
		 - IsAotCompiled: {IsAotCompiled}
		 - IsDynamicCodeSupported: {IsDynamicCodeSupported}
		 - RuntimeFeature.IsDynamicCodeCompiled: {RuntimeFeature.IsDynamicCodeCompiled}
		 - RuntimeFeature.IsDynamicCodeSupported: {RuntimeFeature.IsDynamicCodeSupported}
		 - Compilation Mode: {(IsAotCompiled ? "Native AOT" : "JIT")}
		 - Platform: {Environment.OSVersion.Platform}
		 - Framework: {Environment.Version}
	""";
}
