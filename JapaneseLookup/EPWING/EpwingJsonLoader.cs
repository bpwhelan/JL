﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using JapaneseLookup.Dicts;

namespace JapaneseLookup.EPWING
{
    public static class EpwingJsonLoader
    {
        public static async Task Loader(DictType dictType, string dictPath)
        {
            if (!(Directory.Exists(dictPath) || File.Exists(dictPath)))
                return;

            List<EpwingEntry> epwingEntryList = new();

            string[] jsonFiles = Directory.GetFiles(dictPath, "*_bank_*.json");
            // string[] jsonFiles = Directory.GetFiles(dictPath, "*.json");

            foreach (string jsonFile in jsonFiles)
            {
                await using FileStream openStream = File.OpenRead(jsonFile);
                var jsonObject = await JsonSerializer.DeserializeAsync<List<List<JsonElement>>>(openStream);

                Debug.Assert(jsonObject != null, nameof(jsonObject) + " != null");
                foreach (var obj in jsonObject)
                {
                    epwingEntryList.Add(new EpwingEntry(obj));
                }
            }

            DictionaryBuilder(epwingEntryList, ConfigManager.Dicts[dictType].Contents);
            ConfigManager.Dicts[dictType].Contents.TrimExcess();
        }

        public static void DictionaryBuilder(List<EpwingEntry> epwingEntryList,
            Dictionary<string, List<IResult>> epwingDictionary)
        {
            foreach (var entry in epwingEntryList)
            {
                if ("" != entry.DefinitionTags)
                    Debug.WriteLine(entry.DefinitionTags);
                // if ("" != entry.Rules)
                //     Debug.WriteLine(entry.Expression + " " + entry.Rules);
                if (0 != entry.Score)
                    Debug.WriteLine(entry.Score);
                if ("" != entry.TermTags)
                    Debug.WriteLine(entry.TermTags);

                var result = new EpwingResult
                {
                    Definitions = new List<List<string>> { entry.Glosssary },
                    Readings = new List<string> { entry.Reading ?? entry.Expression },
                    PrimarySpelling = entry.Expression,
                    WordClasses = new List<List<string>> { new() { entry.Rules } }
                };
                // if (entry.Expression == "アイドル")
                //     Console.WriteLine();

                if (epwingDictionary.TryGetValue(entry.Expression, out List<IResult> tempList))
                    tempList.Add(result);
                else
                    tempList = new() { result };

                Debug.Assert(entry.Reading != null, "entry.Reading != null");
                if (entry.Reading != "")
                    epwingDictionary[entry.Reading] = tempList;
                // string hira = entry.Reading == "" ? null : entry.Reading;
                string kata = Kana.KatakanaToHiraganaConverter(entry.Expression);

                epwingDictionary[kata] = tempList;
                epwingDictionary[entry.Expression] = tempList;
            }
        }
    }
}