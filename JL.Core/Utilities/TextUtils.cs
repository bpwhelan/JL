using System.Text;
using System.Text.RegularExpressions;
using JL.Core.Config;

namespace JL.Core.Utilities;

public static class TextUtils
{
    private const char HighSurrogateStart = '\uD800';
    private const char Noncharacter = '\uFFFE';

    // See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Globalization/Normalization.Icu.cs
    // Modified from private static bool HasInvalidUnicodeSequence(ReadOnlySpan<char> s)
    private static int FirstInvalidUnicodeSequenceIndex(ReadOnlySpan<char> text)
    {
        for (int i = text.IndexOfAnyInRange(HighSurrogateStart, Noncharacter); (uint)i < (uint)text.Length; i++)
        {
            ref readonly char c = ref text[i];

            if (c < HighSurrogateStart)
            {
                continue;
            }

            if (c is Noncharacter || char.IsLowSurrogate(c))
            {
                return i;
            }

            if (char.IsHighSurrogate(c))
            {
                if ((uint)(i + 1) >= (uint)text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return i;
                }

                ++i;
            }
        }

        return -1;
    }

    private static string RemoveInvalidUnicodeSequences(string text, int index)
    {
        StringBuilder sb = new(text[..index], text.Length - 1);

        for (int i = index + 1; i < text.Length; i++)
        {
            char c = text[i];

            if (c < HighSurrogateStart)
            {
                _ = sb.Append(c);
            }

            else if (c is not Noncharacter && !char.IsLowSurrogate(c))
            {
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        _ = sb.Append(c).Append(text[i + 1]);
                        ++i;
                    }
                }

                else
                {
                    _ = sb.Append(c);
                }
            }
        }

        return sb.ToString();
    }

    public static string SanitizeText(string text)
    {
        int firstInvalidUnicodeCharIndex = FirstInvalidUnicodeSequenceIndex(text);
        if (firstInvalidUnicodeCharIndex >= 0)
        {
            text = RemoveInvalidUnicodeSequences(text, firstInvalidUnicodeCharIndex);
        }

        CoreConfigManager coreConfigManager = CoreConfigManager.Instance;
        if (coreConfigManager.TextBoxTrimWhiteSpaceCharacters)
        {
            text = text.Trim();
        }

        if (coreConfigManager.TextBoxRemoveNewlines)
        {
            text = text.ReplaceLineEndings("");
        }

        List<KeyValuePair<Regex, string>>? regexReplacements = RegexReplacerUtils.s_regexReplacements;
        if (regexReplacements is not null)
        {
            foreach (ref readonly KeyValuePair<Regex, string> regexReplacementKeyValuePair in regexReplacements.AsReadOnlySpan())
            {
                text = regexReplacementKeyValuePair.Key.Replace(text, regexReplacementKeyValuePair.Value);
            }
        }

        return text;
    }

    public static bool StartsWithWhiteSpace(string text)
    {
        char firstChar = text[0];
        return !char.IsHighSurrogate(firstChar)
            ? char.IsWhiteSpace(firstChar)
            : Rune.IsWhiteSpace(new Rune(firstChar, text[1]));
    }

    public static string GetFirstCharacter(string text)
    {
        char firstChar = text[0];
        return !char.IsHighSurrogate(firstChar)
            ? firstChar.ToString()
            : char.ConvertFromUtf32(char.ConvertToUtf32(firstChar, text[1]));
    }
}
