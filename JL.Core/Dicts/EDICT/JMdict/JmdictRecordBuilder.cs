﻿using JL.Core.Utilities;

namespace JL.Core.Dicts.EDICT.JMdict;

internal static class JmdictRecordBuilder
{
    public static void AddToDictionary(JmdictEntry entry, Dictionary<string, List<IDictRecord>> jmdictDictionary)
    {
        // entry (k_ele*, r_ele+, sense+)
        // k_ele (keb, ke_inf*, ke_pri*)
        // r_ele (reb, re_restr*, re_inf*, re_pri*)
        // sense (stagk*, stagr*, pos*, xref*, ant*, field*, misc*, s_inf*, dial*, gloss*)

        Dictionary<string, JmdictRecord> recordDictionary = new();

        int kEleListCount = entry.KanjiElements.Count;
        for (int i = 0; i < kEleListCount; i++)
        {
            KanjiElement kanjiElement = entry.KanjiElements[i];

            JmdictRecord record = new();
            string key = kanjiElement.Keb!;

            record.PrimarySpelling = key;

            record.PrimarySpellingOrthographyInfoList = kanjiElement.KeInfList;
            //record.PriorityList = kanjiElement.KePriList;

            int lREleListCount = entry.ReadingElements.Count;
            for (int j = 0; j < lREleListCount; j++)
            {
                ReadingElement readingElement = entry.ReadingElements[j];

                if (!readingElement.ReRestrList.Any() || readingElement.ReRestrList.Contains(key))
                {
                    record.Readings?.Add(readingElement.Reb);
                    record.ReadingsOrthographyInfoList?.Add(readingElement.ReInfList);
                }
            }

            int senseListCount = entry.SenseList.Count;
            for (int j = 0; j < senseListCount; j++)
            {
                Sense sense = entry.SenseList[j];

                if ((!sense.StagKList.Any() && !sense.StagRList.Any())
                    || sense.StagKList.Contains(key)
                    || sense.StagRList.Intersect(record.Readings!).Any())
                {
                    ProcessSense(record, sense);
                }
            }

            recordDictionary.Add(key, record);
        }

        List<string> alternativeSpellings = recordDictionary.Keys.ToList();

        foreach ((string key, JmdictRecord result) in recordDictionary)
        {
            int alternativeSpellingsCount = alternativeSpellings.Count;
            for (int i = 0; i < alternativeSpellingsCount; i++)
            {
                string spelling = alternativeSpellings[i];

                if (key != spelling)
                {
                    result.AlternativeSpellings!.Add(spelling);

                    if (recordDictionary.TryGetValue(spelling, out JmdictRecord? tempResult))
                    {
                        result.AlternativeSpellingsOrthographyInfoList!.Add(tempResult.PrimarySpellingOrthographyInfoList);
                    }
                }
            }
        }

        List<string> allReadings = entry.ReadingElements.Select(rEle => rEle.Reb).ToList();
        List<List<string>> allROrthographyInfoLists = entry.ReadingElements.Select(rEle => rEle.ReInfList).ToList();

        int rEleListCount = entry.ReadingElements.Count;
        for (int i = 0; i < rEleListCount; i++)
        {
            ReadingElement readingElement = entry.ReadingElements[i];

            string key = Kana.KatakanaToHiraganaConverter(readingElement.Reb);

            if (recordDictionary.ContainsKey(key))
            {
                continue;
            }

            JmdictRecord record = new()
            {
                AlternativeSpellings = readingElement.ReRestrList.Any()
                    ? readingElement.ReRestrList
                    : new List<string>(alternativeSpellings)
            };

            if (record.AlternativeSpellings.Any())
            {
                record.PrimarySpelling = record.AlternativeSpellings[0];

                record.AlternativeSpellings.RemoveAt(0);

                if (recordDictionary.TryGetValue(record.PrimarySpelling, out JmdictRecord? mainEntry))
                {
                    record.Readings = mainEntry.Readings;
                    record.AlternativeSpellingsOrthographyInfoList = mainEntry.AlternativeSpellingsOrthographyInfoList;
                    record.ReadingsOrthographyInfoList = mainEntry.ReadingsOrthographyInfoList;
                }
            }

            else
            {
                record.PrimarySpelling = readingElement.Reb;
                record.PrimarySpellingOrthographyInfoList = readingElement.ReInfList;

                record.AlternativeSpellings = allReadings.ToList();
                record.AlternativeSpellings.RemoveAt(i);

                record.AlternativeSpellingsOrthographyInfoList = allROrthographyInfoLists.ToList()!;
                record.AlternativeSpellingsOrthographyInfoList.RemoveAt(i);
            }

            int senseListCount = entry.SenseList.Count;
            for (int j = 0; j < senseListCount; j++)
            {
                Sense sense = entry.SenseList[j];

                if ((!sense.StagKList.Any() && !sense.StagRList.Any())
                    || sense.StagRList.Contains(readingElement.Reb)
                    || sense.StagKList.Contains(record.PrimarySpelling)
                    || sense.StagKList.Intersect(record.AlternativeSpellings).Any())
                {
                    ProcessSense(record, sense);
                }
            }

            recordDictionary.Add(key, record);
        }

        foreach (KeyValuePair<string, JmdictRecord> recordKeyValuePair in recordDictionary)
        {
            recordKeyValuePair.Value.Readings = Utils.TrimStringList(recordKeyValuePair.Value.Readings!);
            recordKeyValuePair.Value.AlternativeSpellings = Utils.TrimStringList(recordKeyValuePair.Value.AlternativeSpellings!);
            recordKeyValuePair.Value.PrimarySpellingOrthographyInfoList = Utils.TrimStringList(recordKeyValuePair.Value.PrimarySpellingOrthographyInfoList!);
            recordKeyValuePair.Value.DefinitionInfo = Utils.TrimStringList(recordKeyValuePair.Value.DefinitionInfo!)!;
            recordKeyValuePair.Value.Definitions = TrimListOfLists(recordKeyValuePair.Value.Definitions!)!;
            recordKeyValuePair.Value.ReadingRestrictions = TrimListOfLists(recordKeyValuePair.Value.ReadingRestrictions);
            recordKeyValuePair.Value.SpellingRestrictions = TrimListOfLists(recordKeyValuePair.Value.SpellingRestrictions);
            recordKeyValuePair.Value.Dialects = TrimListOfLists(recordKeyValuePair.Value.Dialects);
            recordKeyValuePair.Value.MiscList = TrimListOfLists(recordKeyValuePair.Value.MiscList);
            recordKeyValuePair.Value.AlternativeSpellingsOrthographyInfoList = TrimListOfLists(recordKeyValuePair.Value.AlternativeSpellingsOrthographyInfoList);
            recordKeyValuePair.Value.ReadingsOrthographyInfoList = TrimListOfLists(recordKeyValuePair.Value.ReadingsOrthographyInfoList);
            recordKeyValuePair.Value.FieldList = TrimListOfLists(recordKeyValuePair.Value.FieldList);
            recordKeyValuePair.Value.WordClasses = TrimListOfLists(recordKeyValuePair.Value.WordClasses);
            recordKeyValuePair.Value.RelatedTerms = TrimListOfLists(recordKeyValuePair.Value.RelatedTerms);
            recordKeyValuePair.Value.Antonyms = TrimListOfLists(recordKeyValuePair.Value.Antonyms);
            recordKeyValuePair.Value.LoanwordEtymology = TrimListOfLists(recordKeyValuePair.Value.LoanwordEtymology);

            recordKeyValuePair.Value.Id = entry.Id;
            string key = Kana.KatakanaToHiraganaConverter(recordKeyValuePair.Key);

            if (jmdictDictionary.TryGetValue(key, out List<IDictRecord>? tempRecordList))
                tempRecordList.Add(recordKeyValuePair.Value);
            else
                tempRecordList = new List<IDictRecord>() { recordKeyValuePair.Value };

            jmdictDictionary[key] = tempRecordList;
        }
    }

