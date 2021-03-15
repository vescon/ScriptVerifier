using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScriptVerifier.UnitTests
{
    [TestClass]
    public class VerifierTests
    {
        [TestMethod]
        public void GivenEmptyScript_WhenNothingIsAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = string.Empty;

            // Act
            var verifier = new Verifier(new EmptyCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        [TestMethod]
        public void GivenAddScript_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
var i = 42;
i = 42 + 42;
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        [TestMethod]
        public void GivenScriptWithNullable_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
int? i = 42;
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        [TestMethod]
        public void GivenScriptWithIntList_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
using System.Collections.Generic;

var list = new List<int> {1, 2, 3,};
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        [TestMethod]
        public void GivenMaliciousReflectionScript_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldFail()
        {
            // Arrange
            var script = @"
var fileClass = Type.GetType(""System.IO.File, System.IO.FileSystem, Version=5.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"");
var method = fileClass.GetMethod(""Exists"");
var exist = (bool)method.Invoke(null, new object[] {@""d:\passwd.txt""});
// no do something malicious ...
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().Throw<ScriptVerificationException>()
                .WithMessage("(2,17): error CS0103: Der Name \"Type\" ist im aktuellen Kontext nicht vorhanden.");
        }

        [TestMethod]
        public void GivenHelloWorldScript_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldFail()
        {
            // Arrange
            var script = @"
namespace HelloWorld
{
    class Hello {         
        static void Main(string[] args)
        {
            System.Console.WriteLine(""Hello World!"");
        }
    }
}
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().Throw<ScriptVerificationException>().WithMessage(
                "(7,13): error CS0234: Der Typ- oder Namespacename \"Console\" ist im Namespace \"System\" nicht vorhanden. (Möglicherweise fehlt ein Assemblyverweis.)");
        }

        [TestMethod]
        public void GivenHelloWorldScript_WhenOnlyConsoleTypeIsAllowed_ThenTheVerificationShouldFail()
        {
            // Arrange
            var script = @"
namespace HelloWorld
{
    class Hello {         
        static void Main(string[] args)
        {
            System.Console.WriteLine(""Hello World!"");
        }
    }
}
";

            // Act
            var compilerSetup = new DefaultCompilerSetup();
            compilerSetup.AddAllowedTypes(new List<Type> {typeof(Console)}, false);

            var verifier = new Verifier(compilerSetup);
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().Throw<ScriptVerificationException>().WithMessage(
                "(7,13): error CS0234: Der Typ- oder Namespacename \"Console\" ist im Namespace \"System\" nicht vorhanden. (Möglicherweise fehlt ein Assemblyverweis.)");
        }

        [TestMethod]
        public void GivenHelloWorldScript_WhenConsoleTypeAndAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
namespace HelloWorld
{
    class Hello {         
        static void Main(string[] args)
        {
            System.Console.WriteLine(""Hello World!"");
        }
    }
}
";

            // Act
            var compilerSetup = new DefaultCompilerSetup();
            compilerSetup.AddAllowedTypes(new List<Type> {typeof(Console)}, true);

            var verifier = new Verifier(compilerSetup);
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        [TestMethod]
        public void GivenScriptWithIntArray_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
var a = new int[1];
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        [TestMethod]
        public void GivenScriptWithTypeArray_WhenOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
var a = new Type[1];
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().Throw<ScriptVerificationException>().WithMessage(
                "(2,13): error CS0246: Der Typ- oder Namespacename \"Type\" wurde nicht gefunden (möglicherweise fehlt eine using-Direktive oder ein Assemblyverweis).");
        }

        [TestMethod]
        public void
            GivenUnsafeScript_WhenUnsafeCodeIsNotAllowedAndOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldFail()
        {
            // Arrange
            var script = @"
int a = 42;
unsafe
{
    int *p = &a;
    p = p + 1;
}
";

            // Act
            var verifier = new Verifier(new DefaultCompilerSetup());
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().Throw<ScriptVerificationException>().WithMessage(
                "(3,1): error CS0227: Unsicherer Code wird nur angezeigt, wenn mit /unsafe kompiliert wird.");
        }

        [TestMethod]
        public void
            GivenUnsafeScript_WhenUnsafeCodeIsAllowedAndOnlyDefaultAssembliesAreAllowed_ThenTheVerificationShouldBeOk()
        {
            // Arrange
            var script = @"
int a = 42;
unsafe
{
    int *p = &a;
    p = p + 1;
}
";

            // Act
            var compilerSetup = new DefaultCompilerSetup {AllowUnsafeCode = true};
            var verifier = new Verifier(compilerSetup);
            Action call = () => verifier.Verify(script);

            // Assert
            call.Should().NotThrow();
        }

        private class EmptyCompilerSetup : CompilerSetup
        {
        }
    }
}