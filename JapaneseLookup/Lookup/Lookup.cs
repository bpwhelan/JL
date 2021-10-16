﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JapaneseLookup.CustomDict;
using JapaneseLookup.Deconjugation;
using JapaneseLookup.Dicts;
using JapaneseLookup.EDICT;
using JapaneseLookup.EDICT.JMdict;
using JapaneseLookup.EDICT.JMnedict;
using JapaneseLookup.EPWING;
using JapaneseLookup.KANJIDIC;

namespace JapaneseLookup.Lookup
{
    public static class Lookup
    {
        private static DateTime _lastLookupTime;

        public static List<Dictionary<LookupResult, List<string>>> LookupText(string text)
        {
            var preciseTimeNow = new DateTime(Stopwatch.GetTimestamp());
            if ((preciseTimeNow - _lastLookupTime).Milliseconds < ConfigManager.LookupRate) return null;
            _lastLookupTime = preciseTimeNow;

            var wordResults = new Dictionary<string, AsdfResult>();
            var nameResults = new Dictionary<string, AsdfResult>();
            var epwingWordResultsList = new List<Dictionary<string, AsdfResult>>();
            var kanjiResult = new Dictionary<string, AsdfResult>();
            var customWordResults = new Dictionary<string, AsdfResult>();
            var customNameResults = new Dictionary<string, AsdfResult>();

            if (ConfigManager.KanjiMode)
                if (ConfigManager.Dicts[DictType.Kanjidic]?.Contents.Any() ?? false)
                {
                    return KanjiResultBuilder(GetKanjidicResults(text, DictType.Kanjidic));
                }

            List<string> textInHiraganaList = new();
            List<HashSet<Form>> deconjugationResultsList = new();

            for (int i = 0; i < text.Length; i++)
            {
                var textInHiragana = Kana.KatakanaToHiraganaConverter(text[..^i]);
                textInHiraganaList.Add(textInHiragana);
                deconjugationResultsList.Add(Deconjugator.Deconjugate(textInHiragana));
            }

            foreach ((DictType dictType, Dict dict) in ConfigManager.Dicts)
            {
                switch (dictType)
                {
                    case DictType.JMdict:
                        wordResults = GetJMdictResults(text, textInHiraganaList, deconjugationResultsList, dictType);
                        break;
                    case DictType.JMnedict:
                        nameResults = GetJMnedictResults(text, textInHiraganaList, dictType);
                        break;
                    case DictType.Kanjidic:
                        // handled above and below
                        break;
                    case DictType.UnknownEpwing:
                        epwingWordResultsList.Add(GetEpwingResults(text, textInHiraganaList, deconjugationResultsList,
                            dict.Contents, dictType));
                        break;
                    case DictType.Daijirin:
                        epwingWordResultsList.Add(GetDaijirinResults(text, textInHiraganaList, deconjugationResultsList,
                            dict.Contents, dictType));
                        break;
                    case DictType.Daijisen:
                        // TODO
                        epwingWordResultsList.Add(GetDaijirinResults(text, textInHiraganaList, deconjugationResultsList,
                            dict.Contents, dictType));
                        break;
                    case DictType.Koujien:
                        // TODO
                        epwingWordResultsList.Add(GetDaijirinResults(text, textInHiraganaList, deconjugationResultsList,
                            dict.Contents, dictType));
                        break;
                    case DictType.Meikyou:
                        // TODO
                        epwingWordResultsList.Add(GetDaijirinResults(text, textInHiraganaList, deconjugationResultsList,
                            dict.Contents, dictType));
                        break;
                    case DictType.CustomWordDictionary:
                        customWordResults = GetCustomWordResults(text, textInHiraganaList, deconjugationResultsList,
                            dictType);
                        break;
                    case DictType.CustomNameDictionary:
                        customNameResults = GetCustomNameResults(text, textInHiraganaList, dictType);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (!wordResults.Any() && !nameResults.Any() &&
                (!epwingWordResultsList.Any() || !epwingWordResultsList.First().Any()))
            {
                if (ConfigManager.Dicts[DictType.Kanjidic]?.Contents.Any() ?? false)
                {
                    kanjiResult = GetKanjidicResults(text, DictType.Kanjidic);
                }
            }

            List<Dictionary<LookupResult, List<string>>> lookupResults = new();

            if (wordResults.Any())
                lookupResults.AddRange(WordResultBuilder(wordResults));

            if (epwingWordResultsList.Any())
                foreach (var epwingWordResult in epwingWordResultsList)
                {
                    lookupResults.AddRange(EpwingWordResultBuilder(epwingWordResult));
                }

            if (nameResults.Any())
                lookupResults.AddRange(NameResultBuilder(nameResults));

            if (kanjiResult.Any())
                lookupResults.AddRange(KanjiResultBuilder(kanjiResult));

            if (customWordResults.Any())
                lookupResults.AddRange(CustomWordResultBuilder(customWordResults));

            if (customNameResults.Any())
                lookupResults.AddRange(CustomNameResultBuilder(customNameResults));

            lookupResults = SortLookupResults(lookupResults);
            return lookupResults;
        }

        private static List<Dictionary<LookupResult, List<string>>>
            SortLookupResults(List<Dictionary<LookupResult, List<string>>> lookupResults)
        {
            return lookupResults
                .OrderByDescending(dict => dict[LookupResult.FoundForm][0].Length)
                .ThenBy(dict =>
                {
                    Enum.TryParse(dict[LookupResult.DictType][0], out DictType dictType);
                    return ConfigManager.Dicts[dictType].Priority;
                })
                .ThenBy(dict => Convert.ToInt32(dict[LookupResult.Frequency][0]))
                .ToList();
        }

        private static Dictionary<string, AsdfResult>
            GetJMdictResults(string text, List<string> textInHiraganaList, List<HashSet<Form>> deconjugationResultsList,
                DictType dictType)
        {
            var wordResults =
                new Dictionary<string, AsdfResult>();

            int succAttempt = 0;

            for (int i = 0; i < text.Length; i++)
            {
                bool tryLongVowelConversion = true;

                if (ConfigManager.Dicts[DictType.JMdict].Contents.TryGetValue(textInHiraganaList[i], out var tempResult))
                {
                    wordResults.TryAdd(textInHiraganaList[i],
                        new AsdfResult(tempResult, new List<string>(), text[..^i], dictType));
                    tryLongVowelConversion = false;
                }

                if (succAttempt < 3)
                {
                    foreach (var result in deconjugationResultsList[i])
                    {
                        if (wordResults.ContainsKey(result.Text))
                            continue;

                        if (ConfigManager.Dicts[DictType.JMdict].Contents.TryGetValue(result.Text, out var temp))
                        {
                            List<IResult> resultsList = new();

                            foreach (var rslt1 in temp)
                            {
                                var rslt = (JMdictResult) rslt1;
                                if (rslt.WordClasses.SelectMany(pos => pos).Intersect(result.Tags).Any())
                                {
                                    resultsList.Add(rslt);
                                }
                            }

                            if (resultsList.Any())
                            {
                                wordResults.Add(result.Text,
                                    new AsdfResult(resultsList, result.Process, text[..result.OriginalText.Length],
                                        dictType)
                                );
                                ++succAttempt;
                                tryLongVowelConversion = false;
                            }
                        }
                    }
                }

                if (tryLongVowelConversion && textInHiraganaList[i].Contains("ー") && textInHiraganaList[i][0] != 'ー')
                {
                    string textWithoutLongVowelMark = Kana.LongVowelMarkConverter(textInHiraganaList[i]);
                    if (ConfigManager.Dicts[DictType.JMdict].Contents.TryGetValue(textWithoutLongVowelMark, out var tmpResult))
                    {
                        wordResults.Add(textInHiraganaList[i],
                            new AsdfResult(tmpResult, new List<string>(), text[..^i], dictType));
                    }
                }
            }

            return wordResults;
        }

        private static
            Dictionary<string, AsdfResult> GetJMnedictResults(string text, List<string> textInHiraganaList,
                DictType dictType)
        {
            var nameResults =
                new Dictionary<string, AsdfResult>();

            for (int i = 0; i < text.Length; i++)
            {
                if (ConfigManager.Dicts[DictType.JMnedict].Contents.TryGetValue(textInHiraganaList[i], out var tempNameResult))
                {
                    nameResults.TryAdd(textInHiraganaList[i],
                        new AsdfResult(tempNameResult, new List<string>(), text[..^i], dictType));
                }
            }

            return nameResults;
        }

        private static
            Dictionary<string, AsdfResult> GetKanjidicResults(string text, DictType dictType)
        {
            var kanjiResult =
                new Dictionary<string, AsdfResult>();

            if (ConfigManager.Dicts[DictType.Kanjidic].Contents.TryGetValue(
                text.UnicodeIterator().DefaultIfEmpty(string.Empty).First(), out List<IResult> kResult))
            {
                kanjiResult.Add(text.UnicodeIterator().First(),
                    new AsdfResult(kResult, new List<string>(), text.UnicodeIterator().First(),
                        dictType));
            }

            return kanjiResult;
        }

        private static
            Dictionary<string, AsdfResult> GetDaijirinResults(string text, List<string> textInHiraganaList,
                List<HashSet<Form>> deconjugationResultsList, Dictionary<string, List<IResult>> dict, DictType dictType)
        {
            var daijirinWordResults =
                new Dictionary<string, AsdfResult>();

            int succAttempt = 0;
            for (int i = 0; i < text.Length; i++)
            {
                bool tryLongVowelConversion = true;

                if (dict.TryGetValue(textInHiraganaList[i], out var hiraganaTempResult))
                {
                    daijirinWordResults.TryAdd(textInHiraganaList[i],
                        new AsdfResult(hiraganaTempResult, new List<string>(), text[..^i], dictType));
                    tryLongVowelConversion = false;
                }

                //todo
                if (dict.TryGetValue(text, out var textTempResult))
                {
                    daijirinWordResults.TryAdd(text,
                        new AsdfResult(textTempResult, new List<string>(), text[..^i], dictType));
                    tryLongVowelConversion = false;
                }

                if (succAttempt < 3)
                {
                    foreach (var result in deconjugationResultsList[i])
                    {
                        if (daijirinWordResults.ContainsKey(result.Text))
                            continue;

                        if (dict.TryGetValue(result.Text, out var temp))
                        {
                            List<IResult> resultsList = new();

                            foreach (var rslt1 in temp)
                            {
                                var rslt = (EpwingResult) rslt1;

                                // if (rslt.WordClasses.SelectMany(pos => pos.Except(blacklistedTags))
                                //     .Intersect(result.Tags).Any())
                                // {
                                resultsList.Add(rslt);
                                // }
                            }

                            if (resultsList.Any())
                            {
                                daijirinWordResults.Add(result.Text,
                                    new AsdfResult(resultsList, result.Process, text[..result.OriginalText.Length],
                                        dictType));
                                ++succAttempt;
                                tryLongVowelConversion = false;
                            }
                        }
                    }
                }

                if (tryLongVowelConversion && textInHiraganaList[i].Contains("ー") && textInHiraganaList[i][0] != 'ー')
                {
                    string textWithoutLongVowelMark = Kana.LongVowelMarkConverter(textInHiraganaList[i]);
                    if (dict.TryGetValue(textWithoutLongVowelMark, out var tmpResult))
                    {
                        daijirinWordResults.Add(textInHiraganaList[i],
                            new AsdfResult(tmpResult, new List<string>(), text[..^i], dictType));
                    }
                }
            }

            return daijirinWordResults;
        }

        private static
            Dictionary<string, AsdfResult> GetEpwingResults(string text, List<string> textInHiraganaList,
                List<HashSet<Form>> deconjugationResultsList, Dictionary<string, List<IResult>> dict, DictType dictType)
        {
            var daijirinWordResults =
                new Dictionary<string, AsdfResult>();

            int succAttempt = 0;
            for (int i = 0; i < text.Length; i++)
            {
                bool tryLongVowelConversion = true;

                if (dict.TryGetValue(textInHiraganaList[i], out var hiraganaTempResult))
                {
                    daijirinWordResults.TryAdd(textInHiraganaList[i],
                        new AsdfResult(hiraganaTempResult, new List<string>(), text[..^i], dictType));
                    tryLongVowelConversion = false;
                }

                //todo
                if (dict.TryGetValue(text, out var textTempResult))
                {
                    daijirinWordResults.TryAdd(text,
                        new AsdfResult(textTempResult, new List<string>(), text[..^i], dictType));
                    tryLongVowelConversion = false;
                }

                if (succAttempt < 3)
                {
                    foreach (var result in deconjugationResultsList[i])
                    {
                        if (daijirinWordResults.ContainsKey(result.Text))
                            continue;

                        if (dict.TryGetValue(result.Text, out var temp))
                        {
                            List<IResult> resultsList = new();

                            foreach (var rslt1 in temp)
                            {
                                var rslt = (EpwingResult) rslt1;

                                // if (rslt.WordClasses.SelectMany(pos => pos.Except(blacklistedTags))
                                //     .Intersect(result.Tags).Any())
                                // {
                                resultsList.Add(rslt);
                                // }
                            }

                            if (resultsList.Any())
                            {
                                daijirinWordResults.Add(result.Text,
                                    new AsdfResult(resultsList, result.Process, text[..result.OriginalText.Length],
                                        dictType));
                                ++succAttempt;
                                tryLongVowelConversion = false;
                            }
                        }
                    }
                }

                if (tryLongVowelConversion && textInHiraganaList[i].Contains("ー") && textInHiraganaList[i][0] != 'ー')
                {
                    string textWithoutLongVowelMark = Kana.LongVowelMarkConverter(textInHiraganaList[i]);
                    if (dict.TryGetValue(textWithoutLongVowelMark, out var tmpResult))
                    {
                        daijirinWordResults.Add(textInHiraganaList[i],
                            new AsdfResult(tmpResult, new List<string>(), text[..^i], dictType));
                    }
                }
            }

            return daijirinWordResults;
        }

        private static
            Dictionary<string, AsdfResult> GetCustomWordResults(string text, List<string> textInHiraganaList,
                List<HashSet<Form>> deconjugationResultsList,
                DictType dictType)
        {
            var customWordResults =
                new Dictionary<string, AsdfResult>();

            int succAttempt = 0;

            for (int i = 0; i < text.Length; i++)
            {
                bool tryLongVowelConversion = true;

                if (ConfigManager.Dicts[DictType.CustomWordDictionary].Contents
                    .TryGetValue(textInHiraganaList[i], out var tempResult))
                {
                    customWordResults.TryAdd(textInHiraganaList[i],
                        new AsdfResult(tempResult, new List<string>(), text[..^i], dictType));
                    tryLongVowelConversion = false;
                }

                if (succAttempt < 3)
                {
                    foreach (var result in deconjugationResultsList[i])
                    {
                        if (customWordResults.ContainsKey(result.Text))
                            continue;

                        if (ConfigManager.Dicts[DictType.CustomWordDictionary].Contents.TryGetValue(result.Text, out var temp))
                        {
                            List<IResult> resultsList = new();

                            foreach (var rslt1 in temp)
                            {
                                var rslt = (CustomWordEntry) rslt1;
                                if (rslt.WordClasses.Intersect(result.Tags).Any())
                                {
                                    resultsList.Add(rslt);
                                }
                            }

                            if (resultsList.Any())
                            {
                                customWordResults.Add(result.Text,
                                    new AsdfResult(resultsList, result.Process, text[..result.OriginalText.Length],
                                        dictType));
                                ++succAttempt;
                                tryLongVowelConversion = false;
                            }
                        }
                    }
                }

                if (tryLongVowelConversion && textInHiraganaList[i].Contains("ー") && textInHiraganaList[i][0] != 'ー')
                {
                    string textWithoutLongVowelMark = Kana.LongVowelMarkConverter(textInHiraganaList[i]);
                    if (ConfigManager.Dicts[DictType.CustomWordDictionary].Contents
                        .TryGetValue(textWithoutLongVowelMark, out var tmpResult))
                    {
                        customWordResults.Add(textInHiraganaList[i],
                            new AsdfResult(tmpResult, new List<string>(), text[..^i], dictType));
                    }
                }
            }

            return customWordResults;
        }

        private static
            Dictionary<string, AsdfResult> GetCustomNameResults(string text, List<string> textInHiraganaList,
                DictType dictType)
        {
            var customNameResults =
                new Dictionary<string, AsdfResult>();

            for (int i = 0; i < text.Length; i++)
            {
                if (ConfigManager.Dicts[DictType.CustomNameDictionary].Contents
                    .TryGetValue(textInHiraganaList[i], out var tempNameResult))
                {
                    customNameResults.TryAdd(textInHiraganaList[i],
                        new AsdfResult(tempNameResult, new List<string>(), text[..^i], dictType));
                }
            }

            return customNameResults;
        }

        private static List<Dictionary<LookupResult, List<string>>> WordResultBuilder
            (Dictionary<string, AsdfResult> wordResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var wordResult in wordResults)
            {
                foreach (var iResult in wordResult.Value.ResultsList)
                {
                    var jMDictResult = (JMdictResult) iResult;
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { jMDictResult.PrimarySpelling };

                    var kanaSpellings = jMDictResult.KanaSpellings ?? new List<string>();

                    var readings = jMDictResult.Readings.ToList();
                    var foundForm = new List<string> { wordResult.Value.FoundForm };
                    var edictID = new List<string> { jMDictResult.Id };

                    List<string> alternativeSpellings;
                    if (jMDictResult.AlternativeSpellings != null)
                        alternativeSpellings = jMDictResult.AlternativeSpellings.ToList();
                    else
                        alternativeSpellings = new List<string>();
                    var process = wordResult.Value.ProcessList;

                    List<string> frequency;
                    if (jMDictResult.FrequencyDict != null)
                    {
                        jMDictResult.FrequencyDict.TryGetValue(ConfigManager.FrequencyList, out var freq);
                        if (freq == 0)
                            frequency = new List<string> { MainWindowUtilities.FakeFrequency };
                        else
                            frequency = new List<string> { freq.ToString() };
                    }

                    else frequency = new List<string> { MainWindowUtilities.FakeFrequency };

                    var dictType = new List<string> { wordResult.Value.DictType.ToString() };

                    var definitions = new List<string> { BuildWordDefinition(jMDictResult) };

                    var pOrthographyInfoList = jMDictResult.POrthographyInfoList ?? new List<string>();

                    var rList = jMDictResult.ROrthographyInfoList ?? new List<List<string>>();
                    var aList = jMDictResult.AOrthographyInfoList ?? new List<List<string>>();
                    var rOrthographyInfoList = new List<string>();
                    var aOrthographyInfoList = new List<string>();

                    foreach (var list in rList)
                    {
                        var final = "";
                        foreach (var str in list)
                        {
                            final += str + ", ";
                        }

                        final = final.TrimEnd(", ".ToCharArray());

                        rOrthographyInfoList.Add(final);
                    }

                    foreach (var list in aList)
                    {
                        var final = "";
                        foreach (var str in list)
                        {
                            final += str + ", ";
                        }

                        final = final.TrimEnd(", ".ToCharArray());

                        aOrthographyInfoList.Add(final);
                    }

                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.KanaSpellings, kanaSpellings);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);
                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.EdictID, edictID);
                    result.Add(LookupResult.AlternativeSpellings, alternativeSpellings);
                    result.Add(LookupResult.Process, process);
                    result.Add(LookupResult.Frequency, frequency);
                    result.Add(LookupResult.POrthographyInfoList, pOrthographyInfoList);
                    result.Add(LookupResult.ROrthographyInfoList, rOrthographyInfoList);
                    result.Add(LookupResult.AOrthographyInfoList, aOrthographyInfoList);
                    result.Add(LookupResult.DictType, dictType);

                    results.Add(result);
                }
            }

            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> NameResultBuilder
            (Dictionary<string, AsdfResult> nameResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var nameResult in nameResults)
            {
                foreach (var iResult in nameResult.Value.ResultsList)
                {
                    var jMnedictResult = (JMnedictResult) iResult;
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { jMnedictResult.PrimarySpelling };

                    var readings = jMnedictResult.Readings != null
                        ? jMnedictResult.Readings.ToList()
                        : new List<string>();

                    var foundForm = new List<string> { nameResult.Value.FoundForm };

                    var edictID = new List<string> { jMnedictResult.Id };

                    var dictType = new List<string> { nameResult.Value.DictType.ToString() };

                    var alternativeSpellings = jMnedictResult.AlternativeSpellings ?? new List<string>();

                    var definitions = new List<string> { BuildNameDefinition(jMnedictResult) };

                    result.Add(LookupResult.EdictID, edictID);
                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.AlternativeSpellings, alternativeSpellings);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);

                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.Frequency, new List<string> { MainWindowUtilities.FakeFrequency });
                    result.Add(LookupResult.DictType, dictType);

                    results.Add(result);
                }
            }

            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> KanjiResultBuilder
            (Dictionary<string, AsdfResult> kanjiResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();
            var result = new Dictionary<LookupResult, List<string>>();

            if (!kanjiResults.Any())
                return results;

            var iResult = kanjiResults.First().Value.ResultsList;
            KanjiResult kanjiResult = (KanjiResult) iResult.First();

            var dictType = new List<string> { kanjiResults.First().Value.DictType.ToString() };

            result.Add(LookupResult.FoundSpelling, new List<string> { kanjiResults.First().Key });
            result.Add(LookupResult.Definitions, kanjiResult.Meanings);
            result.Add(LookupResult.OnReadings, kanjiResult.OnReadings);
            result.Add(LookupResult.KunReadings, kanjiResult.KunReadings);
            result.Add(LookupResult.Nanori, kanjiResult.Nanori);
            result.Add(LookupResult.StrokeCount, new List<string> { kanjiResult.StrokeCount.ToString() });
            result.Add(LookupResult.Grade, new List<string> { kanjiResult.Grade.ToString() });
            result.Add(LookupResult.Composition, new List<string> { kanjiResult.Composition });
            result.Add(LookupResult.Frequency, new List<string> { kanjiResult.Frequency.ToString() });

            var foundForm = new List<string> { kanjiResults.First().Value.FoundForm };
            result.Add(LookupResult.FoundForm, foundForm);
            result.Add(LookupResult.DictType, dictType);

            results.Add(result);
            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> EpwingWordResultBuilder
            (Dictionary<string, AsdfResult> wordResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var wordResult in wordResults)
            {
                foreach (var iResult in wordResult.Value.ResultsList)
                {
                    var epwingResult = (EpwingResult) iResult;
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { epwingResult.PrimarySpelling };
                    var readings = epwingResult.Readings.ToList();
                    var foundForm = new List<string> { wordResult.Value.FoundForm };
                    var process = wordResult.Value.ProcessList;
                    List<string> frequency;
                    // TODO
                    // if (jMDictResult.FrequencyDict != null)
                    // {
                    //     jMDictResult.FrequencyDict.TryGetValue(ConfigManager.FrequencyList, out var freq);
                    //     frequency = new List<string> { freq.ToString() };
                    // }
                    //
                    // else frequency = new List<string> { FakeFrequency };
                    frequency = new List<string> { MainWindowUtilities.FakeFrequency };
                    var dictType = new List<string> { wordResult.Value.DictType.ToString() };

                    var definitions = new List<string> { BuildEpwingWordDefinition(epwingResult) };

                    // TODO: Should be filtered while loading the dict ideally (+ it's daijirin specific)
                    if (definitions.First().Contains("→英和"))
                        continue;

                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);
                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.Process, process);
                    result.Add(LookupResult.Frequency, frequency);
                    result.Add(LookupResult.DictType, dictType);

                    results.Add(result);
                }
            }

            return results;
        }

        private static List<Dictionary<LookupResult, List<string>>> CustomWordResultBuilder
            (Dictionary<string, AsdfResult> customWordResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var wordResult in customWordResults)
            {
                foreach (var iResult in wordResult.Value.ResultsList)
                {
                    var customWordDictResults = (CustomWordEntry) iResult;
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { customWordDictResults.PrimarySpelling };

                    var readings = customWordDictResults.Readings.ToList();
                    var foundForm = new List<string> { wordResult.Value.FoundForm };

                    List<string> alternativeSpellings;
                    if (customWordDictResults.AlternativeSpellings != null)
                        alternativeSpellings = customWordDictResults.AlternativeSpellings.ToList();
                    else
                        alternativeSpellings = new();
                    var process = wordResult.Value.ProcessList;

                    List<string> frequency = new() { MainWindowUtilities.FakeFrequency };

                    var dictType = new List<string> { wordResult.Value.DictType.ToString() };

                    var definitions = new List<string> { BuildCustomWordDefinition(customWordDictResults) };

                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);
                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.AlternativeSpellings, alternativeSpellings);
                    result.Add(LookupResult.Process, process);
                    result.Add(LookupResult.Frequency, frequency);
                    result.Add(LookupResult.DictType, dictType);

                    results.Add(result);
                }
            }

            return results;
        }

        private static string BuildWordDefinition(JMdictResult jMDictResult)
        {
            int count = 1;
            string defResult = "";
            for (int i = 0; i < jMDictResult.Definitions.Count; i++)
            {
                if (jMDictResult.WordClasses.Any() && jMDictResult.WordClasses[i].Any())
                {
                    defResult += "(";
                    defResult += string.Join(", ", jMDictResult.WordClasses[i]);
                    defResult += ") ";
                }

                if (jMDictResult.Definitions.Any())
                {
                    defResult += "(" + count + ") ";

                    if (jMDictResult.SpellingInfo.Any() && jMDictResult.SpellingInfo[i] != null)
                    {
                        defResult += "(";
                        defResult += jMDictResult.SpellingInfo[i];
                        defResult += ") ";
                    }

                    if (jMDictResult.MiscList.Any() && jMDictResult.MiscList[i].Any())
                    {
                        defResult += "(";
                        defResult += string.Join(", ", jMDictResult.MiscList[i]);
                        defResult += ") ";
                    }

                    defResult += string.Join("; ", jMDictResult.Definitions[i]) + " ";

                    if (jMDictResult.RRestrictions != null && jMDictResult.RRestrictions[i].Any()
                        || jMDictResult.KRestrictions != null && jMDictResult.KRestrictions[i].Any())
                    {
                        defResult += "(only applies to ";

                        if (jMDictResult.KRestrictions != null && jMDictResult.KRestrictions[i].Any())
                            defResult += string.Join("; ", jMDictResult.KRestrictions[i]);

                        if (jMDictResult.RRestrictions != null && jMDictResult.RRestrictions[i].Any())
                            defResult += string.Join("; ", jMDictResult.RRestrictions[i]);

                        defResult += ") ";
                    }

                    var separator = ConfigManager.NewlineBetweenDefinitions ? "\n" : "";
                    defResult += separator;

                    ++count;
                }
            }

            defResult = defResult.Trim('\n');
            return defResult;
        }

        private static string BuildNameDefinition(JMnedictResult jMDictResult)
        {
            int count = 1;
            string defResult = "";

            if (jMDictResult.NameTypes != null &&
                (jMDictResult.NameTypes.Count > 1 || !jMDictResult.NameTypes.Contains("unclass")))
            {
                foreach (var nameType in jMDictResult.NameTypes)
                {
                    defResult += "(";
                    defResult += nameType;
                    defResult += ") ";
                }
            }

            for (int i = 0; i < jMDictResult.Definitions.Count; i++)
            {
                if (jMDictResult.Definitions.Any())
                {
                    if (jMDictResult.Definitions.Count > 0)
                        defResult += "(" + count + ") ";

                    defResult += string.Join("; ", jMDictResult.Definitions[i]) + " ";
                    ++count;
                }
            }

            return defResult;
        }

        private static string BuildEpwingWordDefinition(EpwingResult jMDictResult)
        {
            //todo
            // int count = 1;
            string defResult = "";
            for (int i = 0; i < jMDictResult.Definitions.Count; i++)
            {
                // if (jMDictResult.WordClasses.Any() && jMDictResult.WordClasses[i].Any())
                // {
                //     defResult += "(";
                //     defResult += string.Join(", ", jMDictResult.WordClasses[i]);
                //     defResult += ") ";
                // }

                if (jMDictResult.Definitions.Any())
                {
                    // defResult += "(" + count + ") ";

                    var separator = ConfigManager.NewlineBetweenDefinitions ? "\n" : "; ";
                    defResult += string.Join(separator, jMDictResult.Definitions[i]);

                    // ++count;
                }
            }

            defResult = defResult.Trim('\n');
            return defResult;
        }

        private static string BuildCustomWordDefinition(CustomWordEntry customWordResult)
        {
            int count = 1;
            string defResult = "";

            if (customWordResult.WordClasses.Any())
            {
                string tempWordClass;
                if (customWordResult.WordClasses.Contains("adj-i"))
                    tempWordClass = "adjective";
                else if (customWordResult.WordClasses.Contains("v1"))
                    tempWordClass = "verb";
                else if (customWordResult.WordClasses.Contains("noun"))
                    tempWordClass = "noun";
                else
                    tempWordClass = "other";

                defResult += "(" + tempWordClass + ") ";
            }

            for (int i = 0; i < customWordResult.Definitions.Count; i++)
            {
                if (customWordResult.Definitions.Any())
                {
                    defResult += "(" + count + ") ";

                    defResult += string.Join("; ", customWordResult.Definitions[i]) + " ";

                    var separator = ConfigManager.NewlineBetweenDefinitions ? "\n" : "";
                    defResult += separator;

                    ++count;
                }
            }

            defResult = defResult.Trim('\n');
            return defResult;
        }

        private static string BuildCustomNameDefinition(CustomNameEntry customNameDictResult)
        {
            string defResult = "(" + customNameDictResult.NameType + ") " + customNameDictResult.Reading;

            return defResult;
        }

        private static List<Dictionary<LookupResult, List<string>>> CustomNameResultBuilder
            (Dictionary<string, AsdfResult> customNameResults)
        {
            var results = new List<Dictionary<LookupResult, List<string>>>();

            foreach (var customNameResult in customNameResults)
            {
                foreach (var iResult in customNameResult.Value.ResultsList)
                {
                    var customNameDictResult = (CustomNameEntry) iResult;
                    var result = new Dictionary<LookupResult, List<string>>();

                    var foundSpelling = new List<string> { customNameDictResult.PrimarySpelling };

                    var readings = new List<string> { customNameDictResult.Reading };

                    var foundForm = new List<string> { customNameResult.Value.FoundForm };

                    var dictType = new List<string> { customNameResult.Value.DictType.ToString() };

                    var definitions = new List<string> { BuildCustomNameDefinition(customNameDictResult) };

                    result.Add(LookupResult.FoundSpelling, foundSpelling);
                    result.Add(LookupResult.Readings, readings);
                    result.Add(LookupResult.Definitions, definitions);

                    result.Add(LookupResult.FoundForm, foundForm);
                    result.Add(LookupResult.Frequency, new List<string> { MainWindowUtilities.FakeFrequency });
                    result.Add(LookupResult.DictType, dictType);

                    results.Add(result);
                }
            }

            return results;
        }

        public static IEnumerable<string> UnicodeIterator(this string s)
        {
            for (int i = 0; i < s.Length; ++i)
            {
                yield return char.ConvertFromUtf32(char.ConvertToUtf32(s, i));
                if (char.IsHighSurrogate(s, i))
                    i++;
            }
        }
    }
}