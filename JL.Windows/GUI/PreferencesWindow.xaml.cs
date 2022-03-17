﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Controls;
using HandyControl.Tools;
using JL.Core;
using JL.Core.Anki;
using JL.Core.Network;
using JL.Core.Utilities;
using JL.Windows.Utilities;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace JL.Windows.GUI
{
    /// <summary>
    /// Interaction logic for PreferenceWindow.xaml
    /// </summary>
    public partial class PreferencesWindow : System.Windows.Window
    {
        private static PreferencesWindow s_instance;
        private bool _setAnkiConfig;

        public static PreferencesWindow Instance
        {
            get { return s_instance ??= new PreferencesWindow(); }
        }

        public PreferencesWindow()
        {
            InitializeComponent();
        }

        #region EventHandlers

        private void ShowColorPicker(object sender, RoutedEventArgs e)
        {
            ColorPicker picker = SingleOpenHelper.CreateControl<ColorPicker>();
            var window = new HandyControl.Controls.PopupWindow { PopupElement = picker, };
            picker.Canceled += delegate { window.Close(); };
            picker.Confirmed += delegate { ColorSetter((Button)sender, picker.SelectedBrush, window); };

            window.ShowDialog(picker, false);
        }

        private static void ColorSetter(Button sender, SolidColorBrush selectedColor,
            HandyControl.Controls.PopupWindow window)
        {
            sender.Background = selectedColor;
            window.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            ConfigManager.Instance.SavePreferences(this);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Visibility = Visibility.Collapsed;
        }

        private async void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var itemTab = (System.Windows.Controls.TabItem)TabControl.SelectedItem;
            if (itemTab == null)
                return;

            switch (itemTab.Header)
            {
                case "Anki":
                    if (ConfigManager.Instance.AnkiIntegration && !_setAnkiConfig)
                    {
                        await SetPreviousMiningConfig();
                        if (MiningSetupComboBoxDeckNames.SelectedItem == null)
                            await PopulateDeckAndModelNames();

                        _setAnkiConfig = true;
                    }

                    break;
            }
        }

        private void CheckForJLUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            Networking.CheckForJLUpdates(false);
        }

        #endregion

        #region MiningSetup

        private async Task SetPreviousMiningConfig()
        {
            try
            {
                AnkiConfig ankiConfig = await AnkiConfig.ReadAnkiConfig();
                if (ankiConfig == null)
                    return;

                MiningSetupComboBoxDeckNames.ItemsSource = new List<string> { ankiConfig.DeckName };
                MiningSetupComboBoxDeckNames.SelectedIndex = 0;
                MiningSetupComboBoxModelNames.ItemsSource = new List<string> { ankiConfig.ModelName };
                MiningSetupComboBoxModelNames.SelectedIndex = 0;
                TagsTextBox.Text = string.Join(",", ankiConfig.Tags);
                CreateFieldElements(ankiConfig.Fields);
            }
            catch (Exception e)
            {
                // config probably doesn't exist; no need to alert the user
                Utils.Logger.Warning(e, "Error setting previous mining config");
            }
        }

        private async Task PopulateDeckAndModelNames()
        {
            Response getNameResponse = await AnkiConnect.GetDeckNames();
            Response getModelResponse = await AnkiConnect.GetModelNames();

            if (getNameResponse != null && getModelResponse != null)
            {
                try
                {
                    List<string> deckNamesList =
                        JsonSerializer.Deserialize<List<string>>(getNameResponse.Result.ToString()!);

                    MiningSetupComboBoxDeckNames.ItemsSource = deckNamesList;

                    List<string> modelNamesList =
                        JsonSerializer.Deserialize<List<string>>(getModelResponse.Result.ToString()!);
                    MiningSetupComboBoxModelNames.ItemsSource = modelNamesList;
                }

                catch
                {
                    WindowsUtils.Alert(AlertLevel.Error, "Error getting deck and model names");
                    Utils.Logger.Error("Error getting deck and model names");
                    MiningSetupComboBoxDeckNames.ItemsSource = "";
                    MiningSetupComboBoxModelNames.ItemsSource = "";
                }
            }

            else
            {
                WindowsUtils.Alert(AlertLevel.Error, "Error getting deck and model names");
                Utils.Logger.Error("Error getting deck and model names");
                MiningSetupComboBoxDeckNames.ItemsSource = "";
                MiningSetupComboBoxModelNames.ItemsSource = "";
            }
        }

        private async void MiningSetupButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            await PopulateDeckAndModelNames();
        }

        private async void MiningSetupButtonGetFields_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string modelName = MiningSetupComboBoxModelNames.SelectionBoxItem.ToString();
                List<string> fieldNames =
                    JsonSerializer.Deserialize<List<string>>((await AnkiConnect.GetModelFieldNames(modelName)).Result
                        .ToString()!);

                Dictionary<string, JLField> fields =
                    fieldNames!.ToDictionary(fieldName => fieldName, _ => JLField.Nothing);

                CreateFieldElements(fields);
            }
            catch (Exception exception)
            {
                WindowsUtils.Alert(AlertLevel.Error, "Error getting fields from AnkiConnect");
                Utils.Logger.Error(exception, "Error getting fields from AnkiConnect");
            }
        }

        private void CreateFieldElements(Dictionary<string, JLField> fields)
        {
            MiningSetupStackPanelFields.Children.Clear();

            IEnumerable<JLField> jlFieldNames = Enum.GetValues(typeof(JLField)).Cast<JLField>();
            string[] descriptions = jlFieldNames
                .Select(jlFieldName => jlFieldName.GetDescription() ?? jlFieldName.ToString()).ToArray();

            try
            {
                foreach ((string fieldName, JLField jlField) in fields)
                {
                    StackPanel stackPanel = new();
                    var textBlockFieldName = new TextBlock { Text = fieldName };
                    var comboBoxJLFields = new System.Windows.Controls.ComboBox
                    {
                        ItemsSource = descriptions,
                        SelectedItem = jlField.GetDescription() ?? jlField.ToString()
                    };

                    stackPanel.Children.Add(textBlockFieldName);
                    stackPanel.Children.Add(comboBoxJLFields);
                    MiningSetupStackPanelFields.Children.Add(stackPanel);
                }
            }
            catch (Exception exception)
            {
                WindowsUtils.Alert(AlertLevel.Error, "Error creating field elements");
                Utils.Logger.Error(exception, "Error creating field elements");
            }
        }

        private async void MiningSetupButtonSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string deckName = MiningSetupComboBoxDeckNames.SelectionBoxItem.ToString();
                string modelName = MiningSetupComboBoxModelNames.SelectionBoxItem.ToString();

                Dictionary<string, JLField> dict = new();
                foreach (StackPanel stackPanel in MiningSetupStackPanelFields.Children)
                {
                    var textBlock = (TextBlock)stackPanel.Children[0];
                    var comboBox = (System.Windows.Controls.ComboBox)stackPanel.Children[1];

                    string selectedDescription = comboBox.SelectionBoxItem.ToString();

                    IEnumerable<JLField> jlFieldNames = Enum.GetValues(typeof(JLField)).Cast<JLField>();
                    JLField result = jlFieldNames.FirstOrDefault(jlFieldName =>
                            (jlFieldName.GetDescription() ?? jlFieldName.ToString()) == selectedDescription,
                        JLField.Nothing);

                    dict.Add(textBlock.Text, result);
                }

                Dictionary<string, JLField> fields = dict;

                string rawTags = TagsTextBox.Text;
                string[] tags = string.IsNullOrEmpty(rawTags)
                    ? Array.Empty<string>()
                    : rawTags.Split(',').Select(s => s.Trim()).ToArray();

                if (MiningSetupComboBoxDeckNames.SelectedItem == null ||
                    MiningSetupComboBoxModelNames.SelectedItem == null)
                {
                    WindowsUtils.Alert(AlertLevel.Error, "Save failed: Incomplete config");
                    Utils.Logger.Error("Save failed: Incomplete config");
                    return;
                }

                AnkiConfig ankiConfig = new(deckName, modelName, fields, tags);
                if (await AnkiConfig.WriteAnkiConfig(ankiConfig).ConfigureAwait(false))
                {
                    WindowsUtils.Alert(AlertLevel.Success, "Saved AnkiConfig");
                    Utils.Logger.Information("Saved AnkiConfig");
                }
            }
            catch (Exception exception)
            {
                WindowsUtils.Alert(AlertLevel.Error, "Error saving AnkiConfig");
                Utils.Logger.Error(exception, "Error saving AnkiConfig");
            }
        }

        #endregion

        #region Keys

        private void KeyGestureToText(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.LeftShift || key == Key.RightShift
                                     || key == Key.LeftCtrl || key == Key.RightCtrl
                                     || key == Key.LeftAlt || key == Key.RightAlt
                                     || key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            StringBuilder hotkeyTextBuilder = new();

            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                hotkeyTextBuilder.Append("Ctrl+");
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                hotkeyTextBuilder.Append("Shift+");
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                hotkeyTextBuilder.Append("Alt+");
            }

            hotkeyTextBuilder.Append(key.ToString());

            ((TextBox)sender).Text = hotkeyTextBuilder.ToString();
        }

        private void ClearKeyGestureButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var dockPanel = (DockPanel)button.Parent;
            TextBox textBox = dockPanel.Children.OfType<TextBox>().ToArray()[0];
            textBox.Text = "None";
        }

        #endregion
    }
}
