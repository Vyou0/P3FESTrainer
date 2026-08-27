using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using P3FESTrainer.Controls;
using P3FESTrainer.Core;
using P3FESTrainer.Data;

namespace P3FESTrainer.Views
{
    public partial class StatsTabView : UserControl
    {
        private MainWindow? _app;
        private (int Id, string Display)[] _personaOptions = Array.Empty<(int, string)>();
        private (int Id, string Display)[] _skillOptions = Array.Empty<(int, string)>();
        private int? _selectedPersonaId;
        private byte? _selectedGrowthByte;
        private readonly int[] _pendingSkills = new int[8];
        private SearchableComboBox[] SkillBoxes => new[] { Skill0, Skill1, Skill2, Skill3, Skill4, Skill5, Skill6, Skill7 };
        private bool _suppressEvents;
        private bool _uiReady;

        private static readonly (int PartyId, string JourneyName, string AnswerName)[] PartyConfigMembers =
        {
            (2, "Yukari Takeba", "Yukari Takeba"),
            (3, "Aigis", "N/A"),
            (4, "Mitsuru Kirijo", "Mitsuru Kirijo"),
            (5, "Junpei Iori", "Junpei Iori"),
            (7, "Akihiko Sanada", "Akihiko Sanada"),
            (8, "Ken Amada", "Ken Amada"),
            (9, "Shinjiro Aragaki", "Metis"),
            (10, "Koromaru", "Koromaru"),
        };

        private (int SlotId, string Display, CharacterSlot? Slot)[] _characterOptions = Array.Empty<(int, string, CharacterSlot?)>();
        private bool IsMcSelected => CharacterBox.SelectedIndex >= 0 && _characterOptions.Length > CharacterBox.SelectedIndex && _characterOptions[CharacterBox.SelectedIndex].SlotId == 0;
        private CharacterSlot? SelectedCharacterSlot => CharacterBox.SelectedIndex >= 0 && _characterOptions.Length > CharacterBox.SelectedIndex ? _characterOptions[CharacterBox.SelectedIndex].Slot : null;
        private string CurrentScenario => (ScenarioBox.SelectedItem as string) == "The Answer" ? "The Answer" : "The Journey";

        public StatsTabView()
        {
            InitializeComponent();
            _uiReady = true;
            SlotBox.ItemsSource = Enumerable.Range(1, 12).Select(i => i.ToString()).ToList();
            SlotBox.SelectedIndex = 0;
            ScenarioBox.ItemsSource = new[] { "The Journey", "The Answer" };
            ScenarioBox.SelectedIndex = 0;
        }

        public void Initialize(MainWindow app)
        {
            _app = app;
            ReloadPersonaOptions();
            ReloadSkillOptions();
            RefreshSkillBoxesDisplay();
            ReloadCharacterOptions();
            ReloadPartySlotOptions();
        }

        private void ReloadCharacterOptions()
        {
            if (_app == null) return;
            string scenario = CurrentScenario;
            var options = new List<(int, string, CharacterSlot?)>
            {
                (0, $"{MainCharacterData.McDisplayName(scenario)} (MC)", null),
            };
            foreach (var c in _app.Data.CharactersForScenario(scenario))
                options.Add((c.SlotId, c.DisplayName(scenario), c));
            _characterOptions = options.ToArray();

            int prevIndex = CharacterBox.SelectedIndex;
            CharacterBox.ItemsSource = _characterOptions.Select(o => o.Display).ToList();
            CharacterBox.SelectedIndex = prevIndex >= 0 && prevIndex < _characterOptions.Length ? prevIndex : 0;
        }

        private void ReloadPartySlotOptions()
        {
            if (_app == null) return;
            string scenario = CurrentScenario;
            var items = new List<string> { "(Empty)" };
            foreach (var p in PartyConfigMembers)
            {
                string name = scenario == "The Journey" ? p.JourneyName : p.AnswerName;
                if (name != "N/A") items.Add(name);
            }
            foreach (var box in new[] { PartySlot2Box, PartySlot3Box, PartySlot4Box })
            {
                int prev = box.SelectedIndex;
                box.ItemsSource = items;
                box.SelectedIndex = prev >= 0 && prev < items.Count ? prev : 0;
            }
        }

