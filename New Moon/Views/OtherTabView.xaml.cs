using System;
using System.Windows;
using System.Windows.Controls;

namespace P3FESTrainer.Views
{
    public partial class OtherTabView : UserControl
    {
        private MainWindow? _app;

        public OtherTabView()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow app)
        {
            _app = app;
        }

        public void LoadCurrent()
        {
            if (_app == null || !_app.Client.Connected) return;

            try
            {
                GivenNameBox.Text = _app.Trainer.GetMcGivenName();
                SurnameBox.Text = _app.Trainer.GetMcSurname();
            }
            catch
            {
                GivenNameBox.Text = "";
                SurnameBox.Text = "";
            }
        }

        private void SetName_Click(object sender, RoutedEventArgs e)
        {
            if (_app == null || !_app.RequireConnected()) return;
            try
            {
                _app.Trainer.SetMcGivenName(GivenNameBox.Text);
                _app.Trainer.SetMcSurname(SurnameBox.Text);
                ResultText.Text = "Name written \u2713";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Write Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
