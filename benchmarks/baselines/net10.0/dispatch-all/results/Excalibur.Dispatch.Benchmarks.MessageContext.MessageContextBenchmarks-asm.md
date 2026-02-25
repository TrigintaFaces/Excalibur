## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_CorrelationId()
       mov       rax,[rcx+8]
       mov       rax,[rax+60]
       ret
; Total bytes of code 9
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_UserId()
       mov       rax,[rcx+8]
       mov       rax,[rax+58]
       ret
; Total bytes of code 9
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_TenantId()
       mov       rax,[rcx+8]
       mov       rax,[rax+90]
       ret
; Total bytes of code 12
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_MessageId()
       push      rsi
       push      rbx
       sub       rsp,28
       mov       rbx,[rcx+8]
       cmp       byte ptr [rbx+108],0
       je        short M00_L01
       mov       rax,[rbx+18]
M00_L00:
       add       rsp,28
       pop       rbx
       pop       rsi
       ret
M00_L01:
       mov       rax,[rbx+18]
       test      rax,rax
       jne       short M00_L00
       lea       rcx,[rbx+110]
       mov       rdx,245002089A0
       xor       r8d,r8d
       call      qword ptr [7FF9B3E86A60]
       mov       rsi,rax
       lea       rcx,[rbx+18]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rax,rsi
       jmp       short M00_L00
; Total bytes of code 85
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_Source()
       mov       rax,[rcx+8]
       mov       rax,[rax+0A8]
       ret
; Total bytes of code 12
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_MessageType()
       mov       rax,[rcx+8]
       mov       rax,[rax+0B0]
       ret
; Total bytes of code 12
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_CorrelationId()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L05
       lea       r8,[rbp-28]
       mov       rdx,249802066C8
       call      qword ptr [7FF9B3D4C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       je        near ptr M00_L04
       mov       rax,[rbp-28]
M00_L01:
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DA5560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,2499F800068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DA5638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       ecx,0D37
       mov       rdx,7FF9B3C78428
       call      qword ptr [7FF9B3A0F210]
       mov       rdx,rax
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      qword ptr [7FF9B3E86AA8]
       int       3
M00_L05:
       mov       r11,7FF9B39505D8
       mov       rdx,249802066C8
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L06
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DA5638]; System.Threading.Lock.Exit(ThreadId)
M00_L06:
       nop
       add       rsp,28
       ret
; Total bytes of code 324
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L16
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L06
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        near ptr M01_L05
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M01_L25
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L24
       test      r14,r14
       je        near ptr M01_L14
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L14
M01_L02:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L11
       mov       rdx,[rbp+8]
       cmp       rdx,rbx
       jne       short M01_L07
       mov       eax,1
M01_L03:
       test      eax,eax
       je        near ptr M01_L11
M01_L04:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L05:
       mov       rdx,7FF9B3EA0D48
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L06:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       near ptr M01_L01
M01_L07:
       test      rdx,rdx
       je        short M01_L10
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L10
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L08
       mov       rdx,rax
       call      qword ptr [7FF9B3A0C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L09
M01_L08:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L09:
       jmp       near ptr M01_L03
M01_L10:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L11:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L02
       jmp       near ptr M01_L24
M01_L12:
       test      eax,eax
       jne       near ptr M01_L04
M01_L13:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       je        near ptr M01_L24
M01_L14:
       cmp       r15d,[rbp+20]
       jne       short M01_L13
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L17
M01_L15:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       short M01_L18
       cmp       rdx,rbx
       jne       short M01_L19
       jmp       near ptr M01_L23
M01_L16:
       mov       ecx,1
       mov       rdx,7FF9B3D49948
       call      qword ptr [7FF9B3A0F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E86B20]
       int       3
M01_L17:
       mov       rdx,7FF9B3EA0C38
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L15
M01_L18:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       near ptr M01_L12
M01_L19:
       test      rdx,rdx
       je        short M01_L22
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L22
       add       rdx,0C
       lea       rax,[rbx+0C]
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M01_L20
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B3A0C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L21
M01_L20:
       mov       rcx,rdx
       mov       r11,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[r11]
       xor       rdx,r8
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L21:
       jmp       near ptr M01_L12
M01_L22:
       xor       eax,eax
       jmp       near ptr M01_L12
M01_L23:
       mov       eax,1
       jmp       near ptr M01_L12
M01_L24:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L25:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 649
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_UserId()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L05
       lea       r8,[rbp-28]
       mov       rdx,1F5002D66F8
       call      qword ptr [7FF9B3D3C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       je        near ptr M00_L04
       mov       rax,[rbp-28]
M00_L01:
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3D95560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1F52A000068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3D95638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       ecx,0D93
       mov       rdx,7FF9B3C68428
       call      qword ptr [7FF9B39FF210]
       mov       rdx,rax
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      qword ptr [7FF9B3E76AA8]
       int       3
M00_L05:
       mov       r11,7FF9B39405D8
       mov       rdx,1F5002D66F8
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L06
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3D95638]; System.Threading.Lock.Exit(ThreadId)
M00_L06:
       nop
       add       rsp,28
       ret
; Total bytes of code 324
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L14
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L06
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        near ptr M01_L05
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M01_L25
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L24
       test      r14,r14
       je        near ptr M01_L11
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L11
M01_L02:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L15
       mov       rdx,[rbp+8]
       cmp       rdx,rbx
       jne       short M01_L07
       mov       eax,1
M01_L03:
       test      eax,eax
       je        near ptr M01_L15
M01_L04:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L05:
       mov       rdx,7FF9B3E90D48
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L06:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       near ptr M01_L01
M01_L07:
       test      rdx,rdx
       je        short M01_L10
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L10
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L08
       mov       rdx,rax
       call      qword ptr [7FF9B39FC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L09
M01_L08:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L09:
       jmp       near ptr M01_L03
M01_L10:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L11:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L23
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L16
M01_L12:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       short M01_L17
       cmp       rdx,rbx
       jne       short M01_L18
       jmp       near ptr M01_L22
M01_L13:
       test      eax,eax
       je        near ptr M01_L23
       jmp       near ptr M01_L04
M01_L14:
       mov       ecx,1
       mov       rdx,7FF9B3D39948
       call      qword ptr [7FF9B39FF210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E76B20]
       int       3
M01_L15:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L02
       jmp       near ptr M01_L24
M01_L16:
       mov       rdx,7FF9B3E90C38
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L12
M01_L17:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       short M01_L13
M01_L18:
       test      rdx,rdx
       je        short M01_L21
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L21
       add       rdx,0C
       lea       rax,[rbx+0C]
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M01_L19
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B39FC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L20
M01_L19:
       mov       rcx,rdx
       mov       r11,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[r11]
       xor       rdx,r8
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L20:
       jmp       near ptr M01_L13
M01_L21:
       xor       eax,eax
       jmp       near ptr M01_L13
M01_L22:
       mov       eax,1
       jmp       near ptr M01_L13
M01_L23:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L11
M01_L24:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L25:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 655
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_TenantId()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L05
       lea       r8,[rbp-28]
       mov       rdx,166002D6720
       call      qword ptr [7FF9B3D5C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       je        near ptr M00_L04
       mov       rax,[rbp-28]
M00_L01:
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DB5560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1666AC00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       ecx,0DB7
       mov       rdx,7FF9B3C88428
       call      qword ptr [7FF9B3A1F210]
       mov       rdx,rax
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      qword ptr [7FF9B3E96AA8]
       int       3
M00_L05:
       mov       r11,7FF9B39605D8
       mov       rdx,166002D6720
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L06
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
M00_L06:
       nop
       add       rsp,28
       ret
; Total bytes of code 324
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L14
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L06
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        near ptr M01_L05
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M01_L25
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L24
       test      r14,r14
       je        near ptr M01_L11
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L11
M01_L02:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L15
       mov       rdx,[rbp+8]
       cmp       rdx,rbx
       jne       short M01_L07
       mov       eax,1
M01_L03:
       test      eax,eax
       je        near ptr M01_L15
M01_L04:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L05:
       mov       rdx,7FF9B3EB0D48
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L06:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       near ptr M01_L01
M01_L07:
       test      rdx,rdx
       je        short M01_L10
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L10
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L08
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L09
M01_L08:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L09:
       jmp       near ptr M01_L03
M01_L10:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L11:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L23
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L16
M01_L12:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       short M01_L17
       cmp       rdx,rbx
       jne       short M01_L18
       jmp       near ptr M01_L22
M01_L13:
       test      eax,eax
       je        near ptr M01_L23
       jmp       near ptr M01_L04
M01_L14:
       mov       ecx,1
       mov       rdx,7FF9B3D59948
       call      qword ptr [7FF9B3A1F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E96B20]
       int       3
M01_L15:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L02
       jmp       near ptr M01_L24
M01_L16:
       mov       rdx,7FF9B3EB0C38
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L12
M01_L17:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       short M01_L13
M01_L18:
       test      rdx,rdx
       je        short M01_L21
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L21
       add       rdx,0C
       lea       rax,[rbx+0C]
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M01_L19
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L20
M01_L19:
       mov       rcx,rdx
       mov       r11,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[r11]
       xor       rdx,r8
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L20:
       jmp       near ptr M01_L13
M01_L21:
       xor       eax,eax
       jmp       near ptr M01_L13
M01_L22:
       mov       eax,1
       jmp       near ptr M01_L13
M01_L23:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L11
M01_L24:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L25:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 655
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_CustomItem()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L05
       lea       r8,[rbp-28]
       mov       rdx,1D300206748
       call      qword ptr [7FF9B3D4C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       je        near ptr M00_L04
       mov       rax,[rbp-28]
M00_L01:
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DA5560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1D35E000068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DA5638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       ecx,0F57
       mov       rdx,7FF9B3C78428
       call      qword ptr [7FF9B3A0F210]
       mov       rdx,rax
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      qword ptr [7FF9B3E86AC0]
       int       3
M00_L05:
       mov       r11,7FF9B39505D8
       mov       rdx,1D300206748
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L06
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DA5638]; System.Threading.Lock.Exit(ThreadId)
M00_L06:
       nop
       add       rsp,28
       ret
; Total bytes of code 324
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L14
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L06
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        near ptr M01_L05
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M01_L25
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L24
       test      r14,r14
       je        near ptr M01_L11
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L11
M01_L02:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L15
       mov       rdx,[rbp+8]
       cmp       rdx,rbx
       jne       short M01_L07
       mov       eax,1
M01_L03:
       test      eax,eax
       je        near ptr M01_L15
M01_L04:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L05:
       mov       rdx,7FF9B3EA0D48
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L06:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       near ptr M01_L01
M01_L07:
       test      rdx,rdx
       je        short M01_L10
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L10
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L08
       mov       rdx,rax
       call      qword ptr [7FF9B3A0C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L09
M01_L08:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L09:
       jmp       near ptr M01_L03
M01_L10:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L11:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L23
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L16
M01_L12:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       short M01_L17
       cmp       rdx,rbx
       jne       short M01_L18
       jmp       near ptr M01_L22
M01_L13:
       test      eax,eax
       je        near ptr M01_L23
       jmp       near ptr M01_L04
M01_L14:
       mov       ecx,1
       mov       rdx,7FF9B3D49948
       call      qword ptr [7FF9B3A0F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E86B38]
       int       3
M01_L15:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L02
       jmp       near ptr M01_L24
M01_L16:
       mov       rdx,7FF9B3EA0C38
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L12
M01_L17:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       short M01_L13
M01_L18:
       test      rdx,rdx
       je        short M01_L21
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L21
       add       rdx,0C
       lea       rax,[rbx+0C]
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M01_L19
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B3A0C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L20
M01_L19:
       mov       rcx,rdx
       mov       r11,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[r11]
       xor       rdx,r8
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L20:
       jmp       near ptr M01_L13
M01_L21:
       xor       eax,eax
       jmp       near ptr M01_L13
M01_L22:
       mov       eax,1
       jmp       near ptr M01_L13
M01_L23:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L11
M01_L24:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L25:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 655
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_TransportSpecific_SQS()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L05
       lea       r8,[rbp-28]
       mov       rdx,1DD002D6848
       call      qword ptr [7FF9B3D5C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       je        near ptr M00_L04
       mov       rax,[rbp-28]
M00_L01:
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DB5560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1DD7A400068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       ecx,0FDF
       mov       rdx,7FF9B3C88428
       call      qword ptr [7FF9B3A1F210]
       mov       rdx,rax
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      qword ptr [7FF9B3E96AA8]
       int       3
M00_L05:
       mov       r11,7FF9B39605D8
       mov       rdx,1DD002D6848
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L06
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
M00_L06:
       nop
       add       rsp,28
       ret
; Total bytes of code 324
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L14
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L06
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        near ptr M01_L05
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M01_L25
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L24
       test      r14,r14
       je        near ptr M01_L11
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L11
M01_L02:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L15
       mov       rdx,[rbp+8]
       cmp       rdx,rbx
       jne       short M01_L07
       mov       eax,1
M01_L03:
       test      eax,eax
       je        near ptr M01_L15
M01_L04:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L05:
       mov       rdx,7FF9B3EB0D48
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L06:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       near ptr M01_L01
M01_L07:
       test      rdx,rdx
       je        short M01_L10
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L10
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L08
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L09
M01_L08:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L09:
       jmp       near ptr M01_L03
M01_L10:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L11:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L23
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L16
M01_L12:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       short M01_L17
       cmp       rdx,rbx
       jne       short M01_L18
       jmp       near ptr M01_L22
M01_L13:
       test      eax,eax
       je        near ptr M01_L23
       jmp       near ptr M01_L04
M01_L14:
       mov       ecx,1
       mov       rdx,7FF9B3D59948
       call      qword ptr [7FF9B3A1F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E96B20]
       int       3
M01_L15:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L02
       jmp       near ptr M01_L24
M01_L16:
       mov       rdx,7FF9B3EB0C38
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L12
M01_L17:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       short M01_L13
M01_L18:
       test      rdx,rdx
       je        short M01_L21
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L21
       add       rdx,0C
       lea       rax,[rbx+0C]
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M01_L19
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L20
M01_L19:
       mov       rcx,rdx
       mov       r11,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[r11]
       xor       rdx,r8
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L20:
       jmp       near ptr M01_L13
M01_L21:
       xor       eax,eax
       jmp       near ptr M01_L13
M01_L22:
       mov       eax,1
       jmp       near ptr M01_L13
M01_L23:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L11
M01_L24:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L25:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 655
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_TransportSpecific_RabbitMQ()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L05
       lea       r8,[rbp-28]
       mov       rdx,21A002068C0
       call      qword ptr [7FF9B3D3C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       test      eax,eax
       je        near ptr M00_L04
       mov       rax,[rbp-28]
M00_L01:
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3D95560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,21A24400068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3D95638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       ecx,102D
       mov       rdx,7FF9B3C68428
       call      qword ptr [7FF9B39FF210]
       mov       rdx,rax
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      qword ptr [7FF9B3E76AA8]
       int       3
M00_L05:
       mov       r11,7FF9B39405D8
       mov       rdx,21A002068C0
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L06
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3D95638]; System.Threading.Lock.Exit(ThreadId)
M00_L06:
       nop
       add       rsp,28
       ret
; Total bytes of code 324
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L14
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L06
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        near ptr M01_L05
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M01_L25
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L24
       test      r14,r14
       je        near ptr M01_L11
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L11
M01_L02:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L15
       mov       rdx,[rbp+8]
       cmp       rdx,rbx
       jne       short M01_L07
       mov       eax,1
M01_L03:
       test      eax,eax
       je        near ptr M01_L15
M01_L04:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L05:
       mov       rdx,7FF9B3E90D48
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L06:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       near ptr M01_L01
M01_L07:
       test      rdx,rdx
       je        short M01_L10
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L10
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L08
       mov       rdx,rax
       call      qword ptr [7FF9B39FC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L09
M01_L08:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L09:
       jmp       near ptr M01_L03
M01_L10:
       xor       eax,eax
       jmp       near ptr M01_L03
M01_L11:
       cmp       r15d,[rbp+20]
       jne       near ptr M01_L23
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L16
M01_L12:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       short M01_L17
       cmp       rdx,rbx
       jne       short M01_L18
       jmp       near ptr M01_L22
M01_L13:
       test      eax,eax
       je        near ptr M01_L23
       jmp       near ptr M01_L04
M01_L14:
       mov       ecx,1
       mov       rdx,7FF9B3D39948
       call      qword ptr [7FF9B39FF210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E76B20]
       int       3
M01_L15:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L02
       jmp       near ptr M01_L24
M01_L16:
       mov       rdx,7FF9B3E90C38
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L12
M01_L17:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       short M01_L13
M01_L18:
       test      rdx,rdx
       je        short M01_L21
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L21
       add       rdx,0C
       lea       rax,[rbx+0C]
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M01_L19
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B39FC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L20
M01_L19:
       mov       rcx,rdx
       mov       r11,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[r11]
       xor       rdx,r8
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M01_L20:
       jmp       near ptr M01_L13
M01_L21:
       xor       eax,eax
       jmp       near ptr M01_L13
M01_L22:
       mov       eax,1
       jmp       near ptr M01_L13
M01_L23:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       near ptr M01_L11
M01_L24:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L25:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 655
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_TryGetValue_Exists()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L01
M00_L00:
       lea       r8,[rbp-28]
       mov       r11,7FF9B39605D8
       mov       rdx,27C804566C8
       call      qword ptr [r11]
       nop
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L01:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DB5560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L02
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,27CF7000068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L02:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L03
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
M00_L03:
       nop
       add       rsp,28
       ret
; Total bytes of code 232
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M01_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M01_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M01_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M01_L00:
       add       rsp,30
       pop       rbx
       ret
M01_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M01_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M02_L10
M02_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M02_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M02_L02
M02_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M02_L01
M02_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M02_L06
M02_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M02_L07
M02_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M02_L09
M02_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M02_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M02_L03
M02_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M02_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M02_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M02_L08:
       mov       r14,rax
       jmp       near ptr M02_L05
M02_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M02_L05
M02_L10:
       cmp       edi,0FFFFFFFF
       je        short M02_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M02_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M02_L00
M02_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M03_L02
       cmp       dword ptr [rcx+18],0
       jne       short M03_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M03_L03
M03_L00:
       add       rsp,28
       ret
M03_L01:
       dec       dword ptr [rcx+18]
       jmp       short M03_L00
M03_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M03_L03:
       call      qword ptr [7FF964230260]
       jmp       short M03_L00
; Total bytes of code 69
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M04_L01
       test      rsi,rsi
       je        short M04_L02
       cmp       [rsi+8],ebx
       jl        short M04_L02
       add       rsi,10
M04_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M04_L00
M04_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M04_L02:
       mov       ecx,[rsi+8]
M04_L03:
       cmp       edi,[rsi+8]
       jae       short M04_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M04_L03
       jmp       short M04_L01
M04_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_TryGetValue_NotExists()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L01
M00_L00:
       lea       r8,[rbp-28]
       mov       r11,7FF9B39305C8
       mov       rdx,233002092F8
       call      qword ptr [r11]
       nop
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L01:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3D85560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L02
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,23303000068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3CFC0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L02:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3D85638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L03
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3D85638]; System.Threading.Lock.Exit(ThreadId)
M00_L03:
       nop
       add       rsp,28
       ret
; Total bytes of code 232
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M01_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M01_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M01_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M01_L00:
       add       rsp,30
       pop       rbx
       ret
M01_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M01_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M02_L10
M02_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M02_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M02_L02
M02_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M02_L01
M02_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M02_L06
M02_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M02_L07
M02_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M02_L09
M02_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M02_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M02_L03
M02_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M02_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M02_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M02_L08:
       mov       r14,rax
       jmp       near ptr M02_L05
M02_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M02_L05
M02_L10:
       cmp       edi,0FFFFFFFF
       je        short M02_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M02_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M02_L00
M02_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M03_L02
       cmp       dword ptr [rcx+18],0
       jne       short M03_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M03_L03
M03_L00:
       add       rsp,28
       ret
M03_L01:
       dec       dword ptr [rcx+18]
       jmp       short M03_L00
M03_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M03_L03:
       call      qword ptr [7FF964230260]
       jmp       short M03_L00
; Total bytes of code 69
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M04_L01
       test      rsi,rsi
       je        short M04_L02
       cmp       [rsi+8],ebx
       jl        short M04_L02
       add       rsi,10
M04_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M04_L00
M04_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M04_L02:
       mov       ecx,[rsi+8]
M04_L03:
       cmp       edi,[rsi+8]
       jae       short M04_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M04_L03
       jmp       short M04_L01
M04_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_ContainsKey_Exists()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L04
       lea       r8,[rbp-28]
       mov       rdx,210004566C8
       call      qword ptr [7FF9B3D3C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
M00_L01:
       nop
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3D95560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,21070000068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3D95638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       r11,7FF9B39405D8
       mov       rdx,210004566C8
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L05
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3D95638]; System.Threading.Lock.Exit(ThreadId)
M00_L05:
       nop
       add       rsp,28
       ret
; Total bytes of code 272
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rsi,rcx
       mov       rbx,rdx
       mov       rdi,r8
       test      rbx,rbx
       je        near ptr M01_L14
       mov       rbp,[rsi+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rsi+19],0
       jne       near ptr M01_L05
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        short M01_L04
M01_L00:
       mov       rcx,r14
       mov       rdx,rbx
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       eax,[rcx+8]
       mov       r8d,eax
       imul      rdx,r8
       shr       rdx,20
       cmp       edx,eax
       jae       near ptr M01_L17
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       je        near ptr M01_L16
M01_L02:
       cmp       r15d,[rbp+20]
       je        short M01_L06
M01_L03:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       short M01_L02
       jmp       near ptr M01_L16
M01_L04:
       mov       rdx,7FF9B3E90C80
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L00
M01_L05:
       mov       rcx,rbx
       mov       rax,[rbx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       short M01_L01
M01_L06:
       mov       rcx,[rsi]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L09
M01_L07:
       mov       rdx,[rbp+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r14],rcx
       jne       near ptr M01_L15
       cmp       rdx,rbx
       jne       short M01_L10
       mov       eax,1
M01_L08:
       test      eax,eax
       je        short M01_L03
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L09:
       mov       rdx,7FF9B3E90BC8
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L07
M01_L10:
       test      rdx,rdx
       je        short M01_L13
       mov       ecx,[rdx+8]
       cmp       ecx,[rbx+8]
       jne       short M01_L13
       lea       rcx,[rdx+0C]
       lea       rax,[rbx+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M01_L11
       mov       rdx,rax
       call      qword ptr [7FF9B39FC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M01_L12
M01_L11:
       mov       r11,[rcx]
       mov       rcx,[rcx+2]
       mov       rdx,[rax]
       xor       r11,rdx
       xor       rcx,[rax+2]
       or        rcx,r11
       sete      al
       movzx     eax,al
M01_L12:
       jmp       near ptr M01_L08
M01_L13:
       xor       eax,eax
       jmp       near ptr M01_L08
M01_L14:
       mov       ecx,1
       mov       rdx,7FF9B3D39948
       call      qword ptr [7FF9B39FF210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E76B08]
       int       3
M01_L15:
       mov       rcx,r14
       mov       r8,rbx
       call      qword ptr [r11]
       jmp       near ptr M01_L08
M01_L16:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L17:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 460
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M06_L06
       cmp       rcx,rdx
       je        short M06_L04
       cmp       r8,10
       jae       short M06_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M06_L00:
       ret
M06_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M06_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
M06_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M06_L10
M06_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M06_L05
M06_L04:
       mov       eax,1
       ret
M06_L05:
       xor       eax,eax
       ret
M06_L06:
       cmp       r8,4
       jb        short M06_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M06_L00
M06_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M06_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M06_L08:
       test      r8b,1
       je        short M06_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M06_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M06_L00
M06_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M06_L05
       jmp       near ptr M06_L02
; Total bytes of code 237
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M07_L01
       test      rsi,rsi
       je        short M07_L02
       cmp       [rsi+8],ebx
       jl        short M07_L02
       add       rsi,10
M07_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M07_L00
M07_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M07_L02:
       mov       ecx,[rsi+8]
M07_L03:
       cmp       edi,[rsi+8]
       jae       short M07_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M07_L03
       jmp       short M07_L01
M07_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_ContainsKey_NotExists()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,40
       lea       rbp,[rsp+60]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r8,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r8
       jne       near ptr M00_L04
       lea       r8,[rbp-28]
       mov       rdx,1E8802092F8
       call      qword ptr [7FF9B3D5C210]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
M00_L01:
       nop
       add       rsp,40
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DB5560]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1E896400068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0D8]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       r11,7FF9B39605C8
       mov       rdx,1E8802092F8
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,28
       cmp       qword ptr [rbp-38],0
       je        short M00_L05
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DB5638]; System.Threading.Lock.Exit(ThreadId)
M00_L05:
       nop
       add       rsp,28
       ret
; Total bytes of code 272
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryGetValue(System.__Canon, System.__Canon ByRef)
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       [rsp+20],rcx
       mov       rbx,rcx
       mov       rsi,rdx
       mov       rdi,r8
       test      rsi,rsi
       je        near ptr M01_L05
       mov       rbp,[rbx+8]
       mov       r14,[rbp+8]
       cmp       byte ptr [rbx+19],0
       jne       short M01_L04
       mov       rcx,[rbx]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+68]
       test      r11,r11
       je        short M01_L03
M01_L00:
       mov       rcx,r14
       mov       rdx,rsi
       call      qword ptr [r11]
       mov       r15d,eax
M01_L01:
       mov       rcx,[rbp+10]
       mov       edx,r15d
       imul      rdx,[rbp+28]
       shr       rdx,20
       inc       rdx
       mov       eax,[rcx+8]
       mov       r8d,eax
       imul      rdx,r8
       shr       rdx,20
       cmp       edx,eax
       jae       near ptr M01_L11
       mov       edx,edx
       mov       rbp,[rcx+rdx*8+10]
       test      rbp,rbp
       jne       short M01_L06
M01_L02:
       xor       eax,eax
       mov       [rdi],rax
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L03:
       mov       rdx,7FF9B3EB0BB0
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L00
M01_L04:
       mov       rcx,rsi
       mov       rax,[rsi]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       mov       r15d,eax
       jmp       short M01_L01
M01_L05:
       mov       ecx,1
       mov       rdx,7FF9B3D59948
       call      qword ptr [7FF9B3A1F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E96AF0]
       int       3
M01_L06:
       cmp       r15d,[rbp+20]
       jne       short M01_L09
       mov       rcx,[rbx]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+60]
       test      r11,r11
       je        short M01_L07
       jmp       short M01_L08
M01_L07:
       mov       rdx,7FF9B3EB0AF8
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M01_L08:
       mov       rdx,[rbp+8]
       mov       rcx,r14
       mov       r8,rsi
       call      qword ptr [r11]
       test      eax,eax
       jne       short M01_L10
M01_L09:
       mov       rbp,[rbp+18]
       test      rbp,rbp
       jne       short M01_L06
       jmp       near ptr M01_L02
M01_L10:
       mov       rdx,[rbp+10]
       mov       rcx,rdi
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       ret
M01_L11:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 334
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M06_L01
       test      rsi,rsi
       je        short M06_L02
       cmp       [rsi+8],ebx
       jl        short M06_L02
       add       rsi,10
M06_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M06_L00
M06_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M06_L02:
       mov       ecx,[rsi+8]
M06_L03:
       cmp       edi,[rsi+8]
       jae       short M06_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M06_L03
       jmp       short M06_L01
M06_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.DirectProperty_Write_CorrelationId()
       mov       rax,[rcx+8]
       mov       rcx,27C002064E8
       mov       [rax+60],rcx
       ret
; Total bytes of code 19
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_Write_NewKey()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,60
       lea       rbp,[rsp+80]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r9,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r9
       jne       near ptr M00_L04
       mov       rdx,[rcx+8]
       mov       r9,1FD00209320
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-28]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,1FD002092F8
       call      qword ptr [7FF9B3D2EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
M00_L01:
       nop
       add       rsp,60
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DB5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1FD08C00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DB5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       r11,7FF9B39605D8
       mov       rdx,1FD002092F8
       mov       r8,1FD00209320
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,48
       cmp       qword ptr [rbp-38],0
       je        short M00_L05
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DB5620]; System.Threading.Lock.Exit(ThreadId)
M00_L05:
       nop
       add       rsp,48
       ret
; Total bytes of code 328
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-40],rax
       mov       [rbp-20],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       movzx     r9d,r9b
       mov       rax,[rbp+18]
       mov       rax,[rax+8]
       mov       [rbp-40],rax
       mov       ebx,[rbp+2C]
       test      r9d,r9d
       jne       near ptr M01_L29
       cmp       byte ptr [rcx+19],0
       jne       near ptr M01_L28
       mov       rax,[rcx]
       mov       r8,[rax+30]
       mov       r8,[r8]
       mov       r11,[r8+78]
       test      r11,r11
       je        near ptr M01_L27
M01_L00:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
M01_L01:
       mov       [rbp-24],eax
M01_L02:
       mov       rax,[rbp+18]
       mov       rcx,[rax+18]
       mov       [rbp-48],rcx
       mov       r8,[rbp+10]
       cmp       [r8],r8d
       mov       rax,[rbp+18]
       mov       r10,[rax+10]
       mov       rax,[rbp+18]
       mov       r9d,[rbp-24]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       mov       [rbp-28],edx
       cmp       r9d,r11d
       jae       near ptr M01_L36
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       xor       ecx,ecx
       mov       [rbp-2C],ecx
       mov       [rbp-30],ecx
       mov       [rbp-34],ecx
       cmp       byte ptr [rbp+40],0
       je        short M01_L04
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rsi,[rcx+rax*8+10]
       test      rsi,rsi
       je        near ptr M01_L10
       mov       rcx,rsi
       call      00007FFA135C0070
       test      eax,eax
       je        near ptr M01_L11
M01_L03:
       mov       dword ptr [rbp-34],1
M01_L04:
       mov       rcx,[rbp+18]
       mov       r8,[rbp+10]
       cmp       rcx,[r8+8]
       jne       near ptr M01_L12
       xor       esi,esi
       mov       rdi,[rbx]
       test      rdi,rdi
       je        near ptr M01_L19
M01_L05:
       mov       ecx,[rbp-24]
       cmp       ecx,[rdi+20]
       jne       near ptr M01_L17
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rax,[rdx+68]
       test      rax,rax
       je        short M01_L08
       mov       rcx,rax
M01_L06:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+80]
       test      r11,r11
       je        short M01_L09
M01_L07:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-40]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       r8,[rbp+10]
       je        near ptr M01_L17
       cmp       byte ptr [rbp+38],0
       je        near ptr M01_L18
       lea       rcx,[rdi+10]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L08:
       mov       rdx,7FF9B3EB0D00
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       short M01_L06
M01_L09:
       mov       rdx,7FF9B3EB1038
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L07
M01_L10:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96A18]
       int       3
M01_L11:
       mov       rcx,rsi
       call      qword ptr [7FF9B3E96A30]
       jmp       near ptr M01_L03
M01_L12:
       mov       rcx,[r8+8]
       mov       [rbp+18],rcx
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+18]
       cmp       rcx,[rdx+8]
       je        near ptr M01_L31
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+8]
       mov       [rbp-40],rcx
       cmp       byte ptr [r8+19],0
       jne       short M01_L15
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+78]
       test      r11,r11
       je        short M01_L13
       jmp       short M01_L14
M01_L13:
       mov       rdx,7FF9B3EB0EF8
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M01_L14:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M01_L16
M01_L15:
       mov       rcx,[rbp+20]
       mov       rax,[rcx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
M01_L16:
       mov       [rbp-24],eax
       mov       r8,[rbp+10]
       jmp       near ptr M01_L31
M01_L17:
       inc       esi
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M01_L05
       jmp       short M01_L19
M01_L18:
       mov       rdx,[rdi+10]
       mov       rcx,[rbp+48]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L19:
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rdx,[rdx+70]
       test      rdx,rdx
       je        short M01_L22
       jmp       short M01_L23
M01_L20:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L21:
       call      CORINFO_HELP_OVERFLOW
       int       3
M01_L22:
       mov       rdx,7FF9B3EB0D88
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdx,rax
M01_L23:
       mov       rcx,rdx
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       rcx,[rbx]
       mov       [rsp+20],rcx
       mov       rcx,rdi
       mov       rdx,[rbp+20]
       mov       r8,[rbp+30]
       mov       r9d,[rbp-24]
       call      qword ptr [7FF9B3E96B20]
       mov       rcx,rbx
       mov       rdx,rdi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L20
       mov       ecx,[rbp-28]
       lea       rdx,[rdx+rcx*4+10]
       mov       ecx,[rdx]
       add       ecx,1
       jo        short M01_L21
       mov       [rdx],ecx
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       ecx,[rbp-28]
       mov       edx,[rdx+rcx*4+10]
       mov       r8,[rbp+10]
       cmp       edx,[r8+10]
       jle       short M01_L24
       mov       dword ptr [rbp-2C],1
M01_L24:
       cmp       esi,64
       jbe       near ptr M01_L30
       mov       rdx,[rbp-40]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer
       call      qword ptr [7FF9B3A16850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        near ptr M01_L30
       mov       dword ptr [rbp-30],1
       jmp       short M01_L30
M01_L25:
       cmp       dword ptr [rbp-34],0
       je        short M01_L26
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L36
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L32
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       jne       short M01_L33
M01_L26:
       xor       eax,eax
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L27:
       mov       rcx,rax
       mov       rdx,7FF9B3EB0EF8
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L28:
       mov       rdx,[rbp+20]
       mov       rcx,rdx
       mov       rax,[rdx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M01_L01
M01_L29:
       mov       eax,ebx
       jmp       near ptr M01_L01
M01_L30:
       call      M01_L37
       jmp       short M01_L34
M01_L31:
       call      M01_L37
       jmp       near ptr M01_L02
M01_L32:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96A18]
       int       3
M01_L33:
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E96A48]
       jmp       short M01_L26
M01_L34:
       mov       ecx,[rbp-2C]
       or        ecx,[rbp-30]
       je        short M01_L35
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+18]
       mov       r8d,[rbp-2C]
       mov       r9d,[rbp-30]
       call      qword ptr [7FF9B3D2F108]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
M01_L35:
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L36:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L37:
       sub       rsp,28
       cmp       dword ptr [rbp-34],0
       je        short M01_L38
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L40
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L39
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       je        short M01_L38
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E96A48]
M01_L38:
       nop
       add       rsp,28
       ret
M01_L39:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96A18]
       int       3
M01_L40:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1188
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M06_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M06_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
M06_L00:
       test      rax,rax
       je        short M06_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       test      rax,rax
       je        short M06_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       test      rax,rax
       jne       short M06_L03
M06_L01:
       xor       edx,edx
M06_L02:
       mov       rax,rdx
       ret
M06_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       test      rax,rax
       je        short M06_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       jmp       short M06_L00
; Total bytes of code 86
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp-40],rcx
       mov       [rbp+10],rcx
       mov       rbx,rdx
       mov       esi,r8d
       mov       edi,r9d
       xor       eax,eax
       mov       [rbp-48],eax
       mov       rax,[rcx+8]
       mov       rax,[rax+18]
       cmp       dword ptr [rax+8],0
       jbe       near ptr M07_L15
       mov       rcx,[rax+10]
       call      qword ptr [7FFA759A0078]; Precode of System.Threading.Monitor.Enter(System.Object)
       mov       dword ptr [rbp-48],1
       mov       rcx,[rbp+10]
       cmp       rbx,[rcx+8]
       jne       near ptr M07_L18
       mov       rax,[rbx+10]
       mov       r14d,[rax+8]
       xor       r15d,r15d
       test      dil,dil
       jne       near ptr M07_L13
M07_L00:
       test      sil,sil
       je        short M07_L02
       test      r15,r15
       jne       short M07_L01
       mov       rcx,[rbp+10]
       call      qword ptr [7FFA759A08F8]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetCountNoLocks()
       mov       rcx,[rbx+10]
       mov       ecx,[rcx+8]
       shr       ecx,2
       cmp       eax,ecx
       jl        near ptr M07_L12
M07_L01:
       mov       rax,[rbx+10]
       mov       eax,[rax+8]
       add       eax,eax
       js        near ptr M07_L17
       mov       ecx,eax
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       r14d,eax
       call      qword ptr [7FFA7599FF68]
       cmp       eax,r14d
       jl        near ptr M07_L17
M07_L02:
       mov       rsi,[rbx+18]
       mov       rdi,rsi
       mov       rcx,[rbp+10]
       cmp       byte ptr [rcx+18],0
       je        short M07_L04
       cmp       dword ptr [rsi+8],400
       jge       short M07_L04
       mov       eax,[rsi+8]
       add       eax,eax
       movsxd    rcx,eax
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r8d,[rsi+8]
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FFA7599FF50]
       mov       rax,[rbx+18]
       mov       esi,[rax+8]
       mov       r13d,[rdi+8]
       cmp       r13d,esi
       jle       short M07_L04
M07_L03:
       call      qword ptr [7FFA7599FE68]
       mov       r8,rax
       movsxd    rdx,esi
       mov       rcx,rdi
       call      qword ptr [7FFA7599F2B0]; Precode of System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       inc       esi
       cmp       r13d,esi
       jg        short M07_L03
M07_L04:
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599FA10]
       mov       rcx,rax
       movsxd    rdx,r14d
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       rsi,rax
       mov       [rbp-60],rsi
       mov       ecx,[rdi+8]
       call      qword ptr [7FFA7599FF18]
       mov       r14,rax
       mov       r12,r15
       test      r12,r12
       jne       short M07_L05
       mov       r12,[rbx+8]
M07_L05:
       mov       rcx,r13
       call      qword ptr [7FFA7599F760]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-78],rax
       lea       rcx,[rax+10]
       mov       rdx,rsi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+20]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       ecx,[rsi+8]
       xor       edx,edx
       div       rcx
       inc       rax
       mov       r12,[rbp-78]
       mov       [r12+28],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F728]
       mov       rcx,rax
       lea       r8,[rbp-48]
       mov       rdx,rbx
       call      qword ptr [7FFA759A0918]
       mov       rbx,[rbx+10]
       xor       eax,eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jg        near ptr M07_L10
