using System.Collections.Generic;

namespace ScriptVerifier
{
    public class VerificationResult
    {
        internal void AddError(string error)
        {
            Errors.Add(error);
        }

        public List<string> Errors { get; } = new List<string>();
    }
}   