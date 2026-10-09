using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using UCoverCraft.App;
using UCoverCraft.App.ViewModels;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.Views;

public class MainWindowTests
{
    [Fact]
    public void MainWindow_RendersLiveA4Preview_AndEnforcesTheStudentLimit()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var handlerField = typeof(Dispatcher).GetField(
                "UnhandledException",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var handlers = handlerField?.GetValue(Application.Current!.Dispatcher) as Delegate;

            Assert.NotNull(handlers);
            Assert.Contains(
                handlers!.GetInvocationList(),
                entry => entry.Method.DeclaringType == typeof(UCoverCraft.App.App));

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, DocumentType.LabReport);

                window.Show();
                window.UpdateLayout();

                var texts = Descendants<TextBlock>(window).Select(block => block.Text).ToList();
                Assert.Contains("LAB REPORT", texts);
                Assert.Contains("Submitted by:", texts);

                var titleSize = 14d * 96 / 72;
                var title = Descendants<TextBlock>(window)
                    .Single(block => block.Text == "LAB REPORT" && Math.Abs(block.FontSize - titleSize) < 0.01);
                Assert.Equal(new FontFamily("Times New Roman"), title.FontFamily);
                Assert.Equal(FontWeights.Bold, title.FontWeight);
                Assert.Equal(titleSize, title.FontSize, 3);

                var logo = Assert.Single(
                    Descendants<Image>(window),
                    image => image != window.AppHeaderIcon && image.Visibility == Visibility.Visible);
                var appAssembly = typeof(MainWindow).Assembly.GetName().Name;
                logo.Source = new BitmapImage(new Uri(
                    $"pack://application:,,,/{appAssembly};component/assets/ulab-logo.png",
                    UriKind.Absolute));
                var bitmap = Assert.IsType<BitmapImage>(logo.Source);
                Assert.Equal(363, bitmap.PixelWidth);
                Assert.Equal(139, bitmap.PixelHeight);

                var page = Descendants<Grid>(window)
                    .Single(grid => Math.Abs(grid.Width - 210d * 96 / 25.4) < 0.01);
                Assert.Equal(297d * 96 / 25.4, page.Height, 3);

                while (viewModel.AddStudentCommand.CanExecute(null))
                {
                    viewModel.AddStudentCommand.Execute(null);
                    viewModel.Students[^1].Name = $"Student {viewModel.Students.Count}";
                }

                window.UpdateLayout();

                Assert.Equal(10, viewModel.Students.Count);
                Assert.NotEqual(string.Empty, viewModel.StudentLimitHint);
                Assert.Contains("Maximum of 10", viewModel.StudentLimitHint);
                Assert.DoesNotContain("24", viewModel.StudentLimitHint);

                var addButton = Descendants<Button>(window)
                    .Single(button => button.Content as string == "Add student");
                Assert.False(addButton.IsEnabled);

