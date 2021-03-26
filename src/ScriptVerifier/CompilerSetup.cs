using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ScriptVerifier
{
    public abstract class CompilerSetup : ICompilerSetup
    {
        public bool AllowUnsafeCode { get; set; }

        public ISet<string> ReferencedAssemblyPaths { get; } = new HashSet<string>();
        public ISet<string> AllowedTypes { get; } = new HashSet<string>();
        public List<Regex> AllowedTypePatterns { get; } = new();

        public void AddReferencedAssembly(string assemblyPath)
        {
            ReferencedAssemblyPaths.Add(assemblyPath);
        }

        public void AddReferencedAssemblies(List<Assembly> assemblies)
        {
            foreach (var assembly in GetValidAssemblyPaths(assemblies))
                AddReferencedAssembly(assembly);
        }

        public void AddAllowedTypePattern(Regex regex)
        {
            AllowedTypePatterns.Add(regex);
        }
        
        public void AddAllowedTypePattern(string fullTypeNameRegex)
        {
            var regEx = new Regex(fullTypeNameRegex, RegexOptions.Singleline);
            AddAllowedTypePattern(regEx);
        }

        public void AddAllowedType(string fullTypeName)
        {
            AllowedTypes.Add(fullTypeName);
        }

        public void AddAllowedType(Type type, bool resolveAndAddDependentAssemblies = true)
        {
            AddAllowedTypes(new List<Type> { type }, resolveAndAddDependentAssemblies);
        }

        public void AddAllowedTypes(List<Type> types, bool resolveAndAddDependentAssemblies = true)
        {
            var assembliesToResolve = new HashSet<Assembly>();
            foreach (var type in types)
            {
                var fullTypeName = GetFullTypeName(type);
                if (fullTypeName == null)
                    continue;

                AddAllowedType(fullTypeName);

                if (resolveAndAddDependentAssemblies)
                    assembliesToResolve.Add(type.Assembly);
            }

            if (!assembliesToResolve.Any()) 
                return;

            var dependentAssemblies = assembliesToResolve
                .SelectMany(x => x.ResolveDependencies(true))
                .ToList();
            AddReferencedAssemblies(dependentAssemblies);
        }

        private static IEnumerable<string> GetValidAssemblyPaths(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                .Where(x => !x.IsDynamic)
                .Select(x => x.Location)
                .Where(x => !string.IsNullOrEmpty(x));
        }

        private static string? GetFullTypeName(Type type)
        {
            var name = type.FullName;
            return string.IsNullOrEmpty(name)
                ? null
                : name.Replace("+", "."); // support nested classes
        }


        public IEnumerable<string> GetReferencedAssemblyPaths()
        {
            return ReferencedAssemblyPaths;
        }

        public IEnumerable<string> GetAllowedTypes()
        {
            return AllowedTypes;
        }

        public IEnumerable<Regex> GetAllowedTypePatterns()
        {
            return AllowedTypePatterns;
        }
    }
}