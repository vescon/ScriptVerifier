using System;
using System.Collections.Generic;
using System.Linq;
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

        private List<PortableExecutableReference> _references;

        public Verifier(ICompilerSetup setup)
        {
            _setup = setup;
        }

        public bool ThrowOnFirstError { get; set; } = true;

        public VerificationResult Verify(string script)
        {
            if (_references == null)
            {
                var assemblyReferences = _setup.GetReferencedAssemblyPaths();
                _references = assemblyReferences
                    .Select(x => MetadataReference.CreateFromFile(x))
                    .ToList();
            }

            var syntaxTree = CSharpSyntaxTree.ParseText(script);

            var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: _setup.AllowUnsafeCode);
            var compilation = CSharpCompilation.Create(
                "ScriptVerification_" + Guid.NewGuid(),
                new[] {syntaxTree},
                _references,
                options);

            var semanticModel = compilation.GetSemanticModel(syntaxTree, false);
            var allDiagnostics = semanticModel.GetDiagnostics();
            var errors = allDiagnostics
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .ToList();
            if (errors.Any())
            {
                var errorMessages = errors.Select(x => x.ToString());
                ThrowVerificationException(errorMessages);
            }

            var allowedTypesNames = new HashSet<string>(_setup.GetAllowedTypeNames());
            var internalTypes = GetInternalTypes(compilation.Assembly.GlobalNamespace);
            foreach (var internalType in internalTypes)
                allowedTypesNames.Add(GetFullName(internalType));

            var verificationResult = new VerificationResult();
            VerifyDeclarations(semanticModel, allowedTypesNames, verificationResult);
            VerifyMethodCalls(semanticModel, allowedTypesNames, verificationResult);

            if (!ThrowOnFirstError && verificationResult.HasError)
                ThrowVerificationException(verificationResult.Errors);

            return verificationResult;
        }

        private void VerifyDeclarations(
            SemanticModel model,
            ISet<string> allowedTypeNames,
            VerificationResult verificationResult)
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
                    verificationResult);
            }
        }

        private void VerifyMethodCalls(
            SemanticModel model,
            ISet<string> allowedTypeNames,
            VerificationResult verificationResult)
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
                    verificationResult);
            }
        }

        private void VerifyType(
            ISymbol type,
            CSharpSyntaxNode variableDeclaration,
            ISet<string> allowedTypeNames,
            VerificationResult result)
        {
            var variableTypeName = "Unknown";
            if (type != null)
            {
                variableTypeName = GetFullName(type);
                if (IsTypeAllowed(variableTypeName, allowedTypeNames))
                    return;
            }

            var message = CreateVerifyExceptionMessage(variableTypeName, variableDeclaration);
            if (ThrowOnFirstError)
                throw new ScriptVerificationException(message);

            result.AddError(message);
        }

        private static bool IsTypeAllowed(
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
            return $"{string.Join("", parts)}.{symbol.MetadataName}";
        }

        private static ISymbol GetRelevantType(ISymbol symbol)
        {
            return symbol switch
            {
                IPointerTypeSymbol pointer => pointer.PointedAtType,
                IArrayTypeSymbol arrayType => arrayType.ElementType,
                _ => symbol
            };
        }

        private static string CreateVerifyExceptionMessage(string invokeType, CSharpSyntaxNode invocationSyntax)
        {
            return
                $"Not allowed type '{invokeType}' used at location '{invocationSyntax.GetLocation().GetLineSpan()}''";
        }

        private static void ThrowVerificationException(IEnumerable<string> errors)
        {
            var message = string.Join(Environment.NewLine, errors);
            throw new ScriptVerificationException(message);
        }
    }
}