    private static void ProcessSense(JmdictRecord jmdictRecord, Sense sense)
    {
        jmdictRecord.Definitions.Add(sense.GlossList);
        jmdictRecord.ReadingRestrictions!.Add(sense.StagRList.Any() ? sense.StagRList : null);
        jmdictRecord.SpellingRestrictions!.Add(sense.StagKList.Any() ? sense.StagKList : null);
        jmdictRecord.WordClasses!.Add(sense.PosList.Any() ? sense.PosList : null);
        jmdictRecord.FieldList!.Add(sense.FieldList.Any() ? sense.FieldList : null);
        jmdictRecord.MiscList!.Add(sense.MiscList.Any() ? sense.MiscList : null);
        jmdictRecord.Dialects!.Add(sense.DialList.Any() ? sense.DialList : null);
        jmdictRecord.DefinitionInfo!.Add(sense.SInf);
        jmdictRecord.RelatedTerms!.Add(sense.XRefList.Any() ? sense.XRefList : null);
        jmdictRecord.Antonyms!.Add(sense.AntList.Any() ? sense.AntList : null);
        jmdictRecord.LoanwordEtymology!.Add(sense.LSourceList.Any() ? sense.LSourceList : null);
    }

    private static List<List<T>?>? TrimListOfLists<T>(List<List<T>?>? listOfLists)
    {
        List<List<T>?>? listOfListClone = listOfLists;

        if (!listOfListClone!.Any() || listOfListClone!.All(l => l == null || !l.Any()))
            listOfListClone = null;
        else
        {
            listOfListClone!.TrimExcess();

            int counter = listOfListClone.Count;
            for (int i = 0; i < counter; i++)
            {
                listOfListClone[i]?.TrimExcess();
            }
        }

        return listOfListClone;
    }
}