using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ScriptVerifier
{
    public static class AssemblyExtensions
    {
        public static ISet<Assembly> ResolveDependencies(this Assembly assembly, bool includeSelf = false)
        {
            var assemblies = new HashSet<Assembly>();
            var assemblyNames = new HashSet<AssemblyName>();
            foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                ResolveDependenciesRecursive(referencedAssembly, assemblies, assemblyNames);

            if (includeSelf)
                assemblies.Add(assembly);
            return assemblies;
        }

        private static void ResolveDependenciesRecursive(
            AssemblyName assemblyName,
            ISet<Assembly> assemblies,
            ISet<AssemblyName> assemblyNames)
        {
            if (assemblyNames.Contains(assemblyName))
                return;
            assemblyNames.Add(assemblyName);

            var assembly = Assembly.Load(assemblyName);
            if (assemblies.Contains(assembly))
                return;
            assemblies.Add(assembly);

            var unhandledAssemblyNames = assembly.GetReferencedAssemblies()
                .Where(x => !assemblyNames.Contains(x));
            foreach (var unhandledAssemblyName in unhandledAssemblyNames)
                ResolveDependenciesRecursive(unhandledAssemblyName, assemblies, assemblyNames);
        }
    }
}