using System.Collections.Generic;

namespace ScriptVerifier
{
    public interface ICompilerSetup
    {
        bool AllowUnsafeCode { get; }

        IEnumerable<string> GetReferencedAssemblyPaths();
        IEnumerable<string> GetAllowedTypeNames();
    }
}