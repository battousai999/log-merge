using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace log_merge
{
    public class Entry
    {
        public string RawText { get; }
        public string Filename { get; }
        public int LineNumber { get; }
        public DateTimeOffset LogDate { get; }
        public List<string> Lines { get; }
        public int PatternMatchStart { get; }
        public int PatternMatchLength { get; }

        public Entry(string rawText, string filename, int lineNumber, DateTimeOffset logDate, int patternMatchStart, int patternMatchLength, IEnumerable<string> lines)
        {
            this.RawText = rawText;
            this.Filename = filename;
            this.LineNumber = lineNumber;
            this.LogDate = logDate;
            this.PatternMatchStart = patternMatchStart;
            this.PatternMatchLength = patternMatchLength;
            this.Lines = lines?.ToList() ?? Enumerable.Empty<string>().ToList();
        }
    }
}
