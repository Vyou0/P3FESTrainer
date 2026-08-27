using System;
using System.Collections.Generic;

namespace P3FESTrainer.Data
{
    /// <summary>
    /// Memory layout and experience table definitions for party members.
    /// </summary>
    public static class PartyMemberData
    {
        public static readonly Dictionary<int, long> PartyExpTable = new();

        public static void LoadPartyExpTable()
        {
            PartyExpTable.Clear();
            foreach (var row in CsvUtil.ReadEmbeddedDataRows("Data.party_exp_table.csv"))
            {
                if (row.Length < 2) continue;
                if (!int.TryParse(row[0], out int level)) continue;

                long exp = 0;
                long confirmed = 0;
                bool hasConfirmed = row.Length >= 3 && !string.IsNullOrWhiteSpace(row[2]) && long.TryParse(row[2], out confirmed);
                if (hasConfirmed)
                {
                    exp = confirmed;
                }
                else if (long.TryParse(row[1], out long estimate))
                {
                    exp = estimate;
                }

                PartyExpTable[level] = exp;
            }
        }

        public static long ExpForLevel(int level)
        {
            if (level < 1 || level > 99) throw new ArgumentOutOfRangeException(nameof(level), "level must be 1-99");
            if (PartyExpTable.Count == 0) LoadPartyExpTable();
            return PartyExpTable[level];
        }

        public static int LevelForExp(long exp)
        {
            if (PartyExpTable.Count == 0) LoadPartyExpTable();
            int level = 0;
            for (int lvl = 1; lvl <= 99; lvl++)
            {
                if (exp < PartyExpTable[lvl]) break;
                level = lvl;
            }
            return level == 0 ? 1 : level;
        }

        public const uint OffHp = 0x1C;
        public const uint OffSp = 0x1E;
        public const uint OffMaxHp = 0x20;
        public const uint OffMaxSp = 0x22;

        public const uint OffExp = 0x58;
        public const uint ExpMax = 0x0098967F;

        public const uint OffConditionA = 0x5C;
        public const uint OffConditionB = 0x60;

        public const uint OffStatSt = 0x76;
        public const uint OffStatMa = 0x78;
        public const uint OffStatEn = 0x8E;
        public const uint OffStatAg = 0xA4;
        public const int StatMax = 0x3E7;

        public const uint OffPersonaLevel = 0xF0;
        public const uint OffPersonaExp = 0xF4;
        public const int PersonaLevelMax = 0x63;
        public const uint PersonaExpMax = 0x0098967F;

        public const uint OffPersonaStatsPacked = 0x108;
        public const uint OffPersonaStatLu = 0x10C;
        public const int PersonaStatMax = 0x63;

        public const uint PartyPersonaStructBase = 0xEC;
        public const uint OffPersonaActivate = PartyPersonaStructBase + 0x00;
        public const uint OffPersonaMod = PartyPersonaStructBase + 0x02;
        public static readonly uint[] OffPersonaSkills =
        {
            PartyPersonaStructBase + 0x0C, PartyPersonaStructBase + 0x0E,
            PartyPersonaStructBase + 0x10, PartyPersonaStructBase + 0x12,
            PartyPersonaStructBase + 0x14, PartyPersonaStructBase + 0x16,
            PartyPersonaStructBase + 0x18, PartyPersonaStructBase + 0x1A,
        };

        public const uint PartySlot2Address = 0x0083A6E0;
        public const uint PartySlot3Address = 0x0083A6E2;
        public const uint PartySlot4Address = 0x0083A6E4;
    }
}
