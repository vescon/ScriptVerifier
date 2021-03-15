using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ScriptVerifier
{
    /// <summary>
    /// https://joshvarty.wordpress.com/2014/10/30/learn-roslyn-now-part-7-introducing-the-semantic-model/
    /// https://joshvarty.wordpress.com/2015/02/05/learn-roslyn-now-part-8-data-flow-analysis/
    /// https://joshvarty.wordpress.com/2015/03/24/learn-roslyn-now-control-flow-analysis/
    /// </summary>
    public class Verifier
    {
        private readonly ICompilerSetup _setup;

        public Verifier(ICompilerSetup setup)
        {
            _setup = setup;
        }

        public VerificationResult Verify(string script, bool throwOnFirstError = true)
        {
            var assemblyReferences = _setup.GetReferencedAssemblyPaths();
            var references = assemblyReferences
                .Select(x => MetadataReference.CreateFromFile(x))
                .ToList();

            var syntaxTree = CSharpSyntaxTree.ParseText(script);
            var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: _setup.AllowUnsafeCode);
            var compilation = CSharpCompilation.Create(
                "ScriptVerification",
                new[] {syntaxTree},
                references,
                options);

            var semanticModel = compilation.GetSemanticModel(syntaxTree, false);
            var allDiagnostics = semanticModel.GetDiagnostics();
            var errors = allDiagnostics
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .ToList();
            if (errors.Any())
            {
                var message = string.Join(Environment.NewLine, allDiagnostics);
                throw new ScriptVerificationException(message);
            }

            var allowedTypesNames = new HashSet<string>(_setup.GetAllowedTypeNames());

            var internalTypes = GetInternalTypes(compilation.Assembly.GlobalNamespace);
            foreach (var internalType in internalTypes)
                allowedTypesNames.Add(GetFullName(internalType));

            var result = new VerificationResult();
            VerifyDeclarations(semanticModel, allowedTypesNames, result, throwOnFirstError);
            VerifyMethodCalls(semanticModel, allowedTypesNames, result, throwOnFirstError);
            return result;
        }

        public void Compile(string script, bool throwOnFirstError = true)
        {
            var returnTypeAsString = GetCSharpRepresentation(typeof(T), true);
            var outerClass = StandardHeader + $"public static class Wrapper {{ public static {returnTypeAsString} expr = {lambda}; }}";

            var compilation = CSharpCompilation.Create("FilterCompiler_" + Guid.NewGuid(),
                new[] { CSharpSyntaxTree.ParseText(outerClass) },
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var assemblyLoadContext = new CollectibleAssemblyLoadContext();
            using var ms = new MemoryStream();

            var cr = compilation.Emit(ms);
            if (!cr.Success)
            {
                throw new InvalidOperationException("Error in expression: " + cr.Diagnostics.First(e =>
                    e.Severity == DiagnosticSeverity.Error).GetMessage());
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = assemblyLoadContext.LoadFromStream(ms);

            var outerClassType = assembly.GetType("Wrapper");

            var exprField = outerClassType.GetField("expr", BindingFlags.Public | BindingFlags.Static);
            // ReSharper disable once PossibleNullReferenceException
            return (T)exprField.GetValue(null);
        }

        private static void VerifyDeclarations(
            SemanticModel model,
            ISet<string> allowedTypeNames,
            VerificationResult verificationResult,
            bool throwOnFirstError)
        {
            var variableDeclarations = model.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<VariableDeclarationSyntax>();
            foreach (var variableDeclaration in variableDeclarations)
            {
                var symbolInfo = ModelExtensions.GetSymbolInfo(model, variableDeclaration.Type);
                var symbol = symbolInfo.Symbol;
                var relevantType = GetRelevantType(symbol);                
                VerifyType(relevantType,
                    variableDeclaration,
                    allowedTypeNames,
                    verificationResult,
                    throwOnFirstError);
            }
        }

        private static ISymbol GetRelevantType(ISymbol symbol)
        {
            switch (symbol)
            {
                case IPointerTypeSymbol pointer:
                {
                    return pointer.PointedAtType;
                }
                case IArrayTypeSymbol arrayType:
                {
                    return arrayType.ElementType;
                }

                case ISymbol variableType:
                {
                    return variableType;
                }

                default:
                    return null;
            }
        }

        private static void VerifyMethodCalls(
            SemanticModel model,
            ISet<string> allowedTypeNames,
            VerificationResult verificationResult,
            bool throwOnFirstError)
        {
            var invocationExpressions = model.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();
            foreach (var invocationExpression in invocationExpressions)
            {
                var symbolInfo = ModelExtensions.GetSymbolInfo(model, invocationExpression);
                var invokedSymbol = symbolInfo.Symbol;
                var invokedType = invokedSymbol?.ContainingType;
                VerifyType(
                    invokedType,
                    invocationExpression,
                    allowedTypeNames,
                    verificationResult,
                    throwOnFirstError);
            }
        }

        private static void VerifyType(
            ISymbol type,
            CSharpSyntaxNode variableDeclaration,
            ISet<string> allowedTypeNames,
            VerificationResult result,
            bool throwOnFirstError)
        {
            var variableTypeName = "Unknown";
            if (type != null)
            {
                variableTypeName = GetFullName(type);
                if (IsAllowed(variableTypeName, allowedTypeNames))
                    return;
            }

            var message = CreateVerifyExceptionMessage(variableTypeName, variableDeclaration);
            AddToResult(message, result, throwOnFirstError);
        }

        private static bool IsAllowed(
            string typeName,
            ISet<string> allowedNamesOrNamespaces)
        {
            if (allowedNamesOrNamespaces.Contains(typeName))
                return true;

            return allowedNamesOrNamespaces
                .Where(x => x.EndsWith("*"))
                .Any(x => typeName.StartsWith(x.TrimEnd('*')));
        }

        private static IEnumerable<INamedTypeSymbol> GetInternalTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var member in namespaceSymbol.GetTypeMembers())
            {
                yield return member;

                // allow nested types
                foreach (var typeMember in member.GetTypeMembers())
                    yield return typeMember;
            }

            foreach (var namespaceMember in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var member in GetInternalTypes(namespaceMember))
                    yield return member;
            }
        }

        private static string GetFullName(ISymbol symbol)
        {
            var parts = symbol.ContainingType?.ToDisplayParts() ?? symbol.ContainingNamespace.ToDisplayParts();

            var sb = new StringBuilder();
            foreach (var part in parts)
                sb.Append(part);

            sb.Append(".");
            sb.Append(symbol.MetadataName);
            return sb.ToString();
        }

        private static void AddToResult(string message, VerificationResult result, bool throwOnFirstError)
        {
            if (throwOnFirstError)
                throw new ScriptVerificationException(message);

            result.AddError(message);
        }

        private static string CreateVerifyExceptionMessage(string invokeType, CSharpSyntaxNode invocationSyntax)
        {
            return
                $"Not allowed type '{invokeType}' used at location '{invocationSyntax.GetLocation().GetLineSpan()}''";
        }

        private class CollectibleAssemblyLoadContext : AssemblyLoadContext, IDisposable
        {
            public CollectibleAssemblyLoadContext() : base(true)
            { }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                return null;
            }

            public void Dispose()
            {
                Unload();
            }
        }
    }
}