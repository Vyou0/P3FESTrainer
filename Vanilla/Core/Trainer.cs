using System;
using System.Collections.Generic;
using System.Linq;
using P3FESTrainer.Data;
using P3FESTrainer.Pine;

namespace P3FESTrainer.Core
{
    public record OwnedItemRow(int Id, string Name, int Qty);
    public record OwnedEquipmentRow(uint Address, int ItemId, string Name, uint PictureMod, ushort RawStat);

    public record PersonaInfo(
        int Active, int PersonaId, int Level, uint Exp,
        int[] Skills, int St, int Ma, int En, int Ag, int Lu);

    public record McSocialStatsInfo(int Academics, int Charm, int Courage);

    public record PartyMemberInfo(
        int SlotId, int Hp, int Sp, uint Exp,
        int StatSt, int StatMa, int StatEn, int StatAg,
        int PersonaLevel, uint PersonaExp,
        int PSt, int PMa, int PEn, int PAg, int PLu,
        int PersonaActivate, int PersonaId, int[] PersonaSkills);

    public record PartyConfiguration(int Slot2, int Slot3, int Slot4);

    public class Trainer
    {
        public const uint YenAddress = 0x0083A6DC;

        private readonly PineClient _client;
        private readonly GameData _data;

        public Trainer(PineClient client, GameData data)
        {
            _client = client;
            _data = data;
        }

        // Yen
        public uint GetYen() => _client.Read32(YenAddress);

        public void SetYen(uint amount) => _client.Write32(YenAddress, amount);

        // Items
        public int GetItemQty(int itemId) => _client.Read8(RequireItem(itemId).Address);

        public void SetItemQty(int itemId, int qty)
        {
            if (qty < 0 || qty > 255) throw new ArgumentOutOfRangeException(nameof(qty), "qty must be between 0 and 255");
            _client.Write8(RequireItem(itemId).Address, (byte)qty);
        }

        private Item RequireItem(int itemId)
        {
            var item = _data.Items.FirstOrDefault(it => it.Id == itemId);
            if (item == null) throw new KeyNotFoundException($"Item with ID {itemId} not found.");
            return item;
        }

        public List<OwnedItemRow> ListOwnedItems()
        {
            List<byte> quantities;
            try
            {
                var b = _client.Batch();
                foreach (var it in _data.Items) b.Read8(it.Address);
                quantities = b.Execute().Select(o => Convert.ToByte(o)).ToList();
            }
            catch (Exception)
            {
                quantities = _data.Items.Select(it => _client.Read8(it.Address)).ToList();
            }
            var outRows = new List<OwnedItemRow>();
            for (int i = 0; i < _data.Items.Count; i++)
            {
                if (quantities[i] > 0)
                    outRows.Add(new OwnedItemRow(_data.Items[i].Id, _data.Items[i].Name, quantities[i]));
            }
            return outRows;
        }

        // Equipment
        public uint? FindFreeEquipmentSlot()
        {
            var addrs = Enumerable.Range(0, EquipmentInventory.MaxSlots)
                .Select(i => EquipmentInventory.Base + (uint)i * EquipmentInventory.Stride)
                .ToList();
            List<ushort> ids;
            try
            {
                var b = _client.Batch();
                foreach (var addr in addrs) b.Read16(addr + EquipmentInventory.OffId);
                ids = b.Execute().Select(o => Convert.ToUInt16(o)).ToList();
            }
            catch (Exception)
            {
                ids = addrs.Select(addr => _client.Read16(addr + EquipmentInventory.OffId)).ToList();
            }
            for (int i = 0; i < addrs.Count; i++)
                if (ids[i] == 0) return addrs[i];
            return null;
        }

