using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BrawlhallaLangReader;

[DoesNotReturn]
static void ExitWithMessage(string message, int exitCode = 1)
{
    Console.WriteLine(message, exitCode == 0 ? Console.Out : Console.In);
    Environment.Exit(exitCode);
}

[DoesNotReturn]
static void ShowHelp()
{
    ExitWithMessage(
@"Usage: lang-reader [mode]
modes:
    -O/--output [path to brawlhalla folder] [path to LanguageTypes.xml file] [output file path]
    --help: show this message
"
    , 0);
}

if (args.Length == 0) ShowHelp();

string mode = args[0];

try
{
    if (mode == "--help")
    {
        ShowHelp();
    }
    else if (mode == "-O" || mode == "--output")
    {
        if (args.Length < 4) ExitWithMessage($"Too few arguments to {mode}");
        if (args.Length > 4) ExitWithMessage($"Too many arguments to {mode}");
        string brawlhallaPath = args[1];
        string languageTypes = args[2];
        string outputPath = args[3];
        string langPath = Path.Combine(brawlhallaPath, "languages");
        if (!Directory.Exists(langPath))
            ExitWithMessage("Given brawlhalla path does not contain a languages folder");
        if (!File.Exists(languageTypes))
            ExitWithMessage("Given LanguageTypes.xml file does not exist");

        XElement langTypes = null!;
        try
        {
            using (FileStream file = File.OpenRead(languageTypes))
                langTypes = XElement.Load(file);
            if (langTypes.Name != "LanguageTypes")
                ExitWithMessage("Invalid LanguageTypes.xml file given");
        }
        catch (Exception e)
        {
            ExitWithMessage($"Parsing LanguageTypes.xml failed with error: {e}");
        }

        // get languages data
        List<(string, uint)> languages = [];
        foreach (XElement lang in langTypes.Elements("LanguageType"))
        {
            string? name = lang.Attribute("LanguageName")?.Value;
            if (name == "Template") continue;

            if (name is null)
            {
                Console.WriteLine($"Skipping LanguageType element that is missing a name: {lang}");
                continue;
            }

            if (languages.Any(l => l.Item1 == name))
            {
                Console.WriteLine($"Duplicate language name {name}. Skipping");
                continue;
            }

            XElement? langIdElement = lang.Element("LanguageID");
            if (langIdElement is null)
            {
                Console.WriteLine($"Language {name} is missing an id element. Skipping");
                continue;
            }

            string langIdString = langIdElement.Value;
            if (!uint.TryParse(langIdString, CultureInfo.InvariantCulture, out uint langId))
            {
                Console.WriteLine($"Language {name} has invalid lang id: {langIdString}. Skipping.");
                continue;
            }

            if (languages.Any(l => l.Item2 == langId))
            {
                Console.WriteLine($"Duplicate language id {langId}. Skipping");
                continue;
            }

            languages.Add((name, langId));
        }

        // create dict
        Dictionary<string, Dictionary<uint, string>> stringDict = [];
        foreach ((string langName, uint langId) in languages)
        {
            string langFilePath = Path.Combine(langPath, $"language.{langId}.bin");
            if (!File.Exists(langFilePath))
            {
                Console.WriteLine($"Language {langName} is missing a language file (id {langId}). Skipping");
                continue;
            }

            LangFile file;
            try
            {
                using FileStream stream = File.OpenRead(langFilePath);
                file = LangFile.Load(stream);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while parsing language file for {langName}: {e}");
                continue;
            }

            foreach ((string key, string value) in file.Entries)
            {
                if (!stringDict.ContainsKey(key))
                    stringDict[key] = [];
                stringDict[key][langId] = value;
            }
        }

        // now to dump
        try
        {
            using FileStream stream = File.OpenWrite(outputPath);
            using StreamWriter writer = new(stream);

            writer.Write("StringKey");
            foreach ((string name, _) in languages)
                writer.Write('\t' + name);
            // go in alphabetical order
            string[] keys = [.. stringDict.Keys];
            Array.Sort(keys);

            foreach (string key in keys)
            {
                writer.Write('\n' + key);
                Dictionary<uint, string> values = stringDict[key];
                foreach ((string name, uint langId) in languages)
                {
                    if (!values.ContainsKey(langId))
                        Console.WriteLine($"Language {name} is missing value for key {key}. Using empty string.");
                    string value = values.GetValueOrDefault(langId, "");
                    // normalize newlines to \n
                    value = value.Replace("\n", "\\n");
                    writer.Write('\t' + value);
                }
            }
        }
        catch (Exception e)
        {
            ExitWithMessage($"Writing to output path failed with error: {e}");
        }
    }
    else
    {
        ExitWithMessage($"Invalid mode {mode}. Use --help to see a list of available modes.");
    }
}
catch (Exception e)
{
    ExitWithMessage($"Unhandled error: {e}");
}