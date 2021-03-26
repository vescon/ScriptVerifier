using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        private List<PortableExecutableReference>? _references;

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

            var allowedTypePatterns = _setup.GetAllowedTypePatterns().ToList();

            var allowedTypes = new HashSet<string>(_setup.GetAllowedTypes());
            var internalTypes = GetInternalTypes(compilation.Assembly.GlobalNamespace);
            foreach (var internalType in internalTypes)
                allowedTypes.Add(GetFullName(internalType));

            var verificationResult = new VerificationResult();
            Verify(semanticModel, allowedTypes, allowedTypePatterns, verificationResult);

            if (!ThrowOnFirstError && verificationResult.HasError)
                ThrowVerificationException(verificationResult.Errors);

            return verificationResult;
        }

        private void Verify(
            SemanticModel semanticModel,
            ISet<string> allowedTypes,
            List<Regex> allowedTypePatterns,
            VerificationResult verificationResult)
        {
            var syntaxNodes = semanticModel.SyntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<CSharpSyntaxNode>();
            foreach (var syntaxNode in syntaxNodes)
            {
                var type = GetType(semanticModel, syntaxNode);
                if (type == null)
                    continue;

                VerifyType(type, syntaxNode, allowedTypes, allowedTypePatterns, verificationResult);
            }
        }

        private void VerifyType(
            ISymbol type,
            CSharpSyntaxNode syntaxNode,
            ISet<string> allowedTypes,
            List<Regex> allowedTypePatterns,
            VerificationResult result)
        {
            var typeName = GetFullName(type);
            if (IsTypeAllowed(typeName, allowedTypes, allowedTypePatterns))
                return;

            var message = CreateVerifyExceptionMessage(typeName, syntaxNode);
            if (ThrowOnFirstError)
                throw new ScriptVerificationException(message);

            result.AddError(message);
        }

        private static ISymbol? GetType(SemanticModel semanticModel, CSharpSyntaxNode syntaxNode)
        {
            switch (syntaxNode)
            {
                case VariableDeclarationSyntax variableDeclaration:
                    var symbolInfo = ModelExtensions.GetSymbolInfo(semanticModel, variableDeclaration.Type);
                    var symbol = symbolInfo.Symbol;
                    return GetRelevantType(symbol);

                case InvocationExpressionSyntax invocationExpression:
                    var symbolInfo1 = ModelExtensions.GetSymbolInfo(semanticModel, invocationExpression);
                    var invokedSymbol = symbolInfo1.Symbol;
                    return invokedSymbol?.ContainingType;

                default:
                    return null;
            }
        }

        private static bool IsTypeAllowed(
            string typeName,
            ISet<string> allowedTypes,
            IEnumerable<Regex> allowedTypePatterns)
        {
            return allowedTypes.Contains(typeName)
                   || allowedTypePatterns.Any(x => x.IsMatch(typeName));
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

        private static ISymbol? GetRelevantType(ISymbol? symbol)
        {
            return symbol switch
            {
                IPointerTypeSymbol pointer => pointer.PointedAtType,
                IArrayTypeSymbol arrayType => arrayType.ElementType,
                _ => symbol
            };
        }

        private static string CreateVerifyExceptionMessage(string invokeType, CSharpSyntaxNode syntaxNode)
        {
            return
                $"Not allowed type '{invokeType}' used at location '{syntaxNode.GetLocation().GetLineSpan()}''";
        }

        private static void ThrowVerificationException(IEnumerable<string> errors)
        {
            var message = string.Join(Environment.NewLine, errors);
            throw new ScriptVerificationException(message);
        }
    }
}