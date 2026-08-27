namespace P3FESTrainer.Data
{
    /// <summary>
    /// Inventory base address and struct offsets for owned equipment.
    /// </summary>
    public static class EquipmentInventory
    {
        public const uint Base = 0x010C15F0;
        public const uint Stride = 0x14;

        public const uint OffId = 0x00;        // Item ID (2 bytes)
        public const uint OffPicture = 0x04;   // Picture Modifier / category tag (4 bytes)
        public const uint OffAttribute = 0x09; // Bonus Attribute ID (1 byte)
        public const uint OffAttack = 0x0A;  // Weapon Attack (2 bytes)
        public const uint OffHit = 0x0C;     // Weapon Accuracy (2 bytes)
        public const uint OffDefence = 0x0E; // Armor Defence (2 bytes)
        public const uint OffEvasion = 0x10; // Feet Evasion (2 bytes)

        public const uint PictureArmorBody = 0x00010000;
        public const uint PictureArmorFeet = 0x00020000;
        public const uint PictureAccessory = 0x00040000;

        public const int MaxSlots = 290;
    }
}
