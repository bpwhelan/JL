using JL.Core.Dicts;

namespace JL.Core.Lookup;

public sealed class LookupResult
{
    // common (required for sorting)
    public string MatchedText { get; }
    public string DeconjugatedMatchedText { get; init; }
    public List<LookupFrequencyResult>? Frequencies { get; init; }
    public Dict Dict { get; }
    public string PrimarySpelling { get; init; }

    public IList<string>? Readings { get; init; }
    public string? FormattedDefinitions { get; init; }
    public int EdictId { get; init; }
    public IReadOnlyList<string>? AlternativeSpellings { get; init; }
    public string? Process { get; init; }
    public List<string>? PrimarySpellingOrthographyInfoList { get; init; }
    public List<string>? ReadingsOrthographyInfoList { get; }
    public List<string>? AlternativeSpellingsOrthographyInfoList { get; }

    // Kanji
    public IReadOnlyList<string>? OnReadings { get; init; }
    public IReadOnlyList<string>? KunReadings { get; init; }
    public List<string>? NanoriReadings { get; init; }
    public int StrokeCount { get; init; }
    public string? KanjiComposition { get; init; }
    public int KanjiGrade { get; init; }
    public string? KanjiStats { get; init; }

    internal LookupResult(
        string primarySpelling,
        string matchedText,
        string deconjugatedMatchedText,
        Dict dict,
        IList<string>? readings,
        List<LookupFrequencyResult>? frequencies = null,
        IReadOnlyList<string>? alternativeSpellings = null,
        List<string>? primarySpellingOrthographyInfoList = null,
        List<string>? readingsOrthographyInfoList = null,
        List<string>? alternativeSpellingsOrthographyInfoList = null,
        IReadOnlyList<string>? onReadings = null,
        IReadOnlyList<string>? kunReadings = null,
        List<string>? nanoriReadings = null,
        string? formattedDefinitions = null,
        string? process = null,
        string? kanjiComposition = null,
        string? kanjiStats = null,
        int edictId = 0,
        int strokeCount = 0,
        int kanjiGrade = -1
    )
    {
        MatchedText = matchedText;
        DeconjugatedMatchedText = deconjugatedMatchedText;
        Frequencies = frequencies;
        Dict = dict;
        PrimarySpelling = primarySpelling;
        Readings = readings;
        FormattedDefinitions = formattedDefinitions;
        EdictId = edictId;
        AlternativeSpellings = alternativeSpellings;
        Process = process;
        PrimarySpellingOrthographyInfoList = primarySpellingOrthographyInfoList;
        ReadingsOrthographyInfoList = readingsOrthographyInfoList;
        AlternativeSpellingsOrthographyInfoList = alternativeSpellingsOrthographyInfoList;
        OnReadings = onReadings;
        KunReadings = kunReadings;
        NanoriReadings = nanoriReadings;
        StrokeCount = strokeCount;
        KanjiComposition = kanjiComposition;
        KanjiStats = kanjiStats;
        KanjiGrade = kanjiGrade;
    }
}