M07_L06:
       mov       rsi,[rbp-60]
       mov       eax,[rsi+8]
       xor       edx,edx
       div       dword ptr [rdi+8]
       mov       ecx,1
       cmp       eax,1
       cmovg     ecx,eax
       mov       rax,[rbp+10]
       mov       [rax+10],ecx
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M07_L18
M07_L07:
       test      r15,r15
       jne       near ptr M07_L11
       mov       [rbp-68],rdx
       mov       r8d,[rdx+20]
M07_L08:
       mov       rdx,[rbp-68]
       mov       r10,[rdx+18]
       mov       [rbp-80],r10
       mov       rcx,[r12+10]
       mov       [rbp-4C],r8d
       mov       r9d,r8d
       imul      r9,[r12+28]
       shr       r9,20
       inc       r9
       mov       r11d,[rcx+8]
       mov       esi,r11d
       imul      r9,rsi
       shr       r9,20
       mov       rsi,[r12+18]
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rsi+8]
       mov       esi,edx
       cmp       r9d,r11d
       jae       near ptr M07_L15
       mov       eax,r9d
       lea       rax,[rcx+rax*8+10]
       mov       [rbp-70],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F748]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-88],rax
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       r8,[r8+10]
       mov       [rbp-90],r8
       mov       r10,[rbp-70]
       mov       r9,[r10]
       mov       [rbp-98],r9
       lea       rcx,[rax+8]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+10]
       mov       rdx,[rbp-90]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+18]
       mov       rdx,[rbp-98]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       mov       ecx,[rbp-4C]
       mov       [rax+20],ecx
       mov       rcx,[rbp-70]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       cmp       esi,[r14+8]
       jae       near ptr M07_L15
       mov       eax,esi
       lea       rax,[r14+rax*4+10]
       mov       edx,[rax]
       add       edx,1
       jo        near ptr M07_L16
       mov       [rax],edx
       mov       rsi,[rbp-80]
       test      rsi,rsi
       mov       rdx,rsi
       jne       near ptr M07_L07
M07_L09:
       mov       rax,[rbp-58]
       inc       eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jle       near ptr M07_L06
M07_L10:
       mov       [rbp-58],rax
       mov       rdx,[rbx+rax*8+10]
       test      rdx,rdx
       jne       near ptr M07_L07
       jmp       short M07_L09
M07_L11:
       mov       [rbp-68],rdx
       mov       rcx,[rbp+10]
       mov       rcx,[rcx]
       call      qword ptr [7FFA7599FBD8]
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       rcx,r15
       mov       r11,rax
       call      qword ptr [rax]
       mov       r8d,eax
       jmp       near ptr M07_L08
M07_L12:
       mov       rcx,[rbp+10]
       mov       eax,[rcx+10]
       add       eax,eax
       mov       [rcx+10],eax
       test      eax,eax
       jge       near ptr M07_L18
       jmp       short M07_L14
M07_L13:
       mov       rcx,[rbx+8]
       call      qword ptr [7FFA7599FF30]
       mov       rdi,rax
       test      rdi,rdi
       je        near ptr M07_L00
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599F550]
       mov       r15,rax
       mov       rcx,rdi
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       call      qword ptr [r11]
       mov       rdx,rax
       mov       rcx,r15
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
       mov       r15,rax
       jmp       near ptr M07_L00
M07_L14:
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       short M07_L18
M07_L15:
       call      qword ptr [7FFA7599F290]
       int       3
M07_L16:
       call      qword ptr [7FFA7599F288]
       int       3
M07_L17:
       call      qword ptr [7FFA7599FF68]
       mov       r14d,eax
       mov       rcx,[rbp+10]
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       near ptr M07_L02
M07_L18:
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
       sub       rsp,28
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,28
       ret
; Total bytes of code 1137
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M08_L01
       test      rsi,rsi
       je        short M08_L02
       cmp       [rsi+8],ebx
       jl        short M08_L02
       add       rsi,10
M08_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M08_L00
M08_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M08_L02:
       mov       ecx,[rsi+8]
M08_L03:
       cmp       edi,[rsi+8]
       jae       short M08_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M08_L03
       jmp       short M08_L01
M08_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ItemsDictionary_Write_ExistingKey()
       push      rbp
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,60
       lea       rbp,[rsp+80]
       xor       eax,eax
       mov       [rbp-28],rax
       mov       rbx,[rcx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        short M00_L02
M00_L00:
       mov       r9,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r9
       jne       near ptr M00_L04
       mov       rdx,[rcx+8]
       mov       r9,1FE002064E8
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-28]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,1FE002066C8
       call      qword ptr [7FF9B3D2EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
M00_L01:
       nop
       add       rsp,60
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       rbp
       ret
M00_L02:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DB5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-38],rsi
       mov       [rbp-2C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L03
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1FE37400068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L03:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DB5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L00
M00_L04:
       mov       r11,7FF9B39605D8
       mov       rdx,1FE002066C8
       mov       r8,1FE002064E8
       call      qword ptr [r11]
       jmp       near ptr M00_L01
       sub       rsp,48
       cmp       qword ptr [rbp-38],0
       je        short M00_L05
       mov       rcx,[rbp-38]
       mov       edx,[rbp-2C]
       call      qword ptr [7FF9B3DB5620]; System.Threading.Lock.Exit(ThreadId)
M00_L05:
       nop
       add       rsp,48
       ret
; Total bytes of code 328
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-40],rax
       mov       [rbp-20],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       movzx     r9d,r9b
       mov       rax,[rbp+18]
       mov       rax,[rax+8]
       mov       [rbp-40],rax
       mov       ebx,[rbp+2C]
       test      r9d,r9d
       jne       near ptr M01_L29
       cmp       byte ptr [rcx+19],0
       jne       near ptr M01_L28
       mov       rax,[rcx]
       mov       r8,[rax+30]
       mov       r8,[r8]
       mov       r11,[r8+78]
       test      r11,r11
       je        near ptr M01_L27
M01_L00:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
M01_L01:
       mov       [rbp-24],eax
M01_L02:
       mov       rax,[rbp+18]
       mov       rcx,[rax+18]
       mov       [rbp-48],rcx
       mov       r8,[rbp+10]
       cmp       [r8],r8d
       mov       rax,[rbp+18]
       mov       r10,[rax+10]
       mov       rax,[rbp+18]
       mov       r9d,[rbp-24]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       mov       [rbp-28],edx
       cmp       r9d,r11d
       jae       near ptr M01_L36
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       xor       ecx,ecx
       mov       [rbp-2C],ecx
       mov       [rbp-30],ecx
       mov       [rbp-34],ecx
       cmp       byte ptr [rbp+40],0
       je        short M01_L04
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rsi,[rcx+rax*8+10]
       test      rsi,rsi
       je        near ptr M01_L11
       mov       rcx,rsi
       call      00007FFA135C0070
       test      eax,eax
       je        near ptr M01_L12
M01_L03:
       mov       dword ptr [rbp-34],1
M01_L04:
       mov       rcx,[rbp+18]
       mov       r8,[rbp+10]
       cmp       rcx,[r8+8]
       jne       near ptr M01_L13
       xor       esi,esi
       mov       rdi,[rbx]
       test      rdi,rdi
       je        near ptr M01_L19
M01_L05:
       mov       ecx,[rbp-24]
       cmp       ecx,[rdi+20]
       jne       near ptr M01_L10
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rax,[rdx+68]
       test      rax,rax
       je        short M01_L08
       mov       rcx,rax
M01_L06:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+80]
       test      r11,r11
       je        short M01_L09
M01_L07:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-40]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       r8,[rbp+10]
       je        short M01_L10
       cmp       byte ptr [rbp+38],0
       je        near ptr M01_L18
       lea       rcx,[rdi+10]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L08:
       mov       rdx,7FF9B3EB0D00
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       short M01_L06
M01_L09:
       mov       rdx,7FF9B3EB1038
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L07
M01_L10:
       inc       esi
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M01_L05
       jmp       near ptr M01_L19
M01_L11:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96A30]
       int       3
M01_L12:
       mov       rcx,rsi
       call      qword ptr [7FF9B3E951D0]; System.Threading.Monitor.Enter_Slowpath(System.Object)
       jmp       near ptr M01_L03
M01_L13:
       mov       rcx,[r8+8]
       mov       [rbp+18],rcx
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+18]
       cmp       rcx,[rdx+8]
       je        near ptr M01_L31
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+8]
       mov       [rbp-40],rcx
       cmp       byte ptr [r8+19],0
       jne       short M01_L16
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+78]
       test      r11,r11
       je        short M01_L14
       jmp       short M01_L15
M01_L14:
       mov       rdx,7FF9B3EB0EF8
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M01_L15:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M01_L17
M01_L16:
       mov       rcx,[rbp+20]
       mov       rax,[rcx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
M01_L17:
       mov       [rbp-24],eax
       mov       r8,[rbp+10]
       jmp       near ptr M01_L31
M01_L18:
       mov       rdx,[rdi+10]
       mov       rcx,[rbp+48]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L19:
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rdx,[rdx+70]
       test      rdx,rdx
       je        short M01_L22
       jmp       short M01_L23
M01_L20:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L21:
       call      CORINFO_HELP_OVERFLOW
       int       3
M01_L22:
       mov       rdx,7FF9B3EB0D88
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdx,rax
M01_L23:
       mov       rcx,rdx
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       rcx,[rbx]
       mov       [rsp+20],rcx
       mov       rcx,rdi
       mov       rdx,[rbp+20]
       mov       r8,[rbp+30]
       mov       r9d,[rbp-24]
       call      qword ptr [7FF9B3E96B20]
       mov       rcx,rbx
       mov       rdx,rdi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L20
       mov       ecx,[rbp-28]
       lea       rdx,[rdx+rcx*4+10]
       mov       ecx,[rdx]
       add       ecx,1
       jo        short M01_L21
       mov       [rdx],ecx
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       ecx,[rbp-28]
       mov       edx,[rdx+rcx*4+10]
       mov       r8,[rbp+10]
       cmp       edx,[r8+10]
       jle       short M01_L24
       mov       dword ptr [rbp-2C],1
M01_L24:
       cmp       esi,64
       jbe       near ptr M01_L30
       mov       rdx,[rbp-40]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer
       call      qword ptr [7FF9B3A16850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        near ptr M01_L30
       mov       dword ptr [rbp-30],1
       jmp       short M01_L30
M01_L25:
       cmp       dword ptr [rbp-34],0
       je        short M01_L26
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L36
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L32
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       jne       short M01_L33
M01_L26:
       xor       eax,eax
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L27:
       mov       rcx,rax
       mov       rdx,7FF9B3EB0EF8
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L28:
       mov       rdx,[rbp+20]
       mov       rcx,rdx
       mov       rax,[rdx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M01_L01
M01_L29:
       mov       eax,ebx
       jmp       near ptr M01_L01
M01_L30:
       call      M01_L37
       jmp       short M01_L34
M01_L31:
       call      M01_L37
       jmp       near ptr M01_L02
M01_L32:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96A30]
       int       3
M01_L33:
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E96A48]
       jmp       short M01_L26
M01_L34:
       mov       ecx,[rbp-2C]
       or        ecx,[rbp-30]
       je        short M01_L35
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+18]
       mov       r8d,[rbp-2C]
       mov       r9d,[rbp-30]
       call      qword ptr [7FF9B3D2F108]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
M01_L35:
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L36:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L37:
       sub       rsp,28
       cmp       dword ptr [rbp-34],0
       je        short M01_L38
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L40
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L39
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       je        short M01_L38
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E96A48]
M01_L38:
       nop
       add       rsp,28
       ret
M01_L39:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96A30]
       int       3
M01_L40:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1187
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.Threading.Monitor.Enter_Slowpath(System.Object)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp+10],rcx
       lea       rcx,[rbp+10]
       mov       [rbp-98],rcx
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       rcx,[rbp-98]
       call      qword ptr [rax]
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 105
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M07_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M07_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
M07_L00:
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       jne       short M07_L03
M07_L01:
       xor       edx,edx
M07_L02:
       mov       rax,rdx
       ret
M07_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       jmp       short M07_L00
; Total bytes of code 86
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp-40],rcx
       mov       [rbp+10],rcx
       mov       rbx,rdx
       mov       esi,r8d
       mov       edi,r9d
       xor       eax,eax
       mov       [rbp-48],eax
       mov       rax,[rcx+8]
       mov       rax,[rax+18]
       cmp       dword ptr [rax+8],0
       jbe       near ptr M08_L15
       mov       rcx,[rax+10]
       call      qword ptr [7FFA759A0078]; Precode of System.Threading.Monitor.Enter(System.Object)
       mov       dword ptr [rbp-48],1
       mov       rcx,[rbp+10]
       cmp       rbx,[rcx+8]
       jne       near ptr M08_L18
       mov       rax,[rbx+10]
       mov       r14d,[rax+8]
       xor       r15d,r15d
       test      dil,dil
       jne       near ptr M08_L13
M08_L00:
       test      sil,sil
       je        short M08_L02
       test      r15,r15
       jne       short M08_L01
       mov       rcx,[rbp+10]
       call      qword ptr [7FFA759A08F8]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetCountNoLocks()
       mov       rcx,[rbx+10]
       mov       ecx,[rcx+8]
       shr       ecx,2
       cmp       eax,ecx
       jl        near ptr M08_L12
M08_L01:
       mov       rax,[rbx+10]
       mov       eax,[rax+8]
       add       eax,eax
       js        near ptr M08_L17
       mov       ecx,eax
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       r14d,eax
       call      qword ptr [7FFA7599FF68]
       cmp       eax,r14d
       jl        near ptr M08_L17
M08_L02:
       mov       rsi,[rbx+18]
       mov       rdi,rsi
       mov       rcx,[rbp+10]
       cmp       byte ptr [rcx+18],0
       je        short M08_L04
       cmp       dword ptr [rsi+8],400
       jge       short M08_L04
       mov       eax,[rsi+8]
       add       eax,eax
       movsxd    rcx,eax
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r8d,[rsi+8]
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FFA7599FF50]
       mov       rax,[rbx+18]
       mov       esi,[rax+8]
       mov       r13d,[rdi+8]
       cmp       r13d,esi
       jle       short M08_L04
M08_L03:
       call      qword ptr [7FFA7599FE68]
       mov       r8,rax
       movsxd    rdx,esi
       mov       rcx,rdi
       call      qword ptr [7FFA7599F2B0]; Precode of System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       inc       esi
       cmp       r13d,esi
       jg        short M08_L03
M08_L04:
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599FA10]
       mov       rcx,rax
       movsxd    rdx,r14d
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       rsi,rax
       mov       [rbp-60],rsi
       mov       ecx,[rdi+8]
       call      qword ptr [7FFA7599FF18]
       mov       r14,rax
       mov       r12,r15
       test      r12,r12
       jne       short M08_L05
       mov       r12,[rbx+8]
M08_L05:
       mov       rcx,r13
       call      qword ptr [7FFA7599F760]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-78],rax
       lea       rcx,[rax+10]
       mov       rdx,rsi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+20]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       ecx,[rsi+8]
       xor       edx,edx
       div       rcx
       inc       rax
       mov       r12,[rbp-78]
       mov       [r12+28],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F728]
       mov       rcx,rax
       lea       r8,[rbp-48]
       mov       rdx,rbx
       call      qword ptr [7FFA759A0918]
       mov       rbx,[rbx+10]
       xor       eax,eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jg        near ptr M08_L10
M08_L06:
       mov       rsi,[rbp-60]
       mov       eax,[rsi+8]
       xor       edx,edx
       div       dword ptr [rdi+8]
       mov       ecx,1
       cmp       eax,1
       cmovg     ecx,eax
       mov       rax,[rbp+10]
       mov       [rax+10],ecx
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M08_L18
M08_L07:
       test      r15,r15
       jne       near ptr M08_L11
       mov       [rbp-68],rdx
       mov       r8d,[rdx+20]
M08_L08:
       mov       rdx,[rbp-68]
       mov       r10,[rdx+18]
       mov       [rbp-80],r10
       mov       rcx,[r12+10]
       mov       [rbp-4C],r8d
       mov       r9d,r8d
       imul      r9,[r12+28]
       shr       r9,20
       inc       r9
       mov       r11d,[rcx+8]
       mov       esi,r11d
       imul      r9,rsi
       shr       r9,20
       mov       rsi,[r12+18]
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rsi+8]
       mov       esi,edx
       cmp       r9d,r11d
       jae       near ptr M08_L15
       mov       eax,r9d
       lea       rax,[rcx+rax*8+10]
       mov       [rbp-70],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F748]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-88],rax
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       r8,[r8+10]
       mov       [rbp-90],r8
       mov       r10,[rbp-70]
       mov       r9,[r10]
       mov       [rbp-98],r9
       lea       rcx,[rax+8]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+10]
       mov       rdx,[rbp-90]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+18]
       mov       rdx,[rbp-98]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       mov       ecx,[rbp-4C]
       mov       [rax+20],ecx
       mov       rcx,[rbp-70]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       cmp       esi,[r14+8]
       jae       near ptr M08_L15
       mov       eax,esi
       lea       rax,[r14+rax*4+10]
       mov       edx,[rax]
       add       edx,1
       jo        near ptr M08_L16
       mov       [rax],edx
       mov       rsi,[rbp-80]
       test      rsi,rsi
       mov       rdx,rsi
       jne       near ptr M08_L07
M08_L09:
       mov       rax,[rbp-58]
       inc       eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jle       near ptr M08_L06
M08_L10:
       mov       [rbp-58],rax
       mov       rdx,[rbx+rax*8+10]
       test      rdx,rdx
       jne       near ptr M08_L07
       jmp       short M08_L09
M08_L11:
       mov       [rbp-68],rdx
       mov       rcx,[rbp+10]
       mov       rcx,[rcx]
       call      qword ptr [7FFA7599FBD8]
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       rcx,r15
       mov       r11,rax
       call      qword ptr [rax]
       mov       r8d,eax
       jmp       near ptr M08_L08
M08_L12:
       mov       rcx,[rbp+10]
       mov       eax,[rcx+10]
       add       eax,eax
       mov       [rcx+10],eax
       test      eax,eax
       jge       near ptr M08_L18
       jmp       short M08_L14
M08_L13:
       mov       rcx,[rbx+8]
       call      qword ptr [7FFA7599FF30]
       mov       rdi,rax
       test      rdi,rdi
       je        near ptr M08_L00
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599F550]
       mov       r15,rax
       mov       rcx,rdi
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       call      qword ptr [r11]
       mov       rdx,rax
       mov       rcx,r15
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
       mov       r15,rax
       jmp       near ptr M08_L00
M08_L14:
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       short M08_L18
M08_L15:
       call      qword ptr [7FFA7599F290]
       int       3
M08_L16:
       call      qword ptr [7FFA7599F288]
       int       3
M08_L17:
       call      qword ptr [7FFA7599FF68]
       mov       r14d,eax
       mov       rcx,[rbp+10]
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       near ptr M08_L02
M08_L18:
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
       sub       rsp,28
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,28
       ret
; Total bytes of code 1137
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M09_L01
       test      rsi,rsi
       je        short M09_L02
       cmp       [rsi+8],ebx
       jl        short M09_L02
       add       rsi,10
M09_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M09_L00
M09_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M09_L02:
       mov       ecx,[rsi+8]
M09_L03:
       cmp       edi,[rsi+8]
       jae       short M09_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M09_L03
       jmp       short M09_L01
M09_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.GetItem_Typed_String()
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       rbx,[rcx+8]
       cmp       [rbx],bl
       mov       rsi,19980206748
       mov       edi,0C
       mov       ebp,0A
M00_L00:
       movzx     ecx,word ptr [rsi+rdi]
       cmp       ecx,100
       jge       near ptr M00_L16
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M00_L17
M00_L01:
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        near ptr M00_L18
       mov       rbx,[rcx+8]
       mov       rdi,[rbx+8]
       cmp       byte ptr [rcx+19],0
       jne       near ptr M00_L08
       mov       rcx,rdi
       mov       r11,7FF9B39305D0
       mov       rdx,rsi
       call      qword ptr [r11]
       mov       ebp,eax
M00_L02:
       mov       rcx,[rbx+10]
       mov       edx,ebp
       imul      rdx,[rbx+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M00_L30
       mov       edx,edx
       mov       rbx,[rcx+rdx*8+10]
       test      rbx,rbx
       je        near ptr M00_L28
       test      rdi,rdi
       je        near ptr M00_L14
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rdi],rcx
       jne       near ptr M00_L14
M00_L03:
       cmp       ebp,[rbx+20]
       jne       near ptr M00_L20
       mov       rdx,[rbx+8]
       mov       rcx,19980206748
       cmp       rdx,rcx
       jne       short M00_L10
       mov       eax,1
M00_L04:
       test      eax,eax
       je        near ptr M00_L20
M00_L05:
       mov       rdx,[rbx+10]
       mov       ecx,1
M00_L06:
       test      ecx,ecx
       je        near ptr M00_L18
       mov       rax,rdx
       test      rax,rax
       je        short M00_L07
       mov       rcx,offset MT_System.String
       cmp       [rax],rcx
       jne       near ptr M00_L29
M00_L07:
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M00_L08:
       test      byte ptr [7FF9B3EB5168],1
       je        near ptr M00_L19
M00_L09:
       mov       r8,[7FF9B392B118]
       mov       edx,14
       mov       r9,r8
       shr       r9,20
       lea       rcx,[rsi+0C]
       call      qword ptr [7FF9B3E66C70]
       mov       ebp,eax
       jmp       near ptr M00_L02
M00_L10:
       test      rdx,rdx
       je        short M00_L13
       cmp       dword ptr [rdx+8],0A
       jne       short M00_L13
       lea       rcx,[rdx+0C]
       mov       rax,19980206754
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M00_L11
       mov       rdx,rax
       call      qword ptr [7FF9B39EC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M00_L12
M00_L11:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M00_L12:
       jmp       near ptr M00_L04
M00_L13:
       xor       eax,eax
       jmp       near ptr M00_L04
M00_L14:
       cmp       ebp,[rbx+20]
       jne       near ptr M00_L27
       mov       rdx,[rbx+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rdi],rcx
       jne       near ptr M00_L21
       mov       rcx,19980206748
       cmp       rdx,rcx
       jne       near ptr M00_L22
       jmp       near ptr M00_L26
M00_L15:
       test      eax,eax
       je        near ptr M00_L27
       jmp       near ptr M00_L05
M00_L16:
       call      qword ptr [7FF9B3E66B98]
       test      eax,eax
       je        near ptr M00_L01
M00_L17:
       add       rdi,2
       dec       ebp
       jne       near ptr M00_L00
       mov       ecx,0F57
       mov       rdx,7FF9B3C58428
       call      qword ptr [7FF9B39EF210]
       mov       rbx,rax
       mov       ecx,0AB09
       mov       rdx,7FF9B3D75360
       call      qword ptr [7FF9B39EF210]
       mov       rdx,rax
       mov       rcx,rbx
       call      qword ptr [7FF9B3E66B38]
       int       3
M00_L18:
       xor       eax,eax
       jmp       near ptr M00_L07
M00_L19:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B39E5740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M00_L09
M00_L20:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       near ptr M00_L03
       jmp       near ptr M00_L28
M00_L21:
       mov       rcx,rdi
       mov       r8,19980206748
       mov       r11,7FF9B39305C8
       call      qword ptr [r11]
       jmp       near ptr M00_L15
M00_L22:
       test      rdx,rdx
       je        short M00_L25
       mov       ecx,[rdx+8]
       cmp       ecx,0A
       jne       short M00_L25
       add       rdx,0C
       mov       rax,19980206754
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M00_L23
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B39EC330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M00_L24
M00_L23:
       mov       rcx,rdx
       mov       r8,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r11,[r8]
       xor       rdx,r11
       xor       rcx,[r8+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M00_L24:
       jmp       near ptr M00_L15
M00_L25:
       xor       eax,eax
       jmp       near ptr M00_L15
M00_L26:
       mov       eax,1
       jmp       near ptr M00_L15
M00_L27:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       near ptr M00_L14
M00_L28:
       xor       edx,edx
       xor       ecx,ecx
       jmp       near ptr M00_L06
M00_L29:
       call      qword ptr [7FF9B39E6328]; System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       int       3
M00_L30:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 810
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M01_L06
       cmp       rcx,rdx
       je        short M01_L04
       cmp       r8,10
       jae       short M01_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M01_L00:
       ret
M01_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M01_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M01_L05
M01_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M01_L10
M01_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M01_L05
M01_L04:
       mov       eax,1
       ret
M01_L05:
       xor       eax,eax
       ret
M01_L06:
       cmp       r8,4
       jb        short M01_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M01_L00
M01_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M01_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M01_L08:
       test      r8b,1
       je        short M01_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M01_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M01_L00
M01_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M01_L05
       jmp       near ptr M01_L02
; Total bytes of code 237
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-10]
       mov       rdx,rax
       test      dl,1
       jne       short M02_L00
       ret
M02_L00:
       jmp       qword ptr [7FF9B3BEE6E8]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.ChkCastClass(Void*, System.Object)
       test      rdx,rdx
       je        short M03_L00
       cmp       [rdx],rcx
       jne       short M03_L01
M03_L00:
       mov       rax,rdx
       ret
M03_L01:
       jmp       qword ptr [7FF9B3BE4D20]; System.Runtime.CompilerServices.CastHelpers.ChkCastClassSpecial(Void*, System.Object)
; Total bytes of code 20
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
       push      rbx
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       mov       rcx,rbx
       call      qword ptr [7FF964232DF0]; Precode of System.Runtime.CompilerServices.InitHelpers.InitClassSlow(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rbx+20]
       mov       rax,[rax-10]
       mov       [rsp+28],rax
       mov       rax,[rsp+28]
       and       rax,0FFFFFFFFFFFFFFFE
       xor       ecx,ecx
       mov       [rsp+28],rcx
       add       rsp,30
       pop       rbx
       ret
; Total bytes of code 59
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.ChkCastClassSpecial(Void*, System.Object)
       mov       rax,[rdx]
       mov       rax,[rax+10]
       cmp       rax,rcx
       jne       short M05_L01
M05_L00:
       mov       rax,rdx
       ret
M05_L01:
       test      rax,rax
       je        short M05_L04
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L00
       jmp       short M05_L03
M05_L02:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L00
       jmp       short M05_L01
M05_L03:
       test      rax,rax
       je        short M05_L04
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L00
       test      rax,rax
       je        short M05_L04
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L00
       test      rax,rax
       jne       short M05_L02
M05_L04:
       lea       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       jmp       qword ptr [rax]
; Total bytes of code 86
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.GetItem_Typed_Bool()
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       rbx,[rcx+8]
       cmp       [rbx],bl
       mov       rsi,1EB00206810
       mov       edi,0C
       mov       ebp,11
M00_L00:
       movzx     ecx,word ptr [rsi+rdi]
       cmp       ecx,100
       jge       near ptr M00_L17
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M00_L18
M00_L01:
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        near ptr M00_L19
       mov       rbx,[rcx+8]
       mov       rdi,[rbx+8]
       cmp       byte ptr [rcx+19],0
       jne       near ptr M00_L09
       mov       rcx,rdi
       mov       r11,7FF9B39605D0
       mov       rdx,rsi
       call      qword ptr [r11]
       mov       ebp,eax
M00_L02:
       mov       rcx,[rbx+10]
       mov       edx,ebp
       imul      rdx,[rbx+28]
       shr       rdx,20
       inc       rdx
       mov       r8d,[rcx+8]
       mov       eax,r8d
       imul      rdx,rax
       shr       rdx,20
       cmp       edx,r8d
       jae       near ptr M00_L30
       mov       edx,edx
       mov       rbx,[rcx+rdx*8+10]
       test      rbx,rbx
       je        near ptr M00_L29
       test      rdi,rdi
       je        near ptr M00_L15
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rdi],rcx
       jne       near ptr M00_L15
M00_L03:
       cmp       ebp,[rbx+20]
       jne       near ptr M00_L21
       mov       rdx,[rbx+8]
       mov       rcx,1EB00206810
       cmp       rdx,rcx
       jne       near ptr M00_L11
       mov       eax,1
M00_L04:
       test      eax,eax
       je        near ptr M00_L21
M00_L05:
       mov       rbx,[rbx+10]
       mov       edx,1
M00_L06:
       test      edx,edx
       je        near ptr M00_L19
       mov       rdx,offset MT_System.Boolean
       cmp       [rbx],rdx
       je        short M00_L07
       mov       rdx,rbx
       mov       rcx,offset MT_System.Boolean
       call      System.Runtime.CompilerServices.CastHelpers.Unbox(System.Runtime.CompilerServices.MethodTable*, System.Object)
M00_L07:
       movzx     eax,byte ptr [rbx+8]
M00_L08:
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M00_L09:
       test      byte ptr [7FF9B3EE48B8],1
       je        near ptr M00_L20
M00_L10:
       mov       r8,[7FF9B395B118]
       mov       edx,22
       mov       r9,r8
       shr       r9,20
       lea       rcx,[rsi+0C]
       call      qword ptr [7FF9B3E96BF8]
       mov       ebp,eax
       jmp       near ptr M00_L02
M00_L11:
       test      rdx,rdx
       je        short M00_L14
       cmp       dword ptr [rdx+8],11
       jne       short M00_L14
       lea       rcx,[rdx+0C]
       mov       rax,1EB0020681C
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M00_L12
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M00_L13
M00_L12:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M00_L13:
       jmp       near ptr M00_L04
M00_L14:
       xor       eax,eax
       jmp       near ptr M00_L04
M00_L15:
       cmp       ebp,[rbx+20]
       jne       near ptr M00_L28
       mov       rdx,[rbx+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rdi],rcx
       jne       near ptr M00_L22
       mov       rcx,1EB00206810
       cmp       rdx,rcx
       jne       near ptr M00_L23
       jmp       near ptr M00_L27
M00_L16:
       test      eax,eax
       je        near ptr M00_L28
       jmp       near ptr M00_L05
M00_L17:
       call      qword ptr [7FF9B3E96B08]
       test      eax,eax
       je        near ptr M00_L01
M00_L18:
       add       rdi,2
       dec       ebp
       jne       near ptr M00_L00
       mov       ecx,0FBB
       mov       rdx,7FF9B3C88428
       call      qword ptr [7FF9B3A1F210]
       mov       rbx,rax
       mov       ecx,0AB09
       mov       rdx,7FF9B3DA5360
       call      qword ptr [7FF9B3A1F210]
       mov       rdx,rax
       mov       rcx,rbx
       call      qword ptr [7FF9B3E96AA8]
       int       3
M00_L19:
       xor       eax,eax
       jmp       near ptr M00_L08
M00_L20:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B3A15740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M00_L10
M00_L21:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       near ptr M00_L03
       jmp       near ptr M00_L29
M00_L22:
       mov       rcx,rdi
       mov       r8,1EB00206810
       mov       r11,7FF9B39605C8
       call      qword ptr [r11]
       jmp       near ptr M00_L16
M00_L23:
       test      rdx,rdx
       je        short M00_L26
       mov       ecx,[rdx+8]
       cmp       ecx,11
       jne       short M00_L26
       add       rdx,0C
       mov       rax,1EB0020681C
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M00_L24
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M00_L25
M00_L24:
       mov       rcx,rdx
       mov       r8,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r11,[r8]
       xor       rdx,r11
       xor       rcx,[r8+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M00_L25:
       jmp       near ptr M00_L16
M00_L26:
       xor       eax,eax
       jmp       near ptr M00_L16
M00_L27:
       mov       eax,1
       jmp       near ptr M00_L16
M00_L28:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       near ptr M00_L15
M00_L29:
       xor       ebx,ebx
       xor       edx,edx
       jmp       near ptr M00_L06
M00_L30:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 817
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.Unbox(System.Runtime.CompilerServices.MethodTable*, System.Object)
       cmp       [rdx],rcx
       jne       short M01_L00
       lea       rax,[rdx+8]
       ret
M01_L00:
       jmp       qword ptr [7FF9B3E96A90]; System.Runtime.CompilerServices.CastHelpers.Unbox_Helper(System.Runtime.CompilerServices.MethodTable*, System.Object)
; Total bytes of code 16
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M02_L06
       cmp       rcx,rdx
       je        short M02_L04
       cmp       r8,10
       jae       short M02_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M02_L00:
       ret
M02_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M02_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M02_L05
M02_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M02_L10
M02_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M02_L05
M02_L04:
       mov       eax,1
       ret
M02_L05:
       xor       eax,eax
       ret
M02_L06:
       cmp       r8,4
       jb        short M02_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M02_L00
M02_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M02_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M02_L08:
       test      r8b,1
       je        short M02_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M02_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M02_L00
M02_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M02_L05
       jmp       near ptr M02_L02
; Total bytes of code 237
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-10]
       mov       rdx,rax
       test      dl,1
       jne       short M03_L00
       ret
M03_L00:
       jmp       qword ptr [7FF9B3C1E6E8]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.Unbox_Helper(System.Runtime.CompilerServices.MethodTable*, System.Object)
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       rbx,rcx
       mov       rsi,rdx
       mov       rdi,[rsi]
       mov       ecx,[rbx]
       and       ecx,0E0000
       cmp       ecx,60000
       jne       short M04_L01
       mov       ecx,[rdi]
       and       ecx,0E0000
       cmp       ecx,60000
       jne       short M04_L01
       mov       rcx,rbx
       call      qword ptr [7FF964232EF8]
       mov       ebp,eax
       mov       rcx,rdi
       call      qword ptr [7FF964232EF8]
       cmp       ebp,eax
       jne       short M04_L01
M04_L00:
       lea       rax,[rsi+8]
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M04_L01:
       mov       rcx,rbx
       mov       rdx,rdi
       call      qword ptr [7FF964232DC0]
       test      eax,eax
       jne       short M04_L00
       mov       rcx,rsi
       mov       rdx,rbx
       call      qword ptr [7FF964232D20]
       int       3
; Total bytes of code 115
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
       push      rbx
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       mov       rcx,rbx
       call      qword ptr [7FF964232DF0]; Precode of System.Runtime.CompilerServices.InitHelpers.InitClassSlow(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rbx+20]
       mov       rax,[rax-10]
       mov       [rsp+28],rax
       mov       rax,[rsp+28]
       and       rax,0FFFFFFFFFFFFFFFE
       xor       ecx,ecx
       mov       [rsp+28],rcx
       add       rsp,30
       pop       rbx
       ret
; Total bytes of code 59
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.SetItem_Typed()
       push      rbp
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0B8
       lea       rbp,[rsp+0E0]
       xor       eax,eax
       mov       [rbp-98],rax
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       mov       rbx,[rcx+8]
       cmp       [rbx],bl
       mov       rsi,23C802D92F8
       mov       edi,0C
       mov       r14d,8
M00_L00:
       movzx     ecx,word ptr [rsi+rdi]
       cmp       ecx,100
       jge       near ptr M00_L40
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M00_L41
M00_L01:
       mov       rdi,[rbx+8]
       test      rdi,rdi
       je        near ptr M00_L42
M00_L02:
       mov       rcx,23C8B4013A8
       mov       rbx,[rcx]
       test      rbx,rbx
       je        near ptr M00_L44
M00_L03:
       mov       rcx,23C8B4013B0
       mov       rcx,[rcx]
       test      rcx,rcx
       je        near ptr M00_L45
M00_L04:
       mov       [rbp-60],rdi
       mov       [rbp-50],rbx
       mov       [rbp-58],rcx
       mov       rcx,[rbp-60]
       cmp       [rcx],cl
       mov       r11,[rcx+8]
       mov       [rbp-68],r11
       mov       r11,[rbp-68]
       mov       r11,[r11+8]
       mov       [rbp-70],r11
       cmp       byte ptr [rcx+19],0
       jne       near ptr M00_L32
       mov       rcx,[rbp-70]
       mov       r11,7FF9B39605D0
       mov       rdx,rsi
       call      qword ptr [r11]
M00_L05:
       mov       [rbp-2C],eax
M00_L06:
       mov       rcx,[rbp-68]
       mov       rbx,[rcx+8]
       mov       rcx,[rbp-68]
       mov       rcx,[rcx+10]
       mov       rdx,[rbp-68]
       mov       r8d,[rbp-2C]
       imul      r8,[rdx+28]
       shr       r8,20
       inc       r8
       mov       edx,[rcx+8]
       mov       eax,edx
       imul      r8,rax
       shr       r8,20
       cmp       r8d,edx
       jae       near ptr M00_L61
       mov       edx,r8d
       mov       rsi,[rcx+rdx*8+10]
       test      rsi,rsi
       je        near ptr M00_L55
       test      rbx,rbx
       je        near ptr M00_L38
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rbx],rcx
       jne       near ptr M00_L38
M00_L07:
       mov       ecx,[rbp-2C]
       cmp       ecx,[rsi+20]
       jne       near ptr M00_L47
       mov       rdx,[rsi+8]
       mov       rcx,23C802D92F8
       cmp       rdx,rcx
       jne       near ptr M00_L34
       mov       eax,1
M00_L08:
       test      eax,eax
       je        near ptr M00_L47
