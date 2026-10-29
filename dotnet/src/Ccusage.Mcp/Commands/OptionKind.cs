using System.Diagnostics.CodeAnalysis;

namespace Ccusage.Mcp.Commands;

/// <summary>How an engine option's value is typed, validated and rendered into argv.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Members name JSON-schema value types (String, Int); the names are the plan's interface contract.")]
public enum OptionKind
{
    /// <summary>A presence flag (<c>--breakdown</c>).</summary>
    Bool,

    /// <summary>A flag with a <c>--no-&lt;name&gt;</c> negation (<c>--offline</c> / <c>--no-offline</c>).</summary>
    NegatableBool,

    /// <summary>Free text.</summary>
    String,

    /// <summary>A date (YYYY-MM-DD or YYYYMMDD).</summary>
    Date,

    /// <summary>An integer.</summary>
    Int,

    /// <summary>A decimal number.</summary>
    Number,

    /// <summary>An integer or the literal <c>max</c>.</summary>
    IntOrMax,

    /// <summary>One of a fixed set of values.</summary>
    Enum,

    /// <summary>A comma-separated list drawn from a fixed set of values.</summary>
    CsvEnum,

    /// <summary>A filesystem path.</summary>
    Path,

    /// <summary>One or more comma-separated filesystem paths.</summary>
    PathList,
}
