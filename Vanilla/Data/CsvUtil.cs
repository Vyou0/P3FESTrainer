using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace P3FESTrainer.Data
{
    public static class CsvUtil
    {
        public static List<string[]> ReadEmbeddedDataRows(string logicalName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(logicalName)
                ?? throw new FileNotFoundException($"Embedded CSV resource '{logicalName}' not found.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var rows = new List<string[]>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                rows.Add(ParseLine(line));
            }
            if (rows.Count > 0)
                rows.RemoveAt(0); // drop header
            return rows;
        }

        private static string[] ParseLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        public static uint ParseHexAddress(string hex) => Convert.ToUInt32(hex, 16);
    }
}
