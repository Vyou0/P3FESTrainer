using System;
using System.Collections.Generic;

namespace P3FESTrainer.Data
{
    /// <summary>
    /// Constants and memory layout definitions for the main character and Persona data structures.
    /// </summary>
    public static class MainCharacterData
    {
        public static readonly Dictionary<int, long> ExpTable = new();

        public static void LoadExpTable()
        {
            ExpTable.Clear();
            foreach (var row in CsvUtil.ReadEmbeddedDataRows("Data.exp_table.csv"))
                ExpTable[int.Parse(row[0])] = long.Parse(row[1]);
        }

        public static long ExpForLevel(int level)
        {
            if (level < 1 || level > 99) throw new ArgumentOutOfRangeException(nameof(level), "level must be 1-99");
            if (ExpTable.Count == 0) throw new InvalidOperationException("ExpTable not loaded.");
            return ExpTable[level];
        }

        public static int LevelForExp(long exp)
        {
            if (ExpTable.Count == 0) throw new InvalidOperationException("ExpTable not loaded.");
            int level = 0;
            for (int lvl = 1; lvl <= 99; lvl++)
            {
                if (exp < ExpTable[lvl]) break;
                level = lvl;
            }
            return level == 0 ? 1 : level;
        }

        public const double PersonaExpFormulaC1 = 3.1;
        public const double PersonaExpFormulaC2 = 0.019;
        public const double PersonaExpFormulaC3 = 1.4;

        public const int SpecialPersonaIdMin = 0xC0;
        public const int SpecialPersonaIdMax = 0xDF;

        public static bool IsSpecialPersonaId(int personaId) =>
            personaId >= SpecialPersonaIdMin && personaId <= SpecialPersonaIdMax;

        public const uint PersonaGrowthTablePointer = 0x007CE420;
        public const int PersonaGrowthByteFieldOffset = 3;

        public static uint PersonaGrowthByteAddress(int personaId, uint tableBase) =>
            (uint)(tableBase + personaId * PersonaTypeFlagsStride + PersonaGrowthByteFieldOffset);

        public static long ExpForPersonaLevel(int level, int growthByte)
        {
            if (level < 1 || level > 99) throw new ArgumentOutOfRangeException(nameof(level), "level must be 1-99");
            if (growthByte < 0 || growthByte > 255) throw new ArgumentOutOfRangeException(nameof(growthByte), "growthByte must be 0-255");
            double coefficient = PersonaExpFormulaC1 - PersonaExpFormulaC2 * growthByte;
            if (coefficient <= 0)
                throw new InvalidOperationException($"Invalid growthByte value ({growthByte}).");
            return (long)(coefficient * Math.Pow(level, 3) * PersonaExpFormulaC3 + 10.0);
        }

        public static readonly Dictionary<int, string> PersonaTable = new();
        public static readonly Dictionary<int, string> SkillTable = new();

        public static void LoadLookupTables()
        {
            PersonaTable.Clear();
            foreach (var row in CsvUtil.ReadEmbeddedDataRows("Data.personas.csv"))
                PersonaTable[int.Parse(row[0])] = row[2];

            SkillTable.Clear();
            foreach (var row in CsvUtil.ReadEmbeddedDataRows("Data.skills.csv"))
                SkillTable[int.Parse(row[0])] = row[2];
        }

        public static readonly uint[] PersonaSlotBases =
        {
            0x836BAC, 0x836BE0, 0x836C14, 0x836C48, 0x836C7C, 0x836CB0,
            0x836CE4, 0x836D18, 0x836D4C, 0x836D80, 0x836DB4, 0x836DE8,
        };

