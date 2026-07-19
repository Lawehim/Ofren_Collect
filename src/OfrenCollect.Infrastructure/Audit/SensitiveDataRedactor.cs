using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OfrenCollect.Infrastructure.Audit;

/// <summary>
/// Redacts sensitive values before an audit entry is persisted (FR-8.2, NFR-1.6): secrets,
/// tokens, and passwords are replaced wholesale; long digit runs (account numbers/PANs) are
/// masked to their last four. Visibility must never become a secret leak.
/// </summary>
public static partial class SensitiveDataRedactor
{
    private const int MaxLength = 2048;
    private const string Redacted = "***REDACTED***";

    private static readonly string[] SensitiveKeyFragments =
        ["password", "token", "secret", "apikey", "signingkey", "authorization", "pin"];

    /// <summary>Returns a redacted, length-bounded copy of a request/response body.</summary>
    public static string? Redact(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        string result;
        try
        {
            var node = JsonNode.Parse(body);
            if (node is null)
            {
                result = MaskAccountNumbers(body);
            }
            else
            {
                RedactNode(node);
                result = node.ToJsonString();
            }
        }
        catch (JsonException)
        {
            result = MaskAccountNumbers(body);
        }

        return Truncate(result);
    }

    private static void RedactNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (IsSensitiveKey(property.Key))
                    {
                        obj[property.Key] = Redacted;
                    }
                    else if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        obj[property.Key] = MaskAccountNumbers(text);
                    }
                    else if (property.Value is not null)
                    {
                        RedactNode(property.Value);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array.Where(i => i is not null))
                {
                    RedactNode(item!);
                }

                break;
        }
    }

    private static bool IsSensitiveKey(string key) =>
        SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string MaskAccountNumbers(string text) =>
        DigitRunRegex().Replace(text, match =>
        {
            var digits = match.Value;
            return string.Concat(new string('*', digits.Length - 4), digits[^4..]);
        });

    private static string Truncate(string text) =>
        text.Length <= MaxLength ? text : string.Concat(text.AsSpan(0, MaxLength), "…[truncated]");

    [GeneratedRegex(@"\d{10,}")]
    private static partial Regex DigitRunRegex();
}
