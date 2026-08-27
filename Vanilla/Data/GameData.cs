using System;
using System.Collections.Generic;
using System.Linq;

namespace P3FESTrainer.Data
{
    public enum EquipSlot { Weapon, ArmorBody, ArmorFeet, Accessory }

    public class Item
    {
        public int Id;
        public string Name = "";
        public uint Address;
    }

    public class CharacterSlot
    {
        public int SlotId;
        public string NameJourney = "N/A";
        public string NameAnswer = "N/A";
        public uint WeaponAddr, ArmorBodyAddr, ArmorFeetAddr, AccessoryAddr;

        public uint BaseFor(EquipSlot slot) => slot switch
        {
            EquipSlot.Weapon => WeaponAddr,
            EquipSlot.ArmorBody => ArmorBodyAddr,
            EquipSlot.ArmorFeet => ArmorFeetAddr,
            EquipSlot.Accessory => AccessoryAddr,
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };

        public string DisplayName(string scenario) => scenario == "The Journey" ? NameJourney : NameAnswer;

        public uint BaseAddress => WeaponAddr - 0x6C;
    }

    public class WeaponEntry
    {
        public int Id;
        public string Name = "";
        public int? WeaponTypeId;
        public string WeaponTypeName = "";
        public int Attack;
        public int Accuracy;
        public int AttributeId;
    }

    public class ArmorBodyEntry
    {
        public int Id;
        public string Name = "";
        public int Defence;
        public int AttributeId;
    }

    public class ArmorFeetEntry
    {
        public int Id;
        public string Name = "";
        public int Evasion;
        public int AttributeId;
    }

    public class AccessoryEntry
    {
        public int Id;
        public string Name = "";
        public int AttributeId;
    }

    public class GameData
    {
        public List<Item> Items { get; } = new();
        public List<CharacterSlot> Characters { get; } = new();
        public Dictionary<int, WeaponEntry> Weapons { get; } = new();
        public Dictionary<int, ArmorBodyEntry> ArmorBody { get; } = new();
        public Dictionary<int, ArmorFeetEntry> ArmorFeet { get; } = new();
        public Dictionary<int, AccessoryEntry> Accessory { get; } = new();
        public Dictionary<int, string> Attributes { get; } = new();
        public Dictionary<string, int> EquipmentTypes { get; } = new();
        public Dictionary<string, int> WeaponTypes { get; } = new();

        public GameData()
        {
            LoadItems("items.csv");
            LoadCharacters("characters.csv");
            LoadWeapons("weapons.csv");
            LoadArmorBody("armor_body.csv");
            LoadArmorFeet("armor_feet.csv");
            LoadAccessory("accessory.csv");
            LoadAttributes("attributes.csv");
            LoadNameIdTable("equipment_types.csv", EquipmentTypes);
            LoadNameIdTable("weapon_types.csv", WeaponTypes);
        }

        private List<string[]> ReadRows(string fileName)
        {
            return CsvUtil.ReadEmbeddedDataRows("Data." + fileName);
        }

        private void LoadItems(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                int id = int.Parse(row[0]);
                Items.Add(new Item
                {
                    Id = id,
                    Name = row[1],
                    Address = ItemInventory.AddressFor(id),
                });
            }
        }

        private void LoadCharacters(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                Characters.Add(new CharacterSlot
                {
                    SlotId = int.Parse(row[0]),
                    NameJourney = row[1],
                    NameAnswer = row[2],
                    WeaponAddr = CsvUtil.ParseHexAddress(row[3]),
                    ArmorBodyAddr = CsvUtil.ParseHexAddress(row[4]),
                    ArmorFeetAddr = CsvUtil.ParseHexAddress(row[5]),
                    AccessoryAddr = CsvUtil.ParseHexAddress(row[6]),
                });
            }
        }

        private void LoadWeapons(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                int id = int.Parse(row[0]);
                Weapons[id] = new WeaponEntry
                {
                    Id = id,
                    Name = row[1],
                    WeaponTypeId = string.IsNullOrEmpty(row[2]) ? null : int.Parse(row[2]),
                    WeaponTypeName = row[3],
                    Attack = int.Parse(row[4]),
                    Accuracy = int.Parse(row[5]),
                    AttributeId = row.Length > 6 && !string.IsNullOrEmpty(row[6]) ? int.Parse(row[6]) : 0,
                };
            }
        }

        private void LoadArmorBody(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                int id = int.Parse(row[0]);
                ArmorBody[id] = new ArmorBodyEntry
                {
                    Id = id,
                    Name = row[1],
                    Defence = int.Parse(row[2]),
                    AttributeId = row.Length > 3 && !string.IsNullOrEmpty(row[3]) ? int.Parse(row[3]) : 0,
                };
            }
        }

        private void LoadArmorFeet(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                int id = int.Parse(row[0]);
                ArmorFeet[id] = new ArmorFeetEntry
                {
                    Id = id,
                    Name = row[1],
                    Evasion = int.Parse(row[2]),
                    AttributeId = row.Length > 3 && !string.IsNullOrEmpty(row[3]) ? int.Parse(row[3]) : 0,
                };
            }
        }

        private void LoadAccessory(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                int id = int.Parse(row[0]);
                Accessory[id] = new AccessoryEntry
                {
                    Id = id,
                    Name = row[1],
                    AttributeId = row.Length > 2 && !string.IsNullOrEmpty(row[2]) ? int.Parse(row[2]) : 0,
                };
            }
        }

        private void LoadAttributes(string fileName)
        {
            foreach (var row in ReadRows(fileName))
            {
                Attributes[int.Parse(row[0])] = row[1];
            }
        }

        private void LoadNameIdTable(string fileName, Dictionary<string, int> target)
        {
            foreach (var row in ReadRows(fileName))
            {
                target[row[0]] = int.Parse(row[1]);
            }
        }

        public IEnumerable<CharacterSlot> CharactersForScenario(string scenario) =>
            Characters.Where(c => c.DisplayName(scenario) != "N/A");

        public string AttributeName(int attributeId) =>
            attributeId == 0 ? "None" : Attributes.TryGetValue(attributeId, out var n) ? n : $"Unknown ({attributeId})";
    }
}