        public const uint PersonaOffActivate = 0x00;
        public const uint PersonaOffMod = 0x02;
        public const uint PersonaOffLevel = 0x04;
        public const uint PersonaOffExp = 0x08;
        public static readonly uint[] PersonaOffSkills = { 0x0C, 0x0E, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A };
        public const uint PersonaOffSt = 0x1C;
        public const uint PersonaOffMa = 0x1D;
        public const uint PersonaOffEn = 0x1E;
        public const uint PersonaOffAg = 0x1F;
        public const uint PersonaOffLu = 0x20;

        public const int PersonaLevelMax = 0x63;
        public const uint PersonaExpMax = 0x0098967F;
        public const int PersonaStatMax = 0x63;

        public static uint PersonaBaseFor(int slotIndex)
        {
            if (slotIndex < 1 || slotIndex > 12) throw new ArgumentOutOfRangeException(nameof(slotIndex), "slotIndex must be 1-12");
            return PersonaSlotBases[slotIndex - 1];
        }

        public const uint CompendiumTableBase = 0x00836E1C;
        public const uint CompendiumEntryStride = 0x34;

        public static uint CompendiumEntryAddress(int personaId) =>
            (uint)(CompendiumTableBase + personaId * CompendiumEntryStride);

        public const uint PersonaTypeFlagsTableBase = 0x007CE420;
        public const int PersonaTypeFlagsStride = 0x0E;
        public const int PersonaTypeFlagsCompendiumExcludeMask = 0x28;

        public static uint PersonaTypeFlagsAddress(int personaId) =>
            (uint)(PersonaTypeFlagsTableBase + personaId * PersonaTypeFlagsStride);

        public static string McDisplayName(string scenario) => scenario == "The Journey" ? "Makoto" : "Aigis";

        public const uint McAcademicsAddress = 0x00836260;
        public const uint McCharmAddress = 0x00836262;
        public const uint McCourageAddress = 0x00836264;
        public const int McSocialStatMax = 999;
        public const uint McHpSpAddress = 0x0083622C;
        public const uint McExpAddress = 0x00836268;
        public const uint McExpMax = 0x0098967F;

        public const int NameFieldLength = 8;
        public const uint SurnameFieldAddress = 0x00836201;
        public const uint GivenNameFieldAddress = 0x00836213;

        public static byte CharToNameCode(char c)
        {
            if (c == ' ' || !char.IsLetter(c)) return 0x00;
            bool lower = char.IsLower(c);
            char upper = char.ToUpperInvariant(c);
            int idx = upper - 'A';
            if (idx < 0 || idx > 25) return 0x00;
            byte code = idx < 15 ? (byte)(0xA1 + idx) : (byte)(0xB0 + (idx - 15));
            if (lower) code += 0x20;
            return code;
        }

        public static char NameCodeToChar(byte code)
        {
            if (code == 0) return ' ';
            bool lower = (code & 0xF0) == 0xC0 || (code & 0xF0) == 0xD0;
            byte baseCode = lower ? (byte)(code - 0x20) : code;
            int idx;
            if ((baseCode & 0xF0) == 0xA0) idx = (baseCode & 0x0F) - 1;
            else if ((baseCode & 0xF0) == 0xB0) idx = 15 + (baseCode & 0x0F);
            else return ' ';
            if (idx < 0 || idx > 25) return ' ';
            char c = (char)('A' + idx);
            return lower ? char.ToLowerInvariant(c) : c;
        }

        public static byte[] EncodeNameField(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length > NameFieldLength)
                throw new ArgumentException($"Name length must be between 1 and {NameFieldLength} characters.");
            var codes = new byte[NameFieldLength];
            for (int i = 0; i < word.Length; i++)
                codes[i] = CharToNameCode(word[i]);
            return codes;
        }

        public static string DecodeNameField(byte[] codes)
        {
            if (codes.Length != NameFieldLength) throw new ArgumentException($"Codes array must be length {NameFieldLength}.");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < NameFieldLength; i++)
            {
                if (codes[i] == 0) continue;
                sb.Append(NameCodeToChar(codes[i]));
            }
            return sb.ToString().Trim();
        }
    }
}