M00_L09:
       mov       rbx,[rsi+10]
       mov       rdx,offset Excalibur.Dispatch.Messaging.MessageContext+<>c__181`1[[System.__Canon, System.Private.CoreLib]].<SetItem>b__181_1(System.String, System.Object, System.__Canon)
       mov       rcx,[rbp-58]
       cmp       [rcx+18],rdx
       jne       near ptr M00_L56
       mov       rsi,23C802D9320
M00_L10:
       mov       [rbp-78],rsi
       mov       rax,[rbp-68]
       mov       [rbp-80],rax
       mov       [rbp-98],rbx
       mov       rax,[rbp-80]
       mov       rax,[rax+8]
       mov       [rbp-88],rax
       mov       eax,[rbp-2C]
       mov       [rbp-3C],eax
M00_L11:
       mov       rax,[rbp-80]
       mov       rcx,[rax+18]
       mov       r8,rcx
       mov       rax,[rbp-80]
       mov       r10,[rax+10]
       mov       rax,[rbp-80]
       mov       r9d,[rbp-3C]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       cmp       r9d,r11d
       jae       near ptr M00_L61
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       cmp       edx,[r8+8]
       jae       near ptr M00_L61
       mov       ecx,edx
       mov       rcx,[r8+rcx*8+10]
       mov       [rbp-90],rcx
       xor       ecx,ecx
       mov       [rbp-40],ecx
       cmp       qword ptr [rbp-90],0
       je        near ptr M00_L16
       mov       rcx,[rbp-90]
       call      00007FFA135C0070
       test      eax,eax
       jne       short M00_L12
       mov       rcx,[rbp-90]
       call      qword ptr [7FF9B3E96C40]
M00_L12:
       mov       dword ptr [rbp-40],1
       mov       rdx,[rbp-80]
       mov       rcx,[rbp-60]
       cmp       rdx,[rcx+8]
       jne       near ptr M00_L17
       mov       rbx,[rbx]
       test      rbx,rbx
       je        near ptr M00_L27
M00_L13:
       mov       edx,[rbp-3C]
       cmp       edx,[rbx+20]
       jne       near ptr M00_L20
       mov       rdx,[rbx+8]
       mov       rcx,[rbp-88]
       mov       r8,23C802D92F8
       mov       r11,7FF9B39605F0
       call      qword ptr [r11]
       test      eax,eax
       je        near ptr M00_L20
       mov       rsi,[rbx+10]
       test      rsi,rsi
       je        near ptr M00_L28
       cmp       qword ptr [rbp-98],0
       je        near ptr M00_L27
       mov       rdx,offset MT_System.String
       cmp       [rsi],rdx
       jne       near ptr M00_L26
       cmp       rsi,[rbp-98]
       jne       near ptr M00_L21
       mov       edi,1
M00_L14:
       test      edi,edi
       je        near ptr M00_L27
M00_L15:
       lea       rcx,[rbx+10]
       mov       rdx,[rbp-78]
       call      CORINFO_HELP_ASSIGN_REF
       mov       ebx,1
       jmp       near ptr M00_L29
M00_L16:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
M00_L17:
       mov       rcx,[rbp-60]
       mov       rdx,[rcx+8]
       mov       [rbp-80],rdx
       mov       rdx,[rbp-88]
       mov       r11,[rbp-80]
       cmp       rdx,[r11+8]
       je        near ptr M00_L57
       mov       rdx,[rbp-80]
       mov       rdx,[rdx+8]
       mov       [rbp-88],rdx
       cmp       byte ptr [rcx+19],0
       jne       short M00_L18
       mov       rcx,[rbp-88]
       mov       rdx,23C802D92F8
       mov       r11,7FF9B39605E8
       call      qword ptr [r11]
       jmp       short M00_L19
M00_L18:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B3A15740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       r8,[7FF9B395B118]
       mov       edx,10
       mov       r9,r8
       shr       r9,20
       mov       rcx,23C802D9304
       call      qword ptr [7FF9B3E96DA8]
M00_L19:
       mov       [rbp-3C],eax
       mov       rcx,[rbp-60]
       jmp       near ptr M00_L57
M00_L20:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       near ptr M00_L13
       jmp       near ptr M00_L27
M00_L21:
       mov       rdx,[rbp-98]
       mov       rcx,offset MT_System.String
       call      qword ptr [7FF9B3A16850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       jne       short M00_L23
M00_L22:
       xor       edi,edi
       jmp       near ptr M00_L14
M00_L23:
       mov       ecx,[rsi+8]
       cmp       ecx,[rax+8]
       jne       short M00_L22
       lea       rcx,[rsi+0C]
       lea       rdx,[rax+0C]
       mov       r8d,[rsi+8]
       add       r8d,r8d
       cmp       r8,0A
       jne       short M00_L24
       mov       rax,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rdx]
       xor       rax,r8
       xor       rcx,[rdx+2]
       or        rcx,rax
       sete      dil
       movzx     edi,dil
       jmp       short M00_L25
M00_L24:
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       mov       edi,eax
M00_L25:
       jmp       near ptr M00_L14
M00_L26:
       mov       rcx,rsi
       mov       rdx,[rbp-98]
       mov       rax,[rsi]
       mov       rax,[rax+40]
       call      qword ptr [rax+10]
       mov       edi,eax
       jmp       near ptr M00_L14
M00_L27:
       xor       ebx,ebx
       jmp       short M00_L29
M00_L28:
       cmp       qword ptr [rbp-98],0
       jne       short M00_L27
       jmp       near ptr M00_L15
M00_L29:
       mov       rcx,[rbp-90]
       call      00007FFA135EBB70
       test      eax,eax
       je        short M00_L30
       mov       ecx,eax
       mov       rdx,[rbp-90]
       call      qword ptr [7FF9B3E96C58]
M00_L30:
       test      ebx,ebx
       je        near ptr M00_L58
M00_L31:
       xor       ecx,ecx
       mov       [rbp-38],rcx
       add       rsp,0B8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       pop       rbp
       ret
M00_L32:
       test      byte ptr [7FF9B3EE76E8],1
       je        near ptr M00_L46
M00_L33:
       mov       r8,[7FF9B395B118]
       mov       edx,10
       mov       r9,r8
       shr       r9,20
       lea       rcx,[rsi+0C]
       call      qword ptr [7FF9B3E96DA8]
       jmp       near ptr M00_L05
M00_L34:
       test      rdx,rdx
       je        short M00_L37
       cmp       dword ptr [rdx+8],8
       jne       short M00_L37
       lea       rcx,[rdx+0C]
       mov       rax,23C802D9304
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r8d,edx
       cmp       r8,0A
       je        short M00_L35
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M00_L36
M00_L35:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rax]
       xor       rdx,r8
       xor       rcx,[rax+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M00_L36:
       jmp       near ptr M00_L08
M00_L37:
       xor       eax,eax
       jmp       near ptr M00_L08
M00_L38:
       mov       ecx,[rbp-2C]
       cmp       ecx,[rsi+20]
       jne       near ptr M00_L54
       mov       rdx,[rsi+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rbx],rcx
       jne       near ptr M00_L48
       mov       rcx,23C802D92F8
       cmp       rdx,rcx
       jne       near ptr M00_L49
       jmp       near ptr M00_L53
M00_L39:
       test      eax,eax
       je        near ptr M00_L54
       jmp       near ptr M00_L09
M00_L40:
       call      qword ptr [7FF9B3E96CE8]
       test      eax,eax
       je        near ptr M00_L01
M00_L41:
       add       rdi,2
       dec       r14d
       jne       near ptr M00_L00
       mov       ecx,10AB
       mov       rdx,7FF9B3C88428
       call      qword ptr [7FF9B3A1F210]
       mov       rbx,rax
       mov       ecx,0AB09
       mov       rdx,7FF9B3DA5360
       call      qword ptr [7FF9B3A1F210]
       mov       rdx,rax
       mov       rcx,rbx
       call      qword ptr [7FF9B3E96C88]
       int       3
M00_L42:
       mov       rdi,[rbx+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3DB5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-0A0],rdi
       mov       [rbp-44],r14d
       cmp       qword ptr [rbx+8],0
       jne       short M00_L43
       mov       rcx,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rcx,23C8B400068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r15
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D2C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r15
       call      CORINFO_HELP_ASSIGN_REF
M00_L43:
       mov       rbx,[rbx+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3DB5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rdi,rbx
       jmp       near ptr M00_L02
M00_L44:
       mov       rcx,offset MT_System.Func<System.String, System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rdx,23C8B4013A0
       mov       rdx,[rdx]
       mov       rcx,rbx
       mov       r8,offset Excalibur.Dispatch.Messaging.MessageContext+<>c__181`1[[System.__Canon, System.Private.CoreLib]].<SetItem>b__181_0(System.String, System.__Canon)
       call      qword ptr [7FF9B3A16BB0]; System.MulticastDelegate.CtorClosed(System.Object, IntPtr)
       mov       rcx,23C8B4013A8
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M00_L03
M00_L45:
       mov       rcx,offset MT_System.Func<System.String, System.Object, System.String, System.Object>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rdx,23C8B4013A0
       mov       rdx,[rdx]
       mov       rcx,r14
       mov       r8,offset Excalibur.Dispatch.Messaging.MessageContext+<>c__181`1[[System.__Canon, System.Private.CoreLib]].<SetItem>b__181_1(System.String, System.Object, System.__Canon)
       call      qword ptr [7FF9B3A16BB0]; System.MulticastDelegate.CtorClosed(System.Object, IntPtr)
       mov       rcx,23C8B4013B0
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,r14
       jmp       near ptr M00_L04
M00_L46:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B3A15740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M00_L33
M00_L47:
       mov       rsi,[rsi+18]
       test      rsi,rsi
       jne       near ptr M00_L07
       jmp       near ptr M00_L55
M00_L48:
       mov       rcx,rbx
       mov       r8,23C802D92F8
       mov       r11,7FF9B39605D8
       call      qword ptr [r11]
       jmp       near ptr M00_L39
M00_L49:
       test      rdx,rdx
       je        short M00_L52
       mov       ecx,[rdx+8]
       cmp       ecx,8
       jne       short M00_L52
       add       rdx,0C
       mov       rax,23C802D9304
       add       ecx,ecx
       mov       r8d,ecx
       cmp       r8,0A
       je        short M00_L50
       mov       rcx,rdx
       mov       rdx,rax
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M00_L51
M00_L50:
       mov       rcx,rdx
       mov       r8,rax
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r11,[r8]
       xor       rdx,r11
       xor       rcx,[r8+2]
       or        rcx,rdx
       sete      al
       movzx     eax,al
M00_L51:
       jmp       near ptr M00_L39
M00_L52:
       xor       eax,eax
       jmp       near ptr M00_L39
M00_L53:
       mov       eax,1
       jmp       near ptr M00_L39
M00_L54:
       mov       rsi,[rsi+18]
       test      rsi,rsi
       jne       near ptr M00_L38
M00_L55:
       mov       rdx,23C802D92F8
       mov       r8,23C802D9320
       mov       rbx,[rbp-50]
       mov       rcx,[rbx+8]
       call      qword ptr [rbx+18]
       xor       r9d,r9d
       mov       [rsp+28],r9d
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-38]
       mov       [rsp+38],r9
       mov       [rsp+20],rax
       mov       r9d,[rbp-2C]
       shl       r9,20
       or        r9,1
       mov       rdx,[rbp-68]
       mov       r8,23C802D92F8
       mov       rcx,[rbp-60]
       call      qword ptr [7FF9B3D2EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       test      eax,eax
       je        short M00_L58
       jmp       near ptr M00_L31
M00_L56:
       mov       rdx,23C802D92F8
       mov       r8,rbx
       mov       r9,23C802D9320
       mov       rcx,[rcx+8]
       mov       rax,[rbp-58]
       call      qword ptr [rax+18]
       mov       rsi,rax
       mov       rcx,[rbp-58]
       jmp       near ptr M00_L10
M00_L57:
       call      M00_L63
       jmp       near ptr M00_L11
M00_L58:
       mov       rcx,[rbp-68]
       mov       rax,[rbp-60]
       cmp       rcx,[rax+8]
       je        near ptr M00_L06
       mov       rax,[rbp-60]
       mov       rcx,[rax+8]
       mov       [rbp-68],rcx
       mov       rcx,[rbp-70]
       mov       rdx,[rbp-68]
       cmp       rcx,[rdx+8]
       je        near ptr M00_L06
       mov       rcx,[rbp-68]
       mov       rcx,[rcx+8]
       mov       [rbp-70],rcx
       mov       rax,[rbp-60]
       cmp       byte ptr [rax+19],0
       jne       short M00_L59
       mov       rcx,[rbp-70]
       mov       rdx,23C802D92F8
       mov       r11,7FF9B39605F8
       call      qword ptr [r11]
       jmp       short M00_L60
M00_L59:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B3A15740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       r8,[7FF9B395B118]
       mov       edx,10
       mov       r9,r8
       shr       r9,20
       mov       rcx,23C802D9304
       call      qword ptr [7FF9B3E96DA8]
M00_L60:
       mov       [rbp-2C],eax
       jmp       near ptr M00_L06
M00_L61:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
       sub       rsp,48
       cmp       qword ptr [rbp-0A0],0
       je        short M00_L62
       mov       rcx,[rbp-0A0]
       mov       edx,[rbp-44]
       call      qword ptr [7FF9B3DB5620]; System.Threading.Lock.Exit(ThreadId)
M00_L62:
       nop
       add       rsp,48
       ret
M00_L63:
       sub       rsp,48
       cmp       dword ptr [rbp-40],0
       je        short M00_L64
       cmp       qword ptr [rbp-90],0
       je        short M00_L65
       mov       rcx,[rbp-90]
       call      00007FFA135EBB70
       test      eax,eax
       je        short M00_L64
       mov       ecx,eax
       mov       rdx,[rbp-90]
       call      qword ptr [7FF9B3E96C58]
M00_L64:
       nop
       add       rsp,48
       ret
M00_L65:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
; Total bytes of code 2394
```
```assembly
; Excalibur.Dispatch.Messaging.MessageContext+<>c__181`1[[System.__Canon, System.Private.CoreLib]].<SetItem>b__181_1(System.String, System.Object, System.__Canon)
       mov       rax,r9
       ret
; Total bytes of code 4
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-10]
       mov       rdx,rax
       test      dl,1
       jne       short M02_L00
       ret
M02_L00:
       jmp       qword ptr [7FF9B3C1E6D0]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M03_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M03_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M03_L02
M03_L00:
       test      rax,rax
       je        short M03_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M03_L02
       test      rax,rax
       je        short M03_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M03_L02
       test      rax,rax
       jne       short M03_L03
M03_L01:
       xor       edx,edx
M03_L02:
       mov       rax,rdx
       ret
M03_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M03_L02
       test      rax,rax
       je        short M03_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M03_L02
       jmp       short M03_L00
; Total bytes of code 86
```
```assembly
; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       cmp       r8,8
       jb        short M04_L06
       cmp       rcx,rdx
       je        short M04_L04
       cmp       r8,10
       jae       short M04_L01
       add       r8,0FFFFFFFFFFFFFFF8
       mov       rax,[rcx]
       sub       rax,[rdx]
       mov       rcx,[rcx+r8]
       sub       rcx,[rdx+r8]
       or        rax,rcx
       sete      al
       movzx     eax,al
M04_L00:
       ret
M04_L01:
       xor       eax,eax
       add       r8,0FFFFFFFFFFFFFFF0
       je        short M04_L03
       movups    xmm0,[rcx]
       movups    xmm1,[rdx]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M04_L05
M04_L02:
       add       rax,10
       cmp       r8,rax
       ja        short M04_L10
M04_L03:
       movups    xmm0,[rcx+r8]
       movups    xmm1,[rdx+r8]
       pcmpeqb   xmm0,xmm1
       pmovmskb  eax,xmm0
       cmp       eax,0FFFF
       jne       short M04_L05
M04_L04:
       mov       eax,1
       ret
M04_L05:
       xor       eax,eax
       ret
M04_L06:
       cmp       r8,4
       jb        short M04_L07
       add       r8,0FFFFFFFFFFFFFFFC
       mov       eax,[rcx]
       sub       eax,[rdx]
       mov       ecx,[rcx+r8]
       sub       ecx,[rdx+r8]
       or        eax,ecx
       sete      al
       movzx     eax,al
       jmp       short M04_L00
M04_L07:
       xor       eax,eax
       mov       r10,r8
       and       r10,2
       je        short M04_L08
       movzx     eax,word ptr [rcx]
       movzx     r9d,word ptr [rdx]
       sub       eax,r9d
M04_L08:
       test      r8b,1
       je        short M04_L09
       movzx     ecx,byte ptr [rcx+r10]
       movzx     edx,byte ptr [rdx+r10]
       sub       ecx,edx
       or        eax,ecx
M04_L09:
       test      eax,eax
       sete      al
       movzx     eax,al
       jmp       near ptr M04_L00
M04_L10:
       movups    xmm0,[rcx+rax]
       movups    xmm1,[rdx+rax]
       pcmpeqb   xmm0,xmm1
       pmovmskb  r10d,xmm0
       cmp       r10d,0FFFF
       jne       short M04_L05
       jmp       near ptr M04_L02
; Total bytes of code 237
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M05_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M05_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M05_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M05_L00:
       add       rsp,30
       pop       rbx
       ret
M05_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M05_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M06_L10
M06_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].AddOrUpdate[[System.__Canon, System.Private.CoreLib]](System.__Canon, System.Func`3<System.__Canon,System.__Canon,System.__Canon>, System.Func`4<System.__Canon,System.__Canon,System.__Canon,System.__Canon>, System.__Canon)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M06_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M06_L02
M06_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M06_L01
M06_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M06_L06
M06_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].AddOrUpdate[[System.__Canon, System.Private.CoreLib]](System.__Canon, System.Func`3<System.__Canon,System.__Canon,System.__Canon>, System.Func`4<System.__Canon,System.__Canon,System.__Canon,System.__Canon>, System.__Canon)]
       je        near ptr M06_L07
M06_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M06_L09
M06_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M06_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M06_L03
M06_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M06_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M06_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M06_L08:
       mov       r14,rax
       jmp       near ptr M06_L05
M06_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M06_L05
M06_L10:
       cmp       edi,0FFFFFFFF
       je        short M06_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].AddOrUpdate[[System.__Canon, System.Private.CoreLib]](System.__Canon, System.Func`3<System.__Canon,System.__Canon,System.__Canon>, System.Func`4<System.__Canon,System.__Canon,System.__Canon,System.__Canon>, System.__Canon)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M06_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M06_L00
M06_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M07_L02
       cmp       dword ptr [rcx+18],0
       jne       short M07_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M07_L03
M07_L00:
       add       rsp,28
       ret
M07_L01:
       dec       dword ptr [rcx+18]
       jmp       short M07_L00
M07_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M07_L03:
       call      qword ptr [7FF964230260]
       jmp       short M07_L00
; Total bytes of code 69
```
```assembly
; Excalibur.Dispatch.Messaging.MessageContext+<>c__181`1[[System.__Canon, System.Private.CoreLib]].<SetItem>b__181_0(System.String, System.__Canon)
       push      rbp
       mov       rbp,rsp
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       rax,[rbp+20]
       pop       rbp
       ret
; Total bytes of code 22
```
```assembly
; System.MulticastDelegate.CtorClosed(System.Object, IntPtr)
       push      rsi
       push      rbx
       sub       rsp,28
       mov       rbx,rcx
       mov       rsi,r8
       test      rdx,rdx
       je        short M09_L00
       lea       rcx,[rbx+8]
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbx+18],rsi
       add       rsp,28
       pop       rbx
       pop       rsi
       ret
M09_L00:
       call      qword ptr [7FF9B3E9DC80]
       int       3
; Total bytes of code 44
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,50
       lea       rbp,[rsp+80]
       xor       eax,eax
       mov       [rbp-58],rax
       mov       [rbp-38],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       mov       rbx,[rbp+30]
       movzx     r9d,r9b
       mov       r8,[rbp+18]
       mov       r8,[r8+8]
       mov       [rbp-58],r8
       mov       esi,[rbp+2C]
       test      r9d,r9d
       jne       near ptr M10_L19
       cmp       byte ptr [rcx+19],0
       jne       near ptr M10_L18
       mov       rcx,[rcx]
       call      qword ptr [7FFA7599FBD0]
       mov       rcx,[rbp-58]
       mov       r11,rax
       mov       rdx,[rbp+20]
       call      qword ptr [rax]
M10_L00:
       mov       [rbp-3C],eax
M10_L01:
       mov       rax,[rbp+18]
       mov       rcx,[rax+18]
       mov       [rbp-60],rcx
       mov       r8,[rbp+10]
       cmp       [r8],r8d
       mov       rax,[rbp+18]
       mov       r10,[rax+10]
       mov       rax,[rbp+18]
       mov       r9d,[rbp-3C]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       mov       [rbp-40],edx
       cmp       r9d,r11d
       jae       near ptr M10_L25
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       xor       esi,esi
       xor       edi,edi
       xor       ecx,ecx
       mov       [rbp-48],ecx
       cmp       byte ptr [rbp+40],0
       je        short M10_L02
       mov       rcx,[rbp-60]
       mov       ecx,[rcx+8]
       cmp       [rbp-40],ecx
       jae       near ptr M10_L12
       mov       rcx,[rbp-60]
       mov       edx,[rbp-40]
       mov       rcx,[rcx+rdx*8+10]
       lea       rdx,[rbp-48]
       call      qword ptr [7FFA759A0080]; Precode of System.Threading.Monitor.Enter(System.Object, Boolean ByRef)
M10_L02:
       mov       rcx,[rbp+18]
       mov       r8,[rbp+10]
       cmp       rcx,[r8+8]
       jne       near ptr M10_L09
       xor       r14d,r14d
       mov       r15,[rbx]
       test      r15,r15
       jne       near ptr M10_L05
M10_L03:
       mov       rcx,[r8]
       call      qword ptr [7FFA7599F740]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r15,rax
       mov       r13,[rbx]
       lea       rcx,[r15+8]
       mov       rdx,[rbp+20]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r15+10]
       mov       rdx,[rbp+30]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r15+18]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       ecx,[rbp-3C]
       mov       [r15+20],ecx
       mov       rcx,rbx
       mov       rdx,r15
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+20]
       mov       eax,[rcx+8]
       cmp       [rbp-40],eax
       jae       near ptr M10_L12
       mov       eax,[rbp-40]
       lea       rcx,[rcx+rax*4+10]
       mov       eax,[rcx]
       add       eax,1
       jo        near ptr M10_L13
       mov       [rcx],eax
       mov       r8,[rbp+10]
       cmp       eax,[r8+10]
       jg        near ptr M10_L15
M10_L04:
       cmp       r14d,64
       jbe       near ptr M10_L20
       jmp       near ptr M10_L16
M10_L05:
       mov       ecx,[rbp-3C]
       cmp       ecx,[r15+20]
       jne       short M10_L06
       mov       rcx,[r8]
       call      qword ptr [7FFA7599F6E8]
       mov       rcx,rax
       call      qword ptr [7FFA7599FDF0]
       mov       rdx,[r15+8]
       mov       rcx,[rbp-58]
       mov       r11,rax
       mov       r8,[rbp+20]
       call      qword ptr [rax]
       test      eax,eax
       mov       r8,[rbp+10]
       jne       short M10_L07
M10_L06:
       inc       r14d
       mov       r15,[r15+18]
       test      r15,r15
       jne       short M10_L05
       jmp       near ptr M10_L03
M10_L07:
       cmp       byte ptr [rbp+38],0
       je        near ptr M10_L14
       lea       rcx,[r15+10]
       mov       rdx,[rbp+30]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      qword ptr [7FFA7599F2A0]; CORINFO_HELP_CHECKED_ASSIGN_REF
M10_L08:
       xor       ecx,ecx
       mov       [rbp-4C],ecx
       jmp       near ptr M10_L17
M10_L09:
       mov       rcx,[r8+8]
       mov       [rbp+18],rcx
       mov       rcx,[rbp-58]
       mov       rax,[rbp+18]
       cmp       rcx,[rax+8]
       je        near ptr M10_L23
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+8]
       mov       [rbp-58],rcx
       cmp       byte ptr [r8+19],0
       jne       short M10_L10
       mov       rcx,[r8]
       call      qword ptr [7FFA7599FBD0]
       mov       rcx,[rbp-58]
       mov       r11,rax
       mov       rdx,[rbp+20]
       call      qword ptr [rax]
       jmp       short M10_L11
M10_L10:
       mov       rcx,[rbp+20]
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].AddOrUpdate[[System.__Canon, System.Private.CoreLib]](System.__Canon, System.Func`3<System.__Canon,System.__Canon,System.__Canon>, System.Func`4<System.__Canon,System.__Canon,System.__Canon,System.__Canon>, System.__Canon)]
       cmp       [rcx],ecx
       call      qword ptr [r11]
M10_L11:
       mov       [rbp-3C],eax
       mov       r8,[rbp+10]
       jmp       near ptr M10_L23
M10_L12:
       call      qword ptr [7FFA7599F290]
       int       3
M10_L13:
       call      qword ptr [7FFA7599F288]
       int       3
M10_L14:
       mov       rdx,[r15+10]
       mov       rcx,[rbp+48]
       call      qword ptr [7FFA7599F2A0]; CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M10_L08
M10_L15:
       mov       esi,1
       jmp       near ptr M10_L04
M10_L16:
       mov       rcx,[rbp-58]
       call      qword ptr [7FFA7599FF30]
       mov       ecx,1
       test      rax,rax
       cmovne    edi,ecx
       jmp       short M10_L20
M10_L17:
       call      M10_L26
       nop
       mov       eax,[rbp-4C]
       add       rsp,50
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M10_L18:
       mov       rdx,[rbp+20]
       mov       rcx,rdx
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].AddOrUpdate[[System.__Canon, System.Private.CoreLib]](System.__Canon, System.Func`3<System.__Canon,System.__Canon,System.__Canon>, System.Func`4<System.__Canon,System.__Canon,System.__Canon,System.__Canon>, System.__Canon)]
       cmp       [rcx],ecx
       call      qword ptr [r11]
       jmp       near ptr M10_L00
M10_L19:
       mov       eax,esi
       jmp       near ptr M10_L00
M10_L20:
       mov       r8,[rbp+10]
       cmp       byte ptr [rbp-48],0
       je        short M10_L21
       mov       rcx,[rbp-60]
       mov       ecx,[rcx+8]
       cmp       [rbp-40],ecx
       jae       short M10_L25
       mov       rcx,[rbp-60]
       mov       eax,[rbp-40]
       mov       rcx,[rcx+rax*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       mov       r8,[rbp+10]
M10_L21:
       mov       ecx,esi
       or        ecx,edi
       jne       short M10_L24
M10_L22:
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      qword ptr [7FFA7599F2A0]; CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,50
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M10_L23:
       call      M10_L26
       jmp       near ptr M10_L01
M10_L24:
       mov       rcx,r8
       mov       rdx,[rbp+18]
       mov       r8d,esi
       mov       r9d,edi
       call      qword ptr [7FFA759A0908]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
       jmp       short M10_L22
M10_L25:
       call      qword ptr [7FFA7599F290]
       int       3
M10_L26:
       sub       rsp,28
       cmp       byte ptr [rbp-48],0
       je        short M10_L27
       mov       rcx,[rbp-60]
       mov       ecx,[rcx+8]
       cmp       [rbp-40],ecx
       jae       short M10_L28
       mov       rcx,[rbp-60]
       mov       eax,[rbp-40]
       mov       rcx,[rcx+rax*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
M10_L27:
       nop
       add       rsp,28
       ret
M10_L28:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 955
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
       push      rbx
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       mov       rcx,rbx
       call      qword ptr [7FF964232DF0]; Precode of System.Runtime.CompilerServices.InitHelpers.InitClassSlow(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rbx+20]
       mov       rax,[rax-10]
       mov       [rsp+28],rax
       mov       rax,[rsp+28]
       and       rax,0FFFFFFFFFFFFFFFE
       xor       ecx,ecx
       mov       [rsp+28],rcx
       add       rsp,30
       pop       rbx
       ret
; Total bytes of code 59
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].AddOrUpdate[[System.__Canon, System.Private.CoreLib]](System.__Canon, System.Func`3<System.__Canon,System.__Canon,System.__Canon>, System.Func`4<System.__Canon,System.__Canon,System.__Canon,System.__Canon>, System.__Canon)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A0
       lea       rbp,[rsp+0D0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-90],ymm4
       vmovdqu   ymmword ptr [rbp-70],ymm4
       vmovdqa   xmmword ptr [rbp-50],xmm4
       mov       [rbp-38],rdx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       mov       rax,r8
       mov       r10,[rbp+30]
       test      rax,rax
       je        near ptr M12_L72
       mov       rax,[rbp+20]
       test      r9,r9
       je        near ptr M12_L73
       test      r10,r10
       je        near ptr M12_L74
       mov       rdx,[rcx+8]
       mov       [rbp-58],rdx
       mov       rdx,[rbp-58]
       mov       rdx,[rdx+8]
       mov       [rbp-60],rdx
       cmp       byte ptr [rcx+19],0
       jne       near ptr M12_L62
       mov       rdx,[rcx]
       mov       r11,[rdx+30]
       mov       r11,[r11]
       mov       r11,[r11+70]
       test      r11,r11
       je        near ptr M12_L61
M12_L00:
       mov       rcx,[rbp-60]
       mov       rdx,rax
       call      qword ptr [r11]
M12_L01:
       mov       [rbp-3C],eax
M12_L02:
       mov       r8,[rbp+18]
       mov       rcx,[r8+18]
       mov       rbx,[rcx+18]
       test      rbx,rbx
       je        near ptr M12_L63
M12_L03:
       mov       rcx,[rbp-58]
       mov       rsi,[rcx+8]
       mov       rcx,[rbp-58]
       mov       rcx,[rcx+10]
       mov       rdx,[rbp-58]
       mov       eax,[rbp-3C]
       imul      rax,[rdx+28]
       shr       rax,20
       inc       rax
       mov       edx,[rcx+8]
       mov       r10d,edx
       imul      rax,r10
       shr       rax,20
       cmp       eax,edx
       jae       near ptr M12_L95
       mov       edx,eax
       mov       rdi,[rcx+rdx*8+10]
       test      rdi,rdi
       je        near ptr M12_L84
       test      rsi,rsi
       je        near ptr M12_L68
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rsi],rcx
       jne       near ptr M12_L68
M12_L04:
       mov       ecx,[rbp-3C]
       cmp       ecx,[rdi+20]
       jne       near ptr M12_L75
       mov       rdx,[rdi+8]
       mov       rax,[rbp+20]
       cmp       rdx,rax
       jne       near ptr M12_L64
       mov       ecx,1
M12_L05:
       test      ecx,ecx
       je        near ptr M12_L75
M12_L06:
       mov       rbx,[rdi+10]
       mov       rdx,offset Excalibur.Dispatch.Messaging.MessageContext+<>c__181`1[[System.__Canon, System.Private.CoreLib]].<SetItem>b__181_1(System.String, System.Object, System.__Canon)
       mov       r10,[rbp+30]
       cmp       [r10+18],rdx
       jne       near ptr M12_L85
       mov       r9,[rbp+38]
       mov       rsi,r9
M12_L07:
       mov       [rbp-68],rsi
       mov       rcx,[rbp-58]
       mov       [rbp-70],rcx
       mov       [rbp-90],rbx
       mov       rcx,[rbp-70]
       mov       rcx,[rcx+8]
       mov       [rbp-78],rcx
       mov       ecx,[rbp-3C]
       mov       [rbp-4C],ecx
       mov       rcx,[rbp+10]
       mov       rdx,[rcx]
       mov       r11,[rdx+30]
       mov       r11,[r11]
       mov       r11,[r11+80]
       test      r11,r11
       je        near ptr M12_L71
M12_L08:
       mov       rcx,r11
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rax]
       mov       [rbp-80],rax
       cmp       qword ptr [rbp-80],0
       je        near ptr M12_L33
       mov       rax,[rbp-80]
       mov       rdx,offset MT_System.Collections.Generic.ObjectEqualityComparer<System.Object>
       cmp       [rax],rdx
       jne       near ptr M12_L33
M12_L09:
       mov       rax,[rbp-70]
       mov       rcx,[rax+18]
       mov       r8,rcx
       mov       rax,[rbp-70]
       mov       rbx,[rax+10]
       mov       rax,[rbp-70]
       mov       r10d,[rbp-4C]
       imul      r10,[rax+28]
       shr       r10,20
       inc       r10
       mov       r9d,[rbx+8]
       mov       r11d,r9d
       imul      r10,r11
       shr       r10,20
       mov       eax,r10d
       xor       edx,edx
       div       dword ptr [rcx+8]
       cmp       r10d,r9d
       jae       near ptr M12_L95
       mov       ecx,r10d
       lea       rsi,[rbx+rcx*8+10]
       cmp       edx,[r8+8]
       jae       near ptr M12_L95
       mov       ecx,edx
       mov       rcx,[r8+rcx*8+10]
       mov       [rbp-88],rcx
       xor       ecx,ecx
       mov       [rbp-50],ecx
       cmp       qword ptr [rbp-88],0
       je        near ptr M12_L18
       mov       rcx,[rbp-88]
       call      00007FFA135C0070
       test      eax,eax
       jne       short M12_L10
       mov       rcx,[rbp-88]
       call      qword ptr [7FF9B3E96C40]
M12_L10:
       mov       dword ptr [rbp-50],1
       mov       rcx,[rbp-70]
       mov       rax,[rbp+10]
       cmp       rcx,[rax+8]
       jne       near ptr M12_L19
       mov       rdi,[rsi]
       test      rdi,rdi
       je        near ptr M12_L31
M12_L11:
       mov       ecx,[rbp-4C]
       cmp       ecx,[rdi+20]
       jne       near ptr M12_L24
       mov       rcx,[rax]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r8,[rdx+88]
       test      r8,r8
       je        near ptr M12_L16
       mov       rcx,r8
M12_L12:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+78]
       test      r11,r11
       je        near ptr M12_L17
M12_L13:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-78]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       rax,[rbp+10]
       je        near ptr M12_L24
       mov       r14,[rdi+10]
       test      r14,r14
       je        near ptr M12_L32
       cmp       qword ptr [rbp-90],0
       je        near ptr M12_L31
       mov       rdx,offset MT_System.String
       cmp       [r14],rdx
       jne       near ptr M12_L30
       cmp       r14,[rbp-90]
       jne       near ptr M12_L25
       mov       r15d,1
M12_L14:
       test      r15d,r15d
       je        near ptr M12_L31
M12_L15:
       lea       rcx,[rdi+10]
       mov       rdx,[rbp-68]
       call      CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       jmp       near ptr M12_L59
M12_L16:
       mov       rdx,7FF9B3EB1EA0
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       near ptr M12_L12
M12_L17:
       mov       rdx,7FF9B3EB1B38
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M12_L13
M12_L18:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
M12_L19:
       mov       rcx,[rax+8]
       mov       [rbp-70],rcx
       mov       rcx,[rbp-78]
       mov       rdx,[rbp-70]
       cmp       rcx,[rdx+8]
       je        near ptr M12_L86
       mov       rcx,[rbp-70]
       mov       rcx,[rcx+8]
       mov       [rbp-78],rcx
       cmp       byte ptr [rax+19],0
       jne       short M12_L22
       mov       rcx,[rax]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        short M12_L20
       jmp       short M12_L21
M12_L20:
       mov       rdx,7FF9B3EB1780
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M12_L21:
       mov       rcx,[rbp-78]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M12_L23
M12_L22:
       mov       rcx,[rbp+20]
       mov       rdx,[rcx]
       mov       rdx,[rdx+40]
       call      qword ptr [rdx+18]
M12_L23:
       mov       [rbp-4C],eax
       mov       rax,[rbp+10]
       jmp       near ptr M12_L86
M12_L24:
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M12_L11
       jmp       near ptr M12_L31
M12_L25:
       mov       rdx,[rbp-90]
       mov       rcx,offset MT_System.String
       call      qword ptr [7FF9B3A16850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       jne       short M12_L27
M12_L26:
       xor       r15d,r15d
       mov       rax,[rbp+10]
       jmp       near ptr M12_L14
M12_L27:
       mov       ecx,[r14+8]
       cmp       ecx,[rax+8]
       jne       short M12_L26
       lea       rcx,[r14+0C]
       lea       rdx,[rax+0C]
       mov       r8d,[r14+8]
       add       r8d,r8d
       cmp       r8,0A
       jne       short M12_L28
       mov       rax,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rdx]
       xor       rax,r8
       xor       rcx,[rdx+2]
       or        rcx,rax
       sete      r15b
       movzx     r15d,r15b
       jmp       short M12_L29
M12_L28:
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       mov       r15d,eax
M12_L29:
       mov       rax,[rbp+10]
       jmp       near ptr M12_L14
M12_L30:
       mov       rcx,r14
       mov       rdx,[rbp-90]
       mov       r8,[r14]
       mov       r8,[r8+40]
       call      qword ptr [r8+10]
       mov       r15d,eax
       mov       rax,[rbp+10]
       jmp       near ptr M12_L14
M12_L31:
       xor       r13d,r13d
       jmp       near ptr M12_L59
M12_L32:
       cmp       qword ptr [rbp-90],0
       jne       short M12_L31
       jmp       near ptr M12_L15
M12_L33:
       mov       rax,[rbp-70]
       mov       rcx,[rax+18]
       mov       r8,rcx
       mov       rax,[rbp-70]
       mov       rbx,[rax+10]
       mov       rax,[rbp-70]
       mov       r10d,[rbp-4C]
       imul      r10,[rax+28]
       shr       r10,20
       inc       r10
       mov       r9d,[rbx+8]
       mov       eax,r9d
       imul      r10,rax
       shr       r10,20
       mov       eax,r10d
       xor       edx,edx
       div       dword ptr [rcx+8]
       cmp       r10d,r9d
       jae       near ptr M12_L95
       mov       ecx,r10d
       lea       rsi,[rbx+rcx*8+10]
       cmp       edx,[r8+8]
       jae       near ptr M12_L95
       mov       ecx,edx
       mov       rcx,[r8+rcx*8+10]
       mov       [rbp-88],rcx
       xor       ecx,ecx
       mov       [rbp-50],ecx
       cmp       qword ptr [rbp-88],0
       je        near ptr M12_L40
       mov       rcx,[rbp-88]
       call      00007FFA135C0070
       test      eax,eax
       je        near ptr M12_L41
M12_L34:
       mov       dword ptr [rbp-50],1
       mov       rcx,[rbp-70]
       mov       rax,[rbp+10]
       cmp       rcx,[rax+8]
       jne       near ptr M12_L42
       mov       rdi,[rsi]
       test      rdi,rdi
       je        near ptr M12_L56
M12_L35:
       mov       ecx,[rbp-4C]
       cmp       ecx,[rdi+20]
       jne       near ptr M12_L49
       mov       rcx,[rax]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r8,[rdx+88]
       test      r8,r8
       je        near ptr M12_L47
       mov       rcx,r8
M12_L36:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+78]
       test      r11,r11
       je        near ptr M12_L48
M12_L37:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-78]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       rax,[rbp+10]
       je        near ptr M12_L49
       mov       r14,[rdi+10]
       mov       rdx,[rbp-80]
       mov       rcx,offset MT_System.Collections.Generic.ObjectEqualityComparer<System.Object>
       cmp       [rdx],rcx
       jne       near ptr M12_L58
       test      r14,r14
       je        near ptr M12_L57
       cmp       qword ptr [rbp-90],0
       je        near ptr M12_L56
       mov       rdx,offset MT_System.String
       cmp       [r14],rdx
       jne       near ptr M12_L55
       cmp       r14,[rbp-90]
       jne       near ptr M12_L50
       mov       r15d,1
M12_L38:
       test      r15d,r15d
       je        near ptr M12_L56
