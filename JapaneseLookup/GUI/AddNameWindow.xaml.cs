﻿using JapaneseLookup.CustomDict;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace JapaneseLookup.GUI
{
    /// <summary>
    /// Interaction logic for AddNameWindow.xaml
    /// </summary>
    public partial class AddNameWindow : Window
    {
        private static AddNameWindow _instance;

        public static AddNameWindow Instance
        {
            get
            {
                if (_instance == null || !_instance.IsLoaded)
                    _instance = new AddNameWindow();

                return _instance;
            }
        }

        public AddNameWindow()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValidated = true;

            if (!MainWindowUtilities.JapaneseRegex.IsMatch(SpellingTextBox.Text))
            {
                SpellingTextBox.BorderBrush = Brushes.Red;
                isValidated = false;
            }
            else if (SpellingTextBox.BorderBrush == Brushes.Red)
            {
                SpellingTextBox.BorderBrush = (SolidColorBrush) new BrushConverter().ConvertFrom("#FF3F3F46");
            }

            if (ReadingTextBox.Text == "")
            {
                ReadingTextBox.BorderBrush = Brushes.Red;
                isValidated = false;
            }
            else if (ReadingTextBox.BorderBrush == Brushes.Red)
            {
                ReadingTextBox.BorderBrush = (SolidColorBrush) new BrushConverter().ConvertFrom("#FF3F3F46");
            }

            if (isValidated)
            {
                string nameType =
                    NameTypeStackPanel.Children.OfType<RadioButton>()
                        .FirstOrDefault(r => r.IsChecked.HasValue && r.IsChecked.Value)!.Content.ToString();
                string spelling = SpellingTextBox.Text;
                string reading = ReadingTextBox.Text;
                await WriteToFile(spelling, reading, nameType);
                CustomNameLoader.AddToDictionary(spelling, reading, nameType);
                Close();
            }
        }

        private static async Task WriteToFile(string spelling, string reading, string type)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append(spelling);
            stringBuilder.Append('\t');
            stringBuilder.Append(reading);
            stringBuilder.Append('\t');
            stringBuilder.Append(type);
            stringBuilder.Append(Environment.NewLine);

            await File.AppendAllTextAsync(
                Path.Join(ConfigManager.ApplicationPath, "Resources/custom_names.txt"),
                stringBuilder.ToString(), Encoding.UTF8);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OtherRadioButton.IsChecked = true;
        }
    }
}