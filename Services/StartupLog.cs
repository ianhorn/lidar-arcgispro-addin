using System;
using System.IO;

namespace StaclidarAddin
{
    /// <summary>Pro greys out the Staclidar button without a word when the pane fails to build, so a failure
    /// there is written to %LOCALAPPDATA%\Staclidar\startup-error.log and shown in a message box.</summary>
    internal static class StartupLog
    {
        public static void Report(string what, Exception ex)
        {
            string path = null;
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Staclidar");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, "startup-error.log");
                File.AppendAllText(path, $"{DateTime.Now:s} while {what}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }
            try
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"Staclidar failed while {what}:{Environment.NewLine}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}"
                    + (path != null ? $"{Environment.NewLine}{Environment.NewLine}Details: {path}" : ""),
                    "Staclidar");
            }
            catch { }
        }
    }
}
