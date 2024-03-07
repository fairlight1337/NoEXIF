namespace NoEXIF
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security;
    using System.Security.Cryptography;
    using System.Security.Principal;
    using System.Windows.Forms;
    using Microsoft.Win32;
    using ImageMagick;
    using System.Linq;

    internal static class Program
    {
        private static readonly string ApplicationPath = Application.ExecutablePath;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // When the application starts without arguments, it's assumed it needs to set up the registry.
            if (args.Length == 0)
            {
                if (!IsAdministrator())
                {
                    // If not an admin, relaunch as admin to set up the registry.
                    RelaunchAsAdministrator(new[] { "setup" });
                }
                else
                {
                    // Setup the registry if running as admin.
                    SetupRegistry();
                }
            }
            else if (args.Length > 0)
            {
                if (!RegistryIsSetup())
                {
                    // If the registry is not set up and the program is not running as admin, relaunch as admin.
                    if (!IsAdministrator())
                    {
                        RelaunchAsAdministrator(new[] { "setup" }.Concat(args).ToArray());
                    }
                    else
                    {
                        // If the registry is not set up but the program is running as admin, set up the registry.
                        SetupRegistry();
                    }
                }

                // Perform the requested actions on files or folders.
                string action = args[0].ToLowerInvariant();

                for (int i = 1; i < args.Length; i++)
                {
                    string path = args[i];
                    switch (action)
                    {
                        case "removeexif":
                            RemoveExif(path);
                            break;
                        case "renametohash":
                            RenameToHash(path);
                            break;
                    }
                }
            }
        }

        private static bool RegistryIsSetup()
        {
            bool isSetup = true;

            // Check each key and its command's value to ensure they are set up correctly.
            isSetup &= CheckRegistryKey(@"*\shell\Remove EXIF Data", "Remove EXIF Data from file", "RemoveEXIF");
            isSetup &= CheckRegistryKey(@"Directory\shell\Remove EXIF Data", "Remove EXIF Data from directory", "RemoveEXIF");
            isSetup &= CheckRegistryKey(@"*\shell\Rename to Hash Value", "Rename file to SHA256 hash", "RenameToHash");
            isSetup &= CheckRegistryKey(@"Directory\shell\Rename to Hash Value", "Rename directory files to SHA256 hash", "RenameToHash");

            return isSetup;
        }

        private static bool CheckRegistryKey(string keyPath, string description, string action)
        {
            using (var key = Registry.ClassesRoot.OpenSubKey(keyPath))
            {
                if (key == null || key.GetValue("") as string != description)
                {
                    return false;
                }

                using (var commandKey = key.OpenSubKey("command"))
                {
                    if (commandKey == null)
                    {
                        return false;
                    }

                    string expectedValue = $"\"{ApplicationPath}\" {action} \"%1\"";
                    string actualValue = commandKey.GetValue("") as string;

                    if (actualValue != expectedValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RelaunchAsAdministrator(string[] args)
        {
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                Verb = "runas",
                Arguments = string.Join(" ", args),
                UseShellExecute = true
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // The user refused the elevation
                MessageBox.Show($"Cannot register application without elevated privileges.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Environment.Exit(0);
        }

        private static void SetupRegistry()
        {
            SetupRegistryKey(@"*\shell\Remove EXIF Data", "Remove EXIF Data from file", "RemoveEXIF");
            SetupRegistryKey(@"Directory\shell\Remove EXIF Data", "Remove EXIF Data from directory", "RemoveEXIF");
            SetupRegistryKey(@"*\shell\Rename to Hash Value", "Rename file to SHA256 hash", "RenameToHash");
            SetupRegistryKey(@"Directory\shell\Rename to Hash Value", "Rename directory files to SHA256 hash", "RenameToHash");
        }

        private static void SetupRegistryKey(string keyPath, string description, string action)
        {
            try
            {
                using (var key = Registry.ClassesRoot.OpenSubKey(keyPath, true))
                {
                    if (key == null || key.GetValue("") as string != description)
                    {
                        Registry.ClassesRoot.CreateSubKey(keyPath).SetValue("", description);
                    }

                    using (var commandKey = key?.OpenSubKey("command", true))
                    {
                        string expectedValue = $"\"{ApplicationPath}\" {action} \"%1\"";
                        if (commandKey == null || commandKey.GetValue("") as string != expectedValue)
                        {
                            (commandKey ?? key.CreateSubKey("command")).SetValue("", expectedValue);
                        }
                    }
                }
            }
            catch (SecurityException ex)
            {
                MessageBox.Show($"Failed to update registry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                // General exception handling
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RemoveExif(string filePath)
        {
            int fileCount = 0;
            if (File.Exists(filePath))
            {
                CorrectOrientationAndRemoveExif(filePath);
                fileCount = 1;
            }
            else if (Directory.Exists(filePath))
            {
                foreach (var file in Directory.EnumerateFiles(filePath, "*.*", SearchOption.AllDirectories))
                {
                    if (MagickFormatInfo.Create(file).SupportsReading)
                    {
                        CorrectOrientationAndRemoveExif(file);
                        fileCount++;
                    }
                }
            }

            MessageBox.Show($"EXIF data removed from {fileCount} files.", "Operation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void CorrectOrientationAndRemoveExif(string filePath)
        {
            using (var image = new MagickImage(filePath))
            {
                // Auto-orient based on the EXIF orientation data
                image.AutoOrient();

                // Now remove the EXIF profile
                image.RemoveProfile("exif");

                // Save the changes
                image.Write(filePath);
            }
        }

        private static void RenameToHash(string filePath)
        {
            if (File.Exists(filePath))
            {
                string hash = ComputeSha256Hash(filePath);
                string newFileName = Path.Combine(Path.GetDirectoryName(filePath), hash + Path.GetExtension(filePath));
                File.Move(filePath, newFileName);

                MessageBox.Show($"File renamed to {newFileName}.", "Operation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static string ComputeSha256Hash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
