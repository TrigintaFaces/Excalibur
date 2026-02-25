// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Google
 /// <summary>
 /// Program entry point demonstrating the optimized function host.
 /// </summary>
 public class Program {
 /// <summary>
 /// Main entry point using the optimized host.
 /// </summary>
 /// <param name="args">Command line arguments.</param>
 /// <returns>Task representing the application execution.</returns>
 public static async Task Main(string[] args)
 {
 // Use the optimized host for minimal cold start
 await OptimizedFunctionHost.RunOptimizedAsync<OptimizedHttpFunction>(args)
 .ConfigureAwait(false);
 }
 }

 /// <summary>
 /// Alternative program showing CloudEvent function with optimization.
 /// </summary>
 public class CloudEventProgram {
 /// <summary>
 /// Main entry point for CloudEvent function.
 /// </summary>
 /// <param name="args">Command line arguments.</param>
 /// <returns>Task representing the application execution.</returns>
 public static async Task Main(string[] args)
 {
 await OptimizedFunctionHost.RunOptimizedAsync<OptimizedPubSubFunction>(args)
 .ConfigureAwait(false);
 }
 }
}
