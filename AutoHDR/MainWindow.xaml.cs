using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace AutoHDREnabler
{
    public partial class MainWindow : Window
    {
        const string D3D_KEY = "SOFTWARE\\Microsoft\\Direct3D";

        public MainWindow()
        {
            InitializeComponent();
            CreateRegistryButton.IsEnabled = false; // Initially disable the button
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Executable Files|*.exe";

            if (openFileDialog.ShowDialog() == true)
            {
                // Set the selected file path in the TextBox
                PathTextBox.Text = openFileDialog.FileName;
                // Enable the "Create Registry Entry" button since a file is selected
                CreateRegistryButton.IsEnabled = true;
            }
        }

        private void CreateRegistryButton_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the path from the TextBox
            string path = PathTextBox.Text;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("Please select a valid executable file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string exeName = path;
            int len = path.Length;

            for (int i = 0; i < len; i++)
            {
                if (path[i] == '/')
                {
                    path = path.Replace('/', '\\');
                    exeName = path.Substring(i + 1);
                    break;
                }
            }

            string key = FindSubkeyByName(path);

            int behaviorExists = 0;
            if (key != null)
            {
                OutputTextBlock.Text += "Found existing key for game (";
                int lastPartIndex = key.LastIndexOf('\\');

                if (lastPartIndex != -1)
                {
                    OutputTextBlock.Text += key.Substring(lastPartIndex + 1);
                }
                else
                {
                    Environment.Exit(1);
                }

                OutputTextBlock.Text += ")\n";
                behaviorExists = PrintD3DBehaviorsValue(key);
            }
            else
            {
                string newSubkey = FindFreeApplicationSubkey();
                OutputTextBlock.Text += "No existing key found, will be created as " + newSubkey + "\n";
                int keyLength = D3D_KEY.Length + newSubkey.Length + 1;
                key = D3D_KEY + "\\" + newSubkey;

                if (CreateRegistryEntry(key, path) != 0)
                {
                    Environment.Exit(1);
                }
            }

            int result = GetYesNoResponse("Force enable Auto HDR?");
            if (result == 0)
            {
                if (behaviorExists != 0)
                {
                    result = GetYesNoResponse("Delete existing D3DBehaviors?");
                    if (result != 0)
                    {
                        if (SetRegistryValue(key, null))
                        {
                            OutputTextBlock.Text += "Success!\n\n";
                        }
                    }
                }
                else
                {
                    OutputTextBlock.Text += "You selected No\n";
                }
                return;
            }

            // Use the CheckBox to determine whether to enable 10-bit
            bool enable10Bit = Enable10BitCheckBox.IsChecked == true;

            string behaviorVal;
            if (enable10Bit)
            {
                behaviorVal = "BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1";
            }
            else
            {
                behaviorVal = "BufferUpgradeOverride=1";
            }

            if (SetRegistryValue(key, behaviorVal))
            {
                OutputTextBlock.Text += "Success!\n\n";
            }
        }

        static int CreateDirect3DRegistryKey()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(D3D_KEY);
            if (key == null)
            {
                try
                {
                    key = Registry.CurrentUser.CreateSubKey(D3D_KEY);
                    if (key != null)
                    {
                        return 0; // Success
                    }
                    else
                    {
                        return 1; // Error creating Direct3D key
                    }
                }
                catch (Exception ex)
                {
                    return 1; // Error creating Direct3D key
                }
            }
            else
            {
                return 0; // Existing Direct3D key found
            }
        }

        static string FindSubkeyByName(string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(D3D_KEY))
            {
                if (key != null)
                {
                    foreach (string subkeyName in key.GetSubKeyNames())
                    {
                        if (subkeyName != "MostRecentApplication")
                        {
                            using (RegistryKey subKey = key.OpenSubKey(subkeyName))
                            {
                                if (subKey != null)
                                {
                                    string value = subKey.GetValue("Name") as string;
                                    if (value != null && value == name)
                                    {
                                        return Path.Combine(D3D_KEY, subkeyName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        static int PrintD3DBehaviorsValue(string keyPath)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                if (key != null)
                {
                    object value = key.GetValue("D3DBehaviors");
                    if (value != null)
                    {
                        return 1; // D3DBehaviors value exists
                    }
                }
            }
            return 0; // No existing D3DBehaviors value
        }

        static string FindFreeApplicationSubkey()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(D3D_KEY))
            {
                if (key != null)
                {
                    int index = 0;
                    while (true)
                    {
                        string subkeyName = "Application" + index;
                        using (RegistryKey opened = key.OpenSubKey(subkeyName))
                        {
                            if (opened == null)
                            {
                                return subkeyName;
                            }
                        }
                        index++;
                    }
                }
            }
            return null;
        }

        static int CreateRegistryEntry(string keyPath, string name)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("Name", name);
                    }
                    else
                    {
                        return 1; // Error creating/opening the registry key
                    }
                }
                return 0; // Success
            }
            catch (Exception ex)
            {
                return 1; // Error creating/opening the registry key
            }
        }

        static int GetYesNoResponse(string query)
        {
            int response;

            do
            {
                MessageBoxResult result = MessageBox.Show(query, "Question", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    response = 1;
                }
                else if (result == MessageBoxResult.No)
                {
                    response = 0;
                }
                else
                {
                    response = -1;
                }
            } while (response == -1);

            return response;
        }

        static bool SetRegistryValue(string subKeyPath, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath))
                {
                    if (key != null)
                    {
                        if (value != null)
                        {
                            key.SetValue("D3DBehaviors", value);
                        }
                        else
                        {
                            key.DeleteValue("D3DBehaviors", false);
                        }
                    }
                    else
                    {
                        return false; // Error opening or creating registry key
                    }
                }
                return true; // Success
            }
            catch (Exception ex)
            {
                return false; // Error opening or creating registry key
            }
        }
    }
}
