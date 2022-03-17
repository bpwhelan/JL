﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using JL.Core;
using JL.Core.Utilities;
using JL.Windows.Utilities;

namespace JL.Windows.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IFrontend
    {
        #region Interface members

        public CoreConfig CoreConfig { get; set; } = ConfigManager.Instance;

        public void PlayAudio(byte[] sound, float volume) => WindowsUtils.PlayAudio(sound, volume);

        public void Alert(AlertLevel alertLevel, string message) => WindowsUtils.Alert(alertLevel, message);

        public bool ShowYesNoDialog(string text, string caption)
        {
            return MessageBox.Show(
                text, caption,
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes,
                MessageBoxOptions.DefaultDesktopOnly) == MessageBoxResult.Yes;
        }

        public void ShowOkDialog(string text, string caption)
        {
            MessageBox.Show(text, caption, MessageBoxButton.OK,
                MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        }

        public Task UpdateJL(Version latestVersion) => WindowsUtils.UpdateJL(latestVersion);

        #endregion

        public static readonly List<string> Backlog = new();

        private int _currentTextIndex;

        private static PopupWindow s_firstPopupWindow;

        public static PopupWindow FirstPopupWindow
        {
            get { return s_firstPopupWindow ??= new PopupWindow(); }
        }

        public static MainWindow Instance { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            {
                Exception ex = (Exception)eventArgs.ExceptionObject;
                Utils.Logger.Error(ex.ToString());
            };

            ClipboardManager windowClipboardManager = new(this);
            windowClipboardManager.ClipboardChanged += ClipboardChanged;

            Instance = this;

            WindowsUtils.InitializeMainWindow();
            MainTextBox.IsInactiveSelectionHighlightEnabled = true;
            MainWindowChrome.Freeze();

            CopyFromClipboard();
        }

        private void CopyFromClipboard()
        {
            bool gotTextFromClipboard = false;
            while (Clipboard.ContainsText() && !gotTextFromClipboard)
            {
                try
                {
                    string text = Clipboard.GetText();
                    gotTextFromClipboard = true;
                    if (Storage.JapaneseRegex.IsMatch(text))
                    {
                        text = text.Trim();
                        MainWindow.Backlog.Add(text);
                        MainTextBox.Text = text;
                        MainTextBox.Foreground = ConfigManager.MainWindowTextColor;
                        _currentTextIndex = MainWindow.Backlog.Count - 1;
                    }
                }
                catch (Exception e)
                {
                    Utils.Logger.Warning(e, "CopyFromClipboard failed");
                }
            }
        }

        private void ClipboardChanged(object sender, EventArgs e)
        {
            CopyFromClipboard();
        }

        public void MainTextBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (ConfigManager.LookupOnSelectOnly || Background.Opacity == 0 || MainTextboxContextMenu.IsVisible) return;
            FirstPopupWindow.TextBox_MouseMove(MainTextBox);
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                string allBacklogText = string.Join("\n", MainWindow.Backlog);
                if (MainTextBox.Text != allBacklogText)
                {
                    if (MainTextBox.GetFirstVisibleLineIndex() == 0)
                    {
                        int caretIndex = allBacklogText.Length - MainTextBox.Text.Length;

                        MainTextBox.Text =
                            "Characters: " + new StringInfo(string.Join("", MainWindow.Backlog))
                                .LengthInTextElements + " / "
                            + "Lines: " + MainWindow.Backlog.Count + "\n"
                            + allBacklogText;
                        MainTextBox.Foreground = ConfigManager.MainWindowBacklogTextColor;

                        if (caretIndex >= 0)
                            MainTextBox.CaretIndex = caretIndex;

                        MainTextBox.ScrollToEnd();
                    }
                }
            }
        }

        private void MinimizeButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpacitySlider.Visibility = Visibility.Collapsed;
            FontSizeSlider.Visibility = Visibility.Collapsed;
            WindowState = WindowState.Minimized;
        }

        private void MinimizeButton_MouseEnter(object sender, MouseEventArgs e)
        {
            MinimizeButton.Foreground = new SolidColorBrush(Colors.SteelBlue);
        }

        private void MainTextBox_MouseLeave(object sender, MouseEventArgs e)
        {
            //OpacitySlider.Visibility = Visibility.Collapsed;
            //FontSizeSlider.Visibility = Visibility.Collapsed;

            if (FirstPopupWindow.MiningMode || ConfigManager.LookupOnSelectOnly) return;

            FirstPopupWindow.Hide();
            FirstPopupWindow.LastText = "";
        }

        private void MinimizeButton_MouseLeave(object sender, MouseEventArgs e)
        {
            MinimizeButton.Foreground = new SolidColorBrush(Colors.White);
        }

        private void CloseButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void CloseButton_MouseEnter(object sender, MouseEventArgs e)
        {
            CloseButton.Foreground = new SolidColorBrush(Colors.SteelBlue);
        }

        private void CloseButton_MouseLeave(object sender, MouseEventArgs e)
        {
            CloseButton.Foreground = new SolidColorBrush(Colors.White);
        }

        private void OpacityButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FontSizeSlider.Visibility = Visibility.Collapsed;

            if (Background.Opacity == 0)
                Background.Opacity = OpacitySlider.Value / 100;

            else if (OpacitySlider.Visibility == Visibility.Collapsed)
            {
                OpacitySlider.Visibility = Visibility.Visible;
                OpacitySlider.Focus();
            }

            else
                OpacitySlider.Visibility = Visibility.Collapsed;
        }

        private void FontSizeButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OpacitySlider.Visibility = Visibility.Collapsed;

            if (FontSizeSlider.Visibility == Visibility.Collapsed)
            {
                FontSizeSlider.Visibility = Visibility.Visible;
                FontSizeSlider.Focus();
            }

            else
                FontSizeSlider.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ConfigManager.SaveBeforeClosing();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Background.Opacity = OpacitySlider.Value / 100;
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MainTextBox.FontSize = FontSizeSlider.Value;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowPreferencesWindowKeyGesture))
            {
                WindowsUtils.ShowPreferencesWindow();
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.MousePassThroughModeKeyGesture))
            {
                Background.Opacity = 0;
                Keyboard.ClearFocus();
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.InvisibleToggleModeKeyGesture))
            {
                ConfigManager.InvisibleMode = !ConfigManager.InvisibleMode;
                MainGrid.Opacity = ConfigManager.InvisibleMode ? 0 : 1;
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.KanjiModeKeyGesture))
            {
                // fixes double toggling KanjiMode
                e.Handled = true;

                ConfigManager.Instance.KanjiMode = !ConfigManager.Instance.KanjiMode;
                FirstPopupWindow.LastText = "";
                MainTextBox_MouseMove(null, null);
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowAddNameWindowKeyGesture))
            {
                if (Storage.Ready)
                    WindowsUtils.ShowAddNameWindow(MainTextBox.SelectedText);
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowAddWordWindowKeyGesture))
            {
                if (Storage.Ready)
                    WindowsUtils.ShowAddWordWindow(MainTextBox.SelectedText);
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ShowManageDictionariesWindowKeyGesture))
            {
                if (Storage.Ready
                    && !Storage.UpdatingJMdict
                    && !Storage.UpdatingJMnedict
                    && !Storage.UpdatingKanjidic)
                {
                    WindowsUtils.ShowManageDictionariesWindow();
                }
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.SearchWithBrowserKeyGesture))
            {
                WindowsUtils.SearchWithBrowser(MainTextBox.SelectedText);
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.InactiveLookupModeKeyGesture))
            {
                ConfigManager.InactiveLookupMode = !ConfigManager.InactiveLookupMode;
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.MotivationKeyGesture))
            {
                WindowsUtils.Motivate($"Resources/Motivation");
            }

            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.ClosePopupKeyGesture))
            {
                FirstPopupWindow.MiningMode = false;
                FirstPopupWindow.TextBlockMiningModeReminder.Visibility = Visibility.Collapsed;

                FirstPopupWindow.PopUpScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                FirstPopupWindow.Hide();
            }
        }

        private void AddName(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowAddNameWindow(MainTextBox.SelectedText);
        }

        private void AddWord(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowAddWordWindow(MainTextBox.SelectedText);
        }

        private void ShowPreferences(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowPreferencesWindow();
        }

        private void SearchWithBrowser(object sender, RoutedEventArgs e)
        {
            WindowsUtils.SearchWithBrowser(MainTextBox.SelectedText);
        }

        private void ShowManageDictionariesWindow(object sender, RoutedEventArgs e)
        {
            WindowsUtils.ShowManageDictionariesWindow();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            SteppedBacklog(e);
        }

        private void SteppedBacklog(KeyEventArgs e)
        {
            if (WindowsUtils.KeyGestureComparer(e, ConfigManager.SteppedBacklogBackwardsKeyGesture))
            {
                if (_currentTextIndex != 0)
                {
                    _currentTextIndex--;
                    MainTextBox.Foreground = ConfigManager.MainWindowBacklogTextColor;
                }

                MainTextBox.Text = MainWindow.Backlog[_currentTextIndex];
            }
            else if (WindowsUtils.KeyGestureComparer(e, ConfigManager.SteppedBacklogForwardsKeyGesture))
            {
                if (_currentTextIndex < MainWindow.Backlog.Count - 1)
                {
                    _currentTextIndex++;
                    MainTextBox.Foreground = ConfigManager.MainWindowBacklogTextColor;
                }

                if (_currentTextIndex == MainWindow.Backlog.Count - 1)
                {
                    MainTextBox.Foreground = ConfigManager.MainWindowTextColor;
                }

                MainTextBox.Text = MainWindow.Backlog[_currentTextIndex];
            }
        }

        private void OpacitySlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            OpacitySlider.Visibility = Visibility.Collapsed;
        }

        private void OpacitySlider_LostFocus(object sender, RoutedEventArgs e)
        {
            OpacitySlider.Visibility = Visibility.Collapsed;
        }

        private void FontSizeSlider_LostMouseCapture(object sender, MouseEventArgs e)
        {
            FontSizeSlider.Visibility = Visibility.Collapsed;
        }

        private void FontSizeSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            FontSizeSlider.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ConfigManager.MainWindowHeight = Height;
            ConfigManager.MainWindowWidth = Width;
        }

        private void MainTextBox_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ManageDictionariesButton.IsEnabled = Storage.Ready
                                                 && !Storage.UpdatingJMdict
                                                 && !Storage.UpdatingJMnedict
                                                 && !Storage.UpdatingKanjidic;

            AddNameButton.IsEnabled = Storage.Ready;
            AddWordButton.IsEnabled = Storage.Ready;
        }

        private void MainTextBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!ConfigManager.LookupOnSelectOnly
                || Background.Opacity == 0
                || ConfigManager.InactiveLookupMode) return;

            //if (ConfigManager.RequireLookupKeyPress
            //    && !Keyboard.Modifiers.HasFlag(ConfigManager.LookupKey))
            //    return;

            FirstPopupWindow.LookupOnSelect(MainTextBox);
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            foreach (PopupWindow popupWindow in Application.Current.Windows.OfType<PopupWindow>().ToList())
            {
                popupWindow.MiningMode = false;
                popupWindow.TextBlockMiningModeReminder.Visibility = Visibility.Collapsed;

                popupWindow.Hide();
            }
        }

        private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            WindowsUtils.Dpi = e.NewDpi;
            WindowsUtils.ActiveScreen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            WindowsUtils.WorkAreaWidth = WindowsUtils.ActiveScreen.Bounds.Width / e.NewDpi.DpiScaleX;
            WindowsUtils.WorkAreaHeight = WindowsUtils.ActiveScreen.Bounds.Height / e.NewDpi.DpiScaleY;
            WindowsUtils.DpiAwareXOffset = ConfigManager.PopupXOffset / e.NewDpi.DpiScaleX;
            WindowsUtils.DpiAwareYOffset = ConfigManager.PopupYOffset / e.NewDpi.DpiScaleY;
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ConfigManager.LookupOnSelectOnly)
            {
                double verticalOffset = MainTextBox.VerticalOffset;
                MainTextBox.Select(0, 0);
                MainTextBox.ScrollToVerticalOffset(verticalOffset);
            }
        }
    }
}
