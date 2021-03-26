using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ScriptVerifier
{
    public abstract class CompilerSetup : ICompilerSetup
    {
        public bool AllowUnsafeCode { get; set; }

        public ISet<string> ReferencedAssemblyPaths { get; } = new HashSet<string>();
        public ISet<string> AllowedTypeNames { get; } = new HashSet<string>();

        public void AddReferencedAssembly(string assemblyPath)
        {
            ReferencedAssemblyPaths.Add(assemblyPath);
        }

        public void AddReferencedAssemblies(List<Assembly> assemblies)
        {
            foreach (var assembly in GetValidAssemblyPaths(assemblies))
                AddReferencedAssembly(assembly);
        }

        public void AddAllowedType(string typeName)
        {
            AllowedTypeNames.Add(typeName);
        }

        public void AddAllowedTypes(List<Type> types, bool addDependentAssemblies)
        {
            var assembliesToResolve = new HashSet<Assembly>();
            foreach (var type in types)
            {
                var fullTypeName = GetFullTypeName(type);
                if (fullTypeName == null)
                    continue;

                AddAllowedType(fullTypeName);

                if (addDependentAssemblies)
                    assembliesToResolve.Add(type.Assembly);
            }

            if (assembliesToResolve.Any())
            {
                var dependentAssemblies = assembliesToResolve
                    .SelectMany(x => x.ResolveDependencies(true))
                    .ToList();
                AddReferencedAssemblies(dependentAssemblies);
            }
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

        public IEnumerable<string> GetAllowedTypeNames()
        {
            return AllowedTypeNames;
        }
    }
}