using Cachet_Monitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cachet_Monitor.Extensions
{
    public static class Extensions
    {
        public static string PrettyLines(this List<string[]> lines, int padding = 1)
        {
            int elementCount = lines[0].Length;
            int[] maxValues = new int[elementCount];

            for (int i = 0; i < elementCount; i++)
                maxValues[i] = lines.Max(x => x[i].Length) + padding;

            var sb = new StringBuilder();
            bool isFirst = true;

            foreach (var line in lines)
            {
                if (!isFirst)
                    sb.AppendLine();

                isFirst = false;

                for (int i = 0; i < line.Length; i++)
                {
                    var value = line[i];
                    sb.Append(value.PadRight(maxValues[i]));
                }
            }
            return Convert.ToString(sb);
        }

        public static ConsoleColor SeverityToColor(this LogSeverity sev)
        {
            switch (sev)
            {
                case LogSeverity.Critical:
                    return ConsoleColor.Red;
                case LogSeverity.Error:
                    return ConsoleColor.Red;
                case LogSeverity.Info:
                    return ConsoleColor.Green;
                case LogSeverity.Warning:
                    return ConsoleColor.Yellow;
                case LogSeverity.Verbose:
                    return ConsoleColor.Cyan;

                default:
                    return ConsoleColor.White;
            }
        }
    }
}
