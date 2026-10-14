using System.Diagnostics;
using System.IO;
using System.Windows;

namespace UCoverCraft.App.Services;

public sealed class DefaultFileLauncher : IFileLauncher
{
    public void OpenFile(string path, Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            var message = $"Failed to open file '{path}': {ex.Message}";
            Debug.WriteLine(message);
            onError?.Invoke(message);
        }
    }

    public void OpenFolder(string path, Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            else
            {
                var message = $"Cannot open folder for '{path}': directory not found";
                Debug.WriteLine(message);
                onError?.Invoke(message);
            }
        }
        catch (Exception ex)
        {
            var message = $"Failed to open folder for '{path}': {ex.Message}";
            Debug.WriteLine(message);
            onError?.Invoke(message);
        }
    }

    public void CopyToClipboard(string text, Action<string>? onError = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Action clipboardAction = () =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                var message = $"Failed to copy to clipboard: {ex.Message}";
                Debug.WriteLine(message);
                onError?.Invoke(message);
            }
        };

        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            clipboardAction();
        }
        else
        {
            Application.Current?.Dispatcher.BeginInvoke(clipboardAction);
        }
    }
}