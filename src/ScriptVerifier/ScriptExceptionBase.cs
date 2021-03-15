using System;

namespace ScriptVerifier
{
    public abstract class ScriptExceptionBase : Exception
    {
        protected ScriptExceptionBase(string message)
            : base(message)
        {
        }

        protected ScriptExceptionBase(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}