M12_L39:
       lea       rcx,[rdi+10]
       mov       rdx,[rbp-68]
       call      CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       jmp       near ptr M12_L59
M12_L40:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
M12_L41:
       mov       rcx,[rbp-88]
       call      qword ptr [7FF9B3E96C40]
       jmp       near ptr M12_L34
M12_L42:
       mov       rcx,[rax+8]
       mov       [rbp-70],rcx
       mov       rcx,[rbp-78]
       mov       rdx,[rbp-70]
       cmp       rcx,[rdx+8]
       je        near ptr M12_L87
       mov       rcx,[rbp-70]
       mov       rcx,[rcx+8]
       mov       [rbp-78],rcx
       cmp       byte ptr [rax+19],0
       jne       short M12_L45
       mov       rcx,[rax]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        short M12_L43
       jmp       short M12_L44
M12_L43:
       mov       rdx,7FF9B3EB1780
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M12_L44:
       mov       rcx,[rbp-78]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M12_L46
M12_L45:
       mov       rcx,[rbp+20]
       mov       rdx,[rcx]
       mov       rdx,[rdx+40]
       call      qword ptr [rdx+18]
M12_L46:
       mov       [rbp-4C],eax
       mov       rax,[rbp+10]
       jmp       near ptr M12_L87
M12_L47:
       mov       rdx,7FF9B3EB1EA0
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       near ptr M12_L36
M12_L48:
       mov       rdx,7FF9B3EB1B38
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M12_L37
M12_L49:
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M12_L35
       jmp       near ptr M12_L56
M12_L50:
       mov       rdx,[rbp-90]
       mov       rcx,offset MT_System.String
       call      qword ptr [7FF9B3A16850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       jne       short M12_L52
M12_L51:
       xor       r15d,r15d
       mov       rax,[rbp+10]
       jmp       near ptr M12_L38
M12_L52:
       mov       ecx,[r14+8]
       cmp       ecx,[rax+8]
       jne       short M12_L51
       lea       rcx,[r14+0C]
       lea       rdx,[rax+0C]
       mov       r8d,[r14+8]
       add       r8d,r8d
       cmp       r8,0A
       jne       short M12_L53
       mov       rax,[rcx]
       mov       rcx,[rcx+2]
       mov       r8,[rdx]
       xor       rax,r8
       xor       rcx,[rdx+2]
       or        rcx,rax
       sete      r15b
       movzx     r15d,r15b
       jmp       short M12_L54
M12_L53:
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       mov       r15d,eax
M12_L54:
       mov       rax,[rbp+10]
       jmp       near ptr M12_L38
M12_L55:
       mov       rcx,r14
       mov       rdx,[rbp-90]
       mov       r8,[r14]
       mov       r8,[r8+40]
       call      qword ptr [r8+10]
       mov       r15d,eax
       mov       rax,[rbp+10]
       jmp       near ptr M12_L38
M12_L56:
       xor       r13d,r13d
       jmp       short M12_L59
M12_L57:
       cmp       qword ptr [rbp-90],0
       jne       short M12_L56
       jmp       near ptr M12_L39
M12_L58:
       mov       rcx,[rbp-80]
       mov       rdx,r14
       mov       r8,[rbp-90]
       mov       r10,[rbp-80]
       mov       r10,[r10]
       mov       r10,[r10+40]
       call      qword ptr [r10+20]
       mov       r15d,eax
       mov       rax,[rbp+10]
       jmp       near ptr M12_L38
M12_L59:
       cmp       qword ptr [rbp-88],0
       je        near ptr M12_L94
       mov       rcx,[rbp-88]
       call      00007FFA135EBB70
       test      eax,eax
       je        short M12_L60
       mov       ecx,eax
       mov       rdx,[rbp-88]
       call      qword ptr [7FF9B3E96C58]
M12_L60:
       test      r13d,r13d
       je        near ptr M12_L89
       mov       rax,[rbp-68]
       add       rsp,0A0
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M12_L61:
       mov       rcx,rdx
       mov       rdx,7FF9B3EB1780
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       mov       rax,[rbp+20]
       jmp       near ptr M12_L00
M12_L62:
       mov       rcx,rax
       mov       rdx,[rax]
       mov       rdx,[rdx+40]
       call      qword ptr [rdx+18]
       jmp       near ptr M12_L01
M12_L63:
       mov       rcx,r8
       mov       rdx,7FF9B3EB1550
       call      qword ptr [7FF9B3A1F630]; System.Runtime.CompilerServices.GenericsHelpers.Method(IntPtr, IntPtr)
       mov       rbx,rax
       mov       r8,[rbp+18]
       jmp       near ptr M12_L03
M12_L64:
       test      rdx,rdx
       je        short M12_L67
       mov       ecx,[rdx+8]
       cmp       ecx,[rax+8]
       jne       short M12_L67
       lea       rcx,[rdx+0C]
       lea       r10,[rax+0C]
       mov       edx,[rdx+8]
       add       edx,edx
       mov       r9d,edx
       cmp       r9,0A
       je        short M12_L65
       mov       rdx,r10
       mov       r8,r9
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M12_L66
M12_L65:
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r9,[r10]
       xor       rdx,r9
       xor       rcx,[r10+2]
       or        rcx,rdx
       sete      cl
       movzx     ecx,cl
       mov       eax,ecx
M12_L66:
       mov       ecx,eax
       mov       rax,[rbp+20]
       mov       r8,[rbp+18]
       jmp       near ptr M12_L05
M12_L67:
       xor       ecx,ecx
       jmp       near ptr M12_L05
M12_L68:
       mov       ecx,[rbp-3C]
       cmp       ecx,[rdi+20]
       jne       near ptr M12_L83
       mov       rcx,[rbx+30]
       mov       rcx,[rcx]
       mov       r11,[rcx+78]
       test      r11,r11
       je        near ptr M12_L76
M12_L69:
       mov       rdx,[rdi+8]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [rsi],rcx
       jne       near ptr M12_L77
       mov       rax,[rbp+20]
       cmp       rdx,rax
       jne       near ptr M12_L78
       jmp       near ptr M12_L82
M12_L70:
       test      ecx,ecx
       je        near ptr M12_L83
       jmp       near ptr M12_L06
M12_L71:
       mov       rcx,rdx
       mov       rdx,7FF9B3EB1E60
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M12_L08
M12_L72:
       mov       ecx,1
       mov       rdx,7FF9B3D59148
       call      qword ptr [7FF9B3A1F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E96BB0]
       int       3
M12_L73:
       mov       ecx,0B9A
       mov       rdx,7FF9B3D59148
       call      qword ptr [7FF9B3A1F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E96BB0]
       int       3
M12_L74:
       mov       ecx,0BBA
       mov       rdx,7FF9B3D59148
       call      qword ptr [7FF9B3A1F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E96BB0]
       int       3
M12_L75:
       mov       rax,[rbp+20]
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M12_L04
       jmp       near ptr M12_L84
M12_L76:
       mov       rcx,rbx
       mov       rdx,7FF9B3EB1B38
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       mov       r8,[rbp+18]
       jmp       near ptr M12_L69
M12_L77:
       mov       rax,[rbp+20]
       mov       rcx,rsi
       mov       r8,rax
       call      qword ptr [r11]
       mov       ecx,eax
       mov       rax,[rbp+20]
       mov       r8,[rbp+18]
       jmp       near ptr M12_L70
M12_L78:
       test      rdx,rdx
       je        short M12_L81
       mov       ecx,[rdx+8]
       cmp       ecx,[rax+8]
       jne       short M12_L81
       add       rdx,0C
       lea       r10,[rax+0C]
       add       ecx,ecx
       mov       r9d,ecx
       cmp       r9,0A
       je        short M12_L79
       mov       rcx,rdx
       mov       rdx,r10
       mov       r8,r9
       call      qword ptr [7FF9B3A1C330]; System.SpanHelpers.SequenceEqual(Byte ByRef, Byte ByRef, UIntPtr)
       jmp       short M12_L80
M12_L79:
       mov       rax,[rbp+20]
       mov       rcx,rdx
       mov       r11,r10
       mov       rdx,[rcx]
       mov       rcx,[rcx+2]
       mov       r10,[r11]
       xor       rdx,r10
       xor       rcx,[r11+2]
       or        rcx,rdx
       sete      cl
       movzx     ecx,cl
       mov       eax,ecx
M12_L80:
       mov       ecx,eax
       mov       rax,[rbp+20]
       mov       r8,[rbp+18]
       jmp       near ptr M12_L70
M12_L81:
       xor       ecx,ecx
       jmp       near ptr M12_L70
M12_L82:
       mov       ecx,1
       jmp       near ptr M12_L70
M12_L83:
       mov       rax,[rbp+20]
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M12_L68
M12_L84:
       mov       rdx,[rbp+20]
       mov       r8,[rbp+38]
       mov       r9,[rbp+28]
       mov       rcx,[r9+8]
       call      qword ptr [r9+18]
       xor       r9d,r9d
       mov       [rsp+28],r9d
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-48]
       mov       [rsp+38],r9
       mov       [rsp+20],rax
       mov       r9d,[rbp-3C]
       shl       r9,20
       or        r9,1
       mov       rdx,[rbp-58]
       mov       r8,[rbp+20]
       mov       rcx,[rbp+10]
       call      qword ptr [7FF9B3D2EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       test      eax,eax
       je        short M12_L89
       jmp       short M12_L88
M12_L85:
       mov       rdx,rax
       mov       r8,rbx
       mov       r9,[rbp+38]
       mov       rcx,[r10+8]
       call      qword ptr [r10+18]
       mov       rsi,rax
       mov       rax,[rbp+20]
       mov       r8,[rbp+18]
       mov       r9,[rbp+38]
       mov       r10,[rbp+30]
       jmp       near ptr M12_L07
M12_L86:
       call      M12_L96
       jmp       near ptr M12_L09
M12_L87:
       call      M12_L99
       jmp       near ptr M12_L33
M12_L88:
       mov       rax,[rbp-48]
       add       rsp,0A0
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M12_L89:
       mov       rcx,[rbp-58]
       mov       rax,[rbp+10]
       cmp       rcx,[rax+8]
       je        near ptr M12_L02
       mov       rax,[rbp+10]
       mov       rcx,[rax+8]
       mov       [rbp-58],rcx
       mov       rcx,[rbp-60]
       mov       rdx,[rbp-58]
       cmp       rcx,[rdx+8]
       je        near ptr M12_L02
       mov       rcx,[rbp-58]
       mov       rcx,[rcx+8]
       mov       [rbp-60],rcx
       mov       rax,[rbp+10]
       cmp       byte ptr [rax+19],0
       jne       short M12_L92
       mov       rcx,[rax]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+70]
       test      r11,r11
       je        short M12_L90
       jmp       short M12_L91
M12_L90:
       mov       rdx,7FF9B3EB1780
       call      qword ptr [7FF9B3A1F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M12_L91:
       mov       rcx,[rbp-60]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M12_L93
M12_L92:
       mov       rcx,[rbp+20]
       mov       rdx,[rcx]
       mov       rdx,[rdx+40]
       call      qword ptr [rdx+18]
M12_L93:
       mov       [rbp-3C],eax
       jmp       near ptr M12_L02
M12_L94:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
M12_L95:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M12_L96:
       sub       rsp,48
       cmp       dword ptr [rbp-50],0
       je        short M12_L97
       cmp       qword ptr [rbp-88],0
       je        short M12_L98
       mov       rcx,[rbp-88]
       call      00007FFA135EBB70
       test      eax,eax
       je        short M12_L97
       mov       ecx,eax
       mov       rdx,[rbp-88]
       call      qword ptr [7FF9B3E96C58]
M12_L97:
       nop
       add       rsp,48
       ret
M12_L98:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
M12_L99:
       sub       rsp,48
       cmp       dword ptr [rbp-50],0
       je        short M12_L100
       cmp       qword ptr [rbp-88],0
       je        short M12_L101
       mov       rcx,[rbp-88]
       call      00007FFA135EBB70
       test      eax,eax
       je        short M12_L100
       mov       ecx,eax
       mov       rdx,[rbp-88]
       call      qword ptr [7FF9B3E96C58]
M12_L100:
       nop
       add       rsp,48
       ret
M12_L101:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E96C28]
       int       3
; Total bytes of code 3188
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ContainsItem_Exists()
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       rbx,[rcx+8]
       cmp       [rbx],bl
       mov       rsi,1BD00206748
       mov       edi,0C
       mov       ebp,0A
M00_L00:
       movzx     ecx,word ptr [rsi+rdi]
       cmp       ecx,100
       jge       near ptr M00_L09
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M00_L10
M00_L01:
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        near ptr M00_L11
       mov       rbx,[rcx+8]
       mov       rdi,[rbx+8]
       cmp       byte ptr [rcx+19],0
       jne       short M00_L04
       mov       rcx,rdi
       mov       r11,7FF9B39605D0
       mov       rdx,rsi
       call      qword ptr [r11]
       mov       ebp,eax
M00_L02:
       mov       rdx,[rbx+10]
       mov       ecx,ebp
       imul      rcx,[rbx+28]
       shr       rcx,20
       inc       rcx
       mov       r8d,[rdx+8]
       mov       r11d,r8d
       imul      rcx,r11
       shr       rcx,20
       cmp       ecx,r8d
       jae       near ptr M00_L13
       mov       ecx,ecx
       mov       rbx,[rdx+rcx*8+10]
       test      rbx,rbx
       je        short M00_L07
M00_L03:
       cmp       ebp,[rbx+20]
       jne       short M00_L06
       mov       rdx,[rbx+8]
       mov       rcx,rdi
       mov       r8,1BD00206748
       mov       r11,7FF9B39605C8
       call      qword ptr [r11]
       test      eax,eax
       je        short M00_L06
       mov       eax,1
       jmp       short M00_L08
M00_L04:
       test      byte ptr [7FF9B3EE48B8],1
       je        near ptr M00_L12
M00_L05:
       mov       r8,[7FF9B395B118]
       mov       edx,14
       mov       r9,r8
       shr       r9,20
       lea       rcx,[rsi+0C]
       call      qword ptr [7FF9B3E96C28]
       mov       ebp,eax
       jmp       near ptr M00_L02
M00_L06:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       short M00_L03
M00_L07:
       xor       eax,eax
M00_L08:
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M00_L09:
       call      qword ptr [7FF9B3E96B38]
       test      eax,eax
       je        near ptr M00_L01
M00_L10:
       add       rdi,2
       dec       ebp
       jne       near ptr M00_L00
       mov       ecx,0F57
       mov       rdx,7FF9B3C88428
       call      qword ptr [7FF9B3A1F210]
       mov       rbx,rax
       mov       ecx,0AB09
       mov       rdx,7FF9B3DA5360
       call      qword ptr [7FF9B3A1F210]
       mov       rdx,rax
       mov       rcx,rbx
       call      qword ptr [7FF9B3E96AD8]
       int       3
M00_L11:
       xor       eax,eax
       jmp       short M00_L08
M00_L12:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B3A15740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M00_L05
M00_L13:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 402
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-10]
       mov       rdx,rax
       test      dl,1
       jne       short M01_L00
       ret
M01_L00:
       jmp       qword ptr [7FF9B3C1E6E8]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
       push      rbx
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       mov       rcx,rbx
       call      qword ptr [7FF964232DF0]; Precode of System.Runtime.CompilerServices.InitHelpers.InitClassSlow(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rbx+20]
       mov       rax,[rax-10]
       mov       [rsp+28],rax
       mov       rax,[rsp+28]
       and       rax,0FFFFFFFFFFFFFFFE
       xor       ecx,ecx
       mov       [rsp+28],rcx
       add       rsp,30
       pop       rbx
       ret
; Total bytes of code 59
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.ContainsItem_NotExists()
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       rbx,[rcx+8]
       cmp       [rbx],bl
       mov       rsi,1AC002092F8
       mov       edi,0C
       mov       ebp,0B
M00_L00:
       movzx     ecx,word ptr [rsi+rdi]
       cmp       ecx,100
       jge       near ptr M00_L09
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M00_L10
M00_L01:
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        near ptr M00_L11
       mov       rbx,[rcx+8]
       mov       rdi,[rbx+8]
       cmp       byte ptr [rcx+19],0
       jne       short M00_L04
       mov       rcx,rdi
       mov       r11,7FF9B39605C0
       mov       rdx,rsi
       call      qword ptr [r11]
       mov       ebp,eax
M00_L02:
       mov       rdx,[rbx+10]
       mov       ecx,ebp
       imul      rcx,[rbx+28]
       shr       rcx,20
       inc       rcx
       mov       r8d,[rdx+8]
       mov       r11d,r8d
       imul      rcx,r11
       shr       rcx,20
       cmp       ecx,r8d
       jae       near ptr M00_L13
       mov       ecx,ecx
       mov       rbx,[rdx+rcx*8+10]
       test      rbx,rbx
       je        short M00_L07
M00_L03:
       cmp       ebp,[rbx+20]
       jne       short M00_L06
       mov       rdx,[rbx+8]
       mov       rcx,rdi
       mov       r8,1AC002092F8
       mov       r11,7FF9B39605B8
       call      qword ptr [r11]
       test      eax,eax
       je        short M00_L06
       mov       eax,1
       jmp       short M00_L08
M00_L04:
       test      byte ptr [7FF9B3EE48B8],1
       je        near ptr M00_L12
M00_L05:
       mov       r8,[7FF9B395B118]
       mov       edx,16
       mov       r9,r8
       shr       r9,20
       lea       rcx,[rsi+0C]
       call      qword ptr [7FF9B3E96C28]
       mov       ebp,eax
       jmp       near ptr M00_L02
M00_L06:
       mov       rbx,[rbx+18]
       test      rbx,rbx
       jne       short M00_L03
M00_L07:
       xor       eax,eax
M00_L08:
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       ret
M00_L09:
       call      qword ptr [7FF9B3E96B38]
       test      eax,eax
       je        near ptr M00_L01
M00_L10:
       add       rdi,2
       dec       ebp
       jne       near ptr M00_L00
       mov       ecx,10D3
       mov       rdx,7FF9B3C88428
       call      qword ptr [7FF9B3A1F210]
       mov       rbx,rax
       mov       ecx,0AB09
       mov       rdx,7FF9B3DA5360
       call      qword ptr [7FF9B3A1F210]
       mov       rdx,rax
       mov       rcx,rbx
       call      qword ptr [7FF9B3E96AD8]
       int       3
M00_L11:
       xor       eax,eax
       jmp       short M00_L08
M00_L12:
       mov       rcx,offset MT_System.Marvin
       call      qword ptr [7FF9B3A15740]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       jmp       near ptr M00_L05
M00_L13:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 402
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-10]
       mov       rdx,rax
       test      dl,1
       jne       short M01_L00
       ret
M01_L00:
       jmp       qword ptr [7FF9B3C1E6E8]; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
       push      rbx
       sub       rsp,30
       xor       eax,eax
       mov       [rsp+28],rax
       mov       rbx,rcx
       mov       rcx,rbx
       call      qword ptr [7FF964232DF0]; Precode of System.Runtime.CompilerServices.InitHelpers.InitClassSlow(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rbx+20]
       mov       rax,[rax-10]
       mov       [rsp+28],rax
       mov       rax,[rsp+28]
       and       rax,0FFFFFFFFFFFFFFFE
       xor       ecx,ecx
       mov       [rsp+28],rcx
       add       rsp,30
       pop       rbx
       ret
; Total bytes of code 59
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.CompoundOperation_CachingMiddlewarePattern()
       push      rbp
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0B0]
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rbp-40],xmm4
       xor       eax,eax
       mov       [rbp-30],rax
       mov       rbx,rcx
       mov       rsi,[rbx+8]
       mov       rcx,[rsi+8]
       test      rcx,rcx
       je        near ptr M00_L05
M00_L00:
       lea       r8,[rbp-30]
       mov       r11,7FF9B3950600
       mov       rdx,1C0002D67A8
       call      qword ptr [r11]
       mov       rsi,[rbx+8]
       mov       rcx,[rsi+8]
       test      rcx,rcx
       je        near ptr M00_L07
M00_L01:
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r15
       jne       near ptr M00_L09
       mov       rdx,[rcx+8]
       mov       r9,1C0002D92F8
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-38]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,1C0002D67A8
       call      qword ptr [7FF9B3D1EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       xor       ecx,ecx
       mov       [rbp-38],rcx
M00_L02:
       mov       rbx,[rbx+8]
       mov       rcx,[rbx+8]
       test      rcx,rcx
       je        near ptr M00_L10
M00_L03:
       cmp       [rcx],r15
       jne       near ptr M00_L12
       mov       rdx,[rcx+8]
       mov       r9,1C0002D9370
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-40]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,1C0002D9328
       call      qword ptr [7FF9B3D1EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
M00_L04:
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       pop       rbp
       ret
M00_L05:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3DA5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-58],rdi
       mov       [rbp-44],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L06
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rcx,1C017800068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r15
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r15
       call      CORINFO_HELP_ASSIGN_REF
M00_L06:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rsi
       jmp       near ptr M00_L00
M00_L07:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3DA5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-60],rdi
       mov       [rbp-48],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L08
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rcx,1C017800068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r15
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r15
       call      CORINFO_HELP_ASSIGN_REF
M00_L08:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rsi
       jmp       near ptr M00_L01
M00_L09:
       mov       r11,7FF9B3950608
       mov       rdx,1C0002D67A8
       mov       r8,1C0002D92F8
       call      qword ptr [r11]
       jmp       near ptr M00_L02
M00_L10:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DA5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-68],rsi
       mov       [rbp-4C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L11
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,1C017800068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L11:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L03
M00_L12:
       mov       r11,7FF9B3950610
       mov       rdx,1C0002D9328
       mov       r8,1C0002D9370
       call      qword ptr [r11]
       jmp       near ptr M00_L04
       sub       rsp,48
       cmp       qword ptr [rbp-58],0
       je        short M00_L13
       mov       rcx,[rbp-58]
       mov       edx,[rbp-44]
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
M00_L13:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-60],0
       je        short M00_L14
       mov       rcx,[rbp-60]
       mov       edx,[rbp-48]
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
M00_L14:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-68],0
       je        short M00_L15
       mov       rcx,[rbp-68]
       mov       edx,[rbp-4C]
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
M00_L15:
       nop
       add       rsp,48
       ret
; Total bytes of code 854
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-40],rax
       mov       [rbp-20],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       movzx     r9d,r9b
       mov       rax,[rbp+18]
       mov       rax,[rax+8]
       mov       [rbp-40],rax
       mov       ebx,[rbp+2C]
       test      r9d,r9d
       jne       near ptr M01_L29
       cmp       byte ptr [rcx+19],0
       jne       near ptr M01_L28
       mov       rax,[rcx]
       mov       r8,[rax+30]
       mov       r8,[r8]
       mov       r11,[r8+80]
       test      r11,r11
       je        near ptr M01_L27
M01_L00:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
M01_L01:
       mov       [rbp-24],eax
M01_L02:
       mov       rax,[rbp+18]
       mov       rcx,[rax+18]
       mov       [rbp-48],rcx
       mov       r8,[rbp+10]
       cmp       [r8],r8d
       mov       rax,[rbp+18]
       mov       r10,[rax+10]
       mov       rax,[rbp+18]
       mov       r9d,[rbp-24]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       mov       [rbp-28],edx
       cmp       r9d,r11d
       jae       near ptr M01_L36
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       xor       ecx,ecx
       mov       [rbp-2C],ecx
       mov       [rbp-30],ecx
       mov       [rbp-34],ecx
       cmp       byte ptr [rbp+40],0
       je        short M01_L04
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rsi,[rcx+rax*8+10]
       test      rsi,rsi
       je        near ptr M01_L10
       mov       rcx,rsi
       call      00007FFA135C0070
       test      eax,eax
       je        near ptr M01_L11
M01_L03:
       mov       dword ptr [rbp-34],1
M01_L04:
       mov       rcx,[rbp+18]
       mov       r8,[rbp+10]
       cmp       rcx,[r8+8]
       jne       near ptr M01_L12
       xor       esi,esi
       mov       rdi,[rbx]
       test      rdi,rdi
       je        near ptr M01_L19
M01_L05:
       mov       ecx,[rbp-24]
       cmp       ecx,[rdi+20]
       jne       near ptr M01_L17
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rax,[rdx+70]
       test      rax,rax
       je        short M01_L08
       mov       rcx,rax
M01_L06:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+88]
       test      r11,r11
       je        short M01_L09
M01_L07:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-40]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       r8,[rbp+10]
       je        near ptr M01_L17
       cmp       byte ptr [rbp+38],0
       je        near ptr M01_L18
       lea       rcx,[rdi+10]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L08:
       mov       rdx,7FF9B3EA0CA0
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       short M01_L06
M01_L09:
       mov       rdx,7FF9B3EA0FD8
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L07
M01_L10:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E86A48]
       int       3
M01_L11:
       mov       rcx,rsi
       call      qword ptr [7FF9B3E86A90]
       jmp       near ptr M01_L03
M01_L12:
       mov       rcx,[r8+8]
       mov       [rbp+18],rcx
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+18]
       cmp       rcx,[rdx+8]
       je        near ptr M01_L31
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+8]
       mov       [rbp-40],rcx
       cmp       byte ptr [r8+19],0
       jne       short M01_L15
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+80]
       test      r11,r11
       je        short M01_L13
       jmp       short M01_L14
M01_L13:
       mov       rdx,7FF9B3EA0E98
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M01_L14:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M01_L16
M01_L15:
       mov       rcx,[rbp+20]
       mov       rax,[rcx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
M01_L16:
       mov       [rbp-24],eax
       mov       r8,[rbp+10]
       jmp       near ptr M01_L31
M01_L17:
       inc       esi
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M01_L05
       jmp       short M01_L19
M01_L18:
       mov       rdx,[rdi+10]
       mov       rcx,[rbp+48]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L19:
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rdx,[rdx+78]
       test      rdx,rdx
       je        short M01_L22
       jmp       short M01_L23
M01_L20:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L21:
       call      CORINFO_HELP_OVERFLOW
       int       3
M01_L22:
       mov       rdx,7FF9B3EA0D28
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdx,rax
M01_L23:
       mov       rcx,rdx
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       rcx,[rbx]
       mov       [rsp+20],rcx
       mov       rcx,rdi
       mov       rdx,[rbp+20]
       mov       r8,[rbp+30]
       mov       r9d,[rbp-24]
       call      qword ptr [7FF9B3E86B68]
       mov       rcx,rbx
       mov       rdx,rdi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L20
       mov       ecx,[rbp-28]
       lea       rdx,[rdx+rcx*4+10]
       mov       ecx,[rdx]
       add       ecx,1
       jo        short M01_L21
       mov       [rdx],ecx
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       ecx,[rbp-28]
       mov       edx,[rdx+rcx*4+10]
       mov       r8,[rbp+10]
       cmp       edx,[r8+10]
       jle       short M01_L24
       mov       dword ptr [rbp-2C],1
M01_L24:
       cmp       esi,64
       jbe       near ptr M01_L30
       mov       rdx,[rbp-40]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer
       call      qword ptr [7FF9B3A06850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        near ptr M01_L30
       mov       dword ptr [rbp-30],1
       jmp       short M01_L30
M01_L25:
       cmp       dword ptr [rbp-34],0
       je        short M01_L26
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L36
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L32
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       jne       short M01_L33
M01_L26:
       xor       eax,eax
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L27:
       mov       rcx,rax
       mov       rdx,7FF9B3EA0E98
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L28:
       mov       rdx,[rbp+20]
       mov       rcx,rdx
       mov       rax,[rdx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M01_L01
M01_L29:
       mov       eax,ebx
       jmp       near ptr M01_L01
M01_L30:
       call      M01_L37
       jmp       short M01_L34
M01_L31:
       call      M01_L37
       jmp       near ptr M01_L02
M01_L32:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E86A48]
       int       3
M01_L33:
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E86A60]
       jmp       short M01_L26
M01_L34:
       mov       ecx,[rbp-2C]
       or        ecx,[rbp-30]
       je        short M01_L35
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+18]
       mov       r8d,[rbp-2C]
       mov       r9d,[rbp-30]
       call      qword ptr [7FF9B3D1F108]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
M01_L35:
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L36:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L37:
       sub       rsp,28
       cmp       dword ptr [rbp-34],0
       je        short M01_L38
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L40
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L39
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       je        short M01_L38
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E86A60]
M01_L38:
       nop
       add       rsp,28
       ret
M01_L39:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E86A48]
       int       3
M01_L40:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1194
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M06_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M06_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
M06_L00:
       test      rax,rax
       je        short M06_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       test      rax,rax
       je        short M06_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       test      rax,rax
       jne       short M06_L03
M06_L01:
       xor       edx,edx
M06_L02:
       mov       rax,rdx
       ret
M06_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       test      rax,rax
       je        short M06_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M06_L02
       jmp       short M06_L00
; Total bytes of code 86
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp-40],rcx
       mov       [rbp+10],rcx
       mov       rbx,rdx
       mov       esi,r8d
       mov       edi,r9d
       xor       eax,eax
       mov       [rbp-48],eax
       mov       rax,[rcx+8]
       mov       rax,[rax+18]
       cmp       dword ptr [rax+8],0
       jbe       near ptr M07_L15
       mov       rcx,[rax+10]
       call      qword ptr [7FFA759A0078]; Precode of System.Threading.Monitor.Enter(System.Object)
       mov       dword ptr [rbp-48],1
       mov       rcx,[rbp+10]
       cmp       rbx,[rcx+8]
       jne       near ptr M07_L18
       mov       rax,[rbx+10]
       mov       r14d,[rax+8]
       xor       r15d,r15d
       test      dil,dil
       jne       near ptr M07_L13
M07_L00:
       test      sil,sil
       je        short M07_L02
       test      r15,r15
       jne       short M07_L01
       mov       rcx,[rbp+10]
       call      qword ptr [7FFA759A08F8]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetCountNoLocks()
       mov       rcx,[rbx+10]
       mov       ecx,[rcx+8]
       shr       ecx,2
       cmp       eax,ecx
       jl        near ptr M07_L12
M07_L01:
       mov       rax,[rbx+10]
       mov       eax,[rax+8]
       add       eax,eax
       js        near ptr M07_L17
       mov       ecx,eax
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       r14d,eax
       call      qword ptr [7FFA7599FF68]
       cmp       eax,r14d
       jl        near ptr M07_L17
M07_L02:
       mov       rsi,[rbx+18]
       mov       rdi,rsi
       mov       rcx,[rbp+10]
       cmp       byte ptr [rcx+18],0
       je        short M07_L04
       cmp       dword ptr [rsi+8],400
       jge       short M07_L04
       mov       eax,[rsi+8]
       add       eax,eax
       movsxd    rcx,eax
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r8d,[rsi+8]
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FFA7599FF50]
       mov       rax,[rbx+18]
       mov       esi,[rax+8]
       mov       r13d,[rdi+8]
       cmp       r13d,esi
       jle       short M07_L04
M07_L03:
       call      qword ptr [7FFA7599FE68]
       mov       r8,rax
       movsxd    rdx,esi
       mov       rcx,rdi
       call      qword ptr [7FFA7599F2B0]; Precode of System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       inc       esi
       cmp       r13d,esi
       jg        short M07_L03
M07_L04:
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599FA10]
       mov       rcx,rax
       movsxd    rdx,r14d
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       rsi,rax
       mov       [rbp-60],rsi
       mov       ecx,[rdi+8]
       call      qword ptr [7FFA7599FF18]
       mov       r14,rax
       mov       r12,r15
       test      r12,r12
       jne       short M07_L05
       mov       r12,[rbx+8]
M07_L05:
       mov       rcx,r13
       call      qword ptr [7FFA7599F760]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-78],rax
       lea       rcx,[rax+10]
       mov       rdx,rsi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+20]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       ecx,[rsi+8]
       xor       edx,edx
       div       rcx
       inc       rax
       mov       r12,[rbp-78]
       mov       [r12+28],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F728]
       mov       rcx,rax
       lea       r8,[rbp-48]
       mov       rdx,rbx
       call      qword ptr [7FFA759A0918]
       mov       rbx,[rbx+10]
       xor       eax,eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jg        near ptr M07_L10
M07_L06:
       mov       rsi,[rbp-60]
       mov       eax,[rsi+8]
       xor       edx,edx
       div       dword ptr [rdi+8]
       mov       ecx,1
       cmp       eax,1
       cmovg     ecx,eax
       mov       rax,[rbp+10]
       mov       [rax+10],ecx
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M07_L18
M07_L07:
       test      r15,r15
       jne       near ptr M07_L11
       mov       [rbp-68],rdx
       mov       r8d,[rdx+20]
M07_L08:
       mov       rdx,[rbp-68]
       mov       r10,[rdx+18]
       mov       [rbp-80],r10
       mov       rcx,[r12+10]
       mov       [rbp-4C],r8d
       mov       r9d,r8d
       imul      r9,[r12+28]
       shr       r9,20
       inc       r9
       mov       r11d,[rcx+8]
       mov       esi,r11d
       imul      r9,rsi
       shr       r9,20
       mov       rsi,[r12+18]
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rsi+8]
       mov       esi,edx
       cmp       r9d,r11d
       jae       near ptr M07_L15
       mov       eax,r9d
       lea       rax,[rcx+rax*8+10]
       mov       [rbp-70],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F748]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-88],rax
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       r8,[r8+10]
       mov       [rbp-90],r8
       mov       r10,[rbp-70]
       mov       r9,[r10]
       mov       [rbp-98],r9
       lea       rcx,[rax+8]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+10]
       mov       rdx,[rbp-90]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+18]
       mov       rdx,[rbp-98]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       mov       ecx,[rbp-4C]
       mov       [rax+20],ecx
       mov       rcx,[rbp-70]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       cmp       esi,[r14+8]
       jae       near ptr M07_L15
       mov       eax,esi
       lea       rax,[r14+rax*4+10]
       mov       edx,[rax]
       add       edx,1
       jo        near ptr M07_L16
       mov       [rax],edx
       mov       rsi,[rbp-80]
       test      rsi,rsi
       mov       rdx,rsi
       jne       near ptr M07_L07
M07_L09:
       mov       rax,[rbp-58]
       inc       eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jle       near ptr M07_L06
M07_L10:
       mov       [rbp-58],rax
       mov       rdx,[rbx+rax*8+10]
       test      rdx,rdx
       jne       near ptr M07_L07
       jmp       short M07_L09
M07_L11:
       mov       [rbp-68],rdx
       mov       rcx,[rbp+10]
       mov       rcx,[rcx]
       call      qword ptr [7FFA7599FBD8]
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       rcx,r15
       mov       r11,rax
       call      qword ptr [rax]
       mov       r8d,eax
       jmp       near ptr M07_L08
M07_L12:
       mov       rcx,[rbp+10]
       mov       eax,[rcx+10]
       add       eax,eax
       mov       [rcx+10],eax
       test      eax,eax
       jge       near ptr M07_L18
       jmp       short M07_L14
M07_L13:
       mov       rcx,[rbx+8]
       call      qword ptr [7FFA7599FF30]
       mov       rdi,rax
       test      rdi,rdi
       je        near ptr M07_L00
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599F550]
       mov       r15,rax
       mov       rcx,rdi
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       call      qword ptr [r11]
       mov       rdx,rax
       mov       rcx,r15
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
       mov       r15,rax
       jmp       near ptr M07_L00
M07_L14:
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       short M07_L18
M07_L15:
       call      qword ptr [7FFA7599F290]
       int       3
M07_L16:
       call      qword ptr [7FFA7599F288]
       int       3
M07_L17:
       call      qword ptr [7FFA7599FF68]
       mov       r14d,eax
       mov       rcx,[rbp+10]
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       near ptr M07_L02
M07_L18:
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
       sub       rsp,28
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,28
       ret
; Total bytes of code 1137
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M08_L01
       test      rsi,rsi
       je        short M08_L02
       cmp       [rsi+8],ebx
       jl        short M08_L02
       add       rsi,10
M08_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M08_L00
M08_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M08_L02:
       mov       ecx,[rsi+8]
M08_L03:
       cmp       edi,[rsi+8]
       jae       short M08_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M08_L03
       jmp       short M08_L01
M08_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.CompoundOperation_ValidationMiddlewarePattern()
       push      rbp
       push      r15
       push      r14
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,78
       lea       rbp,[rsp+0A0]
       xor       eax,eax
       mov       [rbp-40],rax
       mov       [rbp-48],rax
       mov       rbx,rcx
       mov       rsi,[rbx+8]
       mov       rcx,rsi
       cmp       [rcx],cl
       cmp       [rcx],cl
       mov       rcx,rsi
       cmp       [rcx],cl
       mov       rdi,[rsi+8]
       test      rdi,rdi
       je        near ptr M00_L04
M00_L00:
       mov       rcx,offset MT_System.Boolean
       call      CORINFO_HELP_NEWSFAST
       mov       byte ptr [rax+8],1
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rdi],r15
       jne       near ptr M00_L06
       mov       rdx,[rdi+8]
       mov       [rsp+20],rax
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-40]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       rcx,rdi
       mov       r8,24480456810
       call      qword ptr [7FF9B3D1EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       xor       r8d,r8d
       mov       [rbp-40],r8
M00_L01:
       mov       rbx,[rbx+8]
       mov       rsi,[rbx+8]
       test      rsi,rsi
       je        near ptr M00_L07
M00_L02:
       mov       rcx,offset MT_System.DateTimeOffset
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rcx,[rbp-38]
       call      qword ptr [7FF9B3DA4AB0]; System.DateTimeOffset.get_UtcNow()
       vmovups   xmm0,[rbp-38]
       vmovups   [rbx+8],xmm0
       cmp       [rsi],r15
       jne       near ptr M00_L09
       mov       rdx,[rsi+8]
       mov       [rsp+20],rbx
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-48]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       rcx,rsi
       mov       r8,244804592F8
       call      qword ptr [7FF9B3D1EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
M00_L03:
       nop
       add       rsp,78
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r14
       pop       r15
       pop       rbp
       ret
M00_L04:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3DA5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-58],rdi
       mov       [rbp-4C],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L05
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rcx,244F5800068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r15
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r15
       call      CORINFO_HELP_ASSIGN_REF
M00_L05:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rdi,rsi
       jmp       near ptr M00_L00
M00_L06:
       mov       r8,rax
       mov       rcx,rdi
       mov       r11,7FF9B39505E8
       mov       rdx,24480456810
       call      qword ptr [r11]
       jmp       near ptr M00_L01
