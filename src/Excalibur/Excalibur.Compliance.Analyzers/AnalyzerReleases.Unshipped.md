; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Analyzer%20Diagnostics.md

### New Rules

Rule ID  | Category                        | Severity | Notes
---------|---------------------------------|----------|-------
EXCMP001 | Excalibur.Compliance.Encryption | Warning  | An encryption annotation is on a property whose type cannot carry ciphertext
EXCMP002 | Excalibur.Compliance.Encryption | Warning  | An encryption annotation is on a property without both a getter and a setter
EXCMP003 | Excalibur.Compliance.Encryption | Warning  | An encryption annotation is on a property the framework never inspects
EXCMP004 | Excalibur.Compliance.Encryption | Warning  | A property is both erasable personal data and encrypted under a surviving purpose
