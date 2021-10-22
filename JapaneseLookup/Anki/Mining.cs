﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace JapaneseLookup.Anki
{
    public static class Mining
    {
        // TODO: Check if audio was grabbed and tell the user if it was not
        public static async Task Mine(string foundSpelling, string readings, string definitions, string context,
            string foundForm, string edictID, string timeLocal, string alternativeSpellings,
            string frequency, string strokeCount, string grade, string composition)
        {
            try
            {
                var ankiConfig = await AnkiConfig.ReadAnkiConfig();
                if (ankiConfig == null) return;

                string deckName = ankiConfig.DeckName;
                string modelName = ankiConfig.ModelName;

                var rawFields = ankiConfig.Fields;
                var fields =
                    ConvertFields(
                        rawFields,
                        foundSpelling,
                        readings,
                        definitions,
                        context,
                        foundForm,
                        edictID,
                        timeLocal,
                        alternativeSpellings,
                        frequency,
                        strokeCount,
                        grade,
                        composition
                    );

                Dictionary<string, object> options = new()
                {
                    {
                        "allowDuplicate",
                        ConfigManager.AllowDuplicateCards
                    },
                };
                string[] tags = ankiConfig.Tags;

                // idk if this gets the right audio for every word
                readings ??= "";
                string reading = readings.Split(",")[0];
                if (reading == "") reading = foundSpelling;

                Dictionary<string, object>[] audio =
                {
                    new()
                    {
                        {
                            "url",
                            $"http://assets.languagepod101.com/dictionary/japanese/audiomp3.php?kanji={foundSpelling}&kana={reading}"
                        },
                        {
                            "filename",
                            $"JL_audio_{foundSpelling}_{reading}.mp3"
                        },
                        {
                            "skipHash",
                            "7e2c2f954ef6051373ba916f000168dc"
                        },
                        {
                            "fields",
                            FindAudioFields(rawFields)
                        },
                    }
                };
                Dictionary<string, object>[] video = null;
                Dictionary<string, object>[] picture = null;

                var note = new Note(deckName, modelName, fields, options, tags, audio, video, picture);
                Response response = await AnkiConnect.AddNoteToDeck(note);

                if (response == null)
                {
                    Console.WriteLine($"Mining failed for {foundSpelling}");
                }
                else
                {
                    Console.WriteLine($"Mined {foundSpelling}");
                    if (ConfigManager.ForceSync) await AnkiConnect.Sync();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Console.WriteLine($"Mining failed for {foundSpelling}");
            }
        }

        private static Dictionary<string, object> ConvertFields(Dictionary<string, JLField> fields,
            string foundSpelling, string readings, string definitions, string context,
            string foundForm, string edictID, string timeLocal, string alternativeSpellings, string frequency,
            string strokeCount, string grade, string composition)
        {
            var dict = new Dictionary<string, object>();
            foreach ((string key, JLField value) in fields)
            {
                switch (value)
                {
                    case JLField.Nothing:
                        break;
                    case JLField.FoundSpelling:
                        dict.Add(key, foundSpelling);
                        break;
                    case JLField.Readings:
                        dict.Add(key, readings);
                        break;
                    case JLField.Definitions:
                        dict.Add(key, definitions);
                        break;
                    case JLField.FoundForm:
                        dict.Add(key, foundForm);
                        break;
                    case JLField.Context:
                        dict.Add(key, context);
                        break;
                    case JLField.Audio:
                        // needs to be handled separately (by FindAudioFields())
                        break;
                    case JLField.EdictID:
                        dict.Add(key, edictID);
                        break;
                    case JLField.TimeLocal:
                        dict.Add(key, timeLocal);
                        break;
                    case JLField.AlternativeSpellings:
                        dict.Add(key, alternativeSpellings);
                        break;
                    case JLField.Frequency:
                        dict.Add(key, frequency);
                        break;
                    case JLField.StrokeCount:
                        dict.Add(key, strokeCount);
                        break;
                    case JLField.Grade:
                        dict.Add(key, grade);
                        break;
                    case JLField.Composition:
                        dict.Add(key, composition);
                        break;
                    default:
                        return null;
                }
            }

            return dict
                .Where(kvp => kvp.Value != null)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private static List<string> FindAudioFields(Dictionary<string, JLField> fields)
        {
            var audioFields = new List<string>();
            audioFields.AddRange(fields.Keys.Where(key => JLField.Audio.Equals(fields[key])));

            return audioFields;
        }
    }
}