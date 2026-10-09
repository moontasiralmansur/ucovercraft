using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using UCoverCraft.Core.Mvvm;
using UCoverCraft.Core.Models;
using UCoverCraft.Core.Templates;
using DocxRenderer = UCoverCraft.Docx.CoverPageRenderer;
using PdfRenderer = UCoverCraft.Pdf.CoverPageRenderer;

namespace UCoverCraft.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string InvalidDateMessage = "Submission date is invalid.";

    private static readonly string[] ValidatedFieldNames =
    [
        nameof(DocumentTitle),
        nameof(Number),
        nameof(CourseTitle),
        nameof(CourseCode),
        nameof(SubmittedToName),
        nameof(SubmissionDay),
        nameof(SubmissionMonth),
        nameof(SubmissionYear),
    ];

    private static readonly HashSet<string> PreviewFieldNames =
    [
        nameof(SelectedDocumentType),
        nameof(DocumentTitle),
        nameof(Number),
        nameof(TitleTopic),
        nameof(CourseTitle),
        nameof(CourseCode),
        nameof(Section),
        nameof(SubmittedToName),
        nameof(SubmittedToDesignation),
        nameof(SubmittedToDepartment),
        nameof(SubmissionDay),
        nameof(SubmissionMonth),
        nameof(SubmissionYear),
    ];

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    private readonly HashSet<string> _touchedFields = [];
    private readonly HashSet<StudentViewModel> _touchedStudents = [];

    private string _status = "Ready";
    private DocumentTypeOption? _selectedDocumentType;
    private string _documentTitle = string.Empty;
    private string _number = string.Empty;
    private string _titleTopic = string.Empty;
    private string _courseTitle = string.Empty;
    private string _courseCode = string.Empty;
    private string _section = string.Empty;
    private string _submittedToName = string.Empty;
    private string _submittedToDesignation = string.Empty;
    private string _submittedToDepartment = string.Empty;
    private string _submissionDay = string.Empty;
    private string _submissionMonth = string.Empty;
    private string _submissionYear = string.Empty;
    private string _documentTitleError = string.Empty;
    private string _numberError = string.Empty;
    private string _courseTitleError = string.Empty;
    private string _courseCodeError = string.Empty;
    private string _submittedToNameError = string.Empty;
    private string _submissionDateError = string.Empty;
    private bool _hasErrors;
    private bool _isExporting;

    public MainViewModel()
    {
        DocumentTypes =
        [
            new DocumentTypeOption(DocumentType.Assignment, "ASSIGNMENT"),
            new DocumentTypeOption(DocumentType.LabReport, "LAB REPORT"),
            new DocumentTypeOption(DocumentType.ProjectReport, "PROJECT REPORT"),
            new DocumentTypeOption(DocumentType.Custom, "CUSTOM"),
        ];

        ExportPdfCommand = new RelayCommand(ExportPdf, CanExport);
        ExportDocxCommand = new RelayCommand(ExportDocx, CanExport);
        AddStudentCommand = new RelayCommand(AddStudent, CanAddStudent);
        RemoveStudentCommand = new RelayCommand<StudentViewModel>(RemoveStudent, CanRemoveStudent);

        Students.CollectionChanged += OnStudentsChanged;
        Students.Add(CreateStudent());

        PropertyChanged += OnPreviewSourceChanged;
        RefreshPreview();
    }

    public string Title => "UCoverCraft";

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public IReadOnlyList<DocumentTypeOption> DocumentTypes { get; }

    public DocumentTypeOption? SelectedDocumentType
    {
        get => _selectedDocumentType;
        set
        {
            if (!SetProperty(ref _selectedDocumentType, value))
            {
                return;
            }

            _documentTitle = IsCustomTitleEditable ? string.Empty : _selectedDocumentType?.Label ?? string.Empty;
            _touchedFields.Add(nameof(DocumentTitle));
            OnPropertyChanged(nameof(DocumentTitle));
            OnPropertyChanged(nameof(IsCustomTitleEditable));
            OnPropertyChanged(nameof(IsDocumentTypePlaceholderVisible));
            OnPropertyChanged(nameof(IsTopicTitleVisible));
            Revalidate();
        }
    }

    public bool IsCustomTitleEditable => SelectedDocumentType?.Value == DocumentType.Custom;

    public bool IsDocumentTypePlaceholderVisible => SelectedDocumentType is null;

    public bool IsTopicTitleVisible => SelectedDocumentType is not null;

    public string DocumentTitle
    {
        get => _documentTitle;
        set
        {
            _touchedFields.Add(nameof(DocumentTitle));
            if (IsCustomTitleEditable)
            {
                SetProperty(ref _documentTitle, value ?? string.Empty);
            }
            else if (value != _documentTitle)
            {
                OnPropertyChanged();
            }

            Revalidate();
        }
    }

    public string Number
    {
        get => _number;
        set => SetEdited(ref _number, value);
    }

    public string TitleTopic
    {
        get => _titleTopic;
        set => SetProperty(ref _titleTopic, value ?? string.Empty);
    }

    public string CourseTitle
    {
        get => _courseTitle;
        set => SetEdited(ref _courseTitle, value);
    }

    public string CourseCode
    {
        get => _courseCode;
        set => SetEdited(ref _courseCode, value);
    }

    public string Section
    {
        get => _section;
        set => SetProperty(ref _section, value ?? string.Empty);
    }

    public string SubmittedToName
    {
        get => _submittedToName;
        set => SetEdited(ref _submittedToName, value);
    }

    public string SubmittedToDesignation
    {
        get => _submittedToDesignation;
        set => SetProperty(ref _submittedToDesignation, value ?? string.Empty);
    }

    public string SubmittedToDepartment
    {
        get => _submittedToDepartment;
        set => SetProperty(ref _submittedToDepartment, value ?? string.Empty);
    }

    public string SubmissionDay
    {
        get => _submissionDay;
        set => SetEdited(ref _submissionDay, value);
    }

    public string SubmissionMonth
    {
        get => _submissionMonth;
        set => SetEdited(ref _submissionMonth, value);
    }

    public string SubmissionYear
    {
        get => _submissionYear;
        set => SetEdited(ref _submissionYear, value);
    }

    public ObservableCollection<StudentViewModel> Students { get; } = [];

    public int MaxStudents => CoverPage.MaxStudents;

    public bool IsStudentLimitReached => Students.Count >= CoverPage.MaxStudents;

    public string StudentLimitHint => IsStudentLimitReached
        ? $"Maximum of {CoverPage.MaxStudents} students reached for one cover page. " +
            "Remove a student to add another."
        : string.Empty;

    public CoverPreviewViewModel Preview { get; } = new();

    public RelayCommand AddStudentCommand { get; }

    public RelayCommand<StudentViewModel> RemoveStudentCommand { get; }

    public RelayCommand ExportPdfCommand { get; }

    public RelayCommand ExportDocxCommand { get; }

    public string DocumentTitleError
    {
        get => _documentTitleError;
        private set => SetProperty(ref _documentTitleError, value);
    }

    public string NumberError
    {
        get => _numberError;
        private set => SetProperty(ref _numberError, value);
    }

    public string CourseTitleError
    {
        get => _courseTitleError;
        private set => SetProperty(ref _courseTitleError, value);
    }

    public string CourseCodeError
    {
        get => _courseCodeError;
        private set => SetProperty(ref _courseCodeError, value);
    }

    public string SubmittedToNameError
    {
        get => _submittedToNameError;
        private set => SetProperty(ref _submittedToNameError, value);
    }

    public string SubmissionDateError
    {
        get => _submissionDateError;
        private set => SetProperty(ref _submissionDateError, value);
    }

    public ObservableCollection<string> ValidationErrors { get; } = [];

    public bool HasErrors
    {
        get => _hasErrors;
        private set => SetProperty(ref _hasErrors, value);
    }

    public bool Validate()
    {
        foreach (var fieldName in ValidatedFieldNames)
        {
            _touchedFields.Add(fieldName);
        }

        _touchedStudents.UnionWith(Students);
        Revalidate();
        return !HasErrors;
    }

    public CoverPage BuildCoverPage() => BuildCoverPage(ResolveSubmissionDate(out _));

    private CoverPage BuildCoverPage(DateOnly submissionDate) => new()
    {
        DocumentTitle = _documentTitle,
        Number = _number,
        TitleTopic = _titleTopic,
        CourseTitle = _courseTitle,
        CourseCode = _courseCode,
        Section = string.IsNullOrWhiteSpace(_section) ? null : _section,
        SubmittedTo = new Instructor
        {
            Name = _submittedToName,
            Designation = _submittedToDesignation,
            Department = _submittedToDepartment,
        },
        Students = Students.Select(student => student.ToModel()).ToList(),
        SubmissionDate = submissionDate,
    };

    private async void ExportPdf() =>
        await ExportAsync("PDF", ".pdf", "PDF document|*.pdf", PdfRenderer.Render);

    private async void ExportDocx() =>
        await ExportAsync("DOCX", ".docx", "Word document|*.docx", DocxRenderer.Render);

    private async Task ExportAsync(
        string label,
        string extension,
        string filter,
        Action<CoverPage, string> render)
    {
        if (_isExporting)
        {
            return;
        }

        _isExporting = true;
        RaiseExportCommandsChanged();

        try
        {
            if (!Validate())
            {
                Status = $"Cannot export {label}: fix the validation errors first.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = $"Export {label}",
                FileName = DefaultExportFileName(extension),
                DefaultExt = extension,
                Filter = filter,
                AddExtension = true,
            };

            if (dialog.ShowDialog() != true)
            {
                Status = "Export cancelled.";
                return;
            }

            var outputPath = EnsureExtension(dialog.FileName, extension);
            var coverPage = BuildCoverPage();
            Status = $"Exporting {label}...";

            await Task.Run(() => render(coverPage, outputPath));

            Status = $"{label} exported to {outputPath}";
        }
        catch (Exception exception)
        {
            Status = $"{label} export failed: {exception.Message}";
        }
        finally
        {
            _isExporting = false;
            RaiseExportCommandsChanged();
        }
    }

    private string DefaultExportFileName(string extension) =>
        $"{SanitizeFileName(DocumentTitle)}{extension}";

    internal static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat(title.Where(character => !invalid.Contains(character))).Trim().TrimEnd(' ', '.');

        if (name.Length == 0)
        {
            return "CoverPage";
        }

        return IsReservedDeviceName(name) ? "_" + name : name;
    }

    private static bool IsReservedDeviceName(string name)
    {
        var separatorIndex = name.IndexOf('.');
        var stem = (separatorIndex < 0 ? name : name[..separatorIndex]).TrimEnd(' ', '.');

        return ReservedDeviceNames.Contains(stem);
    }

    private static string EnsureExtension(string path, string extension) =>
        path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;

    private bool CanExport() => !_isExporting;

    private void RaiseExportCommandsChanged()
    {
        ExportPdfCommand.RaiseCanExecuteChanged();
        ExportDocxCommand.RaiseCanExecuteChanged();
    }

    private void SetEdited(ref string field, string? value, [CallerMemberName] string propertyName = "")
    {
        _touchedFields.Add(propertyName);
        SetProperty(ref field, value ?? string.Empty, propertyName);
        Revalidate();
    }

    private void Revalidate()
    {
        var submissionDate = ResolveSubmissionDate(out var dateError);
        var errors = new List<string>(BuildCoverPage(submissionDate).Validate());

        DocumentTitleError = Visible(nameof(DocumentTitle), TakeError(errors, nameof(CoverPage.DocumentTitle), "Cover page title"));
        NumberError = Visible(nameof(Number), TakeError(errors, nameof(CoverPage.Number), "Number"));
        CourseTitleError = Visible(nameof(CourseTitle), TakeError(errors, nameof(CoverPage.CourseTitle), "Course title"));
        CourseCodeError = Visible(nameof(CourseCode), TakeError(errors, nameof(CoverPage.CourseCode), "Course code"));
        SubmittedToNameError = Visible(
            nameof(SubmittedToName),
            TakeError(errors, $"{nameof(CoverPage.SubmittedTo)}.{nameof(Instructor.Name)}", "Instructor name"));

        var dateMessage = TakeError(errors, nameof(CoverPage.SubmissionDate), "Submission date");
        SubmissionDateError = IsDateTouched()
            ? (dateError.Length > 0 ? dateError : dateMessage)
            : string.Empty;

        var studentErrors = new List<string>();
        for (var i = 0; i < Students.Count; i++)
        {
            var student = Students[i];
            var message = TakeError(errors, $"Students[{i}].{nameof(Student.Name)}", "Name");
            var visible = _touchedStudents.Contains(student) ? message : string.Empty;
            student.NameError = visible;

            if (visible.Length > 0)
            {
                studentErrors.Add($"Student {i + 1}: {visible}");
            }
        }

        var summary = new List<string>();
        AddIfNotEmpty(summary, DocumentTitleError);
        AddIfNotEmpty(summary, NumberError);
        AddIfNotEmpty(summary, CourseTitleError);
        AddIfNotEmpty(summary, CourseCodeError);
        AddIfNotEmpty(summary, SubmittedToNameError);
        summary.AddRange(studentErrors);
        AddIfNotEmpty(summary, SubmissionDateError);
        summary.AddRange(errors);

        ValidationErrors.Clear();
        foreach (var message in summary)
        {
            ValidationErrors.Add(message);
        }

        HasErrors = summary.Count > 0;
    }

    private DateOnly ResolveSubmissionDate(out string dateError)
    {
        dateError = string.Empty;

        var day = _submissionDay.Trim();
        var month = _submissionMonth.Trim();
        var year = _submissionYear.Trim();

        if (day.Length == 0 && month.Length == 0 && year.Length == 0)
        {
            return default;
        }

        if (!int.TryParse(day, out var parsedDay) ||
            !int.TryParse(month, out var parsedMonth) ||
            !int.TryParse(year, out var parsedYear) ||
            parsedYear < 1 ||
            parsedYear > 9999 ||
            parsedMonth < 1 ||
            parsedMonth > 12 ||
            parsedDay < 1 ||
            parsedDay > DateTime.DaysInMonth(parsedYear, parsedMonth))
        {
            dateError = InvalidDateMessage;
            return default;
        }

        return new DateOnly(parsedYear, parsedMonth, parsedDay);
    }

    private string Visible(string fieldName, string message) =>
        message.Length > 0 && _touchedFields.Contains(fieldName) ? message : string.Empty;

    private bool IsDateTouched() =>
        _touchedFields.Contains(nameof(SubmissionDay)) ||
        _touchedFields.Contains(nameof(SubmissionMonth)) ||
        _touchedFields.Contains(nameof(SubmissionYear));

    private static string TakeError(List<string> errors, string prefix, string label)
    {
        for (var i = 0; i < errors.Count; i++)
        {
            if (errors[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                var message = label + errors[i][prefix.Length..];
                errors.RemoveAt(i);
                return message;
            }
        }

        return string.Empty;
    }

    private static void AddIfNotEmpty(List<string> errors, string message)
    {
        if (message.Length > 0)
        {
            errors.Add(message);
        }
    }

    private StudentViewModel CreateStudent()
    {
        var student = new StudentViewModel();
        student.Changed += OnStudentChanged;
        return student;
    }

    private bool CanAddStudent() => Students.Count < CoverPage.MaxStudents;

    private void AddStudent()
    {
        if (!CanAddStudent())
        {
            Status = StudentLimitHint;
            return;
        }

        var student = CreateStudent();
        _touchedStudents.Add(student);
        Students.Add(student);
    }

    private bool CanRemoveStudent(StudentViewModel? student) => Students.Count > 1;

    private void RemoveStudent(StudentViewModel? student)
    {
        if (student is null || Students.Count <= 1)
        {
            return;
        }

        _touchedStudents.Remove(student);
        Students.Remove(student);
    }

    private void OnStudentChanged(object? sender, EventArgs e)
    {
        if (sender is StudentViewModel student)
        {
            _touchedStudents.Add(student);
            Revalidate();
            RefreshPreview();
        }
    }

    private void OnStudentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AddStudentCommand.RaiseCanExecuteChanged();
        RemoveStudentCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsStudentLimitReached));
        OnPropertyChanged(nameof(StudentLimitHint));
        Revalidate();
        RefreshPreview();
    }

    private void OnPreviewSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null && PreviewFieldNames.Contains(e.PropertyName))
        {
            RefreshPreview();
        }
    }

    private void RefreshPreview() => Preview.Update(BuildCoverPage());
}