M00_L07:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3DA5548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-60],rsi
       mov       [rbp-50],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L08
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,244F5800068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D1C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L08:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
       mov       rsi,rbx
       jmp       near ptr M00_L02
M00_L09:
       mov       r8,rbx
       mov       rcx,rsi
       mov       r11,7FF9B39505F0
       mov       rdx,244804592F8
       call      qword ptr [r11]
       jmp       near ptr M00_L03
       sub       rsp,48
       cmp       qword ptr [rbp-58],0
       je        short M00_L10
       mov       rcx,[rbp-58]
       mov       edx,[rbp-4C]
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
M00_L10:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-60],0
       je        short M00_L11
       mov       rcx,[rbp-60]
       mov       edx,[rbp-50]
       call      qword ptr [7FF9B3DA5620]; System.Threading.Lock.Exit(ThreadId)
M00_L11:
       nop
       add       rsp,48
       ret
; Total bytes of code 682
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-40],rax
       mov       [rbp-20],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       movzx     r9d,r9b
       mov       rax,[rbp+18]
       mov       rax,[rax+8]
       mov       [rbp-40],rax
       mov       ebx,[rbp+2C]
       test      r9d,r9d
       jne       near ptr M01_L29
       cmp       byte ptr [rcx+19],0
       jne       near ptr M01_L28
       mov       rax,[rcx]
       mov       r8,[rax+30]
       mov       r8,[r8]
       mov       r11,[r8+78]
       test      r11,r11
       je        near ptr M01_L27
M01_L00:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
M01_L01:
       mov       [rbp-24],eax
M01_L02:
       mov       rax,[rbp+18]
       mov       rcx,[rax+18]
       mov       [rbp-48],rcx
       mov       r8,[rbp+10]
       cmp       [r8],r8d
       mov       rax,[rbp+18]
       mov       r10,[rax+10]
       mov       rax,[rbp+18]
       mov       r9d,[rbp-24]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       mov       [rbp-28],edx
       cmp       r9d,r11d
       jae       near ptr M01_L36
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       xor       ecx,ecx
       mov       [rbp-2C],ecx
       mov       [rbp-30],ecx
       mov       [rbp-34],ecx
       cmp       byte ptr [rbp+40],0
       je        short M01_L04
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rsi,[rcx+rax*8+10]
       test      rsi,rsi
       je        near ptr M01_L10
       mov       rcx,rsi
       call      00007FFA135C0070
       test      eax,eax
       je        near ptr M01_L11
M01_L03:
       mov       dword ptr [rbp-34],1
M01_L04:
       mov       rcx,[rbp+18]
       mov       r8,[rbp+10]
       cmp       rcx,[r8+8]
       jne       near ptr M01_L12
       xor       esi,esi
       mov       rdi,[rbx]
       test      rdi,rdi
       je        near ptr M01_L19
M01_L05:
       mov       ecx,[rbp-24]
       cmp       ecx,[rdi+20]
       jne       near ptr M01_L17
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rax,[rdx+68]
       test      rax,rax
       je        short M01_L08
       mov       rcx,rax
M01_L06:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+80]
       test      r11,r11
       je        short M01_L09
M01_L07:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-40]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       r8,[rbp+10]
       je        near ptr M01_L17
       cmp       byte ptr [rbp+38],0
       je        near ptr M01_L18
       lea       rcx,[rdi+10]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L08:
       mov       rdx,7FF9B3EA0D88
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       short M01_L06
M01_L09:
       mov       rdx,7FF9B3EA10C0
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L07
M01_L10:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E86AA8]
       int       3
M01_L11:
       mov       rcx,rsi
       call      qword ptr [7FF9B3E86AF0]
       jmp       near ptr M01_L03
M01_L12:
       mov       rcx,[r8+8]
       mov       [rbp+18],rcx
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+18]
       cmp       rcx,[rdx+8]
       je        near ptr M01_L31
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+8]
       mov       [rbp-40],rcx
       cmp       byte ptr [r8+19],0
       jne       short M01_L15
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+78]
       test      r11,r11
       je        short M01_L13
       jmp       short M01_L14
M01_L13:
       mov       rdx,7FF9B3EA0F80
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M01_L14:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M01_L16
M01_L15:
       mov       rcx,[rbp+20]
       mov       rax,[rcx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
M01_L16:
       mov       [rbp-24],eax
       mov       r8,[rbp+10]
       jmp       near ptr M01_L31
M01_L17:
       inc       esi
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M01_L05
       jmp       short M01_L19
M01_L18:
       mov       rdx,[rdi+10]
       mov       rcx,[rbp+48]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L19:
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rdx,[rdx+70]
       test      rdx,rdx
       je        short M01_L22
       jmp       short M01_L23
M01_L20:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L21:
       call      CORINFO_HELP_OVERFLOW
       int       3
M01_L22:
       mov       rdx,7FF9B3EA0E10
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdx,rax
M01_L23:
       mov       rcx,rdx
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       rcx,[rbx]
       mov       [rsp+20],rcx
       mov       rcx,rdi
       mov       rdx,[rbp+20]
       mov       r8,[rbp+30]
       mov       r9d,[rbp-24]
       call      qword ptr [7FF9B3E86BC8]
       mov       rcx,rbx
       mov       rdx,rdi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L20
       mov       ecx,[rbp-28]
       lea       rdx,[rdx+rcx*4+10]
       mov       ecx,[rdx]
       add       ecx,1
       jo        short M01_L21
       mov       [rdx],ecx
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       ecx,[rbp-28]
       mov       edx,[rdx+rcx*4+10]
       mov       r8,[rbp+10]
       cmp       edx,[r8+10]
       jle       short M01_L24
       mov       dword ptr [rbp-2C],1
M01_L24:
       cmp       esi,64
       jbe       near ptr M01_L30
       mov       rdx,[rbp-40]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer
       call      qword ptr [7FF9B3A06850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        near ptr M01_L30
       mov       dword ptr [rbp-30],1
       jmp       short M01_L30
M01_L25:
       cmp       dword ptr [rbp-34],0
       je        short M01_L26
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L36
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L32
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       jne       short M01_L33
M01_L26:
       xor       eax,eax
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L27:
       mov       rcx,rax
       mov       rdx,7FF9B3EA0F80
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L28:
       mov       rdx,[rbp+20]
       mov       rcx,rdx
       mov       rax,[rdx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M01_L01
M01_L29:
       mov       eax,ebx
       jmp       near ptr M01_L01
M01_L30:
       call      M01_L37
       jmp       short M01_L34
M01_L31:
       call      M01_L37
       jmp       near ptr M01_L02
M01_L32:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E86AA8]
       int       3
M01_L33:
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E86AC0]
       jmp       short M01_L26
M01_L34:
       mov       ecx,[rbp-2C]
       or        ecx,[rbp-30]
       je        short M01_L35
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+18]
       mov       r8d,[rbp-2C]
       mov       r9d,[rbp-30]
       call      qword ptr [7FF9B3D1F108]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
M01_L35:
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L36:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L37:
       sub       rsp,28
       cmp       dword ptr [rbp-34],0
       je        short M01_L38
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L40
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L39
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       je        short M01_L38
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E86AC0]
M01_L38:
       nop
       add       rsp,28
       ret
M01_L39:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E86AA8]
       int       3
M01_L40:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1188
```
```assembly
; System.DateTimeOffset.get_UtcNow()
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       lea       rcx,[rsp+28]
       mov       rax,7FFB0F697650
       call      rax
       mov       rsi,[rsp+28]
       mov       rax,244F5800950
       mov       rdi,[rax]
       sub       rsi,[rdi+8]
       cmp       dword ptr [7FFA1394F778],0
       jne       short M02_L03
M02_L00:
       mov       eax,0B2D05E00
       cmp       rsi,rax
       jb        short M02_L01
       call      qword ptr [7FF9B3DA54B8]; System.DateTime.UpdateLeapSecondCacheAndReturnUtcNow()
       jmp       short M02_L02
M02_L01:
       add       rsi,[rdi+10]
       mov       rax,rsi
M02_L02:
       mov       rcx,3FFFFFFFFFFFFFFF
       and       rax,rcx
       xor       ecx,ecx
       mov       [rbx],ecx
       mov       [rbx+8],rax
       mov       rax,rbx
       add       rsp,30
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M02_L03:
       call      CORINFO_HELP_POLL_GC
       jmp       short M02_L00
; Total bytes of code 122
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M03_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M03_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M03_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M03_L00:
       add       rsp,30
       pop       rbx
       ret
M03_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M03_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M04_L10
M04_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M04_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M04_L02
M04_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M04_L01
M04_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M04_L06
M04_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M04_L07
M04_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M04_L09
M04_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M04_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M04_L03
M04_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M04_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M04_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M04_L08:
       mov       r14,rax
       jmp       near ptr M04_L05
M04_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M04_L05
M04_L10:
       cmp       edi,0FFFFFFFF
       je        short M04_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M04_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M04_L00
M04_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M05_L02
       cmp       dword ptr [rcx+18],0
       jne       short M05_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M05_L03
M05_L00:
       add       rsp,28
       ret
M05_L01:
       dec       dword ptr [rcx+18]
       jmp       short M05_L00
M05_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M05_L03:
       call      qword ptr [7FF964230260]
       jmp       short M05_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M07_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M07_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
M07_L00:
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       jne       short M07_L03
M07_L01:
       xor       edx,edx
M07_L02:
       mov       rax,rdx
       ret
M07_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       jmp       short M07_L00
; Total bytes of code 86
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp-40],rcx
       mov       [rbp+10],rcx
       mov       rbx,rdx
       mov       esi,r8d
       mov       edi,r9d
       xor       eax,eax
       mov       [rbp-48],eax
       mov       rax,[rcx+8]
       mov       rax,[rax+18]
       cmp       dword ptr [rax+8],0
       jbe       near ptr M08_L15
       mov       rcx,[rax+10]
       call      qword ptr [7FFA759A0078]; Precode of System.Threading.Monitor.Enter(System.Object)
       mov       dword ptr [rbp-48],1
       mov       rcx,[rbp+10]
       cmp       rbx,[rcx+8]
       jne       near ptr M08_L18
       mov       rax,[rbx+10]
       mov       r14d,[rax+8]
       xor       r15d,r15d
       test      dil,dil
       jne       near ptr M08_L13
M08_L00:
       test      sil,sil
       je        short M08_L02
       test      r15,r15
       jne       short M08_L01
       mov       rcx,[rbp+10]
       call      qword ptr [7FFA759A08F8]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetCountNoLocks()
       mov       rcx,[rbx+10]
       mov       ecx,[rcx+8]
       shr       ecx,2
       cmp       eax,ecx
       jl        near ptr M08_L12
M08_L01:
       mov       rax,[rbx+10]
       mov       eax,[rax+8]
       add       eax,eax
       js        near ptr M08_L17
       mov       ecx,eax
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       r14d,eax
       call      qword ptr [7FFA7599FF68]
       cmp       eax,r14d
       jl        near ptr M08_L17
M08_L02:
       mov       rsi,[rbx+18]
       mov       rdi,rsi
       mov       rcx,[rbp+10]
       cmp       byte ptr [rcx+18],0
       je        short M08_L04
       cmp       dword ptr [rsi+8],400
       jge       short M08_L04
       mov       eax,[rsi+8]
       add       eax,eax
       movsxd    rcx,eax
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r8d,[rsi+8]
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FFA7599FF50]
       mov       rax,[rbx+18]
       mov       esi,[rax+8]
       mov       r13d,[rdi+8]
       cmp       r13d,esi
       jle       short M08_L04
M08_L03:
       call      qword ptr [7FFA7599FE68]
       mov       r8,rax
       movsxd    rdx,esi
       mov       rcx,rdi
       call      qword ptr [7FFA7599F2B0]; Precode of System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       inc       esi
       cmp       r13d,esi
       jg        short M08_L03
M08_L04:
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599FA10]
       mov       rcx,rax
       movsxd    rdx,r14d
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       rsi,rax
       mov       [rbp-60],rsi
       mov       ecx,[rdi+8]
       call      qword ptr [7FFA7599FF18]
       mov       r14,rax
       mov       r12,r15
       test      r12,r12
       jne       short M08_L05
       mov       r12,[rbx+8]
M08_L05:
       mov       rcx,r13
       call      qword ptr [7FFA7599F760]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-78],rax
       lea       rcx,[rax+10]
       mov       rdx,rsi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+20]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       ecx,[rsi+8]
       xor       edx,edx
       div       rcx
       inc       rax
       mov       r12,[rbp-78]
       mov       [r12+28],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F728]
       mov       rcx,rax
       lea       r8,[rbp-48]
       mov       rdx,rbx
       call      qword ptr [7FFA759A0918]
       mov       rbx,[rbx+10]
       xor       eax,eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jg        near ptr M08_L10
M08_L06:
       mov       rsi,[rbp-60]
       mov       eax,[rsi+8]
       xor       edx,edx
       div       dword ptr [rdi+8]
       mov       ecx,1
       cmp       eax,1
       cmovg     ecx,eax
       mov       rax,[rbp+10]
       mov       [rax+10],ecx
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M08_L18
M08_L07:
       test      r15,r15
       jne       near ptr M08_L11
       mov       [rbp-68],rdx
       mov       r8d,[rdx+20]
M08_L08:
       mov       rdx,[rbp-68]
       mov       r10,[rdx+18]
       mov       [rbp-80],r10
       mov       rcx,[r12+10]
       mov       [rbp-4C],r8d
       mov       r9d,r8d
       imul      r9,[r12+28]
       shr       r9,20
       inc       r9
       mov       r11d,[rcx+8]
       mov       esi,r11d
       imul      r9,rsi
       shr       r9,20
       mov       rsi,[r12+18]
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rsi+8]
       mov       esi,edx
       cmp       r9d,r11d
       jae       near ptr M08_L15
       mov       eax,r9d
       lea       rax,[rcx+rax*8+10]
       mov       [rbp-70],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F748]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-88],rax
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       r8,[r8+10]
       mov       [rbp-90],r8
       mov       r10,[rbp-70]
       mov       r9,[r10]
       mov       [rbp-98],r9
       lea       rcx,[rax+8]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+10]
       mov       rdx,[rbp-90]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+18]
       mov       rdx,[rbp-98]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       mov       ecx,[rbp-4C]
       mov       [rax+20],ecx
       mov       rcx,[rbp-70]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       cmp       esi,[r14+8]
       jae       near ptr M08_L15
       mov       eax,esi
       lea       rax,[r14+rax*4+10]
       mov       edx,[rax]
       add       edx,1
       jo        near ptr M08_L16
       mov       [rax],edx
       mov       rsi,[rbp-80]
       test      rsi,rsi
       mov       rdx,rsi
       jne       near ptr M08_L07
M08_L09:
       mov       rax,[rbp-58]
       inc       eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jle       near ptr M08_L06
M08_L10:
       mov       [rbp-58],rax
       mov       rdx,[rbx+rax*8+10]
       test      rdx,rdx
       jne       near ptr M08_L07
       jmp       short M08_L09
M08_L11:
       mov       [rbp-68],rdx
       mov       rcx,[rbp+10]
       mov       rcx,[rcx]
       call      qword ptr [7FFA7599FBD8]
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       rcx,r15
       mov       r11,rax
       call      qword ptr [rax]
       mov       r8d,eax
       jmp       near ptr M08_L08
M08_L12:
       mov       rcx,[rbp+10]
       mov       eax,[rcx+10]
       add       eax,eax
       mov       [rcx+10],eax
       test      eax,eax
       jge       near ptr M08_L18
       jmp       short M08_L14
M08_L13:
       mov       rcx,[rbx+8]
       call      qword ptr [7FFA7599FF30]
       mov       rdi,rax
       test      rdi,rdi
       je        near ptr M08_L00
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599F550]
       mov       r15,rax
       mov       rcx,rdi
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       call      qword ptr [r11]
       mov       rdx,rax
       mov       rcx,r15
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
       mov       r15,rax
       jmp       near ptr M08_L00
M08_L14:
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       short M08_L18
M08_L15:
       call      qword ptr [7FFA7599F290]
       int       3
M08_L16:
       call      qword ptr [7FFA7599F288]
       int       3
M08_L17:
       call      qword ptr [7FFA7599FF68]
       mov       r14d,eax
       mov       rcx,[rbp+10]
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       near ptr M08_L02
M08_L18:
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
       sub       rsp,28
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,28
       ret
; Total bytes of code 1137
```
```assembly
; System.DateTime.UpdateLeapSecondCacheAndReturnUtcNow()
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,68
       lea       rbp,[rsp+80]
       call      qword ptr [7FF964217418]
       mov       rax,[rax]
       lea       rcx,[rbp-20]
       call      rax
       mov       rdx,346DC5D63886594B
       mov       rax,rdx
       mul       qword ptr [rbp-20]
       shr       rdx,0B
       imul      rax,rdx,2710
       mov       rbx,[rbp-20]
       sub       rbx,rax
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       cmp       dword ptr [rax],0
       jne       near ptr M09_L04
M09_L00:
       lea       rcx,[rbp-20]
       lea       rdx,[rbp-30]
       call      qword ptr [7FF964244680]
       test      eax,eax
       je        near ptr M09_L05
       cmp       word ptr [rbp-24],3C
       jae       near ptr M09_L06
       mov       eax,0B2D05E00
       add       rax,[rbp-20]
       mov       [rbp-38],rax
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       cmp       dword ptr [rax],0
       jne       near ptr M09_L07
M09_L01:
       lea       rcx,[rbp-38]
       lea       rdx,[rbp-48]
       call      qword ptr [7FF964244680]
       test      eax,eax
       je        short M09_L05
       movzx     ecx,word ptr [rbp-3C]
       cmp       cx,[rbp-24]
       jne       short M09_L08
       mov       rsi,[rbp-20]
       lea       rcx,[rbp-30]
       mov       rdx,rbx
       call      qword ptr [7FF96422A910]
       mov       rbx,rax
M09_L02:
       call      qword ptr [7FF964221680]
       mov       rdi,rax
       call      qword ptr [7FF9642176A0]
       mov       [rdi+8],rsi
       mov       [rdi+10],rbx
       mov       rcx,rax
       mov       rdx,rdi
       call      qword ptr [7FF964216FD8]; CORINFO_HELP_CHECKED_ASSIGN_REF
       add       rbx,[rbp-20]
       sub       rbx,rsi
       mov       rax,rbx
M09_L03:
       add       rsp,68
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M09_L04:
       call      qword ptr [7FF964217028]; CORINFO_HELP_POLL_GC
       jmp       near ptr M09_L00
M09_L05:
       call      qword ptr [7FF96422A930]
       jmp       short M09_L03
M09_L06:
       lea       rcx,[rbp-30]
       mov       rdx,rbx
       call      qword ptr [7FF96422A910]
       jmp       short M09_L03
M09_L07:
       call      qword ptr [7FF964217028]; CORINFO_HELP_POLL_GC
       jmp       near ptr M09_L01
M09_L08:
       movups    xmm0,[rbp-30]
       movups    [rbp-58],xmm0
       mov       word ptr [rbp-50],0
       mov       word ptr [rbp-4E],0
       mov       word ptr [rbp-4C],0
       mov       word ptr [rbp-4A],0
       lea       rcx,[rbp-58]
       lea       rdx,[rbp-60]
       call      qword ptr [7FF964227FB0]
       test      eax,eax
       je        short M09_L05
       mov       rsi,0C87700CB80
       add       rsi,[rbp-60]
       mov       rcx,[rbp-20]
       sub       rcx,rsi
       mov       edx,0B2D05E00
       cmp       rcx,rdx
       jae       short M09_L06
       lea       rcx,[rbp-58]
       xor       edx,edx
       call      qword ptr [7FF96422A910]
       mov       rbx,0C87700CB80
       add       rbx,rax
       jmp       near ptr M09_L02
; Total bytes of code 402
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M10_L01
       test      rsi,rsi
       je        short M10_L02
       cmp       [rsi+8],ebx
       jl        short M10_L02
       add       rsi,10
M10_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M10_L00
M10_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M10_L02:
       mov       ecx,[rsi+8]
M10_L03:
       cmp       edi,[rsi+8]
       jae       short M10_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M10_L03
       jmp       short M10_L01
M10_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.CompoundOperation_TransportReceiverPattern()
       push      rbp
       push      r15
       push      r14
       push      r13
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0B0
       lea       rbp,[rsp+0E0]
       xor       eax,eax
       mov       [rbp-58],rax
       vxorps    xmm4,xmm4,xmm4
       vmovdqu   ymmword ptr [rbp-50],ymm4
       mov       rbx,rcx
       mov       rsi,[rbx+8]
       cmp       qword ptr [rsi+8],0
       je        near ptr M00_L10
       mov       rcx,[rsi+8]
M00_L00:
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       cmp       [rcx],r15
       jne       near ptr M00_L12
       mov       rdx,[rcx+8]
       mov       r9,19B802D92F8
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-38]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,19B802D6848
       call      qword ptr [7FF9B3D0EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       xor       ecx,ecx
       mov       [rbp-38],rcx
M00_L01:
       mov       rsi,[rbx+8]
       cmp       qword ptr [rsi+8],0
       je        near ptr M00_L13
       mov       rcx,[rsi+8]
M00_L02:
       cmp       [rcx],r15
       jne       near ptr M00_L15
       mov       rdx,[rcx+8]
       mov       r9,19B802D9358
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-40]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,19B802D9328
       call      qword ptr [7FF9B3D0EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       xor       ecx,ecx
       mov       [rbp-40],rcx
M00_L03:
       mov       rsi,[rbx+8]
       cmp       qword ptr [rsi+8],0
       je        near ptr M00_L16
       mov       rcx,[rsi+8]
M00_L04:
       cmp       [rcx],r15
       jne       near ptr M00_L18
       mov       rdx,[rcx+8]
       mov       r9,19B802D93B8
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-48]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,19B802D9388
       call      qword ptr [7FF9B3D0EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       xor       ecx,ecx
       mov       [rbp-48],rcx
M00_L05:
       mov       rsi,[rbx+8]
       cmp       qword ptr [rsi+8],0
       je        near ptr M00_L19
       mov       rcx,[rsi+8]
M00_L06:
       cmp       [rcx],r15
       jne       near ptr M00_L21
       mov       rdx,[rcx+8]
       mov       r9,19B802D1240
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-50]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,19B802D93E0
       call      qword ptr [7FF9B3D0EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       xor       ecx,ecx
       mov       [rbp-50],rcx
M00_L07:
       mov       rbx,[rbx+8]
       cmp       qword ptr [rbx+8],0
       je        near ptr M00_L22
       mov       rcx,[rbx+8]
M00_L08:
       cmp       [rcx],r15
       jne       near ptr M00_L24
       mov       rdx,[rcx+8]
       mov       r9,19B802D1240
       mov       [rsp+20],r9
       mov       dword ptr [rsp+28],1
       mov       dword ptr [rsp+30],1
       lea       r9,[rbp-58]
       mov       [rsp+38],r9
       xor       r9d,r9d
       mov       r8,19B802D9410
       call      qword ptr [7FF9B3D0EC88]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
M00_L09:
       nop
       add       rsp,0B0
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M00_L10:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3D95548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-78],rdi
       mov       [rbp-5C],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L11
       mov       r15,offset MT_System.Collections.Concurrent.ConcurrentDictionary<System.String, System.Object>
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r15,rax
       mov       rcx,19B8AC00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r15
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r15
       call      CORINFO_HELP_ASSIGN_REF
M00_L11:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rsi
       jmp       near ptr M00_L00
M00_L12:
       mov       r11,7FF9B3940618
       mov       rdx,19B802D6848
       mov       r8,19B802D92F8
       call      qword ptr [r11]
       jmp       near ptr M00_L01
M00_L13:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3D95548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-80],rdi
       mov       [rbp-60],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L14
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rcx,19B8AC00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r13
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r13
       call      CORINFO_HELP_ASSIGN_REF
M00_L14:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rsi
       jmp       near ptr M00_L02
M00_L15:
       mov       r11,7FF9B3940620
       mov       rdx,19B802D9328
       mov       r8,19B802D9358
       call      qword ptr [r11]
       jmp       near ptr M00_L03
M00_L16:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3D95548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-88],rdi
       mov       [rbp-64],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L17
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rcx,19B8AC00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r13
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r13
       call      CORINFO_HELP_ASSIGN_REF
M00_L17:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rsi
       jmp       near ptr M00_L04
M00_L18:
       mov       r11,7FF9B3940628
       mov       rdx,19B802D9388
       mov       r8,19B802D93B8
       call      qword ptr [r11]
       jmp       near ptr M00_L05
M00_L19:
       mov       rdi,[rsi+10]
       cmp       [rdi],dil
       mov       rcx,rdi
       call      qword ptr [7FF9B3D95548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       r14d,eax
       mov       [rbp-90],rdi
       mov       [rbp-68],r14d
       cmp       qword ptr [rsi+8],0
       jne       short M00_L20
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       mov       rcx,19B8AC00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r13
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rsi+8]
       mov       rdx,r13
       call      CORINFO_HELP_ASSIGN_REF
M00_L20:
       mov       rsi,[rsi+8]
       mov       rcx,rdi
       mov       edx,r14d
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rsi
       jmp       near ptr M00_L06
M00_L21:
       mov       r11,7FF9B3940630
       mov       rdx,19B802D93E0
       mov       r8,19B802D1240
       call      qword ptr [r11]
       jmp       near ptr M00_L07
M00_L22:
       mov       rsi,[rbx+10]
       cmp       [rsi],sil
       mov       rcx,rsi
       call      qword ptr [7FF9B3D95548]; System.Threading.Lock.EnterAndGetCurrentThreadId()
       mov       edi,eax
       mov       [rbp-98],rsi
       mov       [rbp-6C],edi
       cmp       qword ptr [rbx+8],0
       jne       short M00_L23
       mov       rcx,r15
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rcx,19B8AC00068
       mov       rcx,[rcx]
       mov       [rsp+20],rcx
       mov       rcx,r14
       mov       edx,20
       mov       r8d,1F
       mov       r9d,1
       call      qword ptr [7FF9B3D0C0C0]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M00_L23:
       mov       rbx,[rbx+8]
       mov       rcx,rsi
       mov       edx,edi
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
       mov       rcx,rbx
       jmp       near ptr M00_L08
M00_L24:
       mov       r11,7FF9B3940638
       mov       rdx,19B802D9410
       mov       r8,19B802D1240
       call      qword ptr [r11]
       jmp       near ptr M00_L09
       sub       rsp,48
       cmp       qword ptr [rbp-78],0
       je        short M00_L25
       mov       rcx,[rbp-78]
       mov       edx,[rbp-5C]
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
M00_L25:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-80],0
       je        short M00_L26
       mov       rcx,[rbp-80]
       mov       edx,[rbp-60]
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
M00_L26:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-88],0
       je        short M00_L27
       mov       rcx,[rbp-88]
       mov       edx,[rbp-64]
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
M00_L27:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-90],0
       je        short M00_L28
       mov       rcx,[rbp-90]
       mov       edx,[rbp-68]
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
M00_L28:
       nop
       add       rsp,48
       ret
       sub       rsp,48
       cmp       qword ptr [rbp-98],0
       je        short M00_L29
       mov       rcx,[rbp-98]
       mov       edx,[rbp-6C]
       call      qword ptr [7FF9B3D95620]; System.Threading.Lock.Exit(ThreadId)
M00_L29:
       nop
       add       rsp,48
       ret
; Total bytes of code 1550
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].TryAddInternal(Tables<System.__Canon,System.__Canon>, System.__Canon, System.Nullable`1<Int32>, System.__Canon, Boolean, Boolean, System.__Canon ByRef)
       push      rbp
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,58
       lea       rbp,[rsp+70]
       xor       eax,eax
       mov       [rbp-40],rax
       mov       [rbp-20],rcx
       mov       [rbp+10],rcx
       mov       [rbp+18],rdx
       mov       [rbp+20],r8
       mov       [rbp+28],r9
       movzx     r9d,r9b
       mov       rax,[rbp+18]
       mov       rax,[rax+8]
       mov       [rbp-40],rax
       mov       ebx,[rbp+2C]
       test      r9d,r9d
       jne       near ptr M01_L29
       cmp       byte ptr [rcx+19],0
       jne       near ptr M01_L28
       mov       rax,[rcx]
       mov       r8,[rax+30]
       mov       r8,[r8]
       mov       r11,[r8+78]
       test      r11,r11
       je        near ptr M01_L27
M01_L00:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
M01_L01:
       mov       [rbp-24],eax
M01_L02:
       mov       rax,[rbp+18]
       mov       rcx,[rax+18]
       mov       [rbp-48],rcx
       mov       r8,[rbp+10]
       cmp       [r8],r8d
       mov       rax,[rbp+18]
       mov       r10,[rax+10]
       mov       rax,[rbp+18]
       mov       r9d,[rbp-24]
       imul      r9,[rax+28]
       shr       r9,20
       inc       r9
       mov       r11d,[r10+8]
       mov       ebx,r11d
       imul      r9,rbx
       shr       r9,20
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rcx+8]
       mov       [rbp-28],edx
       cmp       r9d,r11d
       jae       near ptr M01_L36
       mov       ecx,r9d
       lea       rbx,[r10+rcx*8+10]
       xor       ecx,ecx
       mov       [rbp-2C],ecx
       mov       [rbp-30],ecx
       mov       [rbp-34],ecx
       cmp       byte ptr [rbp+40],0
       je        short M01_L04
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rsi,[rcx+rax*8+10]
       test      rsi,rsi
       je        near ptr M01_L10
       mov       rcx,rsi
       call      00007FFA135C0070
       test      eax,eax
       je        near ptr M01_L11
M01_L03:
       mov       dword ptr [rbp-34],1
M01_L04:
       mov       rcx,[rbp+18]
       mov       r8,[rbp+10]
       cmp       rcx,[r8+8]
       jne       near ptr M01_L12
       xor       esi,esi
       mov       rdi,[rbx]
       test      rdi,rdi
       je        near ptr M01_L19
M01_L05:
       mov       ecx,[rbp-24]
       cmp       ecx,[rdi+20]
       jne       near ptr M01_L17
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rax,[rdx+68]
       test      rax,rax
       je        short M01_L08
       mov       rcx,rax
M01_L06:
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+80]
       test      r11,r11
       je        short M01_L09
M01_L07:
       mov       rdx,[rdi+8]
       mov       rcx,[rbp-40]
       mov       r8,[rbp+20]
       call      qword ptr [r11]
       test      eax,eax
       mov       r8,[rbp+10]
       je        near ptr M01_L17
       cmp       byte ptr [rbp+38],0
       je        near ptr M01_L18
       lea       rcx,[rdi+10]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L08:
       mov       rdx,7FF9B3E90CA0
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       short M01_L06
M01_L09:
       mov       rdx,7FF9B3E90FD8
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       short M01_L07
M01_L10:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E76A00]
       int       3
M01_L11:
       mov       rcx,rsi
       call      qword ptr [7FF9B3E76A48]; System.Threading.Monitor.Enter_Slowpath(System.Object)
       jmp       near ptr M01_L03
M01_L12:
       mov       rcx,[r8+8]
       mov       [rbp+18],rcx
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+18]
       cmp       rcx,[rdx+8]
       je        near ptr M01_L31
       mov       rcx,[rbp+18]
       mov       rcx,[rcx+8]
       mov       [rbp-40],rcx
       cmp       byte ptr [r8+19],0
       jne       short M01_L15
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       r11,[rdx+78]
       test      r11,r11
       je        short M01_L13
       jmp       short M01_L14
M01_L13:
       mov       rdx,7FF9B3E90E98
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
M01_L14:
       mov       rcx,[rbp-40]
       mov       rdx,[rbp+20]
       call      qword ptr [r11]
       jmp       short M01_L16
M01_L15:
       mov       rcx,[rbp+20]
       mov       rax,[rcx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
M01_L16:
       mov       [rbp-24],eax
       mov       r8,[rbp+10]
       jmp       near ptr M01_L31
M01_L17:
       inc       esi
       mov       rdi,[rdi+18]
       test      rdi,rdi
       jne       near ptr M01_L05
       jmp       short M01_L19
M01_L18:
       mov       rdx,[rdi+10]
       mov       rcx,[rbp+48]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       jmp       near ptr M01_L25
M01_L19:
       mov       rcx,[r8]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rdx,[rdx+70]
       test      rdx,rdx
       je        short M01_L22
       jmp       short M01_L23
M01_L20:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L21:
       call      CORINFO_HELP_OVERFLOW
       int       3
M01_L22:
       mov       rdx,7FF9B3E90D28
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdx,rax
M01_L23:
       mov       rcx,rdx
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       rcx,[rbx]
       mov       [rsp+20],rcx
       mov       rcx,rdi
       mov       rdx,[rbp+20]
       mov       r8,[rbp+30]
       mov       r9d,[rbp-24]
       call      qword ptr [7FF9B3E76B20]
       mov       rcx,rbx
       mov       rdx,rdi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L20
       mov       ecx,[rbp-28]
       lea       rdx,[rdx+rcx*4+10]
       mov       ecx,[rdx]
       add       ecx,1
       jo        short M01_L21
       mov       [rdx],ecx
       mov       rdx,[rbp+18]
       mov       rdx,[rdx+20]
       mov       ecx,[rdx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L20
       mov       ecx,[rbp-28]
       mov       edx,[rdx+rcx*4+10]
       mov       r8,[rbp+10]
       cmp       edx,[r8+10]
       jle       short M01_L24
       mov       dword ptr [rbp-2C],1
M01_L24:
       cmp       esi,64
       jbe       near ptr M01_L30
       mov       rdx,[rbp-40]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer
       call      qword ptr [7FF9B39F6850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        near ptr M01_L30
       mov       dword ptr [rbp-30],1
       jmp       short M01_L30
M01_L25:
       cmp       dword ptr [rbp-34],0
       je        short M01_L26
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       near ptr M01_L36
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L32
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       jne       short M01_L33
M01_L26:
       xor       eax,eax
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L27:
       mov       rcx,rax
       mov       rdx,7FF9B3E90E98
       call      qword ptr [7FF9B39FF4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       r11,rax
       jmp       near ptr M01_L00
M01_L28:
       mov       rdx,[rbp+20]
       mov       rcx,rdx
       mov       rax,[rdx]
       mov       rax,[rax+40]
       call      qword ptr [rax+18]
       jmp       near ptr M01_L01
M01_L29:
       mov       eax,ebx
       jmp       near ptr M01_L01
M01_L30:
       call      M01_L37
       jmp       short M01_L34
M01_L31:
       call      M01_L37
       jmp       near ptr M01_L02
M01_L32:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E76A00]
       int       3
M01_L33:
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E76A18]
       jmp       short M01_L26
M01_L34:
       mov       ecx,[rbp-2C]
       or        ecx,[rbp-30]
       je        short M01_L35
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+18]
       mov       r8d,[rbp-2C]
       mov       r9d,[rbp-30]
       call      qword ptr [7FF9B3D0F108]; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
M01_L35:
       mov       rcx,[rbp+48]
       mov       rdx,[rbp+30]
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       eax,1
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       rbp
       ret
M01_L36:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L37:
       sub       rsp,28
       cmp       dword ptr [rbp-34],0
       je        short M01_L38
       mov       rcx,[rbp-48]
       mov       ecx,[rcx+8]
       cmp       [rbp-28],ecx
       jae       short M01_L40
       mov       rcx,[rbp-48]
       mov       eax,[rbp-28]
       mov       rbx,[rcx+rax*8+10]
       test      rbx,rbx
       je        short M01_L39
       mov       rcx,rbx
       call      00007FFA135EBB70
       test      eax,eax
       je        short M01_L38
       mov       ecx,eax
       mov       rdx,rbx
       call      qword ptr [7FF9B3E76A18]
M01_L38:
       nop
       add       rsp,28
       ret
M01_L39:
       xor       ecx,ecx
       call      qword ptr [7FF9B3E76A00]
       int       3
M01_L40:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 1188
```
```assembly
; System.Threading.Lock.EnterAndGetCurrentThreadId()
       push      rbx
       sub       rsp,30
       mov       rbx,rcx
       call      qword ptr [7FF964218E38]
       mov       r8d,[rax+10]
       test      r8d,r8d
       je        short M02_L01
       mov       eax,[rbx+14]
       mov       [rsp+2C],eax
       test      al,3
       jne       short M02_L01
       lea       ecx,[rax+1]
       lea       rdx,[rbx+14]
       lock cmpxchg [rdx],ecx
       mov       ecx,[rsp+2C]
       cmp       eax,ecx
       jne       short M02_L01
       mov       [rbx+10],r8d
       mov       eax,r8d
M02_L00:
       add       rsp,30
       pop       rbx
       ret
M02_L01:
       mov       rcx,rbx
       mov       edx,0FFFFFFFF
       call      qword ptr [7FF964230248]
       jmp       short M02_L00
; Total bytes of code 82
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]]..ctor(Int32, Int32, Boolean, System.Collections.Generic.IEqualityComparer`1<System.__Canon>)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,38
       mov       [rsp+30],rcx
       mov       rsi,rcx
       mov       edi,edx
       mov       ebx,r8d
       mov       ebp,r9d
       mov       r14,[rsp+0A0]
       test      edi,edi
       jle       near ptr M03_L10
M03_L00:
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       ecx,ebx
       call      qword ptr [7FFA759A0238]; Precode of System.ArgumentOutOfRangeException.ThrowIfNegative[[System.Int32, System.Private.CoreLib]](Int32, System.String)
       cmp       ebx,edi
       cmovl     ebx,edi
       mov       ecx,ebx
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       ebx,eax
       movsxd    rcx,edi
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r15d,[rdi+8]
       test      r15d,r15d
       je        near ptr M03_L12
       lea       rcx,[rdi+10]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       r13d,1
       cmp       r15d,1
       jle       short M03_L02
M03_L01:
       call      qword ptr [7FFA7599FE68]
       lea       rcx,[rdi+r13*8+10]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       inc       r13d
       cmp       r15d,r13d
       jg        short M03_L01
M03_L02:
       mov       ecx,r15d
       call      qword ptr [7FFA7599FF18]
       mov       r13,rax
       mov       r12,[rsi]
       mov       rcx,r12
       call      qword ptr [7FFA7599FA00]
       mov       rcx,rax
       movsxd    rdx,ebx
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       [rsp+28],rax
       test      r14,r14
       je        near ptr M03_L06