                var hint = Descendants<TextBlock>(window)
                    .Single(block => block.Text == viewModel.StudentLimitHint && block.Text.Length > 0);
                Assert.Equal(Visibility.Visible, hint.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Header_ShowsASingleIconNextToTheApplicationIdentity()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var header = NearestAncestor<Border>(
                    Descendants<TextBlock>(window)
                        .Single(block => block.Text == "Developed by Moontasir Al Mansur at ULAB"));
                var labels = Descendants<TextBlock>(header).Select(block => block.Text).ToList();

                Assert.Contains("UCoverCraft", labels);
                Assert.Contains("Developed by Moontasir Al Mansur at ULAB", labels);
                Assert.Same(window.AppHeaderIcon, Assert.Single(Descendants<Image>(header)));
                Assert.NotNull(window.Icon);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplicationIconResource_LoadsForTheWindowAndTheHeader()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var appAssembly = typeof(MainWindow).Assembly.GetName().Name;
                var resource = new BitmapImage(new Uri(
                    $"pack://application:,,,/{appAssembly};component/assets/ucovercraft.png",
                    UriKind.Absolute));

                Assert.True(resource.PixelWidth > 0);
                Assert.True(resource.PixelHeight > 0);

                var iconFrames = BitmapDecoder.Create(
                    new Uri(
                        $"pack://application:,,,/{appAssembly};component/assets/ucovercraft.ico",
                        UriKind.Absolute),
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad).Frames;
                var iconFrameSizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
                Assert.Equal(
                    iconFrameSizes,
                    iconFrames.Select(frame => frame.PixelWidth).OrderBy(size => size).ToArray());

                var windowIcon = Assert.IsAssignableFrom<BitmapSource>(window.Icon);
                Assert.Contains(windowIcon.PixelWidth, iconFrameSizes);
                Assert.Contains(windowIcon.PixelHeight, iconFrameSizes);

                var largestFrame = iconFrames.Single(frame => frame.PixelWidth == 256);
                var corner = new byte[4];
                new FormatConvertedBitmap(largestFrame, PixelFormats.Bgra32, null, 0)
                    .CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
                Assert.Equal(0, corner[3]);

                var iconOpaqueMean = OpaqueChannelMean(largestFrame);
                var pngOpaqueMean = OpaqueChannelMean(resource);
                for (var channel = 0; channel < pngOpaqueMean.Length; channel++)
                {
                    Assert.InRange(
                        iconOpaqueMean[channel],
                        pngOpaqueMean[channel] - 6,
                        pngOpaqueMean[channel] + 6);
                }

                var headerIcon = Assert.IsAssignableFrom<BitmapSource>(window.AppHeaderIcon.Source);
                Assert.Equal(resource.PixelWidth, headerIcon.PixelWidth);
                Assert.Equal(resource.PixelHeight, headerIcon.PixelHeight);
                Assert.True(window.AppHeaderIcon.ActualWidth > 0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DocumentCard_ExposesNumberAndTitleTopicInputs()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

                var window = new MainWindow();
                try
                {
                    var viewModel = SelectDocumentType(window, DocumentType.Assignment);
                    window.Show();
                    window.UpdateLayout();

                    viewModel.Number = "1";
                    viewModel.TitleTopic = "Distributed Systems";
                    window.UpdateLayout();

                    var card = NearestAncestor<Border>(
                        Descendants<TextBlock>(window).Single(block => block.Text == "Document Type / Title"));
                    var fieldLabelStyle = (Style)Application.Current!.Resources["FieldLabelStyle"];
                    var labels = Descendants<TextBlock>(card)
                        .Where(block => block.Style == fieldLabelStyle)
                        .Select(block => block.Text)
                        .ToList();

                    Assert.Contains("Document Number", labels);
                    Assert.Contains("Title/Topic", labels);

                    var inputs = Descendants<TextBox>(card).Select(box => box.Text).ToList();

                    Assert.Equal(3, inputs.Count);
                    Assert.Contains("ASSIGNMENT", inputs);
                    Assert.Contains("1", inputs);
                    Assert.Contains("Distributed Systems", inputs);
                }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OptionalFields_RefreshTheLivePreviewImmediately()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, DocumentType.Assignment);
                window.Show();
                window.UpdateLayout();

                viewModel.Number = "1";
                viewModel.TitleTopic = "Distributed Systems";
                window.UpdateLayout();

                var texts = Descendants<TextBlock>(window).Select(block => block.Text).ToList();

                Assert.Contains("ASSIGNMENT 01", texts);
                Assert.Contains("Title: ", texts);
                Assert.Contains("Distributed Systems", texts);

                viewModel.Number = string.Empty;
                viewModel.TitleTopic = string.Empty;
                window.UpdateLayout();

                var cleared = Descendants<TextBlock>(window).Select(block => block.Text).ToList();

                Assert.Contains("ASSIGNMENT", cleared);
                Assert.DoesNotContain(cleared, text => text.StartsWith("Title: ", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SubmissionDateLabel_IsVisibleBeforeADateIsSupplied_AndShowsTheValueAfterwards()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, DocumentType.Assignment);
                window.Show();
                window.UpdateLayout();

                var before = Descendants<TextBlock>(window).Select(block => block.Text).ToList();

                Assert.Contains("Date of Submission: ", before);
                Assert.DoesNotContain(before, text => text.Contains("0001", StringComparison.Ordinal));
                Assert.DoesNotContain(before, text => text.Contains("June", StringComparison.Ordinal));
                Assert.Equal(string.Empty, viewModel.SubmissionDay);
                Assert.Equal(string.Empty, viewModel.SubmissionMonth);
                Assert.Equal(string.Empty, viewModel.SubmissionYear);

                viewModel.SubmissionDay = "13";
                viewModel.SubmissionMonth = "6";
                viewModel.SubmissionYear = "2026";
                window.UpdateLayout();

                var after = Descendants<TextBlock>(window).Select(block => block.Text).ToList();

                Assert.Contains("Date of Submission: ", after);
                Assert.Contains("13 June, 2026", after);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SubmittedBy_FieldsAreLabelledNameAndId()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var card = NearestAncestor<Border>(
                    Descendants<TextBlock>(window).Single(block => block.Text == "Submitted By"));
                var fieldLabelStyle = (Style)Application.Current!.Resources["FieldLabelStyle"];
                var labels = Descendants<TextBlock>(card)
                    .Where(block => block.Style == fieldLabelStyle)
                    .Select(block => block.Text)
                    .ToList();

                Assert.Contains("Name", labels);
                Assert.Contains("ID", labels);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static UCoverCraft.App.App EnsureApplication()
    {
        var application = Application.Current as UCoverCraft.App.App;
        if (application is null)
        {
            application = new UCoverCraft.App.App();
            application.InitializeComponent();
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        return application;
    }

    private static MainViewModel SelectDocumentType(MainWindow window, DocumentType type)
    {
        var viewModel = (MainViewModel)window.DataContext;
        viewModel.SelectedDocumentType =
            viewModel.DocumentTypes.First(option => option.Value == type);
        return viewModel;
    }

    private static double[] OpaqueChannelMean(BitmapSource source)
    {
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);

        var sums = new double[4];
        var count = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] != 255)
            {
                continue;
            }

            for (var channel = 0; channel < sums.Length; channel++)
            {
                sums[channel] += pixels[offset + channel];
            }

            count++;
        }

        Assert.True(count > 0);

        for (var channel = 0; channel < sums.Length; channel++)
        {
            sums[channel] /= count;
        }

        return sums;
    }

    private static T NearestAncestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        throw new InvalidOperationException($"No {typeof(T).Name} ancestor was found.");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }

    private static readonly object UiSync = new();
    private static readonly Queue<Action> UiQueue = new();
    private static Thread? uiThread;

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var completed = false;

        lock (UiSync)
        {
            UiQueue.Enqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    lock (UiSync)
                    {
                        completed = true;
                        Monitor.PulseAll(UiSync);
                    }
                }
            });
            Monitor.Pulse(UiSync);
            EnsureUiThread();

            while (!completed)
            {
                Monitor.Wait(UiSync);
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void EnsureUiThread()
    {
        if (uiThread is not null)
        {
            return;
        }

        uiThread = new Thread(() =>
        {
            while (true)
            {
                Action work;
                lock (UiSync)
                {
                    while (UiQueue.Count == 0)
                    {
                        Monitor.Wait(UiSync);
                    }

                    work = UiQueue.Dequeue();
                }

                work();
            }
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.IsBackground = true;
        uiThread.Start();
    }
}