        private void ScenarioBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ReloadCharacterOptions();
            ReloadPartySlotOptions();
            ApplyCharacterModeVisibility();
            if (_app == null || !_app.Client.Connected) return;
            LoadCurrent();
        }

        private void CharacterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyCharacterModeVisibility();
            if (_app == null || !_app.Client.Connected) return;
            LoadCurrent();
        }

        private void ApplyCharacterModeVisibility()
        {
            bool mc = IsMcSelected;
            McSocialStatsGroup.Visibility = (mc && CurrentScenario != "The Answer") ? Visibility.Visible : Visibility.Collapsed;
            SlotBox.Visibility = mc ? Visibility.Visible : Visibility.Collapsed;
            MemberLevelPanel.Visibility = mc ? Visibility.Visible : Visibility.Collapsed;
            HpSpGroup.Visibility = mc ? Visibility.Visible : Visibility.Collapsed;
            HpSpGroup.IsEnabled = true;
            FillNearLevelUpButton.IsEnabled = true;
        }

        private void SetParty_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            string scenario = CurrentScenario;
            int SlotIdFor(ComboBox box)
            {
                if (box.SelectedIndex <= 0) return 0;
                string display = (string)box.SelectedItem;
                var match = PartyConfigMembers.FirstOrDefault(p => (scenario == "The Journey" ? p.JourneyName : p.AnswerName) == display);
                return match.PartyId;
            }
            try
            {
                _app.Trainer.SetPartyConfiguration(SlotIdFor(PartySlot2Box), SlotIdFor(PartySlot3Box), SlotIdFor(PartySlot4Box));
                ResultText.Text = "Party configuration written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPartyConfiguration()
        {
            if (_app == null || !_app.Client.Connected) return;
            string scenario = CurrentScenario;
            PartyConfiguration cfg;
            try { cfg = _app.Trainer.GetPartyConfiguration(); }
            catch { return; }
            void Apply(ComboBox box, int slotId)
            {
                if (slotId == 0) { box.SelectedIndex = 0; return; }
                var match = PartyConfigMembers.FirstOrDefault(p => p.PartyId == slotId);
                if (match.PartyId == 0) { box.SelectedIndex = 0; return; }
                string name = scenario == "The Journey" ? match.JourneyName : match.AnswerName;
                int idx = box.Items.IndexOf(name);
                box.SelectedIndex = idx >= 0 ? idx : 0;
            }
            Apply(PartySlot2Box, cfg.Slot2);
            Apply(PartySlot3Box, cfg.Slot3);
            Apply(PartySlot4Box, cfg.Slot4);
        }

        private void SetHpSp_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            if (!int.TryParse(HpBox.Text, out int hp) || !int.TryParse(SpBox.Text, out int sp))
            {
                MessageBox.Show("HP/SP must be valid whole numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                if (IsMcSelected) _app.Trainer.SetMcHpSp(hp, sp);
                else
                {
                    var slot = SelectedCharacterSlot;
                    if (slot == null) return;
                    _app.Trainer.SetPartyMemberHpSp(slot, hp, sp);
                }
                ResultText.Text = "HP/SP written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CharLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_uiReady) return;
            CharLevelValueText.Text = ((int)CharLevelSlider.Value).ToString();
            if (_suppressEvents) return;
            SyncCharExpFromLevel();
        }

        private void CharLevelValueBox_LostFocus(object sender, RoutedEventArgs e) => CommitCharLevelBox();
        private void CharLevelValueBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitCharLevelBox();
            e.Handled = true;
        }

        private void CommitCharLevelBox()
        {
            if (!_uiReady) return;
            if (int.TryParse(CharLevelValueText.Text, out int v))
                CharLevelSlider.Value = Math.Max((int)CharLevelSlider.Minimum, Math.Min((int)CharLevelSlider.Maximum, v));
            CharLevelValueText.Text = ((int)CharLevelSlider.Value).ToString();
        }

        private void SyncCharExpFromLevel()
        {
            int level = (int)CharLevelSlider.Value;
            try
            {
                long exp = MainCharacterData.ExpForLevel(level);
                _suppressEvents = true;
                CharExpBox.Text = exp.ToString();
                _suppressEvents = false;
                RefreshCharNextExp();
            }
            catch { }
        }

        private void CharExpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady || _suppressEvents) return;
            RefreshCharNextExp();
        }

        private void RefreshCharNextExp()
        {
            int level = (int)CharLevelSlider.Value;
            if (!long.TryParse(CharExpBox.Text, out long currentExp)) { CharNextExpText.Text = "-"; return; }
            if (level >= 99) { CharNextExpText.Text = "-"; return; }
            try
            {
                long needed = MainCharacterData.ExpForLevel(level + 1) - currentExp;
                CharNextExpText.Text = needed.ToString();
            }
            catch { CharNextExpText.Text = "-"; }
        }

        private void SetCharLevel_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            if (!uint.TryParse(CharExpBox.Text, out uint exp))
            {
                MessageBox.Show("Total XP must be a valid whole number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                _app.Trainer.SetMcExp(exp);
                ResultText.Text = "Level/XP written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MemberFill_Click(object sender, RoutedEventArgs e)
        {
            int level = (int)CharLevelSlider.Value;
            if (!long.TryParse(CharExpBox.Text, out long currentExp)) return;
            if (level >= 99) return;
            try
            {
                long needed = MainCharacterData.ExpForLevel(level + 1) - currentExp;
                if (needed > 1)
                    CharExpBox.Text = (currentExp + needed - 1).ToString();
            }
            catch { }
        }

        private static string GetAcademicsRank(int v) => v switch
        {
            <= 19 => "Slacker",
            <= 79 => "Average",
            <= 139 => "Above Average",
            <= 199 => "Smart",
            <= 259 => "Intelligent",
            _ => "Genius (Max)"
        };

        private static string GetCharmRank(int v) => v switch
        {
            <= 19 => "Plain",
            <= 29 => "Unpolished",
            <= 49 => "Confident",
            <= 59 => "Smooth",
            <= 79 => "Popular",
            _ => "Charismatic (Max)"
        };

        private static string GetCourageRank(int v) => v switch
        {
            <= 19 => "Timid",
            <= 29 => "Ordinary",
            <= 49 => "Determined",
            <= 59 => "Tough",
            <= 79 => "Fearless",
            _ => "Badass (Max)"
        };

        private void McStatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_uiReady) return;
            var slider = (Slider)sender;
            int v = (int)slider.Value;
            switch (slider.Tag as string)
            {
                case "Academics":
                    AcademicsValueText.Text = v.ToString();
                    AcademicsRankText.Text = GetAcademicsRank(v);
                    break;
                case "Charm":
                    CharmValueText.Text = v.ToString();
                    CharmRankText.Text = GetCharmRank(v);
                    break;
                case "Courage":
                    CourageValueText.Text = v.ToString();
                    CourageRankText.Text = GetCourageRank(v);
                    break;
            }
        }

        private void McStatValueBox_LostFocus(object sender, RoutedEventArgs e) => CommitMcStatBox((TextBox)sender);
        private void McStatValueBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitMcStatBox((TextBox)sender);
            e.Handled = true;
        }

        private void CommitMcStatBox(TextBox box)
        {
            if (!_uiReady) return;
            var slider = (box.Tag as string) switch
            {
                "Academics" => AcademicsSlider,
                "Charm" => CharmSlider,
                "Courage" => CourageSlider,
                _ => null,
            };
            if (slider == null) return;
            if (int.TryParse(box.Text, out int v))
                slider.Value = Math.Max((int)slider.Minimum, Math.Min((int)slider.Maximum, v));
            box.Text = ((int)slider.Value).ToString();
        }

        private void SetMcSocialStats_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            try
            {
                _app.Trainer.SetMcSocialStats((int)AcademicsSlider.Value, (int)CharmSlider.Value, (int)CourageSlider.Value);
                ResultText.Text = "Social stats written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMcSocialStats()
        {
            if (_app == null || !_app.Client.Connected) return;
            try
            {
                var info = _app.Trainer.GetMcSocialStats();
                _suppressEvents = true;
                AcademicsSlider.Value = Math.Min(260, info.Academics);
                CharmSlider.Value = Math.Min(80, info.Charm);
                CourageSlider.Value = Math.Min(80, info.Courage);
                AcademicsValueText.Text = ((int)AcademicsSlider.Value).ToString();
                CharmValueText.Text = ((int)CharmSlider.Value).ToString();
                CourageValueText.Text = ((int)CourageSlider.Value).ToString();
                AcademicsRankText.Text = GetAcademicsRank((int)AcademicsSlider.Value);
                CharmRankText.Text = GetCharmRank((int)CharmSlider.Value);
                CourageRankText.Text = GetCourageRank((int)CourageSlider.Value);
                _suppressEvents = false;
            }
            catch { }
        }

        private void ReloadPersonaOptions()
        {
            var list = new List<(int Id, string Display)> { (0, "(Empty)") };
            list.AddRange(MainCharacterData.PersonaTable
                .OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, $"{kv.Key:X3}  {kv.Value}")));
            _personaOptions = list.ToArray();
            PersonaBox.SetOptions(_personaOptions.Select(p => p.Display));
        }

        private void ReloadSkillOptions()
        {
            var list = new List<(int Id, string Display)> { (0, "(Empty)") };
            list.AddRange(MainCharacterData.SkillTable
                .OrderBy(kv => kv.Key)
                .Select(kv => (kv.Key, $"{kv.Key:X3}  {kv.Value}")));
            _skillOptions = list.ToArray();
            foreach (var box in SkillBoxes)
                box.SetOptions(_skillOptions.Select(s => s.Display));
        }

        private void RefreshSkillBoxesDisplay()
        {
            var boxes = SkillBoxes;
            for (int i = 0; i < 8; i++)
            {
                int sid = _pendingSkills[i];
                boxes[i].Set(sid != 0 ? $"{sid:X3}  {(MainCharacterData.SkillTable.TryGetValue(sid, out var n) ? n : "?")}" : "(Empty)");
            }
        }

        private int SelectedSlot() => SlotBox.SelectedIndex >= 0 ? SlotBox.SelectedIndex + 1 : 1;

        private void SlotBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady || _suppressEvents) return;
            if (_app == null || !_app.Client.Connected) return;
            if (!IsMcSelected) return;
            LoadCurrent();
        }

        private void PersonaBox_SelectionCommitted(object sender, EventArgs e)
        {
            string text = PersonaBox.Get();
            var match = _personaOptions.FirstOrDefault(p => p.Display == text);
            if (match.Display == null) return;
            _selectedPersonaId = match.Id;

            if (_selectedPersonaId == 0)
            {
                _selectedGrowthByte = null;
                _suppressEvents = true;
                LevelSlider.Value = 1;
                ExpBox.Text = "0";
                StSlider.Value = 0;
                MaSlider.Value = 0;
                EnSlider.Value = 0;
                AgSlider.Value = 0;
                LuSlider.Value = 0;
                for (int i = 0; i < 8; i++) _pendingSkills[i] = 0;
                RefreshSkillBoxesDisplay();
                _suppressEvents = false;
                RefreshNextExp();
                return;
            }

            RefreshGrowthByte();
            SyncExpFromLevel();
            RefreshNextExp();
        }

        private void SkillBox_SelectionCommitted(object sender, EventArgs e)
        {
            var boxes = SkillBoxes;
            int idx = Array.IndexOf(boxes, sender);
            if (idx < 0) return;
            string text = boxes[idx].Get();
            var match = _skillOptions.FirstOrDefault(s => s.Display == text);
            _pendingSkills[idx] = match.Display != null ? match.Id : 0;
        }

        private void RefreshGrowthByte()
        {
            _selectedGrowthByte = null;
            int? pid = _selectedPersonaId;
            if (pid == null || pid.Value == 0 || MainCharacterData.IsSpecialPersonaId(pid.Value)) return;
            if (_app == null || !_app.Client.Connected) return;
            try
            {
                uint tableBase = _app.Client.Read32(MainCharacterData.PersonaGrowthTablePointer);
                _selectedGrowthByte = _app.Client.Read8(MainCharacterData.PersonaGrowthByteAddress(pid.Value, tableBase));
            }
            catch { _selectedGrowthByte = null; }
        }

        private void LevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_uiReady) return;
            LevelValueText.Text = ((int)LevelSlider.Value).ToString();
            if (_suppressEvents) return;
            SyncExpFromLevel();
        }

        private void StatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_uiReady) return;
            var slider = (Slider)sender;
            int v = (int)slider.Value;
            switch (slider.Tag as string)
            {
                case "ST": StValueText.Text = v.ToString(); break;
                case "MA": MaValueText.Text = v.ToString(); break;
                case "EN": EnValueText.Text = v.ToString(); break;
                case "AG": AgValueText.Text = v.ToString(); break;
                case "LU": LuValueText.Text = v.ToString(); break;
            }
        }

        private void LevelValueBox_LostFocus(object sender, RoutedEventArgs e) => CommitLevelBox();
        private void LevelValueBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitLevelBox();
            e.Handled = true;
        }

        private void CommitLevelBox()
        {
            if (!_uiReady) return;
            if (int.TryParse(LevelValueText.Text, out int v))
                LevelSlider.Value = Math.Max((int)LevelSlider.Minimum, Math.Min((int)LevelSlider.Maximum, v));
            LevelValueText.Text = ((int)LevelSlider.Value).ToString();
        }

        private void StatValueBox_LostFocus(object sender, RoutedEventArgs e) => CommitStatBox((TextBox)sender);
        private void StatValueBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            CommitStatBox((TextBox)sender);
            e.Handled = true;
        }

        private void CommitStatBox(TextBox box)
        {
            if (!_uiReady) return;
            var slider = (box.Tag as string) switch
            {
                "ST" => StSlider,
                "MA" => MaSlider,
                "EN" => EnSlider,
                "AG" => AgSlider,
                "LU" => LuSlider,
                _ => null,
            };
            if (slider == null) return;
            if (int.TryParse(box.Text, out int v))
                slider.Value = Math.Max((int)slider.Minimum, Math.Min((int)slider.Maximum, v));
            box.Text = ((int)slider.Value).ToString();
        }

        private void SyncExpFromLevel()
        {
            int level = (int)LevelSlider.Value;
            if (_selectedPersonaId != null && MainCharacterData.IsSpecialPersonaId(_selectedPersonaId.Value))
            {
                try
                {
                    long exp = PartyMemberData.ExpForLevel(level);
                    _suppressEvents = true;
                    ExpBox.Text = exp.ToString();
                    _suppressEvents = false;
                    RefreshNextExp();
                }
                catch { }
            }
            else
            {
                if (_selectedGrowthByte == null) return;
                try
                {
                    long exp = MainCharacterData.ExpForPersonaLevel(level, _selectedGrowthByte.Value);
                    _suppressEvents = true;
                    ExpBox.Text = exp.ToString();
                    _suppressEvents = false;
                    RefreshNextExp();
                }
                catch { }
            }
        }

        private void ExpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady || _suppressEvents) return;
            RefreshNextExp();
        }

        private void RefreshNextExp()
        {
            int level = (int)LevelSlider.Value;
            if (!long.TryParse(ExpBox.Text, out long currentExp)) { NextExpText.Text = "-"; return; }
            if (level >= 99) { NextExpText.Text = "-"; return; }

            if (_selectedPersonaId != null && MainCharacterData.IsSpecialPersonaId(_selectedPersonaId.Value))
            {
                try
                {
                    long needed = PartyMemberData.ExpForLevel(level + 1) - currentExp;
                    NextExpText.Text = needed.ToString();
                }
                catch { NextExpText.Text = "-"; }
            }
            else
            {
                if (_selectedGrowthByte == null) { NextExpText.Text = "-"; return; }
                try
                {
                    long needed = MainCharacterData.ExpForPersonaLevel(level + 1, _selectedGrowthByte.Value) - currentExp;
                    NextExpText.Text = needed.ToString();
                }
                catch { NextExpText.Text = "-"; }
            }
        }

        private void FillNearLevelUp_Click(object sender, RoutedEventArgs e)
        {
            int level = (int)LevelSlider.Value;
            if (!long.TryParse(ExpBox.Text, out long currentExp))
            {
                MessageBox.Show("Level/Total XP is invalid.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (level >= 99)
            {
                MessageBox.Show("Already at max level.", "Max Level Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedPersonaId != null && MainCharacterData.IsSpecialPersonaId(_selectedPersonaId.Value))
            {
                long needed;
                try { needed = PartyMemberData.ExpForLevel(level + 1) - currentExp; }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Calculation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (needed <= 1) return;
                ExpBox.Text = (currentExp + needed - 1).ToString();
            }
            else
            {
                if (_selectedGrowthByte == null)
                {
                    MessageBox.Show("Connect to game and select a Persona first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                long needed;
                try { needed = MainCharacterData.ExpForPersonaLevel(level + 1, _selectedGrowthByte.Value) - currentExp; }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Calculation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (needed <= 1) return;
                ExpBox.Text = (currentExp + needed - 1).ToString();
            }
        }

        public void LoadCurrent()
        {
            if (_app == null || !_app.RequireConnected()) return;
            ApplyCharacterModeVisibility();
            LoadPartyConfiguration();

            if (IsMcSelected)
            {
                LoadMcSocialStats();

                try
                {
                    uint mcExp = _app.Trainer.GetMcExp();
                    _suppressEvents = true;
                    CharLevelSlider.Value = Math.Max(1, MainCharacterData.LevelForExp(mcExp));
                    _suppressEvents = false;
                    CharExpBox.Text = mcExp.ToString();
                    RefreshCharNextExp();
                }
                catch { }

                try
                {
                    var (hp, sp) = _app.Trainer.GetMcHpSp();
                    HpBox.Text = hp.ToString();
                    SpBox.Text = sp.ToString();
                }
                catch { }

                Core.PersonaInfo info;
                try { info = _app.Trainer.ReadPersona(SelectedSlot()); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Read Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int pid = info.PersonaId;
                if (pid == 0 || info.Active == 0)
                {
                    _selectedPersonaId = 0;
                    _suppressEvents = true;
                    LevelSlider.Value = 1;
                    _suppressEvents = false;
                    ExpBox.Text = "0";
                    StSlider.Value = 0;
                    MaSlider.Value = 0;
                    EnSlider.Value = 0;
                    AgSlider.Value = 0;
                    LuSlider.Value = 0;
                    for (int i = 0; i < 8; i++) _pendingSkills[i] = 0;
                    RefreshSkillBoxesDisplay();
                    PersonaBox.Set("(Empty)");
                    _selectedGrowthByte = null;
                    RefreshNextExp();
                    ResultText.Text = "Slot is (Empty)";
                }
                else
                {
                    _suppressEvents = true;
                    LevelSlider.Value = info.Level;
                    _suppressEvents = false;
                    ExpBox.Text = info.Exp.ToString();
                    StSlider.Value = Math.Min(99, info.St + 3);
                    MaSlider.Value = Math.Min(99, info.Ma + 3);
                    EnSlider.Value = info.En;
                    AgSlider.Value = info.Ag;
                    LuSlider.Value = info.Lu;
                    for (int i = 0; i < 8; i++) _pendingSkills[i] = info.Skills[i];
                    RefreshSkillBoxesDisplay();

                    string pname = MainCharacterData.PersonaTable.TryGetValue(pid, out var n) ? n : "(unknown name)";
                    _selectedPersonaId = pid;
                    RefreshGrowthByte();
                    RefreshNextExp();
                    PersonaBox.Set($"{pid:X3}  {pname}");
                    ResultText.Text = $"Current persona: {pname} ({pid:X3})";
                }
            }
            else
            {
                var slot = SelectedCharacterSlot;
                if (slot == null) return;
                PartyMemberInfo info;
                try { info = _app.Trainer.ReadPartyMember(slot); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Read Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                HpBox.Text = info.Hp.ToString();
                SpBox.Text = info.Sp.ToString();

                int pid = info.PersonaId;
                if (pid == 0)
                {
                    _selectedPersonaId = 0;
                    _suppressEvents = true;
                    LevelSlider.Value = 1;
                    _suppressEvents = false;
                    ExpBox.Text = "0";
                    StSlider.Value = 0;
                    MaSlider.Value = 0;
                    EnSlider.Value = 0;
                    AgSlider.Value = 0;
                    LuSlider.Value = 0;
                    for (int i = 0; i < 8; i++) _pendingSkills[i] = 0;
                    RefreshSkillBoxesDisplay();
                    PersonaBox.Set("(Empty)");
                    RefreshNextExp();
                }
                else
                {
                    _suppressEvents = true;
                    LevelSlider.Value = Math.Max(1, info.PersonaLevel);
                    _suppressEvents = false;

                    ExpBox.Text = info.PersonaExp.ToString();
                    StSlider.Value = info.PSt;
                    MaSlider.Value = info.PMa;
                    EnSlider.Value = info.PEn;
                    AgSlider.Value = info.PAg;
                    LuSlider.Value = info.PLu;

                    _selectedPersonaId = pid;
                    RefreshGrowthByte();
                    string pname = MainCharacterData.PersonaTable.TryGetValue(pid, out var n2) ? n2 : "(unknown)";
                    PersonaBox.Set($"{pid:X3}  {pname}");
                    for (int i = 0; i < 8; i++) _pendingSkills[i] = info.PersonaSkills[i];
                    RefreshSkillBoxesDisplay();
                    RefreshNextExp();
                }

                ResultText.Text = $"Loaded {_characterOptions.First(o => o.SlotId == slot.SlotId).Display}";
            }
        }

        private void WritePersona_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;

            if (IsMcSelected)
            {
                if (_selectedPersonaId == null)
                {
                    MessageBox.Show("Please select a Persona first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_selectedPersonaId.Value == 0)
                {
                    try
                    {
                        _app.Trainer.SetPersona(
                            SelectedSlot(), 0,
                            level: 1, exp: 0, skills: new int[8],
                            st: 0, ma: 0, en: 0, ag: 0, lu: 0,
                            activate: false, registerCompendium: false);
                        ResultText.Text = "Slot cleared to (Empty) \u2713";
                        LoadCurrent();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                if (!uint.TryParse(ExpBox.Text, out uint exp))
                {
                    MessageBox.Show("Total XP must be a valid whole number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                try
                {
                    _app.Trainer.SetPersona(
                        SelectedSlot(), _selectedPersonaId.Value,
                        level: (int)LevelSlider.Value, exp: exp, skills: _pendingSkills,
                        st: Math.Max(0, (int)StSlider.Value - 3), ma: Math.Max(0, (int)MaSlider.Value - 3), en: (int)EnSlider.Value,
                        ag: (int)AgSlider.Value, lu: (int)LuSlider.Value, activate: true);
                    ResultText.Text = "Written \u2713";
                    LoadCurrent();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                var slot = SelectedCharacterSlot;
                if (slot == null) return;

                if (_selectedPersonaId == 0)
                {
                    try
                    {
                        _app.Trainer.SetPartyMemberPersona(
                            slot, level: 1, exp: 0,
                            pSt: 0, pMa: 0, pEn: 0, pAg: 0, pLu: 0,
                            skills: new int[8], personaId: 0, activate: false);
                        ResultText.Text = "Party persona cleared to (Empty) \u2713";
                        LoadCurrent();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                if (!uint.TryParse(ExpBox.Text, out uint exp))
                {
                    MessageBox.Show("Total XP must be a valid whole number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                try
                {
                    _app.Trainer.SetPartyMemberPersona(
                        slot, level: (int)LevelSlider.Value, exp: exp,
                        pSt: (int)StSlider.Value, pMa: (int)MaSlider.Value, pEn: (int)EnSlider.Value,
                        pAg: (int)AgSlider.Value, pLu: (int)LuSlider.Value,
                        skills: _pendingSkills, personaId: _selectedPersonaId, activate: true);
                    ResultText.Text = "Written \u2713";
                    LoadCurrent();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
