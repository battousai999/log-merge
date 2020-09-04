using System;
using System.Collections.Generic;
using System.Text;

namespace log_merge
{
    public class LogEntryComparer : IComparer<Entry>
    {
        public static LogEntryComparer Default { get; } = new LogEntryComparer();

        public int Compare(Entry x, Entry y)
        {
            if (x == y)
                return 0;

            if (x == null)
                return 1;
            else if (y == null)
                return -1;

            // For same filename, order by line number; for different filename, reverse order
            // by line number.
            if (StringComparer.OrdinalIgnoreCase.Equals(x.Filename, y.Filename))
                return Comparer<int>.Default.Compare(x.LineNumber, y.LineNumber);
            else
                return -Comparer<int>.Default.Compare(x.LineNumber, y.LineNumber);
        }
    }
}
