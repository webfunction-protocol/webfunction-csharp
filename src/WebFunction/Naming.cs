namespace WebFunction;

/// <summary>Endpoint-name normalization helpers.</summary>
public static class Naming
{
    /// <summary>
    /// Converts an idiomatic C# identifier (as arrives from dynamic-dispatch member access, e.g.
    /// <c>ListItems</c>) into the hyphenated endpoint name used on the wire (<c>list-items</c>).
    /// Also normalizes already-hyphenated or underscored names.
    /// </summary>
    public static string Dashify(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                sb.Append('-');
                continue;
            }

            if (char.IsUpper(c) && i > 0 && name[i - 1] != '-' && name[i - 1] != '_')
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
