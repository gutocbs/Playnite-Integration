namespace VnManager.PlayniteInstallation.Domain;

public static class JapaneseText
{
    public static bool Contains(string? text) => text is not null && text.Any(IsJapanese);

    private static bool IsJapanese(char character) =>
        character is >= '぀' and <= 'ヿ' // Hiragana and Katakana
            or >= 'ㇰ' and <= 'ㇿ' // Katakana phonetic extensions
            or >= '㐀' and <= '䶿' // CJK extension A
            or >= '一' and <= '鿿' // CJK unified ideographs
            or >= 'ｦ' and <= 'ﾟ'; // Half-width Katakana
}
