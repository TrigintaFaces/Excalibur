// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Running;

namespace Excalibur.Dispatch.Benchmarks;

public class Program
{
	public static void Main(string[] args)
	{
		if (args.Length > 0 && string.Equals(args[0], "asynclocal-density-probe", StringComparison.Ordinal))
		{
			Diagnostics.AsyncLocalDensityProbe.RunAsync().GetAwaiter().GetResult();
			return;
		}

		_ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
	}
}
