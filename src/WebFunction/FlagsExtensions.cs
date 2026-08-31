namespace WebFunction;

/// <summary>
/// Extension method for flag lists (as used by <see cref="Endpoint"/>, <see cref="Argument"/>,
/// <see cref="AttributeDef"/> and <see cref="Package"/>). Mirrors the reference Ruby gem's
/// <c>Flaggable</c> module. Kept separate from <see cref="Naming"/> so it can't collide with the
/// <c>Flags</c> property those same classes expose.
/// </summary>
public static class FlagsExtensions
{
    /// <summary>Whether this flag list contains <paramref name="flag"/>.</summary>
    public static bool HasFlag(this IReadOnlyList<string> flags, string flag) => flags.Contains(flag);
}
