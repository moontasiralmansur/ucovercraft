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
    public void CoverPageTypeCard_ExposesTheNumberAndTopicTitleInputs()
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

                var card = CoverPageTypeCard(window);
                var labels = VisibleFieldLabels(card);

                Assert.Equal(
                    new[] { "Cover Page Type", "Number (optional)", "Topic / Title (optional)" },
                    labels);
                Assert.Single(Descendants<ComboBox>(card));

                var inputs = VisibleTextBoxes(card).Select(box => box.Text).ToList();

                Assert.Equal(2, inputs.Count);
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
    public void CoverPageTypeCard_ShowsOnlyTheComboBoxAndNumberOnStartup()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var viewModel = (MainViewModel)window.DataContext;
                var header = CoverPageTypeHeader(window);
                var card = CoverPageTypeCard(window);
                var placeholder = Descendants<TextBlock>(card)
                    .Single(block => block.Text == "Select document type...");

                Assert.Equal("Cover Page Type", header.Text);
                Assert.Null(viewModel.SelectedDocumentType);
                Assert.False(viewModel.IsTopicTitleVisible);
                Assert.Equal(Visibility.Visible, placeholder.Visibility);

                var labels = VisibleFieldLabels(card);

                Assert.Equal(new[] { "Cover Page Type", "Number (optional)" }, labels);

                var inputs = VisibleTextBoxes(card);

                Assert.Single(inputs);
                Assert.Equal(string.Empty, inputs[0].Text);
                Assert.Equal(string.Empty, viewModel.Number);
                Assert.False(IsEffectivelyVisible(TextBoxAfter(card, "Cover Page Title")));
                Assert.False(IsEffectivelyVisible(TextBoxAfter(card, "Topic / Title (optional)")));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(DocumentType.Assignment)]
    [InlineData(DocumentType.LabReport)]
    [InlineData(DocumentType.ProjectReport)]
    [InlineData(DocumentType.Custom)]
    public void NumberField_IsVisibleForEveryCoverPageType(DocumentType type)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, type);
                window.Show();
                window.UpdateLayout();

                viewModel.Number = "7";
                window.UpdateLayout();

                var card = CoverPageTypeCard(window);
                var numberBox = TextBoxAfter(card, "Number (optional)");

                Assert.Contains("Number (optional)", VisibleFieldLabels(card));
                Assert.True(IsEffectivelyVisible(numberBox));
                Assert.Equal("7", numberBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(DocumentType.Assignment)]
    [InlineData(DocumentType.LabReport)]
    [InlineData(DocumentType.ProjectReport)]
    [InlineData(DocumentType.Custom)]
    public void TopicTitleField_IsVisibleForEveryCoverPageType_AndMapsToTitleTopic(DocumentType type)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, type);
                window.Show();
                window.UpdateLayout();

                var card = CoverPageTypeCard(window);
                var label = Descendants<TextBlock>(card)
                    .Single(block => block.Text == "Topic / Title (optional)");
                var topicBox = TextBoxAfter(card, "Topic / Title (optional)");

                Assert.True(viewModel.IsTopicTitleVisible);
                Assert.True(IsEffectivelyVisible(label));
                Assert.True(IsEffectivelyVisible(topicBox));

                viewModel.TitleTopic = "Distributed Systems";
                window.UpdateLayout();

                Assert.Equal("Distributed Systems", topicBox.Text);

                topicBox.Text = "Compiler Design";

                Assert.Equal("Compiler Design", viewModel.TitleTopic);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(DocumentType.Assignment)]
    [InlineData(DocumentType.LabReport)]
    [InlineData(DocumentType.ProjectReport)]
    public void PresetTypes_DoNotShowACoverPageTitleFieldOrTheAutomaticTitleHint(DocumentType type)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, type);
                window.Show();
                window.UpdateLayout();

                var card = CoverPageTypeCard(window);
                var labels = VisibleFieldLabels(card);

                Assert.False(viewModel.IsCustomTitleEditable);
                Assert.DoesNotContain("Cover Page Title", labels);
                Assert.DoesNotContain("Document Title", labels);
                Assert.False(IsEffectivelyVisible(TextBoxAfter(card, "Cover Page Title")));
                Assert.DoesNotContain(
                    Descendants<TextBlock>(window),
                    block => block.Text == "Automatically set from Document Type.");
                Assert.Contains("Number (optional)", labels);
                Assert.Contains("Topic / Title (optional)", labels);
                Assert.Equal(
                    viewModel.DocumentTypes.First(option => option.Value == type).Label,
                    viewModel.DocumentTitle);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CustomCoverPageTitleError_UsesTheUpdatedWordingInlineAndInTheSummary()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, DocumentType.Custom);
                window.Show();
                window.UpdateLayout();

                var card = CoverPageTypeCard(window);
                var titleBox = TextBoxAfter(card, "Cover Page Title");

                titleBox.Text = "   ";
                window.UpdateLayout();

                Assert.Equal("Cover page title is required.", viewModel.DocumentTitleError);
                Assert.Contains("Cover page title is required.", viewModel.ValidationErrors);

                var inline = Descendants<TextBlock>(card)
                    .Single(block => block.Text == "Cover page title is required.");

                Assert.True(IsEffectivelyVisible(inline));

                var banner = NearestAncestor<Border>(
                    Descendants<TextBlock>(window).Single(block => block.Text == "Please fix the following:"));
                var bannerTexts = Descendants<TextBlock>(banner).Select(block => block.Text).ToList();

                Assert.Contains("Cover page title is required.", bannerTexts);
                Assert.DoesNotContain(bannerTexts, text => text.StartsWith("Document title", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FormCards_AreCenteredWithinTheFormViewport()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var scrollViewer = Descendants<ScrollViewer>(window)
                    .Single(viewer => viewer.Content is StackPanel);
                var container = Assert.IsType<StackPanel>(scrollViewer.Content);
                var cards = container.Children
                    .OfType<Border>()
                    .Where(border => border.IsVisible)
                    .ToList();

                Assert.Equal(HorizontalAlignment.Stretch, container.HorizontalAlignment);
                Assert.True(container.MaxWidth > 0);
                Assert.True(scrollViewer.ViewportWidth > container.MaxWidth);
                Assert.NotEmpty(cards);

                AssertCardsAreCentered(scrollViewer, container, cards);

                window.Width = window.MinWidth;
                window.UpdateLayout();

                AssertCardsAreCentered(scrollViewer, container, cards);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Custom_ShowsAnEditableCoverPageTitleAboveTheTopicTitleField()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                var viewModel = SelectDocumentType(window, DocumentType.Custom);
                window.Show();
                window.UpdateLayout();

                var card = CoverPageTypeCard(window);
                var labels = VisibleFieldLabels(card);

                Assert.Equal(
                    new[]
                    {
                        "Cover Page Type",
                        "Cover Page Title",
                        "Number (optional)",
                        "Topic / Title (optional)",
                    },
                    labels);
                Assert.True(viewModel.IsCustomTitleEditable);
                Assert.DoesNotContain("Document Title", labels);

                var titleBox = TextBoxAfter(card, "Cover Page Title");

                Assert.True(IsEffectivelyVisible(titleBox));
                Assert.True(titleBox.IsEnabled);

                titleBox.Text = "Smart Campus Navigation";

                Assert.Equal("Smart Campus Navigation", viewModel.DocumentTitle);
                Assert.Equal("Smart Campus Navigation", viewModel.BuildCoverPage().DocumentTitle);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DocumentTypePlaceholder_IsShownOnStartup_AndRemovedAfterSelectingAPreset()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var viewModel = (MainViewModel)window.DataContext;
                var placeholder = Descendants<TextBlock>(window)
                    .Single(block => block.Text == "Select document type...");

                Assert.Null(viewModel.SelectedDocumentType);
                Assert.True(viewModel.IsDocumentTypePlaceholderVisible);
                Assert.False(viewModel.IsTopicTitleVisible);
                Assert.Equal(Visibility.Visible, placeholder.Visibility);
                Assert.DoesNotContain(
                    Descendants<TextBlock>(window),
                    block => block.Text == "Automatically set from Document Type.");
                Assert.Equal(
                    new[] { "ASSIGNMENT", "LAB REPORT", "PROJECT REPORT", "CUSTOM" },
                    viewModel.DocumentTypes.Select(option => option.Label));

                SelectDocumentType(window, DocumentType.Assignment);
                window.UpdateLayout();

                Assert.NotNull(viewModel.SelectedDocumentType);
                Assert.False(viewModel.IsDocumentTypePlaceholderVisible);
                Assert.True(viewModel.IsTopicTitleVisible);
                Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
                Assert.Equal("ASSIGNMENT", viewModel.DocumentTitle);
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

    private static void AssertCardsAreCentered(
        ScrollViewer scrollViewer,
        StackPanel container,
        IReadOnlyList<Border> cards)
    {
        foreach (var card in cards)
        {
            var left = card.TransformToAncestor(scrollViewer).Transform(new Point(0, 0)).X;
            var right = left + card.ActualWidth;

            Assert.Equal(container.ActualWidth, card.ActualWidth, 3);
            Assert.True(card.ActualWidth < scrollViewer.ViewportWidth);
            Assert.True(Math.Abs(left - (scrollViewer.ViewportWidth - right)) < 0.5);
        }
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

    private static Style ResourceStyle(string key) => (Style)Application.Current!.Resources[key];

    private static TextBlock CoverPageTypeHeader(MainWindow window) =>
        Descendants<TextBlock>(window)
            .Single(block => block.Text == "Cover Page Type" && block.Style == ResourceStyle("SectionHeaderStyle"));

    private static Border CoverPageTypeCard(MainWindow window) =>
        NearestAncestor<Border>(CoverPageTypeHeader(window));

    private static IReadOnlyList<string> VisibleFieldLabels(Border card) =>
        Descendants<TextBlock>(card)
            .Where(block => block.Style == ResourceStyle("FieldLabelStyle"))
            .Where(block => IsEffectivelyVisible(block))
            .Select(block => block.Text)
            .ToList();

    private static IReadOnlyList<TextBox> VisibleTextBoxes(Border card) =>
        Descendants<TextBox>(card).Where(block => IsEffectivelyVisible(block)).ToList();

    private static TextBox TextBoxAfter(Border card, string labelText)
    {
        var label = Descendants<TextBlock>(card).Single(block => block.Text == labelText);
        var children = ((Panel)VisualTreeHelper.GetParent(label)).Children;
        var start = children.IndexOf(label);

        for (var i = start + 1; i < children.Count; i++)
        {
            if (children[i] is TextBox box)
            {
                return box;
            }
        }

        throw new InvalidOperationException($"No textbox follows '{labelText}'.");
    }

    private static bool IsEffectivelyVisible(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is UIElement element && element.Visibility != Visibility.Visible)
            {
                return false;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return true;
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