M03_L03:
       mov       rcx,r12
       call      qword ptr [7FFA7599F908]
       cmp       rax,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       je        near ptr M03_L07
M03_L04:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       cmp       rax,r14
       je        near ptr M03_L09
M03_L05:
       mov       rcx,r12
       call      qword ptr [7FFA7599F750]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       r12,rax
       lea       rcx,[r12+10]
       mov       rdx,[rsp+28]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r13
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+8]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       rdi,[rsp+28]
       mov       edi,[rdi+8]
       mov       ecx,edi
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [r12+28],rax
       lea       rcx,[rsi+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       [rsi+18],bpl
       mov       [rsi+14],ebx
       mov       eax,edi
       xor       edx,edx
       div       r15d
       mov       [rsi+10],eax
       add       rsp,38
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M03_L06:
       mov       rcx,r12
       call      qword ptr [7FFA7599F4D8]
       mov       rcx,rax
       call      qword ptr [7FFA759A01E0]; Precode of System.Collections.Generic.EqualityComparer`1[[System.__Canon, System.Private.CoreLib]].get_Default()
       mov       r14,rax
       jmp       near ptr M03_L03
M03_L07:
       mov       rcx,r14
       call      qword ptr [7FFA759A0140]; Precode of System.Collections.Generic.NonRandomizedStringEqualityComparer.GetStringComparer(System.Object)
       mov       [rsp+20],rax
       test      rax,rax
       je        near ptr M03_L04
       mov       rcx,r12
       call      qword ptr [7FFA7599F540]
       mov       rcx,rax
       mov       r14,[rsp+20]
       mov       rax,r14
       cmp       [rax],rcx
       je        short M03_L08
       mov       rdx,r14
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
M03_L08:
       mov       r14,rax
       jmp       near ptr M03_L05
M03_L09:
       mov       byte ptr [rsi+19],1
       jmp       near ptr M03_L05
M03_L10:
       cmp       edi,0FFFFFFFF
       je        short M03_L11
       call      qword ptr [7FFA759A03C8]
       mov       rbx,rax
       call      qword ptr [7FFA7599FE80]
       mov       rdi,rax
       mov       rdx,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       mov       rdx,[rdx]
       mov       rcx,rdi
       mov       r8,rbx
       call      qword ptr [7FFA759A0000]
       mov       rcx,rdi
       call      qword ptr [7FFA7599F278]; CORINFO_HELP_THROW
       int       3
M03_L11:
       cmp       [rsi],esi
       call      qword ptr [7FFA7599FFA0]; Precode of System.Environment.get_ProcessorCount()
       mov       edi,eax
       jmp       near ptr M03_L00
M03_L12:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 594
```
```assembly
; System.Threading.Lock.Exit(ThreadId)
       sub       rsp,28
       cmp       [rcx+10],edx
       jne       short M04_L02
       cmp       dword ptr [rcx+18],0
       jne       short M04_L01
       xor       edx,edx
       mov       [rcx+10],edx
       lea       rdx,[rcx+14]
       mov       eax,0FFFFFFFF
       lock xadd [rdx],eax
       lea       edx,[rax-1]
       cmp       edx,80
       jae       short M04_L03
M04_L00:
       add       rsp,28
       ret
M04_L01:
       dec       dword ptr [rcx+18]
       jmp       short M04_L00
M04_L02:
       call      qword ptr [7FF96422D5C8]
       int       3
M04_L03:
       call      qword ptr [7FF964230260]
       jmp       short M04_L00
; Total bytes of code 69
```
```assembly
; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,0A8
       lea       rbp,[rsp+0E0]
       xor       r8d,r8d
       mov       [rsp+20],r8
       mov       r8,rdx
       mov       [rbp-9C],r8
       mov       rdx,rcx
       mov       [rbp-0A4],rdx
       xor       ecx,ecx
       mov       [rbp-0AC],rcx
       mov       r9d,0FFFFFFFF
       mov       [rbp-94],r9d
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       r8,[rbp-9C]
       mov       rdx,[rbp-0A4]
       mov       rcx,[rbp-0AC]
       mov       r9d,[rbp-94]
       call      qword ptr [rax]
       mov       rbx,rax
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       mov       rax,rbx
       add       rsp,0A8
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 166
```
```assembly
; System.Threading.Monitor.Enter_Slowpath(System.Object)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp+10],rcx
       lea       rcx,[rbp+10]
       mov       [rbp-98],rcx
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217018]; CORINFO_HELP_JIT_PINVOKE_BEGIN
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       mov       rcx,[rbp-98]
       call      qword ptr [rax]
       lea       rcx,[rbp-90]
       call      qword ptr [7FF964217020]; CORINFO_HELP_JIT_PINVOKE_END
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
; Total bytes of code 105
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M07_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M07_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
M07_L00:
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       jne       short M07_L03
M07_L01:
       xor       edx,edx
M07_L02:
       mov       rax,rdx
       ret
M07_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       test      rax,rax
       je        short M07_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M07_L02
       jmp       short M07_L00
; Total bytes of code 86
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GrowTable(Tables<System.__Canon,System.__Canon>, Boolean, Boolean)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,88
       lea       rbp,[rsp+0C0]
       mov       [rbp-40],rcx
       mov       [rbp+10],rcx
       mov       rbx,rdx
       mov       esi,r8d
       mov       edi,r9d
       xor       eax,eax
       mov       [rbp-48],eax
       mov       rax,[rcx+8]
       mov       rax,[rax+18]
       cmp       dword ptr [rax+8],0
       jbe       near ptr M08_L15
       mov       rcx,[rax+10]
       call      qword ptr [7FFA759A0078]; Precode of System.Threading.Monitor.Enter(System.Object)
       mov       dword ptr [rbp-48],1
       mov       rcx,[rbp+10]
       cmp       rbx,[rcx+8]
       jne       near ptr M08_L18
       mov       rax,[rbx+10]
       mov       r14d,[rax+8]
       xor       r15d,r15d
       test      dil,dil
       jne       near ptr M08_L13
M08_L00:
       test      sil,sil
       je        short M08_L02
       test      r15,r15
       jne       short M08_L01
       mov       rcx,[rbp+10]
       call      qword ptr [7FFA759A08F8]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].GetCountNoLocks()
       mov       rcx,[rbx+10]
       mov       ecx,[rcx+8]
       shr       ecx,2
       cmp       eax,ecx
       jl        near ptr M08_L12
M08_L01:
       mov       rax,[rbx+10]
       mov       eax,[rax+8]
       add       eax,eax
       js        near ptr M08_L17
       mov       ecx,eax
       call      qword ptr [7FFA759A0408]; Precode of System.Collections.HashHelpers.GetPrime(Int32)
       mov       r14d,eax
       call      qword ptr [7FFA7599FF68]
       cmp       eax,r14d
       jl        near ptr M08_L17
M08_L02:
       mov       rsi,[rbx+18]
       mov       rdi,rsi
       mov       rcx,[rbp+10]
       cmp       byte ptr [rcx+18],0
       je        short M08_L04
       cmp       dword ptr [rsi+8],400
       jge       short M08_L04
       mov       eax,[rsi+8]
       add       eax,eax
       movsxd    rcx,eax
       call      qword ptr [7FFA7599FF10]
       mov       rdi,rax
       mov       r8d,[rsi+8]
       mov       rcx,rsi
       mov       rdx,rdi
       call      qword ptr [7FFA7599FF50]
       mov       rax,[rbx+18]
       mov       esi,[rax+8]
       mov       r13d,[rdi+8]
       cmp       r13d,esi
       jle       short M08_L04
M08_L03:
       call      qword ptr [7FFA7599FE68]
       mov       r8,rax
       movsxd    rdx,esi
       mov       rcx,rdi
       call      qword ptr [7FFA7599F2B0]; Precode of System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       inc       esi
       cmp       r13d,esi
       jg        short M08_L03
M08_L04:
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599FA10]
       mov       rcx,rax
       movsxd    rdx,r14d
       call      qword ptr [7FFA7599F2C8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       rsi,rax
       mov       [rbp-60],rsi
       mov       ecx,[rdi+8]
       call      qword ptr [7FFA7599FF18]
       mov       r14,rax
       mov       r12,r15
       test      r12,r12
       jne       short M08_L05
       mov       r12,[rbx+8]
M08_L05:
       mov       rcx,r13
       call      qword ptr [7FFA7599F760]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-78],rax
       lea       rcx,[rax+10]
       mov       rdx,rsi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+18]
       mov       rdx,rdi
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+20]
       mov       rdx,r14
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-78]
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       ecx,[rsi+8]
       xor       edx,edx
       div       rcx
       inc       rax
       mov       r12,[rbp-78]
       mov       [r12+28],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F728]
       mov       rcx,rax
       lea       r8,[rbp-48]
       mov       rdx,rbx
       call      qword ptr [7FFA759A0918]
       mov       rbx,[rbx+10]
       xor       eax,eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jg        near ptr M08_L10
M08_L06:
       mov       rsi,[rbp-60]
       mov       eax,[rsi+8]
       xor       edx,edx
       div       dword ptr [rdi+8]
       mov       ecx,1
       cmp       eax,1
       cmovg     ecx,eax
       mov       rax,[rbp+10]
       mov       [rax+10],ecx
       lea       rcx,[rax+8]
       mov       rdx,r12
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M08_L18
M08_L07:
       test      r15,r15
       jne       near ptr M08_L11
       mov       [rbp-68],rdx
       mov       r8d,[rdx+20]
M08_L08:
       mov       rdx,[rbp-68]
       mov       r10,[rdx+18]
       mov       [rbp-80],r10
       mov       rcx,[r12+10]
       mov       [rbp-4C],r8d
       mov       r9d,r8d
       imul      r9,[r12+28]
       shr       r9,20
       inc       r9
       mov       r11d,[rcx+8]
       mov       esi,r11d
       imul      r9,rsi
       shr       r9,20
       mov       rsi,[r12+18]
       mov       eax,r9d
       xor       edx,edx
       div       dword ptr [rsi+8]
       mov       esi,edx
       cmp       r9d,r11d
       jae       near ptr M08_L15
       mov       eax,r9d
       lea       rax,[rcx+rax*8+10]
       mov       [rbp-70],rax
       mov       rcx,r13
       call      qword ptr [7FFA7599F748]
       mov       rcx,rax
       call      qword ptr [7FFA7599F2C0]; CORINFO_HELP_NEWFAST
       mov       [rbp-88],rax
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       r8,[r8+10]
       mov       [rbp-90],r8
       mov       r10,[rbp-70]
       mov       r9,[r10]
       mov       [rbp-98],r9
       lea       rcx,[rax+8]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+10]
       mov       rdx,[rbp-90]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       lea       rcx,[rax+18]
       mov       rdx,[rbp-98]
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       mov       rax,[rbp-88]
       mov       ecx,[rbp-4C]
       mov       [rax+20],ecx
       mov       rcx,[rbp-70]
       mov       rdx,rax
       call      qword ptr [7FFA7599F298]; CORINFO_HELP_ASSIGN_REF
       cmp       esi,[r14+8]
       jae       near ptr M08_L15
       mov       eax,esi
       lea       rax,[r14+rax*4+10]
       mov       edx,[rax]
       add       edx,1
       jo        near ptr M08_L16
       mov       [rax],edx
       mov       rsi,[rbp-80]
       test      rsi,rsi
       mov       rdx,rsi
       jne       near ptr M08_L07
M08_L09:
       mov       rax,[rbp-58]
       inc       eax
       mov       edx,[rbx+8]
       cmp       edx,eax
       jle       near ptr M08_L06
M08_L10:
       mov       [rbp-58],rax
       mov       rdx,[rbx+rax*8+10]
       test      rdx,rdx
       jne       near ptr M08_L07
       jmp       short M08_L09
M08_L11:
       mov       [rbp-68],rdx
       mov       rcx,[rbp+10]
       mov       rcx,[rcx]
       call      qword ptr [7FFA7599FBD8]
       mov       r8,[rbp-68]
       mov       rdx,[r8+8]
       mov       rcx,r15
       mov       r11,rax
       call      qword ptr [rax]
       mov       r8d,eax
       jmp       near ptr M08_L08
M08_L12:
       mov       rcx,[rbp+10]
       mov       eax,[rcx+10]
       add       eax,eax
       mov       [rcx+10],eax
       test      eax,eax
       jge       near ptr M08_L18
       jmp       short M08_L14
M08_L13:
       mov       rcx,[rbx+8]
       call      qword ptr [7FFA7599FF30]
       mov       rdi,rax
       test      rdi,rdi
       je        near ptr M08_L00
       mov       rcx,[rbp+10]
       mov       r13,[rcx]
       mov       rcx,r13
       call      qword ptr [7FFA7599F550]
       mov       r15,rax
       mov       rcx,rdi
       lea       r11,[System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)]
       call      qword ptr [r11]
       mov       rdx,rax
       mov       rcx,r15
       call      qword ptr [7FFA7599F2D0]; Precode of System.Runtime.CompilerServices.CastHelpers.ChkCastAny(Void*, System.Object)
       mov       r15,rax
       jmp       near ptr M08_L00
M08_L14:
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       short M08_L18
M08_L15:
       call      qword ptr [7FFA7599F290]
       int       3
M08_L16:
       call      qword ptr [7FFA7599F288]
       int       3
M08_L17:
       call      qword ptr [7FFA7599FF68]
       mov       r14d,eax
       mov       rcx,[rbp+10]
       mov       dword ptr [rcx+10],7FFFFFFF
       jmp       near ptr M08_L02
M08_L18:
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,88
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
       sub       rsp,28
       mov       rcx,[rbp+10]
       mov       edx,[rbp-48]
       call      qword ptr [7FFA759A0928]; Precode of System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       nop
       add       rsp,28
       ret
; Total bytes of code 1137
```
```assembly
; System.Collections.Concurrent.ConcurrentDictionary`2[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]].ReleaseLocks(Int32)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,20
       mov       ebx,edx
       mov       rcx,[rcx+8]
       mov       rsi,[rcx+18]
       xor       edi,edi
       test      ebx,ebx
       jle       short M09_L01
       test      rsi,rsi
       je        short M09_L02
       cmp       [rsi+8],ebx
       jl        short M09_L02
       add       rsi,10
M09_L00:
       mov       rcx,[rsi]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       add       rsi,8
       dec       ebx
       jne       short M09_L00
M09_L01:
       add       rsp,20
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M09_L02:
       mov       ecx,[rsi+8]
M09_L03:
       cmp       edi,[rsi+8]
       jae       short M09_L04
       mov       ecx,edi
       mov       rcx,[rsi+rcx*8+10]
       call      qword ptr [7FFA759A0088]; Precode of System.Threading.Monitor.Exit(System.Object)
       inc       edi
       cmp       edi,ebx
       jl        short M09_L03
       jmp       short M09_L01
M09_L04:
       call      qword ptr [7FFA7599F290]
       int       3
; Total bytes of code 98
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.CompoundOperation_FullHotPathAccess()
       push      rsi
       push      rbx
       sub       rsp,28
       mov       rbx,rcx
       mov       rsi,[rbx+8]
       mov       rax,rsi
       cmp       [rax],al
       cmp       [rax],al
       mov       rax,rsi
       cmp       [rax],al
       mov       rax,rsi
       cmp       [rax],al
       cmp       byte ptr [rsi+108],0
       je        short M00_L01
M00_L00:
       mov       rax,[rbx+8]
       mov       rcx,rax
       cmp       [rcx],cl
       cmp       [rcx],cl
       mov       rcx,rax
       cmp       [rcx],cl
       mov       rcx,rax
       cmp       [rcx],cl
       add       rsp,28
       pop       rbx
       pop       rsi
       ret
M00_L01:
       cmp       qword ptr [rsi+18],0
       jne       short M00_L00
       lea       rcx,[rsi+110]
       mov       rdx,1CA002089A0
       xor       r8d,r8d
       call      qword ptr [7FF9B3E66BC8]
       lea       rcx,[rsi+18]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       jmp       short M00_L00
; Total bytes of code 114
```

## .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```assembly
; Excalibur.Dispatch.Benchmarks.MessageContext.MessageContextBenchmarks.CreateChildContext_Basic()
       mov       rcx,[rcx+8]
       cmp       [rcx],ecx
       jmp       qword ptr [7FF9B3DA9B68]; Excalibur.Dispatch.Messaging.MessageContext.CreateChildContext()
; Total bytes of code 12
```
```assembly
; Excalibur.Dispatch.Messaging.MessageContext.CreateChildContext()
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,108
       vzeroupper
       lea       rbp,[rsp+140]
       xor       eax,eax
       mov       [rbp-118],rax
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rbp-110],xmm4
       mov       rax,0FFFFFFFFFFFFFF40
M01_L00:
       vmovdqa   xmmword ptr [rbp+rax-40],xmm4
       vmovdqa   xmmword ptr [rbp+rax-30],xmm4
       vmovdqa   xmmword ptr [rbp+rax-20],xmm4
       add       rax,30
       jne       short M01_L00
       mov       [rbp-40],rax
       mov       [rbp+10],rcx
       lea       rcx,[rbp-0C0]
       call      CORINFO_HELP_INIT_PINVOKE_FRAME
       mov       rbx,rax
       mov       rcx,rsp
       mov       [rbp-0A8],rcx
       mov       rcx,rbp
       mov       [rbp-98],rcx
       mov       rcx,offset MT_Excalibur.Dispatch.Messaging.MessageContext
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-0D0],rax
       mov       rcx,205714008D8
       mov       rsi,[rcx]
       mov       [rbp-0E0],rsi
       mov       rsi,[rbp+10]
       mov       rdi,[rsi+40]
       mov       [rbp-0D8],rdi
       mov       rcx,offset MT_System.Threading.Lock
       call      CORINFO_HELP_NEWSFAST
       mov       word ptr [rax+1C],16
       mov       rdi,[rbp-0D0]
       lea       rcx,[rdi+10]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       lea       rcx,[rbp-58]
       mov       rax,7FF9B3DADE48
       mov       [rbp-0B0],rax
       lea       rax,[M01_L01]
       mov       [rbp-0A0],rax
       lea       rax,[rbp-0C0]
       mov       [rbx+8],rax
       mov       byte ptr [rbx+4],0
       mov       rax,7FFB0E9F9AE0
       call      rax
M01_L01:
       mov       byte ptr [rbx+4],1
       cmp       dword ptr [7FFA1394F778],0
       je        short M01_L02
       call      qword ptr [7FFA1393D608]; CORINFO_HELP_STOP_FOR_GC
M01_L02:
       mov       rcx,[rbp-0B8]
       mov       [rbx+8],rcx
       test      eax,eax
       jne       near ptr M01_L96
       vmovups   xmm0,[rbp-58]
       mov       rdi,[rbp-0D0]
       vmovups   [rdi+110],xmm0
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.MessageVersionMetadata
       call      CORINFO_HELP_NEWSFAST
       mov       rcx,100000001
       mov       [rax+8],rcx
       mov       dword ptr [rax+10],1
       lea       rcx,[rdi+20]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Serialization.SerializableValidationResult
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       mov       rcx,205714008F8
       mov       rdx,[rcx]
       lea       rcx,[rbx+8]
       call      CORINFO_HELP_ASSIGN_REF
       mov       byte ptr [rbx+10],1
       lea       rcx,[rdi+28]
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.AuthorizationResult
       call      CORINFO_HELP_NEWSFAST
       mov       byte ptr [rax+10],1
       lea       rcx,[rdi+30]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,offset MT_<>z__ReadOnlySingleElementList<System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       rcx,20500206AA0
       mov       [rax+8],rcx
       mov       rcx,rax
       call      qword ptr [7FF9B3DB4C18]; Excalibur.Dispatch.Abstractions.Routing.RoutingResult.NormalizeBusNames(System.Collections.Generic.IEnumerable`1<System.String>)
       mov       rcx,rax
       call      qword ptr [7FF9B3DB4C18]; Excalibur.Dispatch.Abstractions.Routing.RoutingResult.NormalizeBusNames(System.Collections.Generic.IEnumerable`1<System.String>)
       mov       rbx,rax
       mov       rcx,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<System.String>
       cmp       [rbx],rcx
       jne       near ptr M01_L97
       mov       rcx,[rbx+8]
       mov       r11,7FF9B39506E0
       call      qword ptr [r11]
M01_L03:
       test      eax,eax
       je        near ptr M01_L98
       mov       rcx,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<System.String>
       cmp       [rbx],rcx
       jne       near ptr M01_L99
       mov       rcx,[rbx+8]
       mov       r11,7FF9B39506E8
       call      qword ptr [r11]
       mov       esi,eax
M01_L04:
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       test      esi,esi
       jl        near ptr M01_L100
       test      esi,esi
       je        near ptr M01_L101
       mov       edx,esi
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.IRouteResult[]
       call      CORINFO_HELP_NEWARR_1_PTR
       lea       rcx,[r14+8]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
M01_L05:
       mov       rcx,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<System.String>
       cmp       [rbx],rcx
       jne       near ptr M01_L104
       mov       rcx,[rbx+8]
       mov       r11,offset MT_System.Collections.Generic.List<System.String>
       cmp       [rcx],r11
       jne       near ptr M01_L91
       mov       esi,[rcx+10]
M01_L06:
       test      esi,esi
       je        near ptr M01_L103
       mov       rbx,[rbx+8]
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>
       cmp       [rbx],rcx
       jne       near ptr M01_L92
       cmp       dword ptr [rbx+10],0
       je        near ptr M01_L102
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>+Enumerator
       call      CORINFO_HELP_NEWSFAST
       mov       rsi,rax
       mov       r15d,[rbx+14]
       lea       rcx,[rsi+8]
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       xor       ecx,ecx
       mov       [rsi+10],rcx
       mov       [rsi+18],r15d
       mov       [rsi+1C],ecx
M01_L07:
       mov       [rbp-0F0],rsi
       cmp       qword ptr [rbp-0F0],0
       je        near ptr M01_L19
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>+Enumerator
       mov       rsi,[rbp-0F0]
       cmp       [rsi],rcx
       jne       near ptr M01_L19
M01_L08:
       lea       rbx,[rsi+8]
       mov       r15,[rbx]
       mov       ecx,[rbx+10]
       mov       rdx,[rbx]
       cmp       ecx,[rdx+14]
       jne       near ptr M01_L28
       mov       ecx,[rbx+14]
       cmp       ecx,[r15+10]
       jae       near ptr M01_L20
       mov       r13,[r15+8]
       mov       r12d,[rbx+14]
       cmp       r12d,[r13+8]
       jae       near ptr M01_L29
       mov       ecx,r12d
       mov       rdx,[r13+rcx*8+10]
       lea       rcx,[rbx+8]
       call      CORINFO_HELP_ASSIGN_REF
       inc       dword ptr [rbx+14]
       mov       r12,[rsi+10]
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.RouteResult
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       test      r12,r12
       je        short M01_L12
       xor       ebx,ebx
       cmp       dword ptr [r12+8],0
       jle       short M01_L12
M01_L09:
       mov       ecx,ebx
       movzx     ecx,word ptr [r12+rcx*2+0C]
       cmp       ecx,100
       jge       near ptr M01_L21
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M01_L22
M01_L10:
       mov       rdx,r12
M01_L11:
       lea       rcx,[r13+8]
       call      CORINFO_HELP_ASSIGN_REF
       xor       ecx,ecx
       mov       [r13+10],rcx
       inc       dword ptr [r14+14]
       mov       rcx,[r14+8]
       mov       eax,[r14+10]
       cmp       [rcx+8],eax
       ja        short M01_L13
       mov       rcx,r14
       mov       rdx,r13
       call      qword ptr [7FF9B3A071C8]; System.Collections.Generic.List`1[[System.__Canon, System.Private.CoreLib]].AddWithResize(System.__Canon)
       jmp       near ptr M01_L08
M01_L12:
       mov       rdx,20500206AA0
       jmp       short M01_L11
M01_L13:
       lea       edx,[rax+1]
       mov       [r14+10],edx
       mov       edx,eax
       mov       r8,r13
       call      System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       jmp       near ptr M01_L08
M01_L14:
       mov       r13,[r15+8]
       cmp       r12d,[r13+8]
       jae       near ptr M01_L29
       mov       ecx,r12d
       mov       rdx,[r13+rcx*8+10]
       lea       rcx,[rbx+8]
       call      CORINFO_HELP_ASSIGN_REF
       inc       dword ptr [rbx+14]
       mov       r12,[rsi+10]
M01_L15:
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.RouteResult
       call      CORINFO_HELP_NEWSFAST
       mov       r13,rax
       test      r12,r12
       je        near ptr M01_L26
       xor       ebx,ebx
       cmp       dword ptr [r12+8],0
       jle       near ptr M01_L26
M01_L16:
       mov       ecx,ebx
       movzx     ecx,word ptr [r12+rcx*2+0C]
       cmp       ecx,100
       jge       near ptr M01_L24
       mov       rax,7FF9635A68D0
       test      byte ptr [rax+rcx],80
       jne       near ptr M01_L25
M01_L17:
       mov       rdx,r12
M01_L18:
       lea       rcx,[r13+8]
       call      CORINFO_HELP_ASSIGN_REF
       xor       ecx,ecx
       mov       [r13+10],rcx
       inc       dword ptr [r14+14]
       mov       rcx,[r14+8]
       mov       eax,[r14+10]
       cmp       [rcx+8],eax
       ja        near ptr M01_L27
       mov       rcx,r14
       mov       rdx,r13
       call      qword ptr [7FF9B3A071C8]; System.Collections.Generic.List`1[[System.__Canon, System.Private.CoreLib]].AddWithResize(System.__Canon)
       nop
M01_L19:
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>+Enumerator
       mov       rsi,[rbp-0F0]
       cmp       [rsi],rcx
       jne       short M01_L23
       lea       rbx,[rsi+8]
       mov       r15,[rbx]
       mov       ecx,[rbx+10]
       mov       rdx,[rbx]
       cmp       ecx,[rdx+14]
       jne       near ptr M01_L28
       mov       r12d,[rbx+14]
       cmp       r12d,[r15+10]
       jb        near ptr M01_L14
M01_L20:
       xor       eax,eax
       mov       [rbx+8],rax
       mov       dword ptr [rbx+14],0FFFFFFFF
       jmp       near ptr M01_L30
M01_L21:
       call      qword ptr [7FF9B3E87C60]
       test      eax,eax
       je        near ptr M01_L10
M01_L22:
       inc       ebx
       cmp       [r12+8],ebx
       jg        near ptr M01_L09
       jmp       near ptr M01_L12
M01_L23:
       mov       rcx,rsi
       mov       r11,7FF9B39506C8
       call      qword ptr [r11]
       test      eax,eax
       je        short M01_L30
       mov       rcx,rsi
       mov       r11,7FF9B39506D0
       call      qword ptr [r11]
       mov       r12,rax
       jmp       near ptr M01_L15
M01_L24:
       call      qword ptr [7FF9B3E87C60]
       test      eax,eax
       je        near ptr M01_L17
M01_L25:
       inc       ebx
       cmp       [r12+8],ebx
       jg        near ptr M01_L16
M01_L26:
       mov       rdx,20500206AA0
       jmp       near ptr M01_L18
M01_L27:
       lea       edx,[rax+1]
       mov       [r14+10],edx
       mov       edx,eax
       mov       r8,r13
       call      System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       jmp       near ptr M01_L19
M01_L28:
       call      qword ptr [7FF9B3A0FC48]
       int       3
M01_L29:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L30:
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>+Enumerator
       cmp       [rsi],rcx
       jne       near ptr M01_L105
M01_L31:
       mov       rcx,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      CORINFO_HELP_NEWSFAST
       mov       rsi,rax
       lea       rcx,[rsi+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M01_L32:
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.RoutingResult
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-0E8],rax
       mov       rbx,[rbp-0E8]
       mov       byte ptr [rbx+38],1
       xor       ecx,ecx
       mov       [rbx+8],rcx
       mov       rdx,rsi
       lea       rcx,[rbx+10]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbx+10]
       mov       r11,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rcx],r11
       jne       near ptr M01_L106
       mov       rcx,[rcx+8]
       mov       r11,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rcx],r11
       jne       near ptr M01_L93
       mov       esi,[rcx+10]
M01_L33:
       test      esi,esi
       je        near ptr M01_L119
       mov       rsi,[rbx+10]
       mov       rcx,20571400930
       mov       r14,[rcx]
       test      r14,r14
       je        near ptr M01_L107
M01_L34:
       test      rsi,rsi
       je        near ptr M01_L122
       test      r14,r14
       je        near ptr M01_L108
       mov       rdx,rsi
       mov       rcx,offset MT_System.Linq.Enumerable+Iterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      qword ptr [7FF9B3A06850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       r15,rax
       test      r15,r15
       jne       near ptr M01_L109
       mov       rdx,rsi
       mov       rcx,offset MT_System.Collections.Generic.IList<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      qword ptr [7FF9B3A0F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       mov       r15,rax
       test      r15,r15
       je        near ptr M01_L113
       mov       rdx,rsi
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.IRouteResult[]
       call      qword ptr [7FF9B3A058F0]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfAny(Void*, System.Object)
       mov       r13,rax
       test      r13,r13
       jne       near ptr M01_L110
       mov       rdx,rsi
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      qword ptr [7FF9B3A06850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       r13,rax
       test      r13,r13
       jne       near ptr M01_L112
       mov       rcx,offset MT_System.Linq.Enumerable+IListSelectIterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       call      CORINFO_HELP_GETCURRENTMANAGEDTHREADID
       mov       [r12+10],eax
       lea       rcx,[r12+18]
       mov       rdx,r15
       call      CORINFO_HELP_ASSIGN_REF
       lea       rcx,[r12+20]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M01_L35:
       mov       rdx,r12
       mov       rcx,offset MT_System.Linq.Enumerable+Iterator<System.String>
       call      qword ptr [7FF9B3A06850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       mov       rsi,rax
       test      rsi,rsi
       je        near ptr M01_L117
       mov       rcx,offset MT_System.Linq.Enumerable+IListSelectIterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.String>
       cmp       [rsi],rcx
       jne       near ptr M01_L116
       mov       rcx,[rsi+18]
       mov       r11,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rcx],r11
       jne       near ptr M01_L114
       mov       rcx,[rcx+8]
       mov       r11,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rcx],r11
       jne       near ptr M01_L94
       mov       r12d,[rcx+10]
M01_L36:
       test      r12d,r12d
       je        near ptr M01_L115
       movsxd    rdx,r12d
       mov       rcx,offset MT_System.String[]
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       r14,rax
       mov       r15,[rsi+18]
       lea       r13,[r14+10]
       mov       r12d,[r14+8]
       mov       rsi,[rsi+20]
       xor       eax,eax
       test      r12d,r12d
       jle       short M01_L38
M01_L37:
       lea       rcx,[r13+rax*8]
       mov       [rbp-118],rcx
       mov       rcx,r15
       mov       [rbp-0C8],rax
       mov       edx,eax
       mov       r11,7FF9B3950728
       call      qword ptr [r11]
       mov       rdx,rax
       mov       rcx,[rsi+8]
       call      qword ptr [rsi+18]
       mov       rcx,[rbp-118]
       mov       rdx,rax
       call      CORINFO_HELP_CHECKED_ASSIGN_REF
       mov       rcx,[rbp-0C8]
       inc       ecx
       cmp       ecx,r12d
       mov       rax,rcx
       jl        short M01_L37
M01_L38:
       lea       rcx,[rbx+18]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,[rbx+10]
       mov       r11,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rcx],r11
       jne       near ptr M01_L120
       mov       rcx,[rcx+8]
       mov       r11,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rcx],r11
       jne       near ptr M01_L95
       mov       esi,[rcx+10]
M01_L39:
       test      esi,esi
       jle       near ptr M01_L131
       mov       rsi,[rbx+10]
       mov       rcx,20571400938
       mov       r14,[rcx]
       test      r14,r14
       je        near ptr M01_L121
M01_L40:
       test      rsi,rsi
       je        near ptr M01_L122
       test      r14,r14
       je        near ptr M01_L123
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.IRouteResult[]
       cmp       [rsi],rcx
       je        near ptr M01_L64
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rsi],rcx
       je        near ptr M01_L63
       mov       rcx,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       cmp       [rsi],rcx
       jne       near ptr M01_L128
       mov       rcx,[rsi+8]
       mov       r11,7FF9B3950758
       call      qword ptr [r11]
       test      eax,eax
       je        near ptr M01_L127
       mov       rcx,[rsi+8]
       mov       r11,7FF9B3950760
       call      qword ptr [r11]
       mov       rcx,rax
M01_L41:
       mov       [rbp-0F8],rcx
       cmp       qword ptr [rbp-0F8],0
       je        short M01_L42
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>+Enumerator
       mov       rax,[rbp-0F8]
       cmp       [rax],rcx
       jne       short M01_L42
       mov       rcx,offset Excalibur.Dispatch.Abstractions.Routing.RoutingResult+<>c.<.ctor>b__6_1(Excalibur.Dispatch.Abstractions.Routing.IRouteResult)
       cmp       [r14+18],rcx
       je        near ptr M01_L47
M01_L42:
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>+Enumerator
       mov       rax,[rbp-0F8]
       cmp       [rax],rcx
       jne       near ptr M01_L50
       lea       rsi,[rax+8]
       mov       rcx,[rsi]
       mov       edx,[rsi+10]
       mov       r8,[rsi]
       cmp       edx,[r8+14]
       jne       near ptr M01_L60
       mov       r13d,[rsi+14]
       cmp       r13d,[rcx+10]
       jb        near ptr M01_L51
M01_L43:
       xor       ecx,ecx
       mov       [rsi+8],rcx
       mov       dword ptr [rsi+14],0FFFFFFFF
       jmp       near ptr M01_L62
M01_L44:
       mov       r8,20002000200020
       or        r8,[r12+0C]
       mov       rcx,610063006F006C
       xor       rcx,r8
       movzx     edx,word ptr [r12+14]
       or        edx,20
       xor       edx,6C
       mov       r11d,edx
       or        rcx,r11
       sete      r8b
       movzx     r8d,r8b
       jmp       short M01_L46
M01_L45:
       mov       r8d,1
M01_L46:
       test      r8d,r8d
       je        near ptr M01_L130
M01_L47:
       mov       rax,[rbp-0F8]
       lea       rsi,[rax+8]
       mov       r14,[rsi]
       mov       ecx,[rsi+10]
       mov       rdx,[rsi]
       cmp       ecx,[rdx+14]
       jne       near ptr M01_L60
       mov       ecx,[rsi+14]
       cmp       ecx,[r14+10]
       jae       short M01_L43
       mov       r15,[r14+8]
       mov       r13d,[rsi+14]
       cmp       r13d,[r15+8]
       jae       near ptr M01_L61
       mov       ecx,r13d
       mov       rdx,[r15+rcx*8+10]
       lea       rcx,[rsi+8]
       call      CORINFO_HELP_ASSIGN_REF
       inc       dword ptr [rsi+14]
       mov       rax,[rbp-0F8]
       mov       rdx,[rax+10]
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.RouteResult
       cmp       [rdx],rcx
       jne       short M01_L49
       mov       r12,[rdx+8]
       mov       rcx,20500206AA0
       cmp       r12,rcx
       je        near ptr M01_L45
       test      r12,r12
       je        short M01_L48
       cmp       dword ptr [r12+8],5
       je        near ptr M01_L44
M01_L48:
       xor       r8d,r8d
       jmp       near ptr M01_L46
M01_L49:
       mov       rcx,rdx
       mov       r11,7FF9B3950768
       call      qword ptr [r11]
       mov       r8d,eax
       jmp       near ptr M01_L46
M01_L50:
       mov       rcx,rax
       mov       r11,7FF9B3950740
       call      qword ptr [r11]
       test      eax,eax
       jne       short M01_L52
       jmp       near ptr M01_L62
M01_L51:
       mov       r15,[rcx+8]
       cmp       r13d,[r15+8]
       jae       near ptr M01_L61
       mov       ecx,r13d
       mov       rdx,[r15+rcx*8+10]
       lea       rcx,[rsi+8]
       call      CORINFO_HELP_ASSIGN_REF
       inc       dword ptr [rsi+14]
       mov       rax,[rbp-0F8]
       mov       rdx,[rax+10]
       mov       rcx,offset Excalibur.Dispatch.Abstractions.Routing.RoutingResult+<>c.<.ctor>b__6_1(Excalibur.Dispatch.Abstractions.Routing.IRouteResult)
       cmp       [r14+18],rcx
       jne       short M01_L53
       jmp       short M01_L54
M01_L52:
       mov       rcx,[rbp-0F8]
       mov       r11,7FF9B3950748
       call      qword ptr [r11]
       mov       rdx,rax
M01_L53:
       mov       rcx,[r14+8]
       call      qword ptr [r14+18]
       mov       r8d,eax
       mov       rax,[rbp-0F8]
       jmp       near ptr M01_L59
M01_L54:
       mov       rax,[rbp-0F8]
       mov       rcx,offset MT_Excalibur.Dispatch.Abstractions.Routing.RouteResult
       cmp       [rdx],rcx
       je        short M01_L55
       mov       rcx,rdx
       mov       r11,7FF9B3950768
       call      qword ptr [r11]
       mov       r8d,eax
       mov       rax,[rbp-0F8]
       jmp       short M01_L59
M01_L55:
       mov       r12,[rdx+8]
       mov       rcx,20500206AA0
       cmp       r12,rcx
       je        short M01_L58
       test      r12,r12
       je        short M01_L56
       cmp       dword ptr [r12+8],5
       je        short M01_L57
M01_L56:
       xor       r8d,r8d
       jmp       short M01_L59
M01_L57:
       mov       r8,20002000200020
       or        r8,[r12+0C]
       mov       rcx,610063006F006C
       xor       rcx,r8
       movzx     edx,word ptr [r12+14]
       or        edx,20
       xor       edx,6C
       mov       r11d,edx
       or        rcx,r11
       sete      r8b
       movzx     r8d,r8b
       jmp       short M01_L59
M01_L58:
       mov       r8d,1
M01_L59:
       test      r8d,r8d
       jne       near ptr M01_L42
       jmp       near ptr M01_L130
M01_L60:
       call      qword ptr [7FF9B3A0FC48]
       int       3
M01_L61:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M01_L62:
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>+Enumerator
       mov       rax,[rbp-0F8]
       cmp       [rax],rcx
       je        short M01_L66
       jmp       near ptr M01_L129
