using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ScriptVerifier
{
    public interface ICompilerSetup
    {
        bool AllowUnsafeCode { get; }

        IEnumerable<string> GetReferencedAssemblyPaths();
        IEnumerable<string> GetAllowedTypes();
        IEnumerable<Regex> GetAllowedTypePatterns();
    }
}