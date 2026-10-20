// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

// Diagnostic reflection only. Neither mode is a benchmark measurement or NativeAOT proof.
var bundle=Path.GetFullPath(args[0]); var mode=args[1];
if(mode is not ("Broken" or "Fixed")) throw new ArgumentException("Expected Broken or Fixed mode.");
AssemblyLoadContext.Default.Resolving += (_,n)=> File.Exists(Path.Combine(bundle,n.Name+".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle,n.Name+".dll")) : null;
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bundle,"Excalibur.Dispatch.Benchmarks.dll"));
var type=assembly.GetType("Excalibur.Dispatch.Benchmarks.Serialization.EncryptionBenchmarks",true)!;
var operations=new[]{"EncryptSmall","DecryptSmall","EncryptMedium","DecryptMedium","EncryptLarge","DecryptLarge","EncryptVeryLarge","DecryptVeryLarge","RoundTripSmall","RoundTripMedium","EncryptWithNonceGeneration"};
var discovered=type.GetMethods().Where(m=>m.CustomAttributes.Any(a=>a.AttributeType.Name=="BenchmarkAttribute")).Select(m=>m.Name).Order().ToArray();
Require(discovered.SequenceEqual(operations.Order()),"Compiled operation census differs.");
var positive=0; var tamper=0; var calls=0; var expectedFailures=0; var cleanupChecks=0;
long baseline=GC.GetTotalMemory(true),maxLive=baseline;
var repeats=mode=="Broken"?1:8;
for(var cycle=0;cycle<repeats;cycle++) {
 var fixture=Activator.CreateInstance(type)!;
 type.GetMethod("GlobalSetup")!.Invoke(fixture,null);
 var aes=(AesGcm)Field(fixture,"_aesGcm"); var key=(byte[])Field(fixture,"_key"); var nonce=(byte[])Field(fixture,"_nonce");
 Require(key.Length==32 && nonce.Length==12,"Key/nonce shape differs.");
 try {
  if(mode=="Broken") {
   foreach(var method in new[]{"DecryptSmall","DecryptMedium","DecryptLarge"}) {
    try { type.GetMethod(method)!.Invoke(fixture,null); throw new InvalidOperationException("Expected old-fixture authentication failure missing."); }
    catch(TargetInvocationException e) when(e.InnerException is AuthenticationTagMismatchException) { expectedFailures++; }
   }
   type.GetMethod("DecryptVeryLarge")!.Invoke(fixture,null); positive++;
  } else {
   var tags=new List<byte[]>();
   foreach(var (name,size) in new[]{("small",1024),("medium",10240),("large",102400),("veryLarge",1048576)}) {
    var payload=(byte[])Field(fixture,"_"+name+"Payload"); var cipher=(byte[])Field(fixture,"_"+name+"Ciphertext"); var tag=(byte[])Field(fixture,"_"+name+"Tag");
    Require(payload.Length==size && cipher.Length==size && tag.Length==16 && !ReferenceEquals(payload,cipher),"Tuple shape differs.");
    Require(tags.All(t=>!ReferenceEquals(t,tag)),"Authentication tag buffer reused across payload sizes."); tags.Add(tag);
    var plain=new byte[size]; aes.Decrypt(nonce,cipher,tag,plain); Require(plain.SequenceEqual(payload),"Plaintext differs from fixture payload."); positive++;
    foreach(var changed in new[]{"tag","ciphertext","nonce"}) {
     var changedTag=tag.ToArray(); var changedCipher=cipher.ToArray(); var changedNonce=nonce.ToArray();
     if(changed=="tag") changedTag[0]^=1; else if(changed=="ciphertext") changedCipher[0]^=1; else changedNonce[0]^=1;
     var failedPlain=Enumerable.Repeat((byte)0xA5,size).ToArray();
     try { aes.Decrypt(changedNonce,changedCipher,changedTag,failedPlain); throw new InvalidOperationException("Tampered tuple authenticated."); }
     catch(AuthenticationTagMismatchException) { Require(failedPlain.All(b=>b==0),"Failed authentication did not clear output."); tamper++; }
    }
    // Tamper controls never mutate fixture operands.
    aes.Decrypt(nonce,cipher,tag,plain); Require(plain.SequenceEqual(payload),"Original tuple changed during negative controls.");
   }
   foreach(var operation in operations) { type.GetMethod(operation)!.Invoke(fixture,null); calls++; }
  }
 } finally {
  type.GetMethod("GlobalCleanup")!.Invoke(fixture,null);
  Require(key.All(b=>b==0),"Cleanup did not zero key bytes.");
  try { aes.Encrypt(new byte[12],new byte[1],new byte[1],new byte[16]); throw new InvalidOperationException("Cleanup left AES usable."); }
  catch(ObjectDisposedException) { cleanupChecks++; }
 }
 maxLive=Math.Max(maxLive,GC.GetTotalMemory(true)); Require(maxLive-baseline<64L*1024*1024,"Managed live growth exceeded budget.");
}
Require(mode=="Broken" ? expectedFailures==3 && positive==1 && calls==0 && tamper==0 : calls==88 && positive==32 && tamper==96,"Incomplete diagnostic population.");
Console.WriteLine(JsonSerializer.Serialize(new { passed=true,mode,repeats,actualBenchmarkCalls=calls,positiveTupleChecks=positive,tamperNegativeChecks=tamper,expectedBrokenFixtureAuthenticationFailures=expectedFailures,cleanupChecks,baselineLiveBytes=baseline,maxLiveBytes=maxLive,liveGrowthBytes=maxLive-baseline,measurementPass=false,aotVerification=false,frameworkIntegrationVerification=false }));
static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
