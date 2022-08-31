using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Fclp;
using Fclp.Internals.Extensions;
using Battousai.Utils.StringUtils;
using static Battousai.Utils.ConsoleUtils;

namespace log_merge
{
    class Program
    {
        static void Main(string[] args)
        {
            RunLoggingExceptions(() =>
            {
                // Setup command-line parsing
                var parser = new FluentCommandLineParser<Args>();

                parser.Setup(x => x.LogFilenames)
                    .As('i', "input-filenames")
                    .Required()
                    .UseForOrphanArguments()
                    .WithDescription("<filename(s)>  The filenames of the log files to merge (required)");

                parser.Setup(x => x.StartPattern)
                    .As('p', "pattern")
                    .Required()
                    .WithDescription($"{"The regex pattern to match the beginning of a log entry (required).  The first\n".WithIndent(15)}{"capture group of the pattern must capture the log date/time.".WithIndent(15)}");

                parser.Setup(x => x.NoColor)
                    .As('n', "no-color")
                    .SetDefault(false)
                    .WithDescription("Supresses coloring of the matched start pattern".WithIndent(15));

                parser.Setup(x => x.ShowHelp)
                    .As('h', "help")
                    .WithDescription("Show this help information".WithIndent(15));

                parser.Setup(x => x.Filter)
                    .As('f', "filter")
                    .WithDescription("<text>         Filter for second capture group in header regex");

                parser.Setup(x => x.Find)
                    .As('s', "search")
                    .WithDescription("<text>         Filter for entries containing search text");

                var results = parser.Parse(args);

                if (results.HasErrors || parser.Object.ShowHelp)
                {
                    Log("Invalid command-line parameters.");
                    Log(@"Example usage: .\log-merge.exe -i c:\path-1\trace.log c:\path-2\trace.log -p <pattern>");
                    Log();

                    var longNamePadding = parser.Options.Max(x => x.LongName.Length);

                    parser.Options
                        .ToList()
                        .ForEach(x =>
                        {
                            var lines = x.Description.Split("\n")
                                .Select((line, i) =>
                                {
                                    if (i == 0)
                                        return $"   {(x.HasShortName ? $"-{x.ShortName}, " : "    ")}--{x.LongName.PadRight(longNamePadding)} {line}";
                                    else
                                        return $"{new String(' ', longNamePadding + 9)} {line}";
                                })
                                .Where(x => !String.IsNullOrWhiteSpace(x))
                                .ToList();

                            lines.ForEach(Log);
                        });

                    Log();

                    return;
                }

                var parameters = parser.Object;
                var filenames = parameters.LogFilenames.SelectMany(x => x.ContainsWildcards() ? EnumerateFiles(x) : x.ToSingleton());
                var isRedirected = Console.IsOutputRedirected;
                var num = filenames.Count();
                var filterHitCount = 0;

                Action<string> writeProgress = str =>
                {
                    if (isRedirected)
                        return;

                    var startingPos = Console.CursorTop;
                    var adjustedStr = str.PadRightWithEllipsis(Console.WindowWidth - 1);
                    var spaces = new String(' ', Console.WindowWidth - adjustedStr.Length - 1);
                    var text = adjustedStr + spaces;

                    Console.Write(text);
                    Console.SetCursorPosition(0, startingPos);
                };

                Action clearProgress = () =>
                {
                    if (isRedirected)
                        return;

                    var startingPos = Console.CursorTop;

                    Console.SetCursorPosition(0, startingPos);
                    Console.Write(new String(' ', Console.WindowWidth - 1));
                    Console.SetCursorPosition(0, startingPos);
                };

                try
                {
                    if (!isRedirected)
                        Console.CursorVisible = false;

                    var content = filenames
                        .SelectMany((x, counter) =>
                        {
                            if (num == 1)
                            {
                                writeProgress($"Reading file: {x}...");
                            }
                            else
                            {
                                if (filterHitCount > 0)
                                {
                                    var countStr = filterHitCount == 1 ? "hit" : "hits";
                                    writeProgress($"Reading files [{counter + 1}/{num}] <{filterHitCount:#,##0} {countStr}>: {x}...");
                                }
                                else
                                    writeProgress($"Reading files [{counter + 1}/{num}]: {x}...");
                            }

                            var values = Utils.WriteSafeReadAllLines(x).Select((y, i) => new { LineNumber = i + 1, LineText = y });

                            return values.Select(value => new { Filename = x, value.LineNumber, value.LineText });
                        });

                    var startPattern = new Regex(parameters.StartPattern, RegexOptions.IgnoreCase);

                    Func<string, bool> isInFilter = text =>
                    {
                        var match = startPattern.Match(text);

                        if (!match.Success || match.Groups.Count < 3)
                            throw new InvalidOperationException("The header regex did not contain a second capture group (with which to filter).");

                        var result = match.Groups[2]?.Value.Contains(parameters.Filter, StringComparison.OrdinalIgnoreCase) ?? false;

                        if (result)
                            filterHitCount += 1;

                        return result;
                    };

                    Func<Entry, bool> isInSearch = entry =>
                    {
                        if (entry == null)
                            return true;

                        var result = entry.RawText.Contains(parameters.Find, StringComparison.OrdinalIgnoreCase) ||
                            entry.Lines.Any(x => x.Contains(parameters.Find, StringComparison.OrdinalIgnoreCase));

                        if (result)
                            filterHitCount += 1;

                        return result;
                    };

                    // Parse lines in log files into Entry objects...
                    var aggregatedEntries = content.Aggregate(
                        new { Results = new List<Entry>(), IsSkipping = true, CurrentEntry = (Entry)null },
                        (acc, x) =>
                        {
                            var lineNumber = x.LineNumber;
                            var text = x.LineText;
                            var filename = x.Filename;

                            var match = startPattern.Match(text);
                            var newIsSkipping = acc.IsSkipping;
                            Entry newCurrentEntry = acc.CurrentEntry;

                            if (match.Success)
                            {
                                var logDate = DateTimeOffset.Parse(match.Groups[1].Value, null, DateTimeStyles.AssumeUniversal);
                                var passesFilter = String.IsNullOrWhiteSpace(parameters.Filter) || isInFilter(text);                                

                                if (passesFilter)
                                {
                                    // Check previous entry to ensure that it "passes search"
                                    var passesSearch = String.IsNullOrWhiteSpace(parameters.Find) || isInSearch(acc.CurrentEntry);

                                    if (!passesSearch)
                                        acc.Results.Remove(acc.CurrentEntry);

                                    newCurrentEntry = new Entry(text, filename, lineNumber, logDate, match.Index, match.Length, text.ToSingleton());
                                    acc.Results.Add(newCurrentEntry);
                                    newIsSkipping = false;
                                }
                                else
                                    newIsSkipping = true;
                            }
                            else
                            {
                                if (!acc.IsSkipping)
                                {
                                    var lastEntry = acc.Results.LastOrDefault();

                                    if (lastEntry == null)
                                        throw new InvalidOperationException("First line must contain a entry header.");

                                    lastEntry.Lines.Add(text);
                                }
                            }

                            return new { acc.Results, IsSkipping = newIsSkipping, CurrentEntry = newCurrentEntry };
                        });

                    if (aggregatedEntries.CurrentEntry != null)
                    {
                        // Check previous entry to ensure that it "passes search"
                        var passesSearch = String.IsNullOrWhiteSpace(parameters.Find) || isInSearch(aggregatedEntries.CurrentEntry);

                        if (!passesSearch)
                            aggregatedEntries.Results.Remove(aggregatedEntries.CurrentEntry);
                    }

                    var entries = aggregatedEntries.Results;

                    clearProgress();

                    // Write ordered entries to output...
                    Action<string, int, int> writeHeaderLine = (text, index, count) =>
                    {
                        if (parameters.NoColor)
                        {
                            Console.WriteLine(text);
                            return;
                        }

                        var pretext = text.Substring(0, index);
                        var innerText = text.Substring(index, count);
                        var remainingText = text.Substring(index + count);

                        if (!String.IsNullOrEmpty(pretext))
                            Console.Write(pretext);

                        var saveColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(innerText);
                        Console.ForegroundColor = saveColor;

                        Console.WriteLine(remainingText);
                    };

                    Action<string> writeLine = text => Console.WriteLine(text);

                    var orderedEntries = entries
                        .OrderBy(x => x.LogDate)
                        .ThenBy(x => x, LogEntryComparer.Default)
                        .ToList();

                    orderedEntries.ForEach(entry =>
                    {
                        var firstLine = entry.Lines.First();
                        var remainingLines = entry.Lines.Skip(1);

                        writeHeaderLine(firstLine, entry.PatternMatchStart, entry.PatternMatchLength);
                        remainingLines.ForEach(x => writeLine(x));
                    });
                }
                finally
                {
                    if (!isRedirected)
                        Console.CursorVisible = true;
                }
            }, false, false);
        }

        private static IEnumerable<string> EnumerateFiles(string wildcardFilename)
        {
            var path = Path.GetDirectoryName(wildcardFilename);
            var searchPattern = Path.GetFileName(wildcardFilename);

            if (String.IsNullOrWhiteSpace(path))
                path = @".\";

            return Directory.GetFiles(path, searchPattern);
        }
    }
}
