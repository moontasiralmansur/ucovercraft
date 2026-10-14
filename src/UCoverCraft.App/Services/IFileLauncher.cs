namespace UCoverCraft.App.Services;

public interface IFileLauncher
{
    void OpenFile(string path, Action<string>? onError = null);
    void OpenFolder(string path, Action<string>? onError = null);
    void CopyToClipboard(string text, Action<string>? onError = null);
}