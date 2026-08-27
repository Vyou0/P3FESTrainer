using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using P3FESTrainer.Controls;
using P3FESTrainer.Data;

namespace P3FESTrainer.Views
{
    public class OwnedRowVm : INotifyPropertyChanged
    {
        public string Type { get; set; } = "";
        public int ItemId { get; set; }
        public string Name { get; set; } = "";
        public uint? Address { get; set; }

        private string _detailText = "";
        public string DetailText
        {
            get => _detailText;
            set { _detailText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailText))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    internal record PickerEntry(int Id, string Name, string Extra, string Stat);

    public partial class InventoryTabView : UserControl
    {
        private const string CategoryItems = "items";
        private static readonly (string Key, string Label)[] CategoryOrder =
        {
            (CategoryItems, "Items"),
            ("weapon", "Weapon"),
            ("armor_body", "Armor: Body"),
            ("armor_feet", "Armor: Feet"),
            ("accessory", "Accessory"),
        };

        private MainWindow? _app;
        private ObservableCollection<OwnedRowVm> _ownedAll = new();
        private ObservableCollection<OwnedRowVm> _ownedFiltered = new();
        private PickerEntry[] _entries = Array.Empty<PickerEntry>();
        private List<(int Id, string Label)> _attributeEntries = new();

        public InventoryTabView()
        {
            InitializeComponent();
            OwnedGrid.ItemsSource = _ownedFiltered;
            CategoryBox.SetOptions(CategoryOrder.Select(c => c.Label));
            CategoryBox.Set(CategoryOrder[0].Label);
        }

        public void Initialize(MainWindow app)
        {
            _app = app;
            RefreshEntries();
            BuildAttributeOptions();
            BuildEquipOptions();
        }

        private const string EquipScenario = "The Journey";

        private void BuildEquipOptions()
        {
            if (_app == null) return;
            EquipCharacterBox.ItemsSource = _app.Data.CharactersForScenario(EquipScenario).Select(c => c.DisplayName(EquipScenario)).ToList();
            if (EquipCharacterBox.Items.Count > 0) EquipCharacterBox.SelectedIndex = 0;

            EquipWeaponBox.SetOptions(_app.Data.Weapons.OrderBy(k => k.Key).Select(kv => $"{kv.Key}  {kv.Value.Name}"));
            EquipArmorBodyBox.SetOptions(_app.Data.ArmorBody.OrderBy(k => k.Key).Select(kv => $"{kv.Key}  {kv.Value.Name}"));
            EquipArmorFeetBox.SetOptions(_app.Data.ArmorFeet.OrderBy(k => k.Key).Select(kv => $"{kv.Key}  {kv.Value.Name}"));
            EquipAccessoryBox.SetOptions(_app.Data.Accessory.OrderBy(k => k.Key).Select(kv => $"{kv.Key}  {kv.Value.Name}"));

            LoadEquipCurrent();
        }

        private CharacterSlot? SelectedEquipCharacter()
        {
            if (_app == null || EquipCharacterBox.SelectedIndex < 0) return null;
            string display = (string)EquipCharacterBox.SelectedItem;
            return _app.Data.CharactersForScenario(EquipScenario).FirstOrDefault(c => c.DisplayName(EquipScenario) == display);
        }

        private void EquipCharacterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadEquipCurrent();

        private void LoadEquipCurrent()
        {
            if (_app == null || !_app.Client.Connected) return;
            var slot = SelectedEquipCharacter();
            if (slot == null) return;
            try
            {
                string LabelFor(Dictionary<int, string> names, int id) => names.TryGetValue(id, out var n) ? $"{id}  {n}" : id.ToString();
                EquipWeaponBox.Set(LabelFor(_app.Data.Weapons.ToDictionary(k => k.Key, k => k.Value.Name), _app.Trainer.ReadCharacterEquip(slot, EquipSlot.Weapon)));
                EquipArmorBodyBox.Set(LabelFor(_app.Data.ArmorBody.ToDictionary(k => k.Key, k => k.Value.Name), _app.Trainer.ReadCharacterEquip(slot, EquipSlot.ArmorBody)));
                EquipArmorFeetBox.Set(LabelFor(_app.Data.ArmorFeet.ToDictionary(k => k.Key, k => k.Value.Name), _app.Trainer.ReadCharacterEquip(slot, EquipSlot.ArmorFeet)));
                EquipAccessoryBox.Set(LabelFor(_app.Data.Accessory.ToDictionary(k => k.Key, k => k.Value.Name), _app.Trainer.ReadCharacterEquip(slot, EquipSlot.Accessory)));
                EquipResultText.Text = "";
            }
            catch (Exception ex)
            {
                EquipResultText.Text = $"Read failed: {ex.Message}";
            }
        }

