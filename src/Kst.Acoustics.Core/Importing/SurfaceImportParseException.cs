namespace Kst.Acoustics.Core.Importing;

public sealed class SurfaceImportParseException : Exception
{
    public SurfaceImportParseException(string message, int lineNumber)
        : base($"Satır {lineNumber}: {message}")
    {
        LineNumber = lineNumber;
    }

    public int LineNumber { get; }
}
