[![.github/workflows/main.yaml](https://github.com/vescon/ScriptVerifier/actions/workflows/main.yaml/badge.svg?branch=main)](https://github.com/vescon/ScriptVerifier/actions/workflows/main.yaml)
[![NuGet](https://img.shields.io/nuget/v/ScriptVerifier.svg)](https://www.nuget.org/packages/ScriptVerifier/)

# ScriptVerifier
Verifies that a C# code uses only permitted types and assemblies


## Supported features
- Specify allowed types as type name strings
- Specify allowed type names as regex pattern
- Specify referenced assemblies via file paths

## Quickstart

1. Install the ScriptVerifier NuGet package (`Install-Package ScriptVerifier`)
2. Configure a the ICompilerSetup.
3. Verify C# code with "Verifier.Verify()"

## Sample

[Program.cs](src/ScriptVerifier.Sample/Program.cs)

```csharp
using System;

namespace ScriptVerifier.Sample
{
    public class Program
    {
        public static void Main()
        {
            TestMaliciousScript();
            TestScriptWithPermittedTypes();
        }

        private static void TestMaliciousScript()
        {
            var script = @"
using System;

var i = 42;
i = 42 + 42;

var fileType = Type.GetType(""System.IO.File, System.IO.FileSystem""); // use reflection to execute malicious code
var deleteMethod = fileType!.GetMethod(""Delete"");
deleteMethod!.Invoke(null, new object[] {@""d:\passwd.txt""});
";

            RunVerification(script, new DefaultCompilerSetup());

            // Not allowed type 'System.Type' used at location ': (6,0)-(6,67)''
        }

        private static void TestScriptWithPermittedTypes()
        {
            var script = @"
using System;

var i = 42;
i = 42 + 42;

Console.WriteLine(""Result was: "" + i);
";

            var compilerSetup = new DefaultCompilerSetup();
            compilerSetup.AddAllowedType(typeof(Console));
            
            RunVerification(script, compilerSetup);

            // OK
        }

        private static void RunVerification(string script, ICompilerSetup compilerSetup)
        {
            try
            {
                var verifier = new Verifier(compilerSetup);
                var result = verifier.Verify(script);
                Console.WriteLine("Script verification successful, only allowed types were used!");
            }
            catch (ScriptVerificationException ex)
            {
                Console.WriteLine("Script verification got the following error:");
                Console.WriteLine(ex.Message);
            }
        }
    }
}


```
