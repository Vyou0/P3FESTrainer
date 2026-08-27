using System;
using System.IO;
using System.Windows;
using P3FESTrainer.Core;
using P3FESTrainer.Data;
using P3FESTrainer.Pine;

namespace P3FESTrainer
{
    public partial class MainWindow : Window
    {
        public GameData Data { get; }
        public PineClient Client { get; }
        public Trainer Trainer { get; }

        public MainWindow()
        {
            InitializeComponent();

            Data = new GameData();
            MainCharacterData.LoadLookupTables();
            MainCharacterData.LoadExpTable();
            PartyMemberData.LoadPartyExpTable();

            Client = new PineClient(PineClient.DefaultSlot);
            Trainer = new Trainer(Client, Data);

            YenBox.Text = "0";

            Loaded += (_, _) =>
            {
                StatsTab.Initialize(this);
                InventoryTab.Initialize(this);
                OtherTab.Initialize(this);
            };
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Client.Connect();
                if (!Client.Ping())
                    throw new PineException("PCSX2 did not respond. Is a game currently running?");
                StatusText.Text = "Connected \u2713";
                DisconnectButton.IsEnabled = true;
                RefreshAll();
            }
            catch (Exception ex)
            {
                Client.Close();
                StatusText.Text = "Not connected";
                DisconnectButton.IsEnabled = false;
                MessageBox.Show(
                    $"{ex.Message}\n\nPlease ensure PCSX2 is running, a game is loaded, and " +
                    "Settings > Advanced > Enable PINE Server is enabled.",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            Client.Close();
            StatusText.Text = "Not connected";
            DisconnectButton.IsEnabled = false;
        }

        /// <summary>Checks whether client is connected and alerts user if disconnected.</summary>
        public bool RequireConnected()
        {
            if (!Client.Connected)
            {
                DisconnectButton.IsEnabled = false;
                if (StatusText.Text != "Not connected") StatusText.Text = "Not connected";
                MessageBox.Show("Please click Connect first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void RefreshYen()
        {
            try { YenBox.Text = Trainer.GetYen().ToString(); }
            catch { }
        }

        private void SetYen_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireConnected()) return;
            if (!uint.TryParse(YenBox.Text, out var amount))
            {
                MessageBox.Show("Yen must be a valid whole number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                Trainer.SetYen(amount);
                RefreshYen();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Refreshes state across all tabs.</summary>
        public void RefreshAll()
        {
            RefreshYen();
            InventoryTab.RefreshOwned();
            StatsTab.LoadCurrent();
            OtherTab.LoadCurrent();
        }
    }
}