M01_L63:
       mov       r15d,[rsi+10]
       mov       r13,[rsi+8]
       cmp       [r13+8],r15d
       jb        near ptr M01_L124
       add       r13,10
       jmp       short M01_L65
M01_L64:
       lea       r13,[rsi+10]
       mov       r15d,[rsi+8]
M01_L65:
       xor       esi,esi
       cmp       esi,r15d
       jl        near ptr M01_L125
M01_L66:
       mov       esi,1
M01_L67:
       mov       [rbx+39],sil
       mov       rcx,20571400910
       mov       rdx,[rcx]
       lea       rcx,[rbx+20]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,20571400918
       mov       rdx,[rcx]
       lea       rcx,[rbx+28]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,20571400920
       mov       rdx,[rcx]
       lea       rcx,[rbx+30]
       call      CORINFO_HELP_ASSIGN_REF
       lea       rcx,[rdi+38]
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       cmp       qword ptr [rbp-0D8],0
       je        near ptr M01_L132
       lea       rcx,[rdi+40]
       mov       rdx,[rbp-0D8]
       call      CORINFO_HELP_ASSIGN_REF
       call      qword ptr [7FF9B3DB5440]; System.DateTime.get_UtcNow()
       mov       rcx,3FFFFFFFFFFFFFFF
       and       rcx,rax
       mov       [rdi+0F8],rcx
       lea       rcx,[rdi+0D0]
       mov       rdx,[rbp-0E0]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rsi,[rbp+10]
       mov       rdx,[rsi+60]
       lea       rcx,[rdi+60]
       call      CORINFO_HELP_ASSIGN_REF
       cmp       byte ptr [rsi+108],0
       je        near ptr M01_L133
       mov       rdx,[rsi+18]
M01_L68:
       test      rdx,rdx
       je        near ptr M01_L134
M01_L69:
       lea       rcx,[rdi+68]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rsi+90]
       lea       rcx,[rdi+90]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rsi+58]
       lea       rcx,[rdi+58]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rsi+98]
       lea       rcx,[rdi+98]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rsi+0A0]
       lea       rcx,[rdi+0A0]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rsi+70]
       lea       rcx,[rdi+70]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,[rsi+0A8]
       lea       rcx,[rdi+0A8]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,offset MT_System.Byte[]
       mov       edx,10
       call      CORINFO_HELP_NEWARR_1_VC
       mov       rsi,rax
       call      qword ptr [7FF9B3DB5440]; System.DateTime.get_UtcNow()
       call      qword ptr [7FF9B3DB5440]; System.DateTime.get_UtcNow()
       mov       rdx,3FFFFFFFFFFFFFFF
       and       rdx,rax
       mov       rcx,346DC5D63886594B
       mov       rax,rcx
       imul      rdx
       mov       rbx,rdx
       shr       rbx,3F
       sar       rdx,0B
       add       rbx,rdx
       mov       rcx,gs:[58]
       mov       rcx,[rcx+48]
       cmp       dword ptr [rcx+238],5
       jle       near ptr M01_L135
       mov       rcx,[rcx+240]
       mov       r14,[rcx+28]
       test      r14,r14
       je        near ptr M01_L135
M01_L70:
       mov       rcx,[r14+18]
       cmp       [r14+10],rbx
       setne     al
       movzx     eax,al
       test      eax,eax
       je        short M01_L71
       mov       [r14+10],rbx
       mov       rdx,0FFFFC77CEDD32800
       add       rdx,rbx
       lea       r8,[rcx+1]
       cmp       rcx,rdx
       mov       rcx,rdx
       cmovge    rcx,r8
       mov       [r14+18],rcx
M01_L71:
       mov       rdx,rcx
       sar       rdx,28
       mov       [rsi+10],dl
       mov       rdx,rcx
       sar       rdx,20
       mov       [rsi+11],dl
       mov       rdx,rcx
       sar       rdx,18
       mov       [rsi+12],dl
       mov       rdx,rcx
       sar       rdx,10
       mov       [rsi+13],dl
       mov       rdx,rcx
       sar       rdx,8
       mov       [rsi+14],dl
       mov       [rsi+15],cl
       test      eax,eax
       je        near ptr M01_L82
       mov       ebx,6
       mov       r15d,0A
       mov       rcx,20571401410
       mov       r13,[rcx]
       mov       rcx,gs:[58]
       mov       rcx,[rcx+48]
       cmp       dword ptr [rcx+238],7
       jle       near ptr M01_L136
       mov       rcx,[rcx+240]
       mov       rax,[rcx+38]
       test      rax,rax
       je        near ptr M01_L136
M01_L72:
       mov       rcx,[rax+10]
       mov       eax,[r13+18]
       not       eax
       test      rcx,rcx
       je        near ptr M01_L137
       test      eax,eax
       jl        near ptr M01_L137
       mov       r12d,[rcx+8]
       cmp       r12d,eax
       jle       near ptr M01_L137
       cmp       eax,r12d
       jae       near ptr M01_L146
       mov       rcx,[rcx+rax*8+10]
       test      rcx,rcx
       je        near ptr M01_L137
       cmp       byte ptr [r13+1C],0
       je        near ptr M01_L137
       mov       r12,[rcx+20]
M01_L73:
       mov       rcx,20571401418
       mov       r13,[rcx]
       mov       [rbp-100],r13
       mov       rcx,gs:[58]
       mov       rcx,[rcx+48]
       cmp       dword ptr [rcx+238],6
       jle       near ptr M01_L138
       mov       rcx,[rcx+240]
       mov       rdx,[rcx+30]
       test      rdx,rdx
       je        near ptr M01_L138
M01_L74:
       mov       [rbp-110],rdx
       mov       rcx,[rdx+10]
       mov       rax,[rbp-100]
       mov       r8d,[rax+18]
       not       r8d
       test      rcx,rcx
       je        near ptr M01_L139
       test      r8d,r8d
       jl        near ptr M01_L139
       mov       r10d,[rcx+8]
       cmp       r10d,r8d
       jle       near ptr M01_L139
       cmp       r8d,r10d
       jae       near ptr M01_L146
       mov       rcx,[rcx+r8*8+10]
       test      rcx,rcx
       je        near ptr M01_L139
       cmp       byte ptr [rax+1C],0
       je        near ptr M01_L139
       mov       r8d,[rcx+20]
M01_L75:
       mov       eax,r8d
       lea       ecx,[rax+0A]
       cmp       ecx,800
       jle       short M01_L77
       mov       r15d,r8d
       neg       r15d
       add       r15d,800
       mov       [rsp+20],r15d
       mov       rcx,r12
       mov       edx,r8d
       mov       r8,rsi
       mov       r9d,6
       call      qword ptr [7FF9B3DBD5D8]; System.Buffer.BlockCopy(System.Array, Int32, System.Array, Int32, Int32)
       lea       ebx,[r15+6]
       neg       r15d
       add       r15d,0A
       test      r12,r12
       je        near ptr M01_L144
       lea       rcx,[r12+10]
       mov       edx,[r12+8]
       test      edx,edx
       jle       short M01_L76
       mov       [rbp-68],rcx
       call      qword ptr [7FF9B3E855C0]; System.Security.Cryptography.RandomNumberGeneratorImplementation.GetBytes(Byte*, Int32)
       xor       ecx,ecx
       mov       [rbp-68],rcx
M01_L76:
       xor       ecx,ecx
       mov       [rbp-68],rcx
       xor       edx,edx
       mov       eax,edx
M01_L77:
       mov       [rsp+20],r15d
       mov       rcx,r12
       mov       [rbp-5C],eax
       mov       edx,eax
       mov       r8,rsi
       mov       r9d,ebx
       call      qword ptr [7FF9B3DBD5D8]; System.Buffer.BlockCopy(System.Array, Int32, System.Array, Int32, Int32)
       mov       rcx,r13
       mov       edx,r15d
       add       edx,[rbp-5C]
       mov       rbx,[rbp-110]
       mov       r8,[rbx+10]
       mov       eax,[rcx+18]
       not       eax
       test      r8,r8
       je        short M01_L78
       test      eax,eax
       jl        short M01_L78
       mov       r10d,[r8+8]
       cmp       r10d,eax
       jle       short M01_L78
       cmp       eax,r10d
       jae       near ptr M01_L146
       mov       eax,eax
       mov       rax,[r8+rax*8+10]
       test      rax,rax
       je        short M01_L78
       cmp       byte ptr [rcx+1C],0
       je        short M01_L78
       mov       [rax+20],edx
       jmp       short M01_L79
M01_L78:
       call      qword ptr [7FF9B3E84D68]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].SetValueSlow(Int32, LinkedSlotVolatile<Int32>[])
M01_L79:
       movzx     eax,byte ptr [rsi+16]
       and       eax,7
       shl       eax,16
       movzx     edx,byte ptr [rsi+17]
       shl       edx,0E
       or        eax,edx
       movzx     edx,byte ptr [rsi+18]
       and       edx,3F
       shl       edx,8
       or        eax,edx
       movzx     edx,byte ptr [rsi+19]
       or        eax,edx
M01_L80:
       mov       [r14+20],eax
       mov       ecx,eax
       shr       ecx,16
       and       ecx,0F
       or        ecx,70
       mov       [rsi+16],cl
       shr       eax,8
       and       eax,3F
       or        eax,80
       mov       [rsi+18],al
       add       rsi,10
       vmovups   xmm0,[rsi]
       vmovups   [rbp-48],xmm0
       mov       rcx,offset MT_System.String
       mov       edx,24
       call      00007FFA1363AFE0
       lea       rcx,[rax+0C]
       mov       edx,[rax+8]
       cmp       edx,24
       jl        near ptr M01_L145
       mov       dword ptr [rbp-80],24
       mov       [rbp-88],rcx
       vmovups   xmm0,[rbp-48]
       vmovups   xmm1,[7FF9B3A89D70]
       vpsrlq    xmm2,xmm0,4
       vpunpcklbw xmm3,xmm2,xmm0
       vpunpckhbw xmm0,xmm2,xmm0
       vpand     xmm2,xmm3,[7FF9B3A89D80]
       vpshufb   xmm2,xmm1,xmm2
       vpand     xmm0,xmm0,[7FF9B3A89D80]
       vpshufb   xmm0,xmm1,xmm0
       vpshufb   xmm1,xmm2,[7FF9B3A89D90]
       vpshufb   xmm2,xmm0,[7FF9B3A89DA0]
       vpshufb   xmm3,xmm1,[7FF9B3A89DB0]
       vpshufb   xmm1,xmm1,[7FF9B3A89DC0]
       vpshufb   xmm0,xmm0,[7FF9B3A89DD0]
       vpor      xmm0,xmm0,xmm1
       vpor      xmm0,xmm0,[7FF9B3A89DE0]
       vpmovzxbw xmm1,xmm3
       vpmovzxbw xmm3,xmm2
       vpsrldq   xmm2,xmm2,8
       vpmovzxbw xmm2,xmm2
       vpmovzxbw xmm4,xmm0
       vpsrldq   xmm0,xmm0,8
       vpmovzxbw xmm0,xmm0
       vmovups   [rcx],xmm1
       vmovups   [rcx+28],xmm3
       vmovups   [rcx+38],xmm2
       vmovups   [rcx+10],xmm4
       vmovups   [rcx+20],xmm0
       xor       ecx,ecx
       mov       [rbp-88],rcx
M01_L81:
       xor       ecx,ecx
       mov       [rbp-88],rcx
       lea       rcx,[rdi+18]
       mov       rdx,rax
       call      CORINFO_HELP_ASSIGN_REF
       mov       byte ptr [rdi+108],1
       mov       rax,rdi
       add       rsp,108
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M01_L82:
       mov       r15d,9
       mov       r12d,7
       mov       rcx,20571401410
       mov       r13,[rcx]
       mov       rcx,gs:[58]
       mov       rcx,[rcx+48]
       cmp       dword ptr [rcx+238],7
       jle       near ptr M01_L140
       mov       rcx,[rcx+240]
       mov       rax,[rcx+38]
       test      rax,rax
       je        near ptr M01_L140
M01_L83:
       mov       rcx,[rax+10]
       mov       eax,[r13+18]
       not       eax
       test      rcx,rcx
       je        near ptr M01_L141
       test      eax,eax
       jl        near ptr M01_L141
       mov       edx,[rcx+8]
       cmp       edx,eax
       jle       near ptr M01_L141
       cmp       eax,edx
       jae       near ptr M01_L146
       mov       rcx,[rcx+rax*8+10]
       test      rcx,rcx
       je        near ptr M01_L141
       cmp       byte ptr [r13+1C],0
       je        near ptr M01_L141
       mov       rbx,[rcx+20]
M01_L84:
       mov       rcx,20571401418
       mov       r13,[rcx]
       mov       [rbp-108],r13
       mov       rcx,gs:[58]
       mov       rcx,[rcx+48]
       cmp       dword ptr [rcx+238],6
       jle       near ptr M01_L142
       mov       rcx,[rcx+240]
       mov       rdx,[rcx+30]
       test      rdx,rdx
       je        near ptr M01_L142
M01_L85:
       mov       [rbp-110],rdx
       mov       rcx,[rdx+10]
       mov       rax,[rbp-108]
       mov       r8d,[rax+18]
       not       r8d
       test      rcx,rcx
       je        near ptr M01_L143
       test      r8d,r8d
       jl        near ptr M01_L143
       mov       r10d,[rcx+8]
       cmp       r10d,r8d
       jle       near ptr M01_L143
       cmp       r8d,r10d
       jae       near ptr M01_L146
       mov       rcx,[rcx+r8*8+10]
       test      rcx,rcx
       je        near ptr M01_L143
       cmp       byte ptr [rax+1C],0
       je        near ptr M01_L143
       mov       r8d,[rcx+20]
M01_L86:
       mov       eax,r8d
       lea       ecx,[rax+7]
       cmp       ecx,800
       jle       short M01_L88
       mov       r12d,r8d
       neg       r12d
       add       r12d,800
       mov       [rsp+20],r12d
       mov       rcx,rbx
       mov       edx,r8d
       mov       r8,rsi
       mov       r9d,9
       call      qword ptr [7FF9B3DBD5D8]; System.Buffer.BlockCopy(System.Array, Int32, System.Array, Int32, Int32)
       lea       r15d,[r12+9]
       neg       r12d
       add       r12d,7
       test      rbx,rbx
       je        near ptr M01_L144
       lea       rcx,[rbx+10]
       mov       edx,[rbx+8]
       test      edx,edx
       jle       short M01_L87
       mov       [rbp-78],rcx
       call      qword ptr [7FF9B3E855C0]; System.Security.Cryptography.RandomNumberGeneratorImplementation.GetBytes(Byte*, Int32)
       xor       ecx,ecx
       mov       [rbp-78],rcx
M01_L87:
       xor       ecx,ecx
       mov       [rbp-78],rcx
       xor       edx,edx
       mov       eax,edx
M01_L88:
       mov       [rsp+20],r12d
       mov       rcx,rbx
       mov       [rbp-6C],eax
       mov       edx,eax
       mov       r8,rsi
       mov       r9d,r15d
       call      qword ptr [7FF9B3DBD5D8]; System.Buffer.BlockCopy(System.Array, Int32, System.Array, Int32, Int32)
       mov       rcx,r13
       mov       edx,r12d
       add       edx,[rbp-6C]
       mov       rbx,[rbp-110]
       mov       r8,[rbx+10]
       mov       eax,[rcx+18]
       not       eax
       test      r8,r8
       je        short M01_L89
       test      eax,eax
       jl        short M01_L89
       mov       r10d,[r8+8]
       cmp       r10d,eax
       jle       short M01_L89
       cmp       eax,r10d
       jae       near ptr M01_L146
       mov       eax,eax
       mov       rax,[r8+rax*8+10]
       test      rax,rax
       je        short M01_L89
       cmp       byte ptr [rcx+1C],0
       je        short M01_L89
       mov       [rax+20],edx
       jmp       short M01_L90
M01_L89:
       call      qword ptr [7FF9B3E84D68]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].SetValueSlow(Int32, LinkedSlotVolatile<Int32>[])
M01_L90:
       mov       ecx,[r14+20]
       movzx     edx,byte ptr [rsi+19]
       shr       edx,4
       lea       eax,[rcx+rdx+1]
       mov       ecx,eax
       shr       ecx,0E
       mov       [rsi+17],cl
       mov       [rsi+19],al
       jmp       near ptr M01_L80
M01_L91:
       mov       r11,7FF9B39506F0
       call      qword ptr [r11]
       mov       esi,eax
       jmp       near ptr M01_L06
M01_L92:
       mov       rcx,rbx
       mov       r11,7FF9B39506F8
       call      qword ptr [r11]
       mov       rsi,rax
       jmp       near ptr M01_L07
M01_L93:
       mov       r11,7FF9B3950710
       call      qword ptr [r11]
       mov       esi,eax
       jmp       near ptr M01_L33
M01_L94:
       mov       r11,7FF9B3950720
       call      qword ptr [r11]
       mov       r12d,eax
       jmp       near ptr M01_L36
M01_L95:
       mov       r11,7FF9B3950730
       call      qword ptr [r11]
       mov       esi,eax
       jmp       near ptr M01_L39
M01_L96:
       mov       ecx,eax
       call      qword ptr [7FF9B3E8C3D8]
       int       3
M01_L97:
       mov       rcx,rbx
       mov       r11,7FF9B39506B0
       call      qword ptr [r11]
       jmp       near ptr M01_L03
M01_L98:
       mov       rcx,20571400908
       mov       rsi,[rcx]
       jmp       near ptr M01_L32
M01_L99:
       mov       rcx,rbx
       mov       r11,7FF9B39506B8
       call      qword ptr [r11]
       mov       esi,eax
       jmp       near ptr M01_L04
M01_L100:
       mov       ecx,16
       mov       edx,0D
       call      qword ptr [7FF9B3DB65B0]
       int       3
M01_L101:
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rcx,205714014B8
       mov       rdx,[rcx]
       lea       rcx,[r14+8]
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M01_L05
M01_L102:
       mov       rcx,offset MT_System.SZGenericArrayEnumerator<System.String>
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rcx,205714014C0
       mov       rsi,[rcx]
       jmp       near ptr M01_L07
M01_L103:
       mov       rcx,offset MT_System.SZGenericArrayEnumerator<System.String>
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rcx,205714014C0
       mov       rsi,[rcx]
       jmp       near ptr M01_L07
M01_L104:
       mov       rcx,rbx
       mov       r11,7FF9B39506C0
       call      qword ptr [r11]
       mov       rsi,rax
       jmp       near ptr M01_L07
M01_L105:
       mov       rcx,rsi
       mov       r11,7FF9B39506D8
       call      qword ptr [r11]
       jmp       near ptr M01_L31
M01_L106:
       mov       r11,7FF9B3950700
       call      qword ptr [r11]
       mov       esi,eax
       jmp       near ptr M01_L33
M01_L107:
       mov       rcx,offset MT_System.Func<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rdx,20571400928
       mov       rdx,[rdx]
       mov       rcx,r14
       mov       r8,offset Excalibur.Dispatch.Abstractions.Routing.RoutingResult+<>c.<.ctor>b__6_0(Excalibur.Dispatch.Abstractions.Routing.IRouteResult)
       call      qword ptr [7FF9B3A06BB0]; System.MulticastDelegate.CtorClosed(System.Object, IntPtr)
       mov       rcx,20571400930
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M01_L34
M01_L108:
       mov       ecx,10
       call      qword ptr [7FF9B3A0F930]
       int       3
M01_L109:
       mov       rcx,r15
       mov       rdx,offset MT_System.Linq.Enumerable+Iterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       mov       r8,7FF9B3F310F8
       call      qword ptr [7FF9B3A05920]; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointer(System.Object, IntPtr, IntPtr)
       mov       rcx,r15
       mov       rdx,r14
       call      rax
       mov       r12,rax
       jmp       near ptr M01_L35
M01_L110:
       cmp       dword ptr [r13+8],0
       jne       short M01_L111
       mov       rcx,20571400950
       mov       r12,[rcx]
       jmp       near ptr M01_L35
M01_L111:
       mov       rcx,offset MT_System.Linq.Enumerable+ArraySelectIterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       mov       rcx,r12
       mov       rdx,r13
       mov       r8,r14
       call      qword ptr [7FF9B3E8D380]
       jmp       near ptr M01_L35
M01_L112:
       mov       rcx,offset MT_System.Linq.Enumerable+ListSelectIterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       mov       rcx,r12
       mov       rdx,r13
       mov       r8,r14
       call      qword ptr [7FF9B3E8D398]
       jmp       near ptr M01_L35
M01_L113:
       mov       rcx,offset MT_System.Linq.Enumerable+IEnumerableSelectIterator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       r12,rax
       mov       rcx,r12
       mov       rdx,rsi
       mov       r8,r14
       call      qword ptr [7FF9B3E8D3B0]
       jmp       near ptr M01_L35
M01_L114:
       mov       r11,7FF9B3950718
       call      qword ptr [r11]
       mov       r12d,eax
       jmp       near ptr M01_L36
M01_L115:
       mov       rcx,20571400950
       mov       r14,[rcx]
       jmp       near ptr M01_L38
M01_L116:
       mov       rcx,rsi
       mov       rax,[rsi]
       mov       rax,[rax+48]
       call      qword ptr [rax+30]
       mov       r14,rax
       jmp       near ptr M01_L38
M01_L117:
       mov       rdx,r12
       mov       rcx,offset MT_System.Collections.Generic.ICollection<System.String>
       call      qword ptr [7FF9B3A0F9D8]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       test      rax,rax
       je        short M01_L118
       mov       rdx,rax
       mov       rcx,7FF9B3F316F8
       call      qword ptr [7FF9B3D2E400]; System.Linq.Enumerable.ICollectionToArray[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.ICollection`1<System.__Canon>)
       mov       r14,rax
       jmp       near ptr M01_L38
M01_L118:
       mov       rdx,r12
       mov       rcx,7FF9B3F31780
       call      qword ptr [7FF9B3E8D038]
       mov       r14,rax
       jmp       near ptr M01_L38
M01_L119:
       mov       rcx,20571400918
       mov       r14,[rcx]
       jmp       near ptr M01_L38
M01_L120:
       mov       r11,7FF9B3950708
       call      qword ptr [r11]
       mov       esi,eax
       jmp       near ptr M01_L39
M01_L121:
       mov       rcx,offset MT_System.Func<Excalibur.Dispatch.Abstractions.Routing.IRouteResult, System.Boolean>
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rdx,20571400928
       mov       rdx,[rdx]
       mov       rcx,r14
       mov       r8,offset Excalibur.Dispatch.Abstractions.Routing.RoutingResult+<>c.<.ctor>b__6_1(Excalibur.Dispatch.Abstractions.Routing.IRouteResult)
       call      qword ptr [7FF9B3A06BB0]; System.MulticastDelegate.CtorClosed(System.Object, IntPtr)
       mov       rcx,20571400938
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M01_L40
M01_L122:
       mov       ecx,11
       call      qword ptr [7FF9B3A0F930]
       int       3
M01_L123:
       mov       ecx,0C
       call      qword ptr [7FF9B3A0F930]
       int       3
M01_L124:
       call      qword ptr [7FF9B3A0F480]
       int       3
M01_L125:
       cmp       esi,r15d
       jae       near ptr M01_L146
       mov       rdx,[r13+rsi*8]
       mov       rcx,[r14+8]
       call      qword ptr [r14+18]
       test      eax,eax
       je        short M01_L126
       inc       esi
       cmp       esi,r15d
       jl        short M01_L125
       jmp       near ptr M01_L66
M01_L126:
       xor       esi,esi
       jmp       near ptr M01_L67
M01_L127:
       mov       rcx,offset MT_System.SZGenericArrayEnumerator<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rcx,205714014D8
       mov       rcx,[rcx]
       jmp       near ptr M01_L41
M01_L128:
       mov       rcx,rsi
       mov       r11,7FF9B3950738
       call      qword ptr [r11]
       mov       rcx,rax
       jmp       near ptr M01_L41
M01_L129:
       mov       rcx,rax
       mov       r11,7FF9B3950750
       call      qword ptr [r11]
       jmp       near ptr M01_L66
M01_L130:
       call      M01_L148
       nop
       xor       ebx,ebx
       mov       esi,ebx
       mov       rbx,[rbp-0E8]
       mov       rdi,[rbp-0D0]
       jmp       near ptr M01_L67
M01_L131:
       xor       esi,esi
       jmp       near ptr M01_L67
M01_L132:
       mov       rcx,offset MT_System.ArgumentNullException
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       ecx,0AAE9
       mov       rdx,7FF9B3DA6728
       call      qword ptr [7FF9B3A0F210]
       mov       rdx,rax
       mov       rcx,rdi
       call      qword ptr [7FF9B3D27270]
       mov       rcx,rdi
       call      CORINFO_HELP_THROW
       int       3
M01_L133:
       mov       rdx,[rsi+18]
       test      rdx,rdx
       jne       near ptr M01_L68
       lea       rcx,[rsi+110]
       mov       rdx,205002089A0
       xor       r8d,r8d
       call      qword ptr [7FF9B3E855D8]; System.Guid.ToString(System.String, System.IFormatProvider)
       mov       rbx,rax
       lea       rcx,[rsi+18]
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       mov       rdx,rbx
       jmp       near ptr M01_L68
M01_L134:
       mov       rdx,[rsi+60]
       jmp       near ptr M01_L69
M01_L135:
       mov       ecx,5
       call      qword ptr [7FF9B3E84D08]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedNonGCThreadStaticBase(Int32)
       mov       r14,rax
       jmp       near ptr M01_L70
M01_L136:
       mov       ecx,7
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       jmp       near ptr M01_L72
M01_L137:
       mov       rcx,r13
       call      qword ptr [7FF9B3E85368]; System.Threading.ThreadLocal`1[[System.__Canon, System.Private.CoreLib]].GetValueSlow()
       mov       r12,rax
       jmp       near ptr M01_L73
M01_L138:
       mov       ecx,6
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       mov       rdx,rax
       jmp       near ptr M01_L74
M01_L139:
       mov       rcx,rax
       call      qword ptr [7FF9B3E85428]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].GetValueSlow()
       mov       r8d,eax
       jmp       near ptr M01_L75
M01_L140:
       mov       ecx,7
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       jmp       near ptr M01_L83
M01_L141:
       mov       rcx,r13
       call      qword ptr [7FF9B3E85368]; System.Threading.ThreadLocal`1[[System.__Canon, System.Private.CoreLib]].GetValueSlow()
       mov       rbx,rax
       jmp       near ptr M01_L84
M01_L142:
       mov       ecx,6
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       mov       rdx,rax
       jmp       near ptr M01_L85
M01_L143:
       mov       rcx,rax
       call      qword ptr [7FF9B3E85428]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].GetValueSlow()
       mov       r8d,eax
       jmp       near ptr M01_L86
M01_L144:
       mov       ecx,4C4D
       mov       rdx,7FF9B3E9DF98
       call      qword ptr [7FF9B3A0F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E87F60]
       int       3
M01_L145:
       xor       ecx,ecx
       mov       [rbp-80],ecx
       jmp       near ptr M01_L81
M01_L146:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
       sub       rsp,28
       vzeroupper
       cmp       qword ptr [rbp-0F0],0
       je        short M01_L147
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>+Enumerator
       mov       rax,[rbp-0F0]
       cmp       [rax],rcx
       je        short M01_L147
       mov       rcx,rax
       mov       r11,7FF9B39506D8
       call      qword ptr [r11]
M01_L147:
       nop
       add       rsp,28
       ret
M01_L148:
       sub       rsp,28
       vzeroupper
       cmp       qword ptr [rbp-0F8],0
       je        short M01_L149
       mov       rcx,offset MT_System.Collections.Generic.List<Excalibur.Dispatch.Abstractions.Routing.IRouteResult>+Enumerator
       mov       rax,[rbp-0F8]
       cmp       [rax],rcx
       je        short M01_L149
       mov       rcx,rax
       mov       r11,7FF9B3950750
       call      qword ptr [r11]
M01_L149:
       nop
       add       rsp,28
       ret
; Total bytes of code 6606
```
```assembly
; Excalibur.Dispatch.Abstractions.Routing.RoutingResult.NormalizeBusNames(System.Collections.Generic.IEnumerable`1<System.String>)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,48
       lea       rbp,[rsp+80]
       mov       rbx,rcx
       test      rbx,rbx
       je        near ptr M02_L30
       mov       rcx,offset MT_System.Collections.Generic.List<System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       rsi,rax
       mov       rcx,20571400428
       mov       rdx,[rcx]
       lea       rcx,[rsi+8]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,offset MT_System.Collections.Generic.HashSet<System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       mov       rcx,20571400070
       mov       r14,[rcx]
       mov       rdx,r14
       lea       rcx,[rdi+18]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,20571400048
       cmp       r14,[rcx]
       je        near ptr M02_L29
       mov       rcx,20571400068
       mov       rdx,20571400060
       mov       rax,20571400058
       cmp       r14,[rcx]
       mov       rdx,[rdx]
       cmove     rdx,[rax]
M02_L00:
       lea       rcx,[rdi+18]
       call      CORINFO_HELP_ASSIGN_REF
       mov       rcx,offset MT_<>z__ReadOnlySingleElementList<System.String>
       cmp       [rbx],rcx
       jne       near ptr M02_L28
       mov       rcx,offset MT_<>z__ReadOnlySingleElementList<System.String>+Enumerator
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       mov       rdx,[rbx+8]
       lea       rcx,[r14+8]
       call      CORINFO_HELP_ASSIGN_REF
M02_L01:
       mov       [rbp-48],r14
M02_L02:
       mov       rcx,[rbp-48]
       mov       rbx,[rcx]
       mov       r11,offset MT_System.Collections.Generic.List<System.String>+Enumerator
       cmp       rbx,r11
       je        short M02_L03
       mov       r11,7FF9B3950688
       call      qword ptr [r11]
       test      eax,eax
       je        near ptr M02_L25
       mov       rcx,[rbp-48]
       mov       r11,7FF9B3950690
       call      qword ptr [r11]
       mov       rbx,rax
       jmp       short M02_L04
M02_L03:
       lea       r14,[rcx+8]
       mov       rdx,[r14]
       mov       rax,rdx
       mov       r8d,[r14+10]
       cmp       r8d,[rdx+14]
       jne       near ptr M02_L23
       mov       edx,[r14+14]
       cmp       edx,[rax+10]
       jae       near ptr M02_L19
       mov       rdx,[rax+8]
       mov       eax,[r14+14]
       cmp       eax,[rdx+8]
       jae       near ptr M02_L24
       mov       rdx,[rdx+rax*8+10]
       lea       rcx,[r14+8]
       call      CORINFO_HELP_ASSIGN_REF
       inc       dword ptr [r14+14]
       mov       rcx,[rbp-48]
       mov       rbx,[rcx+10]
M02_L04:
       test      rbx,rbx
       je        near ptr M02_L02
       xor       r14d,r14d
       cmp       dword ptr [rbx+8],0
       jle       near ptr M02_L02
M02_L05:
       movzx     eax,word ptr [rbx+r14*2+0C]
       cmp       eax,100
       jge       near ptr M02_L20
       mov       rdx,7FF9635A68D0
       test      byte ptr [rdx+rax],80
       jne       near ptr M02_L21
M02_L06:
       mov       r14,rbx
       cmp       qword ptr [rdi+8],0
       jne       short M02_L07
       xor       ecx,ecx
       call      qword ptr [7FF9B3A05A88]; System.Collections.HashHelpers.GetPrime(Int32)
       mov       r15d,eax
       movsxd    rdx,r15d
       mov       rcx,offset MT_System.Int32[]
       call      CORINFO_HELP_NEWARR_1_VC
       mov       r13,rax
       movsxd    rdx,r15d
       mov       rcx,offset MT_System.Collections.Generic.HashSet<System.String>+Entry[]
       call      CORINFO_HELP_NEWARR_1_VC
       mov       r12,rax
       mov       dword ptr [rdi+2C],0FFFFFFFF
       lea       rcx,[rdi+8]
       mov       rdx,r13
       call      CORINFO_HELP_ASSIGN_REF
       lea       rcx,[rdi+10]
       mov       rdx,r12
       call      CORINFO_HELP_ASSIGN_REF
       mov       rax,0FFFFFFFFFFFFFFFF
       mov       ecx,r15d
       xor       edx,edx
       div       rcx
       inc       rax
       mov       [rdi+20],rax
M02_L07:
       mov       r15,[rdi+10]
       mov       r13,[rdi+18]
       xor       r12d,r12d
       mov       rcx,r13
       mov       rdx,r14
       mov       r11,7FF9B39506A0
       call      qword ptr [r11]
       mov       r14d,eax
       mov       rdx,[rdi+8]
       mov       ecx,r14d
       imul      rcx,[rdi+20]
       shr       rcx,20
       inc       rcx
       mov       r8d,[rdx+8]
       mov       r11d,r8d
       imul      rcx,r11
       shr       rcx,20
       cmp       ecx,r8d
       jae       near ptr M02_L24
       mov       ecx,ecx
       lea       rax,[rdx+rcx*4+10]
       mov       [rbp-50],rax
       mov       r10d,[rax]
       dec       r10d
       js        short M02_L10
M02_L08:
       cmp       r10d,[r15+8]
       jae       near ptr M02_L24
       mov       edx,r10d
       shl       rdx,4
       lea       r10,[r15+rdx+10]
       mov       [rbp-58],r10
       cmp       [r10+8],r14d
       jne       short M02_L09
       mov       rdx,[r10]
       mov       rcx,r13
       mov       r8,rbx
       mov       r11,7FF9B39506A8
       call      qword ptr [r11]
       test      eax,eax
       mov       r10,[rbp-58]
       jne       near ptr M02_L02
M02_L09:
       mov       r10d,[r10+0C]
       inc       r12d
       cmp       [r15+8],r12d
       jb        near ptr M02_L18
       test      r10d,r10d
       jge       short M02_L08
M02_L10:
       cmp       dword ptr [rdi+30],0
       jg        near ptr M02_L14
       mov       edx,[rdi+28]
       mov       [rbp-3C],edx
       cmp       [r15+8],edx
       jne       short M02_L13
       mov       ecx,[rdi+28]
       lea       eax,[rcx+rcx]
       cmp       eax,7FFFFFC3
       ja        near ptr M02_L22
M02_L11:
       mov       ecx,eax
       call      qword ptr [7FF9B3A05A88]; System.Collections.HashHelpers.GetPrime(Int32)
       mov       r8d,eax
M02_L12:
       mov       rcx,rdi
       mov       edx,r8d
       xor       r8d,r8d
       call      qword ptr [7FF9B3D2E3D0]; System.Collections.Generic.HashSet`1[[System.__Canon, System.Private.CoreLib]].Resize(Int32, Boolean)
       mov       rcx,[rdi+8]
       mov       edx,r14d
       imul      rdx,[rdi+20]
       shr       rdx,20
       inc       rdx
       mov       eax,[rcx+8]
       mov       r8d,eax
       imul      rdx,r8
       shr       rdx,20
       cmp       edx,eax
       jae       near ptr M02_L24
       mov       edx,edx
       lea       rax,[rcx+rdx*4+10]
       mov       r15,rax
       mov       [rbp-50],r15
M02_L13:
       mov       edx,[rbp-3C]
       mov       r15d,edx
       lea       ecx,[r15+1]
       mov       [rdi+28],ecx
       mov       rcx,[rdi+10]
       mov       r8,rcx
       mov       rax,r8
       mov       r8d,r15d
       mov       r15,rax
       jmp       short M02_L15
M02_L14:
       mov       ecx,[rdi+2C]
       mov       r8d,ecx
       dec       dword ptr [rdi+30]
       cmp       ecx,[r15+8]
       jae       near ptr M02_L24
       shl       rcx,4
       mov       ecx,[r15+rcx+1C]
       neg       ecx
       add       ecx,0FFFFFFFD
       mov       [rdi+2C],ecx
M02_L15:
       cmp       r8d,[r15+8]
       jae       near ptr M02_L24
       mov       [rbp-40],r8d
       mov       ecx,r8d
       shl       rcx,4
       lea       rcx,[r15+rcx+10]
       mov       [rcx+8],r14d
       mov       rax,[rbp-50]
       mov       edx,[rax]
       dec       edx
       mov       [rcx+0C],edx
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       mov       r14d,[rbp-40]
       inc       r14d
       mov       rdx,[rbp-50]
       mov       [rdx],r14d
       inc       dword ptr [rdi+34]
       cmp       r12d,64
       jbe       short M02_L16
       mov       rdx,r13
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer
       call      qword ptr [7FF9B3A06850]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rax,rax
       je        short M02_L16
       mov       edx,[r15+8]
       mov       rcx,rdi
       mov       r8d,1
       call      qword ptr [7FF9B3D2E3D0]; System.Collections.Generic.HashSet`1[[System.__Canon, System.Private.CoreLib]].Resize(Int32, Boolean)
       mov       rcx,rdi
       mov       rdx,rbx
       call      qword ptr [7FF9B3C0E370]; System.Collections.Generic.HashSet`1[[System.__Canon, System.Private.CoreLib]].FindItemIndex(System.__Canon)
M02_L16:
       inc       dword ptr [rsi+14]
       mov       r14,[rsi+8]
       mov       r15d,[rsi+10]
       cmp       [r14+8],r15d
       ja        short M02_L17
       mov       rcx,rsi
       mov       rdx,rbx
       call      qword ptr [7FF9B3A071C8]; System.Collections.Generic.List`1[[System.__Canon, System.Private.CoreLib]].AddWithResize(System.__Canon)
       jmp       near ptr M02_L02
M02_L17:
       lea       ecx,[r15+1]
       mov       [rsi+10],ecx
       mov       ecx,r15d
       lea       rcx,[r14+rcx*8+10]
       mov       rdx,rbx
       call      CORINFO_HELP_ASSIGN_REF
       jmp       near ptr M02_L02
M02_L18:
       call      qword ptr [7FF9B3A0F480]
       int       3
M02_L19:
       xor       r11d,r11d
       mov       [r14+8],r11
       mov       dword ptr [r14+14],0FFFFFFFF
       jmp       short M02_L25
M02_L20:
       mov       ecx,eax
       call      qword ptr [7FF9B3E87C60]
       test      eax,eax
       je        near ptr M02_L06