        private static bool TryParseEquipLabel(string text, out int id)
        {
            var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out id);
        }

        private void SetEquip_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            var slot = SelectedEquipCharacter();
            if (slot == null) return;
            try
            {
                if (TryParseEquipLabel(EquipWeaponBox.Get(), out int weaponId)) _app.Trainer.SetCharacterEquip(slot, EquipSlot.Weapon, weaponId);
                if (TryParseEquipLabel(EquipArmorBodyBox.Get(), out int armorBodyId)) _app.Trainer.SetCharacterEquip(slot, EquipSlot.ArmorBody, armorBodyId);
                if (TryParseEquipLabel(EquipArmorFeetBox.Get(), out int armorFeetId)) _app.Trainer.SetCharacterEquip(slot, EquipSlot.ArmorFeet, armorFeetId);
                if (TryParseEquipLabel(EquipAccessoryBox.Get(), out int accessoryId)) _app.Trainer.SetCharacterEquip(slot, EquipSlot.Accessory, accessoryId);
                EquipResultText.Text = "Equipment written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildAttributeOptions()
        {
            if (_app == null) return;
            _attributeEntries = _app.Data.Attributes.OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, Label: $"{kv.Key} - {kv.Value}"))
                .ToList();
            EditAttributeBox.SetOptions(_attributeEntries.Select(a => a.Label));
        }

        private string AttributeLabelFor(int id)
        {
            var match = _attributeEntries.FirstOrDefault(a => a.Id == id);
            return match.Label ?? $"{id} - {_app!.Data.AttributeName(id)}";
        }

