namespace P3FESTrainer.Data
{
    /// <summary>
    /// Inventory base address and offset calculation for consumable items.
    /// </summary>
    public static class ItemInventory
    {
        public const uint Base = 0x011C2D92;
        public const uint Stride = 0x02;

        public static uint AddressFor(int itemId) => Base + (uint)((itemId - 1) * Stride);
    }
}
