using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;
using UCoverCraft.App;
using UCoverCraft.App.ViewModels;
using UCoverCraft.Core.Templates;

namespace UCoverCraft.Tests.Views;

[Collection("Ui")]
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

                var texts = TestUI.Descendants<TextBlock>(window).Select(block => block.Text).ToList();
                Assert.Contains("LAB REPORT", texts);
                Assert.Contains("Submitted by:", texts);

                var titleSize = 14d * 96 / 72;
                var title = TestUI.Descendants<TextBlock>(window)
                    .Single(block => block.Text == "LAB REPORT" && Math.Abs(block.FontSize - titleSize) < 0.01);
                Assert.Equal(new FontFamily("Times New Roman"), title.FontFamily);
                Assert.Equal(FontWeights.Bold, title.FontWeight);
                Assert.Equal(titleSize, title.FontSize, 3);

                var logo = Assert.Single(
                    TestUI.Descendants<Image>(window),
                    image => image != window.AppHeaderIcon && image.Visibility == Visibility.Visible);
                var appAssembly = typeof(MainWindow).Assembly.GetName().Name;
                logo.Source = new BitmapImage(new Uri(
                    $"pack://application:,,,/{appAssembly};component/assets/ulab-logo.png",
                    UriKind.Absolute));
                var bitmap = Assert.IsType<BitmapImage>(logo.Source);
                Assert.Equal(363, bitmap.PixelWidth);
                Assert.Equal(139, bitmap.PixelHeight);

                var page = TestUI.Descendants<Grid>(window)
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

                var addButton = TestUI.Descendants<Button>(window)
                    .Single(button => button.Content as string == "Add student");
                Assert.False(addButton.IsEnabled);

                var hint = TestUI.Descendants<TextBlock>(window)
                    .Single(block => block.Text == viewModel.StudentLimitHint && block.Text.Length > 0);
                Assert.Equal(Visibility.Visible, hint.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(980d)]
    [InlineData(1180d)]
    [InlineData(1600d)]
    public void FormCards_StayWithinTheFormColumnWithoutClippingAtEveryWindowWidth(double requestedWidth)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.Width = requestedWidth;
                window.UpdateLayout();

                var layout = MainLayout(window);
                var form = layout.ColumnDefinitions[0];
                var scrollViewer = FormScrollViewer(window);
                var container = Assert.IsType<StackPanel>(scrollViewer.Content);
                var cards = container.Children
                    .OfType<Border>()
                    .Where(border => border.IsVisible)
                    .ToList();

                Assert.NotEmpty(cards);
                Assert.Equal(460d, form.ActualWidth, 3);
                Assert.Equal(420d, container.MaxWidth, 3);
                Assert.True(
                    container.ActualWidth <= container.MaxWidth,
                    $"Container {container.ActualWidth} exceeds its maximum width {container.MaxWidth}.");
                Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);

                foreach (var card in cards)
                {
                    Assert.Equal(container.ActualWidth, card.ActualWidth, 3);
                    Assert.True(
                        card.ActualWidth < scrollViewer.ViewportWidth,
                        $"Card {card.ActualWidth} overflows viewport {scrollViewer.ViewportWidth}.");
                }

                AssertCardsAreCentered(scrollViewer, container, cards);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(980d)]
    [InlineData(1180d)]
    [InlineData(1600d)]
    public void FormCards_KeepTheirWidthWhenTheScrollBarVisibilityChanges(double requestedWidth)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.Width = requestedWidth;
                window.UpdateLayout();

                var layout = MainLayout(window);
                var scrollViewer = FormScrollViewer(window);
                var container = Assert.IsType<StackPanel>(scrollViewer.Content);
                var cards = container.Children
                    .OfType<Border>()
                    .Where(border => border.IsVisible)
                    .ToList();

                Assert.NotEmpty(cards);

                var columnWidths = layout.ColumnDefinitions.Select(definition => definition.ActualWidth).ToList();
                var cardWidths = cards.Select(card => card.ActualWidth).ToList();
                var containerWidth = container.ActualWidth;

                var modes = new[]
                {
                    ScrollBarVisibility.Visible,
                    ScrollBarVisibility.Disabled,
                    ScrollBarVisibility.Auto,
                };

                foreach (var mode in modes)
                {
                    scrollViewer.VerticalScrollBarVisibility = mode;
                    window.UpdateLayout();

                    AssertWidthsUnchanged(columnWidths, layout.ColumnDefinitions.Select(definition => definition.ActualWidth).ToList());
                    AssertWidthsUnchanged(cardWidths, cards.Select(card => card.ActualWidth).ToList());
                    Assert.Equal(containerWidth, container.ActualWidth, 3);

                    foreach (var card in cards)
                    {
                        Assert.True(
                            card.ActualWidth <= scrollViewer.ViewportWidth,
                            $"Card {card.ActualWidth} overflows viewport {scrollViewer.ViewportWidth} with {mode}.");
                    }

                    AssertCardsAreCentered(scrollViewer, container, cards);
                }

                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                window.UpdateLayout();

                AssertWidthsUnchanged(cardWidths, cards.Select(card => card.ActualWidth).ToList());
                AssertCardsAreCentered(scrollViewer, container, cards);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(980d)]
    [InlineData(1180d)]
    [InlineData(1600d)]
    public void PreviewPage_KeepsItsA4DimensionsAndAspectRatioAtEveryWindowWidth(double requestedWidth)
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.Width = requestedWidth;
                window.UpdateLayout();

                var layout = MainLayout(window);
                var page = TestUI.Descendants<Grid>(window)
                    .Single(grid => Math.Abs(grid.Width - 210d * 96 / 25.4) < 0.01);

                Assert.Equal(297d * 96 / 25.4, page.Height, 3);

                var viewbox = TestUI.Descendants<Viewbox>(window).Single();
                Assert.Equal(Stretch.Uniform, viewbox.Stretch);

                var origin = page.TransformToAncestor(window).Transform(new Point(0, 0));
                var corner = page.TransformToAncestor(window).Transform(new Point(page.Width, page.Height));
                var renderedWidth = corner.X - origin.X;
                var renderedHeight = corner.Y - origin.Y;

                Assert.True(renderedWidth > 0);
                Assert.True(renderedHeight > 0);
                Assert.Equal(210d / 297d, renderedWidth / renderedHeight, 3);

                var layoutOrigin = layout.TransformToAncestor(window).Transform(new Point(0, 0));
                var previewLeft =
                    layoutOrigin.X
                    + layout.ColumnDefinitions[0].ActualWidth
                    + layout.ColumnDefinitions[1].ActualWidth;
                var previewRight = layoutOrigin.X + layout.ActualWidth;
                var previewBottom = layoutOrigin.Y + layout.ActualHeight;

                Assert.True(origin.X >= previewLeft, $"Preview starts at {origin.X}, column at {previewLeft}.");
                Assert.True(corner.X <= previewRight, $"Preview ends at {corner.X}, column at {previewRight}.");
                Assert.True(corner.Y <= previewBottom, $"Preview ends at {corner.Y}, column at {previewBottom}.");
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

                var header = TestUI.NearestAncestor<Border>(
                    TestUI.Descendants<TextBlock>(window)
                        .Single(block => block.Text == "Developed by Moontasir Al Mansur at ULAB"));
                var labels = TestUI.Descendants<TextBlock>(header).Select(block => block.Text).ToList();

                Assert.Contains("UCoverCraft", labels);
                Assert.Contains("Developed by Moontasir Al Mansur at ULAB", labels);
                Assert.Same(window.AppHeaderIcon, Assert.Single(TestUI.Descendants<Image>(header)));
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
                Assert.Single(TestUI.Descendants<ComboBox>(card));

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
    public void CoverPageTypeComboBox_MatchesFullWidthOfTextBoxFields()
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

                var comboBox = TestUI.Descendants<ComboBox>(card).Single();
                var textBoxes = VisibleTextBoxes(card).ToList();

                Assert.NotEmpty(textBoxes);

                var comboBoxWidth = comboBox.ActualWidth;
                foreach (var textBox in textBoxes)
                {
                    var textBoxWidth = textBox.ActualWidth;
                    Assert.True(Math.Abs(textBoxWidth - comboBoxWidth) <= 1.0,
                        $"ComboBox width ({comboBoxWidth}) should match TextBox width ({textBoxWidth})");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CoverPageTypeComboBox_RenderedEdgesAlignWithTextBoxFields()
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

                var comboBox = TestUI.Descendants<ComboBox>(card).Single();
                var textBoxes = VisibleTextBoxes(card).ToList();

                Assert.NotEmpty(textBoxes);

                var comboBoxBounds = TestUI.GetScreenBounds(comboBox);
                Assert.False(comboBoxBounds.IsEmpty, "ComboBox bounds should not be empty");

                foreach (var textBox in textBoxes)
                {
                    var textBoxBounds = TestUI.GetScreenBounds(textBox);
                    Assert.False(textBoxBounds.IsEmpty, "TextBox bounds should not be empty");

                    var leftDiff = Math.Abs(comboBoxBounds.Left - textBoxBounds.Left);
                    var rightDiff = Math.Abs(comboBoxBounds.Right - textBoxBounds.Right);

                    Assert.True(leftDiff <= 1.0,
                        $"Left edges misaligned: ComboBox={comboBoxBounds.Left:F2}, TextBox={textBoxBounds.Left:F2}, diff={leftDiff:F2}");
                    Assert.True(rightDiff <= 1.0,
                        $"Right edges misaligned: ComboBox={comboBoxBounds.Right:F2}, TextBox={textBoxBounds.Right:F2}, diff={rightDiff:F2}");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CoverPageTypeComboBox_PlaceholderEdgesAlignWithTextBoxFields()
    {
        RunOnStaThread(() =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var card = CoverPageTypeCard(window);

                var comboBox = TestUI.Descendants<ComboBox>(card).Single();
                var textBoxes = VisibleTextBoxes(card).ToList();

                Assert.NotEmpty(textBoxes);

                var comboBoxBounds = TestUI.GetScreenBounds(comboBox);
                Assert.False(comboBoxBounds.IsEmpty, "ComboBox bounds should not be empty");

                foreach (var textBox in textBoxes)
                {
                    var textBoxBounds = TestUI.GetScreenBounds(textBox);
                    Assert.False(textBoxBounds.IsEmpty, "TextBox bounds should not be empty");

                    var leftDiff = Math.Abs(comboBoxBounds.Left - textBoxBounds.Left);
                    var rightDiff = Math.Abs(comboBoxBounds.Right - textBoxBounds.Right);

                    Assert.True(leftDiff <= 1.0,
                        $"Left edges misaligned: ComboBox={comboBoxBounds.Left:F2}, TextBox={textBoxBounds.Left:F2}, diff={leftDiff:F2}");
                    Assert.True(rightDiff <= 1.0,
                        $"Right edges misaligned: ComboBox={comboBoxBounds.Right:F2}, TextBox={textBoxBounds.Right:F2}, diff={rightDiff:F2}");
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CoverPageTypeComboBox_OpensDropdownAndSelectsItem()
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
                var card = CoverPageTypeCard(window);
                var comboBox = TestUI.Descendants<ComboBox>(card).Single();

                // Initially no selection
                Assert.Null(viewModel.SelectedDocumentType);
                Assert.Equal(Visibility.Visible, TestUI.Descendants<TextBlock>(card)
                    .Single(b => b.Text == "Select document type").Visibility);

                // Open dropdown by setting IsDropDownOpen
                comboBox.IsDropDownOpen = true;
                window.UpdateLayout();

                // Verify dropdown is open
                Assert.True(comboBox.IsDropDownOpen);

                // Select an item programmatically (simulating user selection)
                var assignmentOption = viewModel.DocumentTypes.First(o => o.Value == DocumentType.Assignment);
                comboBox.SelectedItem = assignmentOption;
                window.UpdateLayout();

                // Verify selection worked
                Assert.NotNull(viewModel.SelectedDocumentType);
                Assert.Equal(DocumentType.Assignment, viewModel.SelectedDocumentType!.Value);
                Assert.Equal("ASSIGNMENT", viewModel.DocumentTitle);
                Assert.False(viewModel.IsDocumentTypePlaceholderVisible);
                Assert.True(viewModel.IsTopicTitleVisible);

                // Verify placeholder is hidden
                var placeholder = TestUI.Descendants<TextBlock>(card)
                    .Single(b => b.Text == "Select document type");
                Assert.Equal(Visibility.Collapsed, placeholder.Visibility);

                // Close dropdown
                comboBox.IsDropDownOpen = false;
                window.UpdateLayout();
                Assert.False(comboBox.IsDropDownOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(DocumentType.Assignment, "ASSIGNMENT")]
    [InlineData(DocumentType.LabReport, "LAB REPORT")]
    [InlineData(DocumentType.ProjectReport, "PROJECT REPORT")]
    [InlineData(DocumentType.Custom, "CUSTOM")]
    public void CoverPageTypeComboBox_DisplaysSelectedItemText(DocumentType type, string expectedLabel)
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
                var card = CoverPageTypeCard(window);
                var comboBox = TestUI.Descendants<ComboBox>(card).Single();

                var option = viewModel.DocumentTypes.First(o => o.Value == type);
                comboBox.SelectedItem = option;
                window.UpdateLayout();

                var displayText = TestUI.Descendants<TextBlock>(comboBox).FirstOrDefault(tb => tb.Text == expectedLabel);
                Assert.NotNull(displayText);
                Assert.True(TestUI.IsEffectivelyVisible(displayText), $"Selected item text '{expectedLabel}' should be visible in the ComboBox");
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
    public void CoverPageTypeComboBox_SelectedTextHasProperLeftPadding(DocumentType type)
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
                var card = CoverPageTypeCard(window);
                var comboBox = TestUI.Descendants<ComboBox>(card).Single();

                var option = viewModel.DocumentTypes.First(o => o.Value == type);
                comboBox.SelectedItem = option;
                window.UpdateLayout();

                // Find the TextBlock displaying the selected text (inside ContentPresenter)
                // Find the ContentPresenter that displays the selected item (has the padding)
                var contentPresenter = TestUI.Descendants<ContentPresenter>(comboBox).FirstOrDefault(cp => cp.Name == "ContentSite");
                Assert.NotNull(contentPresenter);
                Assert.True(TestUI.IsEffectivelyVisible(contentPresenter));

                // Get screen bounds of ComboBox and the ContentPresenter
                var comboBoxBounds = TestUI.GetScreenBounds(comboBox);
                var presenterBounds = TestUI.GetScreenBounds(contentPresenter);

                Assert.False(comboBoxBounds.IsEmpty, "ComboBox bounds should not be empty");
                Assert.False(presenterBounds.IsEmpty, "ContentPresenter bounds should not be empty");

                // The ContentPresenter should be inset from the ComboBox left edge by at least the ControlPadding.Left (12)
                // This matches the TextBox's internal padding (ControlPadding = 12,8)
                var leftInset = presenterBounds.Left - comboBoxBounds.Left;
                Assert.True(leftInset >= 10.0, $"ContentPresenter left inset ({leftInset:F1}) should be at least 10px (ControlPadding.Left=12), ComboBox left={comboBoxBounds.Left:F1}, Presenter left={presenterBounds.Left:F1}");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CoverPageTypeComboBox_OpensDropdownOnMainDisplayAreaClick()
    {
        RunOnStaThread(async () =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var viewModel = (MainViewModel)window.DataContext;
                var card = CoverPageTypeCard(window);
                var comboBox = TestUI.Descendants<ComboBox>(card).Single();

                // Initially no selection, dropdown closed
                Assert.Null(viewModel.SelectedDocumentType);
                Assert.False(comboBox.IsDropDownOpen);

                // Find the ToggleButton in the ComboBox template
                var toggleButton = TestUI.Descendants<ToggleButton>(comboBox).Single(tb => tb.Name == "ToggleButton");

                // Find the ContentPresenter (main display area) inside the ToggleButton template
                var contentPresenter = TestUI.Descendants<ContentPresenter>(toggleButton).Single(cp => cp.Name == "ContentSite");

                // Ensure the element is arranged
                contentPresenter.UpdateLayout();
                window.UpdateLayout();

                // Click the main display area (left side of the ComboBox)
                // Click at 25% width to avoid the arrow area
                var clickPoint = new Point(contentPresenter.ActualWidth * 0.25, contentPresenter.ActualHeight / 2);
                TestUI.ClickElement(contentPresenter, clickPoint);

                // Process pending UI work
                await TestUI.WaitForConditionAsync(() => comboBox.IsDropDownOpen);

                // Verify dropdown opened from the click
                Assert.True(comboBox.IsDropDownOpen, "Dropdown should open when clicking the main display area");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CoverPageTypeComboBox_OpensDropdownOnArrowClick()
    {
        RunOnStaThread(async () =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var viewModel = (MainViewModel)window.DataContext;
                var card = CoverPageTypeCard(window);
                var comboBox = TestUI.Descendants<ComboBox>(card).Single();

                // Initially no selection, dropdown closed
                Assert.Null(viewModel.SelectedDocumentType);
                Assert.False(comboBox.IsDropDownOpen);

                // Find the ToggleButton in the ComboBox template
                var toggleButton = TestUI.Descendants<ToggleButton>(comboBox).Single(tb => tb.Name == "ToggleButton");

                // Find the arrow Border (contains the Path) inside the ToggleButton template
                // The arrow is in Grid.Column="1" of the ToggleButton's internal Grid
                var arrowBorder = TestUI.Descendants<Border>(toggleButton).Single(b => b.Background != null && b.Background == System.Windows.Media.Brushes.Transparent && b.Padding.Left == 12);

                // Ensure the element is arranged
                arrowBorder.UpdateLayout();
                window.UpdateLayout();

                // Click the arrow area (right side of the ComboBox)
                var clickPoint = new Point(arrowBorder.ActualWidth / 2, arrowBorder.ActualHeight / 2);
                TestUI.ClickElement(arrowBorder, clickPoint);

                // Process pending UI work
                await TestUI.WaitForConditionAsync(() => comboBox.IsDropDownOpen);

                // Verify dropdown opened from the click
                Assert.True(comboBox.IsDropDownOpen, "Dropdown should open when clicking the arrow area");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CoverPageTypeComboBox_SelectsItemViaMouseClickAndClosesDropdown()
    {
        RunOnStaThread(async () =>
        {
            EnsureApplication();

            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();

                var viewModel = (MainViewModel)window.DataContext;
                var card = CoverPageTypeCard(window);
                var comboBox = TestUI.Descendants<ComboBox>(card).Single();

                // Initially no selection
                Assert.Null(viewModel.SelectedDocumentType);
                Assert.False(comboBox.IsDropDownOpen);

                // Find the ToggleButton and click main display to open dropdown
                var toggleButton = TestUI.Descendants<ToggleButton>(comboBox).Single(tb => tb.Name == "ToggleButton");
                var contentPresenter = TestUI.Descendants<ContentPresenter>(toggleButton).Single(cp => cp.Name == "ContentSite");

                contentPresenter.UpdateLayout();
                window.UpdateLayout();

                // Click to open dropdown
                var clickPoint = new Point(contentPresenter.ActualWidth * 0.25, contentPresenter.ActualHeight / 2);
                TestUI.ClickElement(contentPresenter, clickPoint);

                await TestUI.WaitForConditionAsync(() => comboBox.IsDropDownOpen);
                Assert.True(comboBox.IsDropDownOpen);

                // Wait for popup to be loaded and find the ComboBoxItem for Assignment
                var popup = TestUI.Descendants<Popup>(comboBox).Single(p => p.Name == "PART_Popup");
                await TestUI.WaitForConditionAsync(() => popup.IsOpen);

                // Find the ItemsPresenter and its children (ComboBoxItems)
                var itemsPresenter = TestUI.Descendants<ItemsPresenter>(popup).Single();
                itemsPresenter.UpdateLayout();
                window.UpdateLayout();

                var comboBoxItems = TestUI.Descendants<ComboBoxItem>(itemsPresenter).ToList();
                Assert.NotEmpty(comboBoxItems);

                // Find the Assignment item (first item)
                var assignmentItem = comboBoxItems.First();
                assignmentItem.UpdateLayout();

                // Click the Assignment item
                TestUI.ClickElement(assignmentItem, new Point(assignmentItem.ActualWidth / 2, assignmentItem.ActualHeight / 2));

                // Wait for dropdown to close and selection to update
                await TestUI.WaitForConditionAsync(() => !comboBox.IsDropDownOpen);
                await TestUI.WaitForConditionAsync(() => viewModel.SelectedDocumentType != null);

                // Verify selection worked and dropdown closed
                Assert.False(comboBox.IsDropDownOpen, "Dropdown should close after item selection");
                Assert.NotNull(viewModel.SelectedDocumentType);
                Assert.Equal(DocumentType.Assignment, viewModel.SelectedDocumentType!.Value);
                Assert.Equal("ASSIGNMENT", viewModel.DocumentTitle);
                Assert.False(viewModel.IsDocumentTypePlaceholderVisible);
                Assert.True(viewModel.IsTopicTitleVisible);

                // Verify placeholder is hidden
                var placeholder = TestUI.Descendants<TextBlock>(card)
                    .Single(b => b.Text == "Select document type");
                Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
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
                var placeholder = TestUI.Descendants<TextBlock>(card)
                    .Single(block => block.Text == "Select document type");

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
                Assert.False(IsVisibleInTree(TextBoxAfter(card, "Cover Page Title")));
                Assert.False(IsVisibleInTree(TextBoxAfter(card, "Topic / Title (optional)")));
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
                Assert.True(IsVisibleInTree(numberBox));
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
                var label = TestUI.Descendants<TextBlock>(card)
                    .Single(block => block.Text == "Topic / Title (optional)");
                var topicBox = TextBoxAfter(card, "Topic / Title (optional)");

                Assert.True(viewModel.IsTopicTitleVisible);
                Assert.True(IsVisibleInTree(label));
                Assert.True(IsVisibleInTree(topicBox));

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
                Assert.False(IsVisibleInTree(TextBoxAfter(card, "Cover Page Title")));
                Assert.DoesNotContain(
                    TestUI.Descendants<TextBlock>(window),
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

                var inline = TestUI.Descendants<TextBlock>(card)
                    .Single(block => block.Text == "Cover page title is required.");

                Assert.True(IsVisibleInTree(inline));

                var banner = TestUI.NearestAncestor<Border>(
                    TestUI.Descendants<TextBlock>(window).Single(block => block.Text == "Please fix the following:"));
                var bannerTexts = TestUI.Descendants<TextBlock>(banner).Select(block => block.Text).ToList();

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

                var scrollViewer = TestUI.Descendants<ScrollViewer>(window)
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

                Assert.True(IsVisibleInTree(titleBox));
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
                var placeholder = TestUI.Descendants<TextBlock>(window)
                    .Single(block => block.Text == "Select document type");

                Assert.Null(viewModel.SelectedDocumentType);
                Assert.True(viewModel.IsDocumentTypePlaceholderVisible);
                Assert.False(viewModel.IsTopicTitleVisible);
                Assert.Equal(Visibility.Visible, placeholder.Visibility);
                Assert.DoesNotContain(
                    TestUI.Descendants<TextBlock>(window),
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

                var texts = TestUI.Descendants<TextBlock>(window).Select(block => block.Text).ToList();

                Assert.Contains("ASSIGNMENT 01", texts);
                Assert.Contains("Title: ", texts);
                Assert.Contains("Distributed Systems", texts);

                viewModel.Number = string.Empty;
                viewModel.TitleTopic = string.Empty;
                window.UpdateLayout();

                var cleared = TestUI.Descendants<TextBlock>(window).Select(block => block.Text).ToList();

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

                var before = TestUI.Descendants<TextBlock>(window).Select(block => block.Text).ToList();

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

                var after = TestUI.Descendants<TextBlock>(window).Select(block => block.Text).ToList();

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

                var card = TestUI.NearestAncestor<Border>(
                    TestUI.Descendants<TextBlock>(window).Single(block => block.Text == "Submitted By"));
                var fieldLabelStyle = (Style)Application.Current!.Resources["FieldLabelStyle"];
                var labels = TestUI.Descendants<TextBlock>(card)
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

    private static Grid MainLayout(MainWindow window) =>
        ((DockPanel)window.Content).Children.OfType<Grid>().Single();

    private static ScrollViewer FormScrollViewer(MainWindow window) =>
        TestUI.Descendants<ScrollViewer>(window).Single(viewer => viewer.Content is StackPanel);

    private static void AssertWidthsUnchanged(IReadOnlyList<double> expected, IReadOnlyList<double> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], actual[i], 3);
        }
    }

    private static UCoverCraft.App.App EnsureApplication() => TestUI.EnsureApplication();

    private static void RunOnStaThread(Action action) => TestUI.RunOnStaThread(action);

    private static MainViewModel SelectDocumentType(MainWindow window, DocumentType type)
    {
        var viewModel = (MainViewModel)window.DataContext;
        viewModel.SelectedDocumentType =
            viewModel.DocumentTypes.First(option => option.Value == type);
        return viewModel;
    }

    private static Style ResourceStyle(string key) => (Style)Application.Current!.Resources[key];

    private static TextBlock CoverPageTypeHeader(MainWindow window) =>
        TestUI.Descendants<TextBlock>(window)
            .Single(block => block.Text == "Cover Page Type" && block.Style == ResourceStyle("SectionHeaderStyle"));

    private static Border CoverPageTypeCard(MainWindow window) =>
        TestUI.NearestAncestor<Border>(CoverPageTypeHeader(window));

    private static IReadOnlyList<string> VisibleFieldLabels(Border card) =>
        TestUI.Descendants<TextBlock>(card)
            .Where(block => block.Style == ResourceStyle("FieldLabelStyle"))
            .Where(block => IsVisibleInTree(block))
            .Select(block => block.Text)
            .ToList();

    private static IReadOnlyList<TextBox> VisibleTextBoxes(Border card) =>
        TestUI.Descendants<TextBox>(card).Where(block => IsVisibleInTree(block)).ToList();

    private static TextBox TextBoxAfter(Border card, string labelText)
    {
        var label = TestUI.Descendants<TextBlock>(card).Single(block => block.Text == labelText);
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

    private static bool IsVisibleInTree(DependencyObject? node)
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
}
