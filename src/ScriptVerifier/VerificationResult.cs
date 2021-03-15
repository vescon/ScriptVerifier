using System.Collections.Generic;
using System.Linq;

namespace ScriptVerifier
{
    public class VerificationResult
    {
        internal void AddError(string error)
        {
            Errors.Add(error);
        }

        public List<string> Errors { get; } = new();
        public bool HasError => Errors.Any();
    }
}   