        private static bool TryParseAttributeLabel(string text, out int id)
        {
            var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out id);
        }

        private string CurrentCategory()
        {
            string label = CategoryBox.Get();
            var match = CategoryOrder.FirstOrDefault(c => c.Label == label);
            return match.Key ?? CategoryItems;
        }

        private void ShowActionControls()
        {
            bool isItems = CurrentCategory() == CategoryItems;
            QtyPanel.Visibility = isItems ? Visibility.Visible : Visibility.Collapsed;
            GivePanel.Visibility = isItems ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string StatPreview(string category, GameData data, int itemId)
        {
            switch (category)
            {
                case "weapon":
                    if (data.Weapons.TryGetValue(itemId, out var w)) return $"Atk {w.Attack} / Hit {w.Accuracy}";
                    return "(no data)";
                case "armor_body":
                    if (data.ArmorBody.TryGetValue(itemId, out var ab)) return $"Def {ab.Defence}";
                    return "(no data)";
                case "armor_feet":
                    if (data.ArmorFeet.TryGetValue(itemId, out var af)) return $"Eva {af.Evasion}";
                    return "(no data)";
                default:
                    return "-";
            }
        }

        private EquipSlot? EquipSlotFor(string category) => category switch
        {
            "weapon" => EquipSlot.Weapon,
            "armor_body" => EquipSlot.ArmorBody,
            "armor_feet" => EquipSlot.ArmorFeet,
            "accessory" => EquipSlot.Accessory,
            _ => null,
        };

        private void CategoryBox_SelectionCommitted(object sender, EventArgs e) => RefreshEntries();

        private void RefreshEntries()
        {
            if (_app == null) return;
            string cat = CurrentCategory();
            var list = new List<PickerEntry>();
            if (cat == CategoryItems)
            {
                foreach (var it in _app.Data.Items)
                    list.Add(new PickerEntry(it.Id, it.Name, "", ""));
            }
            else
            {
                var slot = EquipSlotFor(cat)!.Value;
                switch (slot)
                {
                    case EquipSlot.Weapon:
                        foreach (var kv in _app.Data.Weapons.OrderBy(k => k.Key))
                            list.Add(new PickerEntry(kv.Key, kv.Value.Name, kv.Value.WeaponTypeName, StatPreview(cat, _app.Data, kv.Key)));
                        break;
                    case EquipSlot.ArmorBody:
                        foreach (var kv in _app.Data.ArmorBody.OrderBy(k => k.Key))
                            list.Add(new PickerEntry(kv.Key, kv.Value.Name, "", StatPreview(cat, _app.Data, kv.Key)));
                        break;
                    case EquipSlot.ArmorFeet:
                        foreach (var kv in _app.Data.ArmorFeet.OrderBy(k => k.Key))
                            list.Add(new PickerEntry(kv.Key, kv.Value.Name, "", StatPreview(cat, _app.Data, kv.Key)));
                        break;
                    case EquipSlot.Accessory:
                        foreach (var kv in _app.Data.Accessory.OrderBy(k => k.Key))
                            list.Add(new PickerEntry(kv.Key, kv.Value.Name, "", "-"));
                        break;
                }
            }
            _entries = list.ToArray();
            NameBox.SetOptions(_entries.Select(e => e.Name));
            NameBox.Set("");
            ShowActionControls();
        }

        private int? SelectedItemId()
        {
            string text = NameBox.Get();
            var match = _entries.FirstOrDefault(e => e.Name == text);
            if (match == null)
            {
                MessageBox.Show("Please select an item from the list.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            return match.Id;
        }

        private void SetQty_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            var itemId = SelectedItemId();
            if (itemId == null) return;
            if (!int.TryParse(QtyBox.Text, out int qty) || qty < 0 || qty > 255)
            {
                MessageBox.Show("Quantity must be a whole number between 0 and 255.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                _app.Trainer.SetItemQty(itemId.Value, qty);
                ResultText.Text = "Written \u2713";
                RefreshOwned();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GiveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            var itemId = SelectedItemId();
            if (itemId == null) return;
            var slot = EquipSlotFor(CurrentCategory());
            if (slot == null) return;
            try
            {
                uint addr = _app.Trainer.GiveEquipment(slot.Value, itemId.Value, writeStats: true);
                ResultText.Text = $"Equipment added \u2713";
                RefreshOwned();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshOwned()
        {
            if (_app == null || !_app.Client.Connected) return;
            _ownedAll.Clear();
            string? itemsError = null;
            string? equipError = null;
            try
            {
                foreach (var row in _app.Trainer.ListOwnedItems())
                    _ownedAll.Add(new OwnedRowVm { Type = "Item", ItemId = row.Id, Name = row.Name, DetailText = row.Qty.ToString() });
            }
            catch (Exception ex)
            {
                itemsError = ex.Message;
            }
            try
            {
                foreach (var row in _app.Trainer.ListOwnedEquipment())
                    _ownedAll.Add(new OwnedRowVm { Type = "Equipment", ItemId = row.ItemId, Name = row.Name, Address = row.Address, DetailText = $"0x{row.Address:X}" });
            }
            catch (Exception ex)
            {
                equipError = ex.Message;
            }
            RebuildOwnedRows();
            if (itemsError != null || equipError != null)
            {
                var parts = new List<string>();
                if (itemsError != null) parts.Add($"items: {itemsError}");
                if (equipError != null) parts.Add($"equipment: {equipError}");
                ResultText.Text = "Failed to load all owned inventory: " + string.Join(" | ", parts);
            }
        }

        private void RebuildOwnedRows()
        {
            ClearDescription();
            string q = SearchBox.Text.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(q)
                ? _ownedAll
                : new ObservableCollection<OwnedRowVm>(_ownedAll.Where(r => r.Name.ToLowerInvariant().Contains(q)));
            _ownedFiltered = filtered is ObservableCollection<OwnedRowVm> oc ? oc : new ObservableCollection<OwnedRowVm>(filtered);
            OwnedGrid.ItemsSource = _ownedFiltered;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RebuildOwnedRows();

        private EquipSlot? CategoryForEquipmentId(int itemId) => _app?.Trainer.CategoryForEquipmentId(itemId);

        private uint? _editAddress;
        private EquipSlot? _editSlot;

        private string LookupEquipmentName(int itemId)
        {
            if (_app == null) return $"0x{itemId:X4}";
            if (_app.Data.Weapons.TryGetValue(itemId, out var w)) return w.Name;
            if (_app.Data.ArmorBody.TryGetValue(itemId, out var ab)) return ab.Name;
            if (_app.Data.ArmorFeet.TryGetValue(itemId, out var af)) return af.Name;
            if (_app.Data.Accessory.TryGetValue(itemId, out var ac)) return ac.Name;
            return $"0x{itemId:X4}";
        }

        private void PartyEquipBox_SelectionCommitted(object sender, EventArgs e)
        {
            if (_app == null) return;
            SearchableComboBox? box = sender as SearchableComboBox;
            if (box == null) return;
            if (!TryParseEquipLabel(box.Get(), out int itemId)) return;

            EquipSlot? slot = box switch
            {
                _ when box == EquipWeaponBox => EquipSlot.Weapon,
                _ when box == EquipArmorBodyBox => EquipSlot.ArmorBody,
                _ when box == EquipArmorFeetBox => EquipSlot.ArmorFeet,
                _ when box == EquipAccessoryBox => EquipSlot.Accessory,
                _ => null,
            };
            if (slot == null) return;

            var charSlot = SelectedEquipCharacter();
            if (charSlot == null) return;

            uint addr = charSlot.BaseFor(slot.Value);

            DescName.Text = LookupEquipmentName(itemId);
            DescCategory.Text = slot.Value switch
            {
                EquipSlot.Weapon => "Weapon",
                EquipSlot.ArmorBody => "Armor: Body",
                EquipSlot.ArmorFeet => "Armor: Feet",
                EquipSlot.Accessory => "Accessory",
                _ => "Equipment",
            };
            DescQtyLabel.Visibility = Visibility.Collapsed;
            DescQty.Visibility = Visibility.Collapsed;
            DescWeaponTypeLabel.Visibility = Visibility.Visible;
            DescWeaponType.Visibility = Visibility.Visible;
            DescAttributeLabel.Visibility = Visibility.Visible;
            DescAttribute.Visibility = Visibility.Visible;
            string wtype = "-";
            if (slot == EquipSlot.Weapon && _app.Data.Weapons.TryGetValue(itemId, out var w))
                wtype = string.IsNullOrEmpty(w.WeaponTypeName) ? "-" : w.WeaponTypeName;
            DescWeaponType.Text = wtype;

            LoadEditFieldsLive(slot, addr);
        }

        private void ClearDescription()
        {
            DescName.Text = "-";
            DescCategory.Text = "-";
            DescWeaponType.Text = "-";
            DescAttribute.Text = "-";
            DescQty.Text = "-";
            DescWeaponTypeLabel.Visibility = Visibility.Visible;
            DescWeaponType.Visibility = Visibility.Visible;
            DescAttributeLabel.Visibility = Visibility.Visible;
            DescAttribute.Visibility = Visibility.Visible;
            DescQtyLabel.Visibility = Visibility.Collapsed;
            DescQty.Visibility = Visibility.Collapsed;
            EditPanel.Visibility = Visibility.Collapsed;
            _editAddress = null;
            _editSlot = null;
            EditResultText.Text = "";
        }

        private void OwnedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OwnedGrid.SelectedItem is not OwnedRowVm row || _app == null) return;
            EditResultText.Text = "";
            DescName.Text = row.Name;
            if (row.Type == "Item")
            {
                DescCategory.Text = "Item";
                DescWeaponTypeLabel.Visibility = Visibility.Collapsed;
                DescWeaponType.Visibility = Visibility.Collapsed;
                DescAttributeLabel.Visibility = Visibility.Collapsed;
                DescAttribute.Visibility = Visibility.Collapsed;
                DescQtyLabel.Visibility = Visibility.Visible;
                DescQty.Visibility = Visibility.Visible;
                DescQty.Text = row.DetailText;
                EditPanel.Visibility = Visibility.Collapsed;
                _editAddress = null;
                _editSlot = null;
                return;
            }

            var slot = CategoryForEquipmentId(row.ItemId);
            string catLabel = slot switch
            {
                EquipSlot.Weapon => "Weapon",
                EquipSlot.ArmorBody => "Armor: Body",
                EquipSlot.ArmorFeet => "Armor: Feet",
                EquipSlot.Accessory => "Accessory",
                _ => "Equipment",
            };
            DescCategory.Text = catLabel;
            DescQtyLabel.Visibility = Visibility.Collapsed;
            DescQty.Visibility = Visibility.Collapsed;
            DescWeaponTypeLabel.Visibility = Visibility.Visible;
            DescWeaponType.Visibility = Visibility.Visible;
            DescAttributeLabel.Visibility = Visibility.Visible;
            DescAttribute.Visibility = Visibility.Visible;
            string wtype = "-";
            if (slot == EquipSlot.Weapon && _app.Data.Weapons.TryGetValue(row.ItemId, out var w))
                wtype = string.IsNullOrEmpty(w.WeaponTypeName) ? "-" : w.WeaponTypeName;
            DescWeaponType.Text = wtype;
            DescAttribute.Text = "-";

            if (row.Address is uint addr)
                LoadEditFieldsLive(slot, addr);
            else
            {
                EditPanel.Visibility = Visibility.Collapsed;
                _editAddress = null;
                _editSlot = null;
            }
        }

        private void LoadEditFieldsLive(EquipSlot? slot, uint address)
        {
            if (_app == null) return;
            _editSlot = slot;
            _editAddress = address;
            EditPanel.Visibility = Visibility.Visible;
            WeaponStatEdit.Visibility = Visibility.Collapsed;
            DefenceStatEdit.Visibility = Visibility.Collapsed;
            EvasionStatEdit.Visibility = Visibility.Collapsed;

            if (!_app.Client.Connected)
            {
                EditResultText.Text = "Not connected.";
                return;
            }

            try
            {
                int attrId = _app.Trainer.ReadEquipmentAttribute(address);
                EditAttributeBox.Set(AttributeLabelFor(attrId));
                DescAttribute.Text = _app.Data.AttributeName(attrId);

                switch (slot)
                {
                    case EquipSlot.Weapon:
                        WeaponStatEdit.Visibility = Visibility.Visible;
                        var (atk, hit) = _app.Trainer.ReadWeaponInstanceStats(address);
                        EditAttackBox.Text = atk.ToString();
                        EditAccuracyBox.Text = hit.ToString();
                        break;
                    case EquipSlot.ArmorBody:
                        DefenceStatEdit.Visibility = Visibility.Visible;
                        EditDefenceBox.Text = _app.Trainer.ReadArmorBodyInstanceStat(address).ToString();
                        break;
                    case EquipSlot.ArmorFeet:
                        EvasionStatEdit.Visibility = Visibility.Visible;
                        EditEvasionBox.Text = _app.Trainer.ReadArmorFeetInstanceStat(address).ToString();
                        break;
                    case EquipSlot.Accessory:
                        break;
                }
            }
            catch (Exception ex)
            {
                EditResultText.Text = $"Read failed: {ex.Message}";
            }
        }

        private void EditAttributeBox_SelectionCommitted(object sender, EventArgs e)
        {
            if (_app == null) return;
            if (!TryParseAttributeLabel(EditAttributeBox.Get(), out int attrId)) return;

            if (!_app.RequireConnected()) return;
            if (_editAddress == null) return;
            try
            {
                _app.Trainer.WriteEquipmentAttribute(_editAddress.Value, attrId);
                DescAttribute.Text = _app.Data.AttributeName(attrId);
                EditResultText.Text = "Saved \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditStatBox_LostFocus(object sender, RoutedEventArgs e) => CommitStatBox();

        private void EditStatBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitStatBox();
                e.Handled = true;
            }
        }

        private void CommitStatBox()
        {
            if (_app == null) return;

            if (!_app.RequireConnected()) return;
            if (_editAddress == null || _editSlot == null) return;
            uint address = _editAddress.Value;
            try
            {
                switch (_editSlot.Value)
                {
                    case EquipSlot.Weapon:
                        if (!TryParseStat(EditAttackBox.Text, out int atk) || !TryParseStat(EditAccuracyBox.Text, out int acc))
                        {
                            MessageBox.Show("Attack and Accuracy must be valid numbers (0-65535).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        _app.Trainer.WriteWeaponInstanceStats(address, atk, acc);
                        break;
                    case EquipSlot.ArmorBody:
                        if (!TryParseStat(EditDefenceBox.Text, out int def))
                        {
                            MessageBox.Show("Defence must be a valid number (0-65535).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        _app.Trainer.WriteArmorBodyInstanceStat(address, def);
                        break;
                    case EquipSlot.ArmorFeet:
                        if (!TryParseStat(EditEvasionBox.Text, out int eva))
                        {
                            MessageBox.Show("Evasion must be a valid number (0-65535).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        _app.Trainer.WriteArmorFeetInstanceStat(address, eva);
                        break;
                    case EquipSlot.Accessory:
                        return;
                }
                EditResultText.Text = "Saved \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool TryParseStat(string text, out int value) =>
            int.TryParse(text, out value) && value >= 0 && value <= 65535;

        private void OwnedQtyBox_LostFocus(object sender, RoutedEventArgs e) => CommitQtyBox(sender);

        private void OwnedQtyBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitQtyBox(sender);
                e.Handled = true;
            }
        }

        private void CommitQtyBox(object sender)
        {
            if (sender is not TextBox box || box.DataContext is not OwnedRowVm row || row.Type != "Item" || _app == null) return;
            if (!_app.Client.Connected) return;
            if (!int.TryParse(box.Text, out int qty))
            {
                MessageBox.Show("Quantity must be a whole number between 0 and 255.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Error);
                box.Text = row.DetailText;
                return;
            }
            qty = Math.Max(0, Math.Min(255, qty));
            box.Text = qty.ToString();
            row.DetailText = qty.ToString();
            try
            {
                _app.Trainer.SetItemQty(row.ItemId, qty);
                ResultText.Text = "Written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