M02_L21:
       inc       r14d
       cmp       [rbx+8],r14d
       jg        near ptr M02_L05
       jmp       near ptr M02_L02
M02_L22:
       cmp       ecx,7FFFFFC3
       jge       near ptr M02_L11
       mov       r8d,7FFFFFC3
       jmp       near ptr M02_L12
M02_L23:
       call      qword ptr [7FF9B3A0FC48]
       int       3
M02_L24:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
M02_L25:
       mov       r11,offset MT_<>z__ReadOnlySingleElementList<System.String>+Enumerator
       cmp       rbx,r11
       jne       short M02_L27
M02_L26:
       cmp       dword ptr [rsi+10],0
       je        short M02_L30
       mov       rcx,offset MT_System.Collections.ObjectModel.ReadOnlyCollection<System.String>
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       lea       rcx,[rbx+8]
       mov       rdx,rsi
       call      CORINFO_HELP_ASSIGN_REF
       mov       rax,rbx
       add       rsp,48
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M02_L27:
       mov       rcx,[rbp-48]
       mov       r11,7FF9B3950698
       call      qword ptr [r11]
       jmp       short M02_L26
M02_L28:
       mov       rcx,rbx
       mov       r11,7FF9B3950680
       call      qword ptr [r11]
       mov       r14,rax
       jmp       near ptr M02_L01
M02_L29:
       mov       rdx,20571400050
       mov       rdx,[rdx]
       jmp       near ptr M02_L00
M02_L30:
       mov       rax,20571400900
       mov       rax,[rax]
       add       rsp,48
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
       sub       rsp,28
       cmp       qword ptr [rbp-48],0
       je        short M02_L31
       mov       rcx,[rbp-48]
       mov       rbx,[rcx]
       mov       r11,offset MT_<>z__ReadOnlySingleElementList<System.String>+Enumerator
       cmp       rbx,r11
       je        short M02_L31
       mov       r11,7FF9B3950698
       call      qword ptr [r11]
M02_L31:
       nop
       add       rsp,28
       ret
; Total bytes of code 1431
```
```assembly
; System.Collections.Generic.List`1[[System.__Canon, System.Private.CoreLib]].AddWithResize(System.__Canon)
       push      r14
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,30
       mov       [rsp+28],rcx
       mov       rbx,rcx
       mov       rsi,rdx
       mov       edi,[rbx+10]
       mov       ebp,edi
       lea       ecx,[rbp+1]
       mov       rdx,[rbx+8]
       cmp       dword ptr [rdx+8],0
       jne       short M03_L01
       mov       r14d,4
M03_L00:
       mov       edx,7FFFFFC7
       cmp       r14d,7FFFFFC7
       cmova     r14d,edx
       cmp       r14d,ecx
       cmovl     r14d,ecx
       cmp       r14d,edi
       jge       short M03_L02
       mov       ecx,7
       mov       edx,0F
       call      qword ptr [7FF9B3DB65B0]
       int       3
M03_L01:
       mov       rdx,[rbx+8]
       mov       r14d,[rdx+8]
       add       r14d,r14d
       jmp       short M03_L00
M03_L02:
       mov       rcx,[rbx+8]
       cmp       [rcx+8],r14d
       je        near ptr M03_L08
       test      r14d,r14d
       jg        short M03_L05
       mov       rcx,[rbx]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rdx,[rdx+68]
       test      rdx,rdx
       je        short M03_L04
M03_L03:
       mov       rcx,rdx
       call      System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rdx,[rax]
       lea       rcx,[rbx+8]
       call      CORINFO_HELP_ASSIGN_REF
       jmp       short M03_L08
M03_L04:
       mov       rdx,7FF9B3ECD4B8
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rdx,rax
       jmp       short M03_L03
M03_L05:
       mov       rcx,[rbx]
       mov       rdx,[rcx+30]
       mov       rdx,[rdx]
       mov       rax,[rdx+70]
       test      rax,rax
       je        short M03_L09
       mov       rcx,rax
M03_L06:
       mov       edx,r14d
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       r14,rax
       test      edi,edi
       jle       short M03_L07
       mov       rcx,[rbx+8]
       mov       r8d,edi
       mov       rdx,r14
       call      qword ptr [7FF9B3A0F588]; System.Array.Copy(System.Array, System.Array, Int32)
M03_L07:
       lea       rcx,[rbx+8]
       mov       rdx,r14
       call      CORINFO_HELP_ASSIGN_REF
M03_L08:
       lea       ecx,[rbp+1]
       mov       [rbx+10],ecx
       mov       rcx,[rbx+8]
       movsxd    rdx,ebp
       mov       r8,rsi
       call      System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       nop
       add       rsp,30
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r14
       ret
M03_L09:
       mov       rdx,7FF9B3ECDE98
       call      qword ptr [7FF9B3A0F4B0]; System.Runtime.CompilerServices.GenericsHelpers.Class(IntPtr, IntPtr)
       mov       rcx,rax
       jmp       short M03_L06
; Total bytes of code 303
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.StelemRef(System.Object[], IntPtr, System.Object)
       sub       rsp,28
       mov       eax,[rcx+8]
       cmp       rax,rdx
       jbe       short M04_L02
       lea       rax,[rcx+rdx*8+10]
       mov       rdx,[rcx]
       mov       rdx,[rdx+30]
       test      r8,r8
       je        short M04_L03
       cmp       rdx,[r8]
       jne       short M04_L01
M04_L00:
       mov       rcx,rax
       mov       rdx,r8
       add       rsp,28
       jmp       near ptr 00007FFA13639DD0
M04_L01:
       mov       r10,offset MT_System.Object[]
       cmp       [rcx],r10
       je        short M04_L00
       mov       rcx,rax
       add       rsp,28
       jmp       qword ptr [7FF9B3D24DE0]; System.Runtime.CompilerServices.CastHelpers.StelemRef_Helper(System.Object ByRef, Void*, System.Object)
M04_L02:
       call      qword ptr [7FF9B3E87C00]
       int       3
M04_L03:
       xor       ecx,ecx
       mov       [rax],rcx
       add       rsp,28
       ret
; Total bytes of code 94
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass(Void*, System.Object)
       test      rdx,rdx
       je        short M05_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M05_L02
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L02
M05_L00:
       test      rax,rax
       je        short M05_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L02
       test      rax,rax
       jne       short M05_L03
M05_L01:
       xor       edx,edx
M05_L02:
       mov       rax,rdx
       ret
M05_L03:
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L02
       test      rax,rax
       je        short M05_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L02
       test      rax,rax
       je        short M05_L01
       mov       rax,[rax+10]
       cmp       rax,rcx
       je        short M05_L02
       jmp       short M05_L00
; Total bytes of code 86
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfInterface(Void*, System.Object)
       test      rdx,rdx
       je        short M06_L01
       mov       rax,[rdx]
       movzx     r8d,word ptr [rax+0E]
       test      r8,r8
       je        short M06_L04
       mov       r10,[rax+38]
       cmp       r8,4
       jl        short M06_L03
M06_L00:
       cmp       [r10],rcx
       jne       short M06_L02
M06_L01:
       mov       rax,rdx
       ret
M06_L02:
       cmp       [r10+8],rcx
       je        short M06_L01
       cmp       [r10+10],rcx
       je        short M06_L01
       cmp       [r10+18],rcx
       je        short M06_L01
       add       r10,20
       add       r8,0FFFFFFFFFFFFFFFC
       cmp       r8,4
       jge       short M06_L00
       test      r8,r8
       je        short M06_L04
M06_L03:
       cmp       [r10],rcx
       je        short M06_L01
       add       r10,8
       dec       r8
       test      r8,r8
       jg        short M06_L03
M06_L04:
       test      dword ptr [rax],500C0000
       jne       short M06_L05
       xor       edx,edx
       jmp       short M06_L01
M06_L05:
       jmp       qword ptr [7FF9B3E8D3F8]; System.Runtime.CompilerServices.CastHelpers.IsInstance_Helper(Void*, System.Object)
; Total bytes of code 109
```
```assembly
; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfAny(Void*, System.Object)
       push      rsi
       push      rbx
       test      rdx,rdx
       je        short M07_L02
       mov       rax,[rdx]
       cmp       rax,rcx
       je        short M07_L02
       mov       r8,20571400038
       mov       r8,[r8]
       add       r8,10
       rorx      r10,rax,20
       xor       r10,rcx
       mov       r9,9E3779B97F4A7C15
       imul      r10,r9
       mov       r9d,[r8]
       shrx      r10,r10,r9
       xor       r9d,r9d
M07_L00:
       lea       r11d,[r10+1]
       movsxd    r11,r11d
       lea       r11,[r11+r11*2]
       lea       r11,[r8+r11*8]
       mov       ebx,[r11]
       mov       rsi,[r11+8]
       and       ebx,0FFFFFFFE
       cmp       rsi,rax
       jne       short M07_L03
       mov       rsi,rcx
       xor       rsi,[r11+10]
       cmp       rsi,1
       ja        short M07_L03
       cmp       ebx,[r11]
       jne       short M07_L04
M07_L01:
       cmp       esi,1
       je        short M07_L02
       test      esi,esi
       jne       short M07_L05
       xor       edx,edx
M07_L02:
       mov       rax,rdx
       pop       rbx
       pop       rsi
       ret
M07_L03:
       test      ebx,ebx
       je        short M07_L04
       inc       r9d
       add       r10d,r9d
       and       r10d,[r8+4]
       cmp       r9d,8
       jl        short M07_L00
M07_L04:
       mov       esi,2
       jmp       short M07_L01
M07_L05:
       pop       rbx
       pop       rsi
       jmp       qword ptr [7FF9B3A0FA98]; System.Runtime.CompilerServices.CastHelpers.IsInstanceOfAny_NoCacheLookup(Void*, System.Object)
; Total bytes of code 166
```
```assembly
; Excalibur.Dispatch.Abstractions.Routing.RoutingResult+<>c.<.ctor>b__6_1(Excalibur.Dispatch.Abstractions.Routing.IRouteResult)
       mov       rax,offset MT_Excalibur.Dispatch.Abstractions.Routing.RouteResult
       cmp       [rdx],rax
       jne       short M08_L04
       mov       rax,[rdx+8]
       mov       rcx,20500206AA0
       cmp       rax,rcx
       je        short M08_L02
       test      rax,rax
       je        short M08_L00
       cmp       dword ptr [rax+8],5
       je        short M08_L01
M08_L00:
       xor       eax,eax
       jmp       short M08_L03
M08_L01:
       mov       rcx,20002000200020
       or        rcx,[rax+0C]
       mov       rdx,610063006F006C
       xor       rcx,rdx
       movzx     eax,word ptr [rax+14]
       or        eax,20
       xor       eax,6C
       or        rax,rcx
       sete      al
       movzx     eax,al
       jmp       short M08_L03
M08_L02:
       mov       eax,1
M08_L03:
       ret
M08_L04:
       mov       rcx,rdx
       mov       r11,7FF9B3950A00
       jmp       qword ptr [r11]
; Total bytes of code 119
```
```assembly
; System.DateTime.get_UtcNow()
       push      rbp
       push      rsi
       push      rbx
       sub       rsp,30
       lea       rbp,[rsp+40]
       lea       rcx,[rbp-18]
       mov       rax,7FFB0F697650
       call      rax
       mov       rbx,[rbp-18]
       mov       rax,20571400958
       mov       rsi,[rax]
       sub       rbx,[rsi+8]
       cmp       dword ptr [7FFA1394F778],0
       jne       short M09_L01
M09_L00:
       mov       eax,0B2D05E00
       cmp       rbx,rax
       jae       short M09_L02
       mov       rax,rbx
       add       rax,[rsi+10]
       add       rsp,30
       pop       rbx
       pop       rsi
       pop       rbp
       ret
M09_L01:
       call      CORINFO_HELP_POLL_GC
       jmp       short M09_L00
M09_L02:
       call      qword ptr [7FF9B3DB54D0]; System.DateTime.UpdateLeapSecondCacheAndReturnUtcNow()
       nop
       add       rsp,30
       pop       rbx
       pop       rsi
       pop       rbp
       ret
; Total bytes of code 105
```
```assembly
; System.Buffer.BlockCopy(System.Array, Int32, System.Array, Int32, Int32)
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,28
       mov       rsi,rcx
       mov       edi,edx
       mov       rbx,r8
       mov       ebp,r9d
       mov       r14d,[rsp+90]
       test      rsi,rsi
       je        near ptr M10_L02
       test      rbx,rbx
       je        near ptr M10_L03
       mov       r15d,[rsi+8]
       mov       r13,[rsi]
       mov       rcx,offset MT_System.Byte[]
       cmp       r13,rcx
       jne       near ptr M10_L04
M10_L00:
       mov       r12,r15
       cmp       rsi,rbx
       je        short M10_L01
       mov       r12d,[rbx+8]
       mov       rcx,offset MT_System.Byte[]
       cmp       [rbx],rcx
       jne       near ptr M10_L06
M10_L01:
       test      edi,edi
       jl        near ptr M10_L08
       test      ebp,ebp
       jl        near ptr M10_L09
       test      r14d,r14d
       jl        near ptr M10_L10
       mov       r8d,r14d
       mov       edx,edi
       mov       ecx,ebp
       lea       rax,[rdx+r8]
       cmp       rax,r15
       ja        near ptr M10_L11
       lea       rax,[rcx+r8]
       cmp       rax,r12
       ja        near ptr M10_L11
       lea       rax,[rbx+8]
       mov       r10,[rbx]
       mov       r10d,[r10+4]
       add       r10,0FFFFFFFFFFFFFFF0
       add       r10,rax
       add       rcx,r10
       add       rsi,8
       mov       eax,[r13+4]
       add       rax,0FFFFFFFFFFFFFFF0
       add       rax,rsi
       add       rdx,rax
       call      qword ptr [7FF9B3A05818]; System.SpanHelpers.Memmove(Byte ByRef, Byte ByRef, UIntPtr)
       nop
       add       rsp,28
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       ret
M10_L02:
       mov       ecx,257
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E87F60]
       int       3
M10_L03:
       mov       ecx,25F
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       rcx,rax
       call      qword ptr [7FF9B3E87F60]
       int       3
M10_L04:
       mov       rcx,rsi
       call      00007FFA135DFF20
       mov       ecx,3003FFC
       bt        ecx,eax
       jb        short M10_L05
       mov       rcx,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rbx,rax
       call      qword ptr [7FF9B3E87F78]
       mov       rsi,rax
       mov       ecx,257
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       r8,rax
       mov       rdx,rsi
       mov       rcx,rbx
       call      qword ptr [7FF9B3D27AC8]
       mov       rcx,rbx
       call      CORINFO_HELP_THROW
       int       3
M10_L05:
       movzx     ecx,word ptr [r13]
       imul      r15,rcx
       jmp       near ptr M10_L00
M10_L06:
       mov       rcx,rbx
       call      00007FFA135DFF20
       mov       ecx,3003FFC
       bt        ecx,eax
       jb        short M10_L07
       mov       rcx,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       rdi,rax
       call      qword ptr [7FF9B3E87F78]
       mov       rbp,rax
       mov       ecx,25F
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       r8,rax
       mov       rdx,rbp
       mov       rcx,rdi
       call      qword ptr [7FF9B3D27AC8]
       mov       rcx,rdi
       call      CORINFO_HELP_THROW
       int       3
M10_L07:
       mov       rcx,[rbx]
       movzx     ecx,word ptr [rcx]
       imul      r12,rcx
       jmp       near ptr M10_L01
M10_L08:
       mov       ecx,267
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       rdx,rax
       mov       ecx,edi
       call      qword ptr [7FF9B3E87F18]
       int       3
M10_L09:
       mov       ecx,27B
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       rdx,rax
       mov       ecx,ebp
       call      qword ptr [7FF9B3E87F18]
       int       3
M10_L10:
       mov       ecx,28F
       mov       rdx,7FF9B3944000
       call      qword ptr [7FF9B3A0F210]
       mov       rdx,rax
       mov       ecx,r14d
       call      qword ptr [7FF9B3E87F18]
       int       3
M10_L11:
       mov       rcx,offset MT_System.ArgumentException
       call      CORINFO_HELP_NEWSFAST
       mov       r14,rax
       call      qword ptr [7FF9B3E87F90]
       mov       rdx,rax
       mov       rcx,r14
       call      qword ptr [7FF9B3D25B00]
       mov       rcx,r14
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 647
```
```assembly
; System.Security.Cryptography.RandomNumberGeneratorImplementation.GetBytes(Byte*, Int32)
       push      rbp
       push      r15
       push      r14
       push      r13
       push      r12
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,58
       vzeroupper
       lea       rbp,[rsp+90]
       mov       rbx,rcx
       mov       esi,edx
       lea       rcx,[rbp-70]
       call      CORINFO_HELP_INIT_PINVOKE_FRAME
       mov       rdi,rax
       mov       r8,rsp
       mov       [rbp-58],r8
       mov       r8,rbp
       mov       [rbp-48],r8
       mov       r8d,esi
       mov       rdx,rbx
       xor       ecx,ecx
       mov       r9d,2
       mov       rax,7FF9B3EDE420
       mov       [rbp-60],rax
       lea       rax,[M11_L00]
       mov       [rbp-50],rax
       lea       rax,[rbp-70]
       mov       [rdi+8],rax
       mov       byte ptr [rdi+4],0
       mov       rax,7FFB0CF98F00
       call      rax
M11_L00:
       mov       byte ptr [rdi+4],1
       cmp       dword ptr [7FFA1394F778],0
       je        short M11_L01
       call      qword ptr [7FFA1393D608]; CORINFO_HELP_STOP_FOR_GC
M11_L01:
       mov       rcx,[rbp-68]
       mov       [rdi+8],rcx
       test      eax,eax
       jne       short M11_L02
       add       rsp,58
       pop       rbx
       pop       rsi
       pop       rdi
       pop       r12
       pop       r13
       pop       r14
       pop       r15
       pop       rbp
       ret
M11_L02:
       mov       ecx,eax
       call      qword ptr [7FF9B3E8D3E0]
       mov       rcx,rax
       call      CORINFO_HELP_THROW
       int       3
; Total bytes of code 186
```
```assembly
; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].SetValueSlow(Int32, LinkedSlotVolatile<Int32>[])
       push      rbp
       sub       rsp,50
       lea       rbp,[rsp+50]
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rbp-20],xmm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp+10],rcx
       mov       [rbp+18],edx
       mov       [rbp+20],r8
       mov       rax,[rbp+10]
       mov       eax,[rax+18]
       not       eax
       mov       [rbp-4],eax
       cmp       dword ptr [rbp-4],0
       setl      cl
       movzx     ecx,cl
       mov       rdx,[rbp+10]
       call      qword ptr [7FF9B3E85470]; System.ObjectDisposedException.ThrowIf(Boolean, System.Object)
       cmp       qword ptr [rbp+20],0
       jne       near ptr M12_L00
       mov       eax,[rbp-4]
       lea       ecx,[rax+1]
       call      qword ptr [7FF9B3E85518]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].GetNewTableSize(Int32)
       cdqe
       mov       [rbp-28],rax
       mov       rdx,[rbp-28]
       mov       rcx,offset MT_System.Threading.ThreadLocal<System.Int32>+LinkedSlotVolatile[]
       call      CORINFO_HELP_NEWARR_1_PTR
       mov       [rbp+20],rax
       mov       rax,[rbp+20]
       mov       [rbp-20],rax
       mov       rcx,offset MT_System.Threading.ThreadLocal<System.Int32>+FinalizationHelper
       call      CORINFO_HELP_NEWFAST
       mov       [rbp-18],rax
       mov       rcx,[rbp-18]
       mov       rdx,[rbp-20]
       call      qword ptr [7FF9B3E85530]; System.Threading.ThreadLocal`1+FinalizationHelper[[System.Int32, System.Private.CoreLib]]..ctor(LinkedSlotVolatile<Int32>[])
       mov       ecx,6
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       lea       rcx,[rax+18]
       mov       rdx,[rbp-18]
       call      CORINFO_HELP_ASSIGN_REF
       mov       ecx,6
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       lea       rcx,[rax+10]
       mov       rdx,[rbp+20]
       call      CORINFO_HELP_ASSIGN_REF
M12_L00:
       mov       rax,[rbp+20]
       mov       eax,[rax+8]
       cmp       eax,[rbp-4]
       jg        short M12_L01
       mov       eax,[rbp-4]
       lea       edx,[rax+1]
       lea       rcx,[rbp+20]
       call      qword ptr [7FF9B3E85548]
       mov       ecx,6
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       mov       rax,[rax+18]
       lea       rcx,[rax+8]
       mov       rdx,[rbp+20]
       call      CORINFO_HELP_ASSIGN_REF
       mov       ecx,6
       call      qword ptr [7FF9B3E84D50]; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       lea       rcx,[rax+10]
       mov       rdx,[rbp+20]
       call      CORINFO_HELP_ASSIGN_REF
M12_L01:
       mov       rax,[rbp+20]
       mov       ecx,[rbp-4]
       cmp       ecx,[rax+8]
       jae       short M12_L03
       mov       edx,ecx
       lea       rax,[rax+rdx*8+10]
       cmp       qword ptr [rax],0
       jne       short M12_L02
       mov       rcx,[rbp+10]
       mov       rdx,[rbp+20]
       mov       r8d,[rbp-4]
       mov       r9d,[rbp+18]
       call      qword ptr [7FF9B3E85560]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].CreateLinkedSlot(LinkedSlotVolatile<Int32>[], Int32, Int32)
       nop
       add       rsp,50
       pop       rbp
       ret
M12_L02:
       mov       rax,[rbp+20]
       mov       ecx,[rbp-4]
       cmp       ecx,[rax+8]
       jae       short M12_L03
       mov       edx,ecx
       lea       rax,[rax+rdx*8+10]
       mov       rax,[rax]
       mov       [rbp-10],rax
       mov       rax,[rbp+10]
       movzx     eax,byte ptr [rax+1C]
       test      eax,eax
       sete      cl
       movzx     ecx,cl
       mov       rdx,[rbp+10]
       call      qword ptr [7FF9B3E85470]; System.ObjectDisposedException.ThrowIf(Boolean, System.Object)
       mov       rax,[rbp-10]
       mov       ecx,[rbp+18]
       mov       [rax+20],ecx
       add       rsp,50
       pop       rbp
       ret
M12_L03:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 417
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBase(System.Runtime.CompilerServices.MethodTable*)
       mov       rax,[rcx+20]
       mov       rax,[rax-18]
       mov       rdx,rax
       test      dl,1
       jne       short M13_L00
       ret
M13_L00:
       jmp       qword ptr [7FF9B3A05C38]; System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBaseSlow(System.Runtime.CompilerServices.MethodTable*)
; Total bytes of code 23
```
```assembly
; Excalibur.Dispatch.Abstractions.Routing.RoutingResult+<>c.<.ctor>b__6_0(Excalibur.Dispatch.Abstractions.Routing.IRouteResult)
       mov       rax,offset MT_Excalibur.Dispatch.Abstractions.Routing.RouteResult
       cmp       [rdx],rax
       jne       short M14_L00
       mov       rax,[rdx+8]
       ret
M14_L00:
       mov       rcx,rdx
       mov       r11,7FF9B39509D0
       jmp       qword ptr [r11]
; Total bytes of code 36
```
```assembly
; System.MulticastDelegate.CtorClosed(System.Object, IntPtr)
       push      rsi
       push      rbx
       sub       rsp,28
       mov       rbx,rcx
       mov       rsi,r8
       test      rdx,rdx
       je        short M15_L00
       lea       rcx,[rbx+8]
       call      CORINFO_HELP_ASSIGN_REF
       mov       [rbx+18],rsi
       add       rsp,28
       pop       rbx
       pop       rsi
       ret
M15_L00:
       call      qword ptr [7FF9B3E8FF18]
       int       3
; Total bytes of code 44
```
```assembly
; System.Runtime.CompilerServices.VirtualDispatchHelpers.VirtualFunctionPointer(System.Object, IntPtr, IntPtr)
       push      r15
       push      r14
       push      r13
       push      rdi
       push      rsi
       push      rbp
       push      rbx
       sub       rsp,20
       mov       rdi,rcx
       mov       rbx,rdx
       mov       rsi,r8
       call      qword ptr [7FF964217C18]
       mov       rcx,[rax]
       add       rcx,8
       mov       rax,[rdi]
       mov       r8d,ebx
       rol       r8d,5
       add       r8d,eax
       mov       edx,esi
       ror       edx,5
       add       r8d,edx
       mov       rdx,[rcx]
       movsxd    r10,r8d
       mov       r9,9E3779B97F4A7C15
       imul      r10,r9
       movzx     ecx,byte ptr [rdx+10]
       shr       r10,cl
       xor       ecx,ecx
M16_L00:
       lea       r9d,[r10+1]
       movsxd    r9,r9d
       imul      r9,30
       lea       r9,[rdx+r9+10]
       mov       r11d,[r9]
       mov       ebp,[r9+8]
       mov       r14,[r9+10]
       mov       r15,[r9+18]
       mov       r13,[r9+20]
       cmp       r8d,ebp
       jne       short M16_L01
       mov       rbp,rax
       sub       rbp,r14
       mov       r14,rbx
       sub       r14,r15
       or        rbp,r14
       mov       r14,rsi
       sub       r14,r13
       or        rbp,r14
       jne       short M16_L01
       mov       rax,[r9+28]
       and       r11d,0FFFFFFFE
       cmp       r11d,[r9]
       jne       short M16_L02
       add       rsp,20
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       ret
M16_L01:
       test      r11d,r11d
       je        short M16_L02
       inc       ecx
       add       r10d,ecx
       mov       r9d,[rdx+8]
       add       r9d,0FFFFFFFE
       and       r10d,r9d
       cmp       ecx,8
       jl        short M16_L00
M16_L02:
       mov       r8,rsi
       mov       rcx,rdi
       mov       rdx,rbx
       lea       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       add       rsp,20
       pop       rbx
       pop       rbp
       pop       rsi
       pop       rdi
       pop       r13
       pop       r14
       pop       r15
       jmp       qword ptr [rax]
; Total bytes of code 239
```
```assembly
; System.Linq.Enumerable.ICollectionToArray[[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.ICollection`1<System.__Canon>)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,30
       mov       [rsp+28],rcx
       mov       rbx,rcx
       mov       rsi,rdx
       mov       rcx,rbx
       call      qword ptr [7FFA6B308600]
       mov       rcx,rsi
       mov       r11,rax
       call      qword ptr [rax]
       mov       edi,eax
       test      edi,edi
       je        short M17_L00
       mov       rcx,rbx
       call      qword ptr [7FFA6B307A48]
       mov       rcx,rax
       movsxd    rdx,edi
       call      qword ptr [7FFA6B3056D8]; CORINFO_HELP_NEWARR_1_DIRECT
       mov       rdi,rax
       mov       rcx,rbx
       call      qword ptr [7FFA6B308608]
       mov       rcx,rsi
       mov       r11,rax
       mov       rdx,rdi
       xor       r8d,r8d
       call      qword ptr [rax]
       mov       rax,rdi
       add       rsp,30
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M17_L00:
       mov       rcx,rbx
       call      qword ptr [7FFA6B308228]
       mov       rcx,rax
       lea       rax,[System.Linq.Enumerable.Select[[System.__Canon, System.Private.CoreLib],[System.__Canon, System.Private.CoreLib]](System.Collections.Generic.IEnumerable`1<System.__Canon>, System.Func`2<System.__Canon,System.__Canon>)]
       add       rsp,30
       pop       rbx
       pop       rsi
       pop       rdi
       jmp       qword ptr [rax]
; Total bytes of code 128
```
```assembly
; System.Guid.ToString(System.String, System.IFormatProvider)
       push      rdi
       push      rsi
       push      rbx
       sub       rsp,50
       xor       eax,eax
       mov       [rsp+28],rax
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rsp+30],xmm4
       mov       [rsp+40],rax
       mov       rsi,rcx
       mov       rbx,rdx
       test      rbx,rbx
       je        short M18_L00
       mov       edi,[rbx+8]
       test      edi,edi
       je        short M18_L00
       cmp       edi,1
       jne       near ptr M18_L04
       movzx     eax,word ptr [rbx+0C]
       or        eax,20
       cmp       eax,64
       jg        near ptr M18_L05
       cmp       eax,62
       je        near ptr M18_L08
       cmp       eax,64
       jne       near ptr M18_L10
       mov       rcx,7FF9B3EEF46C
       call      CORINFO_HELP_COUNTPROFILE32
M18_L00:
       mov       edi,24
M18_L01:
       mov       rcx,7FF9B3EEF480
       call      CORINFO_HELP_COUNTPROFILE32
       movsxd    rdx,edi
       mov       rcx,offset MT_System.String
       call      00007FFA1363AFE0
       mov       rdi,rax
       lea       rdx,[rdi+0C]
       mov       r9d,[rdi+8]
       test      rbx,rbx
       jne       short M18_L03
       xor       r8d,r8d
       xor       ecx,ecx
M18_L02:
       mov       [rsp+38],rdx
       mov       [rsp+40],r9d
       mov       [rsp+28],r8
       mov       [rsp+30],ecx
       lea       rdx,[rsp+38]
       lea       r9,[rsp+28]
       lea       r8,[rsp+48]
       mov       rcx,rsi
       call      qword ptr [7FF9B3E855F0]; System.Guid.TryFormatCore[[System.Char, System.Private.CoreLib]](System.Span`1<Char>, Int32 ByRef, System.ReadOnlySpan`1<Char>)
       mov       rax,rdi
       add       rsp,50
       pop       rbx
       pop       rsi
       pop       rdi
       ret
M18_L03:
       lea       r8,[rbx+0C]
       mov       ecx,[rbx+8]
       jmp       short M18_L02
M18_L04:
       mov       rcx,7FF9B3EEF460
       call      CORINFO_HELP_COUNTPROFILE32
       call      qword ptr [7FF9B3E8C228]
       int       3
M18_L05:
       cmp       eax,6E
       je        short M18_L07
       cmp       eax,70
       je        short M18_L06
       cmp       eax,78
       je        short M18_L09
       mov       rcx,7FF9B3EEF464
       call      CORINFO_HELP_COUNTPROFILE32
       jmp       short M18_L10
M18_L06:
       mov       rcx,7FF9B3EEF468
       call      CORINFO_HELP_COUNTPROFILE32
       jmp       short M18_L08
M18_L07:
       mov       rcx,7FF9B3EEF470
       call      CORINFO_HELP_COUNTPROFILE32
       mov       edi,20
       jmp       near ptr M18_L01
M18_L08:
       mov       rcx,7FF9B3EEF474
       call      CORINFO_HELP_COUNTPROFILE32
       mov       edi,26
       jmp       near ptr M18_L01
M18_L09:
       mov       rcx,7FF9B3EEF478
       call      CORINFO_HELP_COUNTPROFILE32
       mov       edi,44
       jmp       near ptr M18_L01
M18_L10:
       mov       rcx,7FF9B3EEF47C
       call      CORINFO_HELP_COUNTPROFILE32
       call      qword ptr [7FF9B3E8C228]
       int       3
; Total bytes of code 395
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedNonGCThreadStaticBase(Int32)
       push      rbx
       sub       rsp,20
       mov       ebx,ecx
       call      qword ptr [7FF96422FD78]; Precode of System.Threading.Thread.GetThreadStaticsBase()
       mov       ecx,ebx
       and       ecx,0FFFFFF
       mov       edx,ecx
       mov       r8d,ebx
       sar       r8d,18
       jne       short M19_L01
       cmp       [rax],ecx
       jle       short M19_L03
       mov       rax,[rax+8]
       cmp       [rax],al
       add       edx,0FFFFFFFE
       movsxd    rcx,edx
       mov       rax,[rax+rcx*8+10]
       test      rax,rax
       je        short M19_L03
M19_L00:
       add       rsp,20
       pop       rbx
       ret
M19_L01:
       mov       ecx,ebx
       sar       ecx,18
       cmp       ecx,2
       jne       short M19_L02
       movsxd    rcx,edx
       add       rax,rcx
       jmp       short M19_L00
M19_L02:
       cmp       [rax+4],edx
       jle       short M19_L03
       mov       rcx,[rax+10]
       movsxd    rax,edx
       mov       rcx,[rcx+rax*8]
       test      rcx,rcx
       je        short M19_L03
       mov       rax,[rcx]
       test      rax,rax
       je        short M19_L03
       jmp       short M19_L00
M19_L03:
       mov       ecx,ebx
       lea       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       add       rsp,20
       pop       rbx
       jmp       qword ptr [rax]
; Total bytes of code 130
```
```assembly
; System.Runtime.CompilerServices.StaticsHelpers.GetOptimizedGCThreadStaticBase(Int32)
       mov       rax,gs:[58]
       mov       rax,[rax+48]
       add       rax,240
       add       rax,0FFFFFFFFFFFFFFF8
       mov       edx,ecx
       and       edx,0FFFFFF
       mov       r8d,edx
       mov       r10d,ecx
       sar       r10d,18
       jne       short M20_L01
       cmp       [rax],edx
       jle       short M20_L03
       mov       rax,[rax+8]
       cmp       [rax],al
       add       r8d,0FFFFFFFE
       movsxd    rdx,r8d
       mov       rax,[rax+rdx*8+10]
       test      rax,rax
       je        short M20_L03
M20_L00:
       ret
M20_L01:
       mov       edx,ecx
       sar       edx,18
       cmp       edx,2
       jne       short M20_L02
       movsxd    rcx,r8d
       add       rax,rcx
       jmp       short M20_L00
M20_L02:
       cmp       [rax+4],r8d
       jle       short M20_L03
       mov       rax,[rax+10]
       movsxd    rdx,r8d
       mov       rax,[rax+rdx*8]
       test      rax,rax
       je        short M20_L03
       mov       rax,[rax]
       test      rax,rax
       je        short M20_L03
       jmp       short M20_L00
M20_L03:
       jmp       qword ptr [7FF9B3A0D4A0]; System.Runtime.CompilerServices.StaticsHelpers.GetGCThreadStaticsByIndexSlow(Int32)
; Total bytes of code 127
```
```assembly
; System.Threading.ThreadLocal`1[[System.__Canon, System.Private.CoreLib]].GetValueSlow()
       push      rsi
       push      rbx
       sub       rsp,28
       mov       rbx,rcx
       mov       [rsp+40],rbx
       mov       eax,[rbx+18]
       not       eax
       test      eax,eax
       jl        short M21_L03
       mov       rax,[System.Reflection.CustomAttributeExtensions.GetCustomAttribute[[System.__Canon, System.Private.CoreLib]](System.Reflection.Assembly)]
       cmp       dword ptr [rax],0
       jne       short M21_L04
M21_L00:
       call      qword ptr [7FF964244770]
       test      eax,eax
       jne       short M21_L05
M21_L01:
       mov       rbx,[rsp+40]
       cmp       qword ptr [rbx+8],0
       jne       short M21_L06
       xor       esi,esi
M21_L02:
       mov       rcx,rbx
       mov       rdx,rsi
       call      qword ptr [7FF96423C3E8]
       mov       rax,rsi
       add       rsp,28
       pop       rbx
       pop       rsi
       ret
M21_L03:
       mov       rbx,[rsp+40]
       mov       rcx,rbx
       call      qword ptr [7FF96422D4A0]
       int       3
M21_L04:
       call      qword ptr [7FF964217028]; CORINFO_HELP_POLL_GC
       jmp       short M21_L00
M21_L05:
       call      qword ptr [7FF9642360D8]
       jmp       short M21_L01
M21_L06:
       mov       rax,[rbx+8]
       mov       rcx,[rax+8]
       call      qword ptr [rax+18]
       mov       rsi,rax
       mov       rcx,rbx
       call      qword ptr [7FF96423C420]
       test      eax,eax
       je        short M21_L02
       call      qword ptr [7FF964221750]
       mov       rbx,rax
       call      qword ptr [7FF96422CF88]
       mov       rdx,rax
       mov       rcx,rbx
       call      qword ptr [7FF96422B218]
       mov       rcx,rbx
       call      qword ptr [7FF964216FA8]; CORINFO_HELP_THROW
       int       3
; Total bytes of code 176
```
```assembly
; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].GetValueSlow()
       push      rbp
       sub       rsp,40
       lea       rbp,[rsp+40]
       vxorps    xmm4,xmm4,xmm4
       vmovdqa   xmmword ptr [rbp-20],xmm4
       xor       eax,eax
       mov       [rbp-10],rax
       mov       [rbp+10],rcx
       mov       rax,[rbp+10]
       mov       eax,[rax+18]
       not       eax
       test      eax,eax
       setl      cl
       movzx     ecx,cl
       mov       rdx,[rbp+10]
       call      qword ptr [7FF9B3E85470]; System.ObjectDisposedException.ThrowIf(Boolean, System.Object)
       call      qword ptr [7FF9B3E85488]; System.Diagnostics.Debugger.NotifyOfCrossThreadDependency()
       mov       rax,[rbp+10]
       cmp       qword ptr [rax+8],0
       jne       short M22_L00
       xor       eax,eax
       mov       [rbp-4],eax
       jmp       short M22_L01
M22_L00:
       mov       rax,[rbp+10]
       mov       rax,[rax+8]
       mov       [rbp-20],rax
       mov       rax,[rbp-20]
       mov       rcx,[rax+8]
       mov       rax,[rbp-20]
       call      qword ptr [rax+18]
       mov       [rbp-4],eax
       mov       rcx,[rbp+10]
       call      qword ptr [7FF9B3E854A0]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].get_IsValueCreated()
       test      eax,eax
       je        short M22_L01
       mov       rcx,offset MT_System.InvalidOperationException
       call      CORINFO_HELP_NEWSFAST
       mov       [rbp-10],rax
       call      qword ptr [7FF9B3E854B8]
       mov       [rbp-18],rax
       mov       rdx,[rbp-18]
       mov       rcx,[rbp-10]
       call      qword ptr [7FF9B3D27A08]
       mov       rcx,[rbp-10]
       call      CORINFO_HELP_THROW
       int       3
M22_L01:
       mov       rcx,[rbp+10]
       mov       edx,[rbp-4]
       call      qword ptr [7FF9B3E854D0]; System.Threading.ThreadLocal`1[[System.Int32, System.Private.CoreLib]].set_Value(Int32)
       mov       eax,[rbp-4]
       add       rsp,40
       pop       rbp
       ret
; Total bytes of code 199
```