        public uint PictureModFor(EquipSlot category, int itemId)
        {
            if (category == EquipSlot.Weapon)
            {
                _data.Weapons.TryGetValue(itemId, out var entry);
                return entry?.WeaponTypeId != null ? (uint)entry.WeaponTypeId.Value : 0;
            }
            return category switch
            {
                EquipSlot.ArmorBody => EquipmentInventory.PictureArmorBody,
                EquipSlot.ArmorFeet => EquipmentInventory.PictureArmorFeet,
                EquipSlot.Accessory => EquipmentInventory.PictureAccessory,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
        }

        public (int attack, int hit)? WeaponStatsFor(int itemId) =>
            _data.Weapons.TryGetValue(itemId, out var w) ? (w.Attack, w.Accuracy) : null;

        public int? ArmorBodyStatFor(int itemId) =>
            _data.ArmorBody.TryGetValue(itemId, out var a) ? a.Defence : null;

        public int? ArmorFeetStatFor(int itemId) =>
            _data.ArmorFeet.TryGetValue(itemId, out var a) ? a.Evasion : null;

        public int AttributeIdFor(EquipSlot category, int itemId) => category switch
        {
            EquipSlot.Weapon => _data.Weapons.TryGetValue(itemId, out var w) ? w.AttributeId : 0,
            EquipSlot.ArmorBody => _data.ArmorBody.TryGetValue(itemId, out var ab) ? ab.AttributeId : 0,
            EquipSlot.ArmorFeet => _data.ArmorFeet.TryGetValue(itemId, out var af) ? af.AttributeId : 0,
            EquipSlot.Accessory => _data.Accessory.TryGetValue(itemId, out var ac) ? ac.AttributeId : 0,
            _ => 0,
        };

        // Equipment Stats
        public int ReadEquipmentAttribute(uint address) => _client.Read8(address + EquipmentInventory.OffAttribute);

        public void WriteEquipmentAttribute(uint address, int attributeId) =>
            _client.Write8(address + EquipmentInventory.OffAttribute, (byte)attributeId);

        public (int attack, int hit) ReadWeaponInstanceStats(uint address) =>
            (_client.Read16(address + EquipmentInventory.OffAttack), _client.Read16(address + EquipmentInventory.OffHit));

        public int ReadArmorBodyInstanceStat(uint address) => _client.Read16(address + EquipmentInventory.OffDefence);

        public int ReadArmorFeetInstanceStat(uint address) => _client.Read16(address + EquipmentInventory.OffEvasion);

        public void WriteWeaponInstanceStats(uint address, int attack, int hit)
        {
            _client.Write16(address + EquipmentInventory.OffAttack, (ushort)attack);
            _client.Write16(address + EquipmentInventory.OffHit, (ushort)hit);
        }

        public void WriteArmorBodyInstanceStat(uint address, int defence) =>
            _client.Write16(address + EquipmentInventory.OffDefence, (ushort)defence);

        public void WriteArmorFeetInstanceStat(uint address, int evasion) =>
            _client.Write16(address + EquipmentInventory.OffEvasion, (ushort)evasion);

        public uint GiveEquipment(EquipSlot category, int itemId, bool writeStats = true)
        {
            uint? addr = FindFreeEquipmentSlot();
            if (addr == null)
                throw new InvalidOperationException("No empty equipment slot available in inventory.");
            uint a = addr.Value;
            _client.Write16(a + EquipmentInventory.OffId, (ushort)itemId);
            _client.Write32(a + EquipmentInventory.OffPicture, PictureModFor(category, itemId));
            if (writeStats)
            {
                switch (category)
                {
                    case EquipSlot.Weapon:
                        var ws = WeaponStatsFor(itemId);
                        if (ws != null)
                        {
                            _client.Write16(a + EquipmentInventory.OffAttack, (ushort)ws.Value.attack);
                            _client.Write16(a + EquipmentInventory.OffHit, (ushort)ws.Value.hit);
                        }
                        break;
                    case EquipSlot.ArmorBody:
                        var df = ArmorBodyStatFor(itemId);
                        if (df != null) _client.Write16(a + EquipmentInventory.OffDefence, (ushort)df.Value);
                        break;
                    case EquipSlot.ArmorFeet:
                        var ev = ArmorFeetStatFor(itemId);
                        if (ev != null) _client.Write16(a + EquipmentInventory.OffEvasion, (ushort)ev.Value);
                        break;
                }
                _client.Write8(a + EquipmentInventory.OffAttribute, (byte)AttributeIdFor(category, itemId));
            }
            return a;
        }

        private string LookupEquipmentName(int itemId)
        {
            if (_data.Weapons.TryGetValue(itemId, out var w)) return w.Name;
            if (_data.ArmorBody.TryGetValue(itemId, out var ab)) return ab.Name;
            if (_data.ArmorFeet.TryGetValue(itemId, out var af)) return af.Name;
            if (_data.Accessory.TryGetValue(itemId, out var ac)) return ac.Name;
            return $"0x{itemId:X4}";
        }

        public EquipSlot? CategoryForEquipmentId(int itemId)
        {
            if (_data.Weapons.ContainsKey(itemId)) return EquipSlot.Weapon;
            if (_data.ArmorBody.ContainsKey(itemId)) return EquipSlot.ArmorBody;
            if (_data.ArmorFeet.ContainsKey(itemId)) return EquipSlot.ArmorFeet;
            if (_data.Accessory.ContainsKey(itemId)) return EquipSlot.Accessory;
            return null;
        }

        public List<OwnedEquipmentRow> ListOwnedEquipment()
        {
            var addrs = Enumerable.Range(0, EquipmentInventory.MaxSlots)
                .Select(i => EquipmentInventory.Base + (uint)i * EquipmentInventory.Stride)
                .ToList();
            List<ushort> ids;
            try
            {
                var b = _client.Batch();
                foreach (var addr in addrs) b.Read16(addr + EquipmentInventory.OffId);
                ids = b.Execute().Select(o => Convert.ToUInt16(o)).ToList();
            }
            catch (Exception)
            {
                ids = addrs.Select(addr => _client.Read16(addr + EquipmentInventory.OffId)).ToList();
            }

            var occupied = new List<(uint addr, ushort val)>();
            for (int i = 0; i < addrs.Count; i++)
                if (ids[i] != 0) occupied.Add((addrs[i], ids[i]));
            if (occupied.Count == 0) return new List<OwnedEquipmentRow>();

            List<object?> details;
            try
            {
                var b = _client.Batch();
                foreach (var (addr, _) in occupied)
                {
                    b.Read32(addr + EquipmentInventory.OffPicture);
                    b.Read16(addr + EquipmentInventory.OffAttack);
                }
                details = b.Execute();
            }
            catch (Exception)
            {
                details = new List<object?>();
                foreach (var (addr, _) in occupied)
                {
                    details.Add(_client.Read32(addr + EquipmentInventory.OffPicture));
                    details.Add(_client.Read16(addr + EquipmentInventory.OffAttack));
                }
            }

            var outRows = new List<OwnedEquipmentRow>();
            for (int i = 0; i < occupied.Count; i++)
            {
                var (addr, val) = occupied[i];
                uint pic = Convert.ToUInt32(details[i * 2]);
                ushort rawStat = Convert.ToUInt16(details[i * 2 + 1]);
                outRows.Add(new OwnedEquipmentRow(addr, val, LookupEquipmentName(val), pic, rawStat));
            }
            return outRows;
        }

        // Persona Roster
        public long ExpForPersonaLevel(int personaId, int level)
        {
            if (MainCharacterData.IsSpecialPersonaId(personaId))
                return PartyMemberData.ExpForLevel(level);
            uint tableBase = _client.Read32(MainCharacterData.PersonaGrowthTablePointer);
            byte growthByte = _client.Read8(MainCharacterData.PersonaGrowthByteAddress(personaId, tableBase));
            return MainCharacterData.ExpForPersonaLevel(level, growthByte);
        }

        public string? SetPersona(
            int slotIndex, int personaId, int level, uint? exp = null, int[]? skills = null,
            int st = MainCharacterData.PersonaStatMax, int ma = MainCharacterData.PersonaStatMax,
            int en = MainCharacterData.PersonaStatMax, int ag = MainCharacterData.PersonaStatMax,
            int lu = MainCharacterData.PersonaStatMax, bool activate = true,
            bool registerCompendium = true, bool forceCompendium = false)
        {
            uint expValue = exp ?? (uint)ExpForPersonaLevel(personaId, level);
            skills ??= new int[8];
            if (skills.Length != 8) throw new ArgumentException("Skills array must contain exactly 8 skill IDs.");

            uint baseAddr = MainCharacterData.PersonaBaseFor(slotIndex);
            var b = _client.Batch();
            b.Write16(baseAddr + MainCharacterData.PersonaOffActivate, (ushort)(activate ? 1 : 0));
            b.Write16(baseAddr + MainCharacterData.PersonaOffMod, (ushort)personaId);
            b.Write8(baseAddr + MainCharacterData.PersonaOffLevel, (byte)level);
            b.Write32(baseAddr + MainCharacterData.PersonaOffExp, expValue);
            for (int i = 0; i < 8; i++)
                b.Write16(baseAddr + MainCharacterData.PersonaOffSkills[i], (ushort)skills[i]);
            b.Write8(baseAddr + MainCharacterData.PersonaOffSt, (byte)st);
            b.Write8(baseAddr + MainCharacterData.PersonaOffMa, (byte)ma);
            b.Write8(baseAddr + MainCharacterData.PersonaOffEn, (byte)en);
            b.Write8(baseAddr + MainCharacterData.PersonaOffAg, (byte)ag);
            b.Write8(baseAddr + MainCharacterData.PersonaOffLu, (byte)lu);
            b.Execute();

            if (registerCompendium)
                return RegisterInCompendium(personaId, level, expValue, skills, st, ma, en, ag, lu, activate, forceCompendium);
            return null;
        }

        public PersonaInfo ReadPersona(int slotIndex)
        {
            uint baseAddr = MainCharacterData.PersonaBaseFor(slotIndex);
            var b = _client.Batch();
            b.Read16(baseAddr + MainCharacterData.PersonaOffActivate);
            b.Read16(baseAddr + MainCharacterData.PersonaOffMod);
            b.Read8(baseAddr + MainCharacterData.PersonaOffLevel);
            b.Read32(baseAddr + MainCharacterData.PersonaOffExp);
            foreach (var off in MainCharacterData.PersonaOffSkills) b.Read16(baseAddr + off);
            b.Read8(baseAddr + MainCharacterData.PersonaOffSt);
            b.Read8(baseAddr + MainCharacterData.PersonaOffMa);
            b.Read8(baseAddr + MainCharacterData.PersonaOffEn);
            b.Read8(baseAddr + MainCharacterData.PersonaOffAg);
            b.Read8(baseAddr + MainCharacterData.PersonaOffLu);
            var r = b.Execute();
            int active = Convert.ToUInt16(r[0]);
            int personaId = Convert.ToUInt16(r[1]);
            int level = Convert.ToByte(r[2]);
            uint exp = Convert.ToUInt32(r[3]);
            var skills = new int[8];
            for (int i = 0; i < 8; i++) skills[i] = Convert.ToUInt16(r[4 + i]);
            int st = Convert.ToByte(r[12]), ma = Convert.ToByte(r[13]), en = Convert.ToByte(r[14]), ag = Convert.ToByte(r[15]), lu = Convert.ToByte(r[16]);
            return new PersonaInfo(active, personaId, level, exp, skills, st, ma, en, ag, lu);
        }

        // Compendium Registration
        public string RegisterInCompendium(
            int personaId, int level, uint exp, int[] skills, int st, int ma, int en, int ag, int lu,
            bool activate = true, bool force = false)
        {
            int typeFlags = _client.Read16(MainCharacterData.PersonaTypeFlagsAddress(personaId));
            if ((typeFlags & MainCharacterData.PersonaTypeFlagsCompendiumExcludeMask) != 0)
                return "skipped_excluded_type";

            if (!force)
            {
                int existingFlags = _client.Read16(MainCharacterData.CompendiumEntryAddress(personaId) + MainCharacterData.PersonaOffActivate);
                if ((existingFlags & 1) != 0) return "skipped_already_registered";
            }

            uint baseAddr = MainCharacterData.CompendiumEntryAddress(personaId);
            skills ??= new int[8];
            if (skills.Length != 8) throw new ArgumentException("Skills array must contain exactly 8 skill IDs.");
            var b = _client.Batch();
            b.Write16(baseAddr + MainCharacterData.PersonaOffActivate, (ushort)(activate ? 1 : 0));
            b.Write16(baseAddr + MainCharacterData.PersonaOffMod, (ushort)personaId);
            b.Write8(baseAddr + MainCharacterData.PersonaOffLevel, (byte)level);
            b.Write32(baseAddr + MainCharacterData.PersonaOffExp, exp);
            for (int i = 0; i < 8; i++)
                b.Write16(baseAddr + MainCharacterData.PersonaOffSkills[i], (ushort)skills[i]);
            b.Write8(baseAddr + MainCharacterData.PersonaOffSt, (byte)st);
            b.Write8(baseAddr + MainCharacterData.PersonaOffMa, (byte)ma);
            b.Write8(baseAddr + MainCharacterData.PersonaOffEn, (byte)en);
            b.Write8(baseAddr + MainCharacterData.PersonaOffAg, (byte)ag);
            b.Write8(baseAddr + MainCharacterData.PersonaOffLu, (byte)lu);
            b.Execute();
            return "registered";
        }

        // MC Social Stats
        public McSocialStatsInfo GetMcSocialStats()
        {
            var b = _client.Batch();
            b.Read16(MainCharacterData.McAcademicsAddress);
            b.Read16(MainCharacterData.McCharmAddress);
            b.Read16(MainCharacterData.McCourageAddress);
            var r = b.Execute();
            return new McSocialStatsInfo(Convert.ToUInt16(r[0]), Convert.ToUInt16(r[1]), Convert.ToUInt16(r[2]));
        }

        public void SetMcSocialStats(int academics, int charm, int courage)
        {
            foreach (var (name, v) in new[] { ("academics", academics), ("charm", charm), ("courage", courage) })
                if (v < 0 || v > MainCharacterData.McSocialStatMax)
                    throw new ArgumentOutOfRangeException(name, $"Value must be between 0 and {MainCharacterData.McSocialStatMax}");
            var b = _client.Batch();
            b.Write16(MainCharacterData.McAcademicsAddress, (ushort)academics);
            b.Write16(MainCharacterData.McCharmAddress, (ushort)charm);
            b.Write16(MainCharacterData.McCourageAddress, (ushort)courage);
            b.Execute();
        }

        // Party Configuration
        public PartyConfiguration GetPartyConfiguration()
        {
            var b = _client.Batch();
            b.Read16(PartyMemberData.PartySlot2Address);
            b.Read16(PartyMemberData.PartySlot3Address);
            b.Read16(PartyMemberData.PartySlot4Address);
            var r = b.Execute();
            return new PartyConfiguration(Convert.ToUInt16(r[0]), Convert.ToUInt16(r[1]), Convert.ToUInt16(r[2]));
        }

        public void SetPartyConfiguration(int slot2, int slot3, int slot4)
        {
            var b = _client.Batch();
            b.Write16(PartyMemberData.PartySlot2Address, (ushort)slot2);
            b.Write16(PartyMemberData.PartySlot3Address, (ushort)slot3);
            b.Write16(PartyMemberData.PartySlot4Address, (ushort)slot4);
            b.Execute();
        }

        // Party Members
        public PartyMemberInfo ReadPartyMember(CharacterSlot slot)
        {
            uint baseAddr = slot.BaseAddress;
            var b = _client.Batch();
            b.Read16(baseAddr + PartyMemberData.OffHp);
            b.Read16(baseAddr + PartyMemberData.OffSp);
            b.Read32(baseAddr + PartyMemberData.OffExp);
            b.Read16(baseAddr + PartyMemberData.OffStatSt);
            b.Read16(baseAddr + PartyMemberData.OffStatMa);
            b.Read16(baseAddr + PartyMemberData.OffStatEn);
            b.Read16(baseAddr + PartyMemberData.OffStatAg);
            b.Read8(baseAddr + PartyMemberData.OffPersonaLevel);
            b.Read32(baseAddr + PartyMemberData.OffPersonaExp);
            b.Read8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 0);
            b.Read8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 1);
            b.Read8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 2);
            b.Read8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 3);
            b.Read8(baseAddr + PartyMemberData.OffPersonaStatLu);
            b.Read16(baseAddr + PartyMemberData.OffPersonaActivate);
            b.Read16(baseAddr + PartyMemberData.OffPersonaMod);
            foreach (var off in PartyMemberData.OffPersonaSkills) b.Read16(baseAddr + off);
            var r = b.Execute();
            var skills = new int[8];
            for (int i = 0; i < 8; i++) skills[i] = Convert.ToUInt16(r[16 + i]);
            return new PartyMemberInfo(
                slot.SlotId,
                Convert.ToUInt16(r[0]), Convert.ToUInt16(r[1]), Convert.ToUInt32(r[2]),
                Convert.ToUInt16(r[3]), Convert.ToUInt16(r[4]), Convert.ToUInt16(r[5]), Convert.ToUInt16(r[6]),
                Convert.ToByte(r[7]), Convert.ToUInt32(r[8]),
                Convert.ToByte(r[9]), Convert.ToByte(r[10]), Convert.ToByte(r[11]), Convert.ToByte(r[12]), Convert.ToByte(r[13]),
                Convert.ToUInt16(r[14]), Convert.ToUInt16(r[15]), skills);
        }

        public void SetPartyMemberPersona(
            CharacterSlot slot, int level, uint exp, int pSt, int pMa, int pEn, int pAg, int pLu,
            int[]? skills = null, int? personaId = null, bool? activate = null)
        {
            uint baseAddr = slot.BaseAddress;
            var b = _client.Batch();
            b.Write8(baseAddr + PartyMemberData.OffPersonaLevel, (byte)level);
            b.Write32(baseAddr + PartyMemberData.OffPersonaExp, exp);
            b.Write8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 0, (byte)pSt);
            b.Write8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 1, (byte)pMa);
            b.Write8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 2, (byte)pEn);
            b.Write8(baseAddr + PartyMemberData.OffPersonaStatsPacked + 3, (byte)pAg);
            b.Write8(baseAddr + PartyMemberData.OffPersonaStatLu, (byte)pLu);
            if (activate != null)
                b.Write16(baseAddr + PartyMemberData.OffPersonaActivate, (ushort)(activate.Value ? 1 : 0));
            if (personaId != null)
                b.Write16(baseAddr + PartyMemberData.OffPersonaMod, (ushort)personaId.Value);
            if (skills != null)
            {
                if (skills.Length != 8) throw new ArgumentException("Skills array must contain exactly 8 skill IDs.");
                for (int i = 0; i < 8; i++)
                    b.Write16(baseAddr + PartyMemberData.OffPersonaSkills[i], (ushort)skills[i]);
            }
            b.Execute();
        }

        // Character EXP
        public uint GetMcExp() => _client.Read32(MainCharacterData.McExpAddress);
        public void SetMcExp(uint exp) => _client.Write32(MainCharacterData.McExpAddress, exp);

        // HP/SP
        public const uint McMaxHpSpTableBase = 0x005DC1E4;
        public const uint McMaxHpSpTableStride = 0x2C;

        public (int hp, int sp) GetMcHpSp()
        {
            var b = _client.Batch();
            b.Read16(MainCharacterData.McHpSpAddress);
            b.Read16(MainCharacterData.McHpSpAddress + 2);
            var r = b.Execute();
            return (Convert.ToUInt16(r[0]), Convert.ToUInt16(r[1]));
        }

        public void SetMcHpSp(int hp, int sp)
        {
            var b = _client.Batch();
            b.Write16(MainCharacterData.McHpSpAddress, (ushort)hp);
            b.Write16(MainCharacterData.McHpSpAddress + 2, (ushort)sp);

            int level = 1;
            try
            {
                uint mcExp = GetMcExp();
                level = Math.Max(1, Math.Min(99, MainCharacterData.LevelForExp(mcExp)));
            }
            catch { }

            uint tableAddr = McMaxHpSpTableBase + (uint)((level - 1) * McMaxHpSpTableStride);
            b.Write16(tableAddr + 0, (ushort)hp);
            b.Write16(tableAddr + 2, (ushort)sp);
            b.Execute();
        }

        public void SetPartyMemberHpSp(CharacterSlot slot, int hp, int sp)
        {
            var b = _client.Batch();
            b.Write16(slot.BaseAddress + PartyMemberData.OffHp, (ushort)hp);
            b.Write16(slot.BaseAddress + PartyMemberData.OffSp, (ushort)sp);
            b.Write16(slot.BaseAddress + PartyMemberData.OffMaxHp, (ushort)hp);
            b.Write16(slot.BaseAddress + PartyMemberData.OffMaxSp, (ushort)sp);
            b.Execute();
        }

        // Character Equipment
        public int ReadCharacterEquip(CharacterSlot slot, EquipSlot category) => _client.Read16(slot.BaseFor(category));

        public void SetCharacterEquip(CharacterSlot slot, EquipSlot category, int itemId, bool writeStats = true)
        {
            uint baseAddr = slot.BaseFor(category);
            _client.Write16(baseAddr + EquipmentInventory.OffId, (ushort)itemId);
            if (writeStats)
            {
                switch (category)
                {
                    case EquipSlot.Weapon:
                        var ws = WeaponStatsFor(itemId);
                        if (ws != null)
                        {
                            _client.Write16(baseAddr + EquipmentInventory.OffAttack, (ushort)ws.Value.attack);
                            _client.Write16(baseAddr + EquipmentInventory.OffHit, (ushort)ws.Value.hit);
                        }
                        break;
                    case EquipSlot.ArmorBody:
                        var df = ArmorBodyStatFor(itemId);
                        if (df != null) _client.Write16(baseAddr + EquipmentInventory.OffDefence, (ushort)df.Value);
                        break;
                    case EquipSlot.ArmorFeet:
                        var ev = ArmorFeetStatFor(itemId);
                        if (ev != null) _client.Write16(baseAddr + EquipmentInventory.OffEvasion, (ushort)ev.Value);
                        break;
                }
                _client.Write8(baseAddr + EquipmentInventory.OffAttribute, (byte)AttributeIdFor(category, itemId));
            }
        }

        // Protagonist Name
        private byte[] ReadNameField(uint address)
        {
            var b = _client.Batch();
            for (int i = 0; i < MainCharacterData.NameFieldLength; i++) b.Read16(address + (uint)(i * 2));
            var r = b.Execute();
            var codes = new byte[MainCharacterData.NameFieldLength];
            for (int i = 0; i < codes.Length; i++) codes[i] = (byte)(Convert.ToUInt16(r[i]) & 0xFF);
            return codes;
        }

        private void WriteNameField(uint address, string word)
        {
            var codes = MainCharacterData.EncodeNameField(word);
            var b = _client.Batch();
            for (int i = 0; i < codes.Length; i++)
            {
                ushort val = 0;
                if (codes[i] != 0)
                {
                    bool isLastChar = (i == word.Length - 1);
                    val = isLastChar ? (ushort)codes[i] : (ushort)(0x8000 | codes[i]);
                }
                b.Write16(address + (uint)(i * 2), val);
            }
            b.Execute();
        }

        public string GetMcSurname() => MainCharacterData.DecodeNameField(ReadNameField(MainCharacterData.SurnameFieldAddress));
        public string GetMcGivenName() => MainCharacterData.DecodeNameField(ReadNameField(MainCharacterData.GivenNameFieldAddress));
        public void SetMcSurname(string name) => WriteNameField(MainCharacterData.SurnameFieldAddress, name);
        public void SetMcGivenName(string name) => WriteNameField(MainCharacterData.GivenNameFieldAddress, name);
    }
}
