
using System;
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
    }
}
