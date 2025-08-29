using System.Windows;
using System.Windows.Input;

namespace Pcnt
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            Loaded += (_, __) =>
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            };

            // 길게 누른 키로 연속 카운트가 싫다면 주석 해제
            // PreviewKeyDown += (_, e) => { if (e.IsRepeat) { e.Handled = true; } };
        }
    }
}
