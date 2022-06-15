
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OneOf;

namespace Neo.BlockchainToolkit
{
    // Note, string is not a valid arg type. Strings need to be converted into byte arrays 
    // by later processing steps. However, the specific conversion of string -> byte array
    // is string content dependent

    public readonly record struct Diagnostic(Diagnostic.SeverityLevel Severity, string Message)
    {
        public enum SeverityLevel { Info, Warning, Error }
        public static Diagnostic Error(string message)
            => new Diagnostic(SeverityLevel.Error, message); 
        public static Diagnostic Info(string message)
            => new Diagnostic(SeverityLevel.Info, message); 
        public static Diagnostic Warning(string message)
            => new Diagnostic(SeverityLevel.Warning, message); 

        public static bool Success(IEnumerable<Diagnostic> diagnostics)
            => !diagnostics.Any(d => d.Severity == SeverityLevel.Error);
    }

    public class DiagnosticException : Exception
    {
        public readonly Diagnostic.SeverityLevel Severity;

        public DiagnosticException(Diagnostic diagnostic) : base(diagnostic.Message)
        {
            this.Severity = diagnostic.Severity;
        }
    }
}
