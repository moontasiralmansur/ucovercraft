using System;
using System.Threading.Tasks;
using UCoverCraft.App.Services;
using UCoverCraft.App.Settings;
using UCoverCraft.App.ViewModels;
using UCoverCraft.Core.Templates;
using Xunit;

namespace UCoverCraft.Tests.ViewModels;

public sealed class ExportViewModelTests
{
    [Fact]
    public void ExportState_StartsAsIdle()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(ExportState.Idle, viewModel.ExportState);
        Assert.False(viewModel.IsExporting);
        Assert.Equal(string.Empty, viewModel.ExportFormat);
        Assert.Equal(string.Empty, viewModel.ExportErrorMessage);
        Assert.Equal(string.Empty, viewModel.ExportOutputPath);
    }

    [Fact]
    public void ExportCommands_Disabled_WhenExporting()
    {
        var viewModel = new MainViewModel();

        Assert.True(viewModel.ExportPdfCommand.CanExecute(null));
        Assert.True(viewModel.ExportDocxCommand.CanExecute(null));
    }

    [Fact]
    public void OpenFileCommand_Disabled_WhenNotInSuccessState()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.OpenFileCommand.CanExecute(null));
    }

    [Fact]
    public void OpenFolderCommand_Disabled_WhenNotInSuccessState()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.OpenFolderCommand.CanExecute(null));
    }

    [Fact]
    public void CopyPathCommand_Disabled_WhenNotInSuccessState()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.CopyPathCommand.CanExecute(null));
    }

    [Fact]
    public void RetryExportCommand_OnlyWorksFromFailureState()
    {
        var viewModel = new MainViewModel();

        viewModel.RetryExportCommand.Execute(null);
        Assert.Equal(ExportState.Idle, viewModel.ExportState);
    }

    [Fact]
    public void RetryExportCommand_DoesNotStartNewExport_WhenAlreadyExporting()
    {
        var viewModel = new MainViewModel
        {
            // Simulate an in-progress export by setting internal state
        };
        var launcher = new TestFileLauncher();
        var viewModelWithLauncher = new MainViewModel(SettingsService.Default, launcher);

        // Manually set state to simulate exporting
        viewModelWithLauncher.GetType().GetField("_isExporting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModelWithLauncher, true);
        viewModelWithLauncher.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModelWithLauncher, ExportState.Failure);
        viewModelWithLauncher.GetType().GetField("_exportFormat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModelWithLauncher, "PDF");

        viewModelWithLauncher.RetryExportCommand.Execute(null);

        Assert.Equal(0, launcher.RenderCallCount);
    }

    [Fact]
    public void DismissExportSuccessCommand_ResetsState()
    {
        var viewModel = new MainViewModel();

        viewModel.DismissExportSuccessCommand.Execute(null);
        Assert.Equal(ExportState.Idle, viewModel.ExportState);
    }

    [Fact]
    public void DismissExportErrorCommand_ResetsState()
    {
        var viewModel = new MainViewModel();

        viewModel.DismissExportErrorCommand.Execute(null);
        Assert.Equal(ExportState.Idle, viewModel.ExportState);
    }

    [Fact]
    public void ResetExportState_ReturnsToIdle_AndClearsData()
    {
        var viewModel = new MainViewModel();

        viewModel.ResetExportState();

        Assert.Equal(ExportState.Idle, viewModel.ExportState);
        Assert.Equal(string.Empty, viewModel.ExportErrorMessage);
        Assert.Equal(string.Empty, viewModel.ExportOutputPath);
        Assert.Equal(string.Empty, viewModel.ExportFormat);
    }

    [Fact]
    public void ResetExportState_RaisesCommandCanExecuteChanged_WhenStateChanges()
    {
        var viewModel = new MainViewModel();
        var pdfCanExecuteChanged = false;
        var docxCanExecuteChanged = false;

        viewModel.ExportPdfCommand.CanExecuteChanged += (_, _) => pdfCanExecuteChanged = true;
        viewModel.ExportDocxCommand.CanExecuteChanged += (_, _) => docxCanExecuteChanged = true;

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);

        viewModel.ResetExportState();

        Assert.True(pdfCanExecuteChanged);
        Assert.True(docxCanExecuteChanged);
    }

    [Fact]
    public void FileLauncher_Abstraction_Works()
    {
        var launcher = new TestFileLauncher();

        launcher.OpenFile("test.pdf");
        launcher.OpenFolder("test.pdf");
        launcher.CopyToClipboard("test path");

        Assert.Equal("test.pdf", launcher.LastOpenedFile);
        Assert.Equal("test.pdf", launcher.LastOpenedFolder);
        Assert.Equal("test path", launcher.LastCopiedText);
    }

    [Fact]
    public void FileLauncher_HandlesNullAndEmptyGracefully()
    {
        var launcher = new TestFileLauncher();

        launcher.OpenFile("");
        launcher.OpenFile(null!);
        launcher.OpenFolder("");
        launcher.OpenFolder(null!);
        launcher.CopyToClipboard("");
        launcher.CopyToClipboard(null!);

        Assert.Null(launcher.LastOpenedFile);
        Assert.Null(launcher.LastOpenedFolder);
        Assert.Null(launcher.LastCopiedText);
    }

    [Fact]
    public void ExportValidationFailure_SetsFailureState_AndResetsExportingFlag()
    {
        var launcher = new TestFileLauncher();
        var viewModel = new MainViewModel(SettingsService.Default, launcher)
        {
            CourseTitle = "",
            CourseCode = "",
            SubmittedToName = "",
        };
        viewModel.SelectedDocumentType = viewModel.DocumentTypes.First(o => o.Value == DocumentType.LabReport);
        viewModel.Students[0].Name = "";

        viewModel.ExportPdfCommand.Execute(null);

        Assert.Equal(ExportState.Failure, viewModel.ExportState);
        Assert.False(viewModel.IsExporting);
        Assert.Contains("validation errors", viewModel.ExportErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.ExportPdfCommand.CanExecute(null));
        Assert.True(viewModel.ExportDocxCommand.CanExecute(null));
    }

    [Fact]
    public void ExportSuccessActions_UseExactOutputPath()
    {
        var launcher = new TestFileLauncher();
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        var testPath = @"C:\Exports\My Report.pdf";
        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, testPath);

        viewModel.OpenFileCommand.Execute(null);
        viewModel.OpenFolderCommand.Execute(null);
        viewModel.CopyPathCommand.Execute(null);

        Assert.Equal(testPath, launcher.LastOpenedFile);
        Assert.Equal(testPath, launcher.LastOpenedFolder);
        Assert.Equal(testPath, launcher.LastCopiedText);
    }

    [Fact]
    public void ExportFailure_DoesNotLeaveStaleSuccessData()
    {
        var launcher = new TestFileLauncher();
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\Old\Success.pdf");
        viewModel.GetType().GetField("_exportFormat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, "PDF");

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Failure);
        viewModel.GetType().GetField("_exportErrorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, "Access denied");

        Assert.Equal(ExportState.Failure, viewModel.ExportState);
        Assert.Equal("Access denied", viewModel.ExportErrorMessage);
    }

    [Fact]
    public void OpenFileCommand_SetsStatus_OnFailure()
    {
        var launcher = new TestFileLauncher { SimulateOpenFileError = "Access denied" };
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\test.pdf");

        viewModel.OpenFileCommand.Execute(null);

        Assert.Equal("Access denied", viewModel.Status);
    }

    [Fact]
    public void OpenFolderCommand_SetsStatus_OnFailure()
    {
        var launcher = new TestFileLauncher { SimulateOpenFolderError = "Directory not found" };
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\test.pdf");

        viewModel.OpenFolderCommand.Execute(null);

        Assert.Equal("Directory not found", viewModel.Status);
    }

    [Fact]
    public void CopyPathCommand_SetsStatus_OnClipboardFailure()
    {
        var launcher = new TestFileLauncher { SimulateCopyError = "Clipboard unavailable" };
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\test.pdf");

        viewModel.CopyPathCommand.Execute(null);

        Assert.Equal("Clipboard unavailable", viewModel.Status);
    }

    [Fact]
    public void OpenFileCommand_Succeeds_WhenLauncherSucceeds()
    {
        var launcher = new TestFileLauncher();
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\test.pdf");

        viewModel.OpenFileCommand.Execute(null);

        Assert.Equal(@"C:\test.pdf", launcher.LastOpenedFile);
        Assert.Equal(string.Empty, viewModel.Status);
    }

    [Fact]
    public void OpenFolderCommand_Succeeds_WhenLauncherSucceeds()
    {
        var launcher = new TestFileLauncher();
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\test.pdf");

        viewModel.OpenFolderCommand.Execute(null);

        Assert.Equal(@"C:\test.pdf", launcher.LastOpenedFolder);
        Assert.Equal(string.Empty, viewModel.Status);
    }

    [Fact]
    public void CopyPathCommand_Succeeds_WhenLauncherSucceeds()
    {
        var launcher = new TestFileLauncher();
        var viewModel = new MainViewModel(SettingsService.Default, launcher);

        viewModel.GetType().GetField("_exportState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, ExportState.Success);
        viewModel.GetType().GetField("_exportOutputPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(viewModel, @"C:\test.pdf");

        viewModel.CopyPathCommand.Execute(null);

        Assert.Equal(@"C:\test.pdf", launcher.LastCopiedText);
        Assert.Equal(string.Empty, viewModel.Status);
    }

    [Fact]
    public void DefaultFileLauncher_CopyToClipboard_HandlesCallbackException()
    {
        var launcher = new DefaultFileLauncher();
        var errorCaptured = false;
        string? capturedError = null;

        launcher.CopyToClipboard("test", error =>
        {
            errorCaptured = true;
            capturedError = error;
        });

        Assert.False(errorCaptured);
        Assert.Null(capturedError);
    }

    private sealed class TestFileLauncher : IFileLauncher
    {
        public int RenderCallCount { get; private set; }
        public string? LastOpenedFile { get; private set; }
        public string? LastOpenedFolder { get; private set; }
        public string? LastCopiedText { get; private set; }
        public string? LastOpenFileError { get; private set; }
        public string? LastOpenFolderError { get; private set; }
        public string? LastCopyError { get; private set; }

        public string? SimulateOpenFileError { get; set; }
        public string? SimulateOpenFolderError { get; set; }
        public string? SimulateCopyError { get; set; }

        public void OpenFile(string path, Action<string>? onError = null)
        {
            if (!string.IsNullOrEmpty(path))
            {
                LastOpenedFile = path;
            }

            if (SimulateOpenFileError != null)
            {
                LastOpenFileError = SimulateOpenFileError;
                onError?.Invoke(SimulateOpenFileError);
            }
        }

        public void OpenFolder(string path, Action<string>? onError = null)
        {
            if (!string.IsNullOrEmpty(path))
            {
                LastOpenedFolder = path;
            }

            if (SimulateOpenFolderError != null)
            {
                LastOpenFolderError = SimulateOpenFolderError;
                onError?.Invoke(SimulateOpenFolderError);
            }
        }

        public void CopyToClipboard(string text, Action<string>? onError = null)
        {
            if (!string.IsNullOrEmpty(text))
            {
                LastCopiedText = text;
            }

            if (SimulateCopyError != null)
            {
                LastCopyError = SimulateCopyError;
                onError?.Invoke(SimulateCopyError);
            }
        }
    }
}