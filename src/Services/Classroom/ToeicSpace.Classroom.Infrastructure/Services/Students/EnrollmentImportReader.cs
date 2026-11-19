using System.Text;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Services.Students;

public sealed class EnrollmentImportReader : IEnrollmentImportReader
{
    public const int MaxRows = 1000;

    public Task<IReadOnlyList<EnrollmentImportRow>> ReadAsync(Stream file, string fileName, CancellationToken ct)
    {
        IReadOnlyList<EnrollmentImportRow> rows = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => ReadCsv(file, ct),
            ".xlsx" => ReadExcel(file, ct),
            _ => throw InvalidFile("Only CSV and .xlsx files are supported.")
        };
        if (rows.Count == 0) throw InvalidFile("File must contain at least one student row.");
        return Task.FromResult(rows);
    }

    private static IReadOnlyList<EnrollmentImportRow> ReadCsv(Stream file, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(file, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(",");
            var (column, isEmail) = FindColumn(parser.ReadFields());
            var rows = new List<EnrollmentImportRow>();
            while (!parser.EndOfData)
            {
                ct.ThrowIfCancellationRequested();
                if (rows.Count >= MaxRows) throw InvalidFile($"File exceeds {MaxRows} data rows.");
                var rowNumber = checked((int)parser.LineNumber);
                var fields = parser.ReadFields() ?? [];
                var value = fields.Length > column ? fields[column].Trim() : string.Empty;
                rows.Add(Row(rowNumber, value, isEmail, fields.Length > column));
            }
            return rows;
        }
        catch (MalformedLineException)
        {
            throw InvalidFile("CSV contains malformed rows.");
        }
        catch (DecoderFallbackException)
        {
            throw InvalidFile("CSV must use UTF-8 encoding.");
        }
    }

    private static IReadOnlyList<EnrollmentImportRow> ReadExcel(Stream file, CancellationToken ct)
    {
        try
        {
            using var workbook = new XLWorkbook(file);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw InvalidFile("Workbook has no worksheet.");
            var header = sheet.FirstRowUsed() ?? throw InvalidFile("Workbook has no header.");
            if (header.RowNumber() != 1) throw InvalidFile("Header must be on the first row.");
            var (column, isEmail) = FindColumn(header.Cells(1, 20).Select(x => x.GetString()).ToArray());
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxRows) throw InvalidFile($"File exceeds {MaxRows} data rows.");
            var rows = new List<EnrollmentImportRow>();
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                ct.ThrowIfCancellationRequested();
                var value = sheet.Cell(rowNumber, column + 1).GetString().Trim();
                rows.Add(Row(rowNumber, value, isEmail, true));
            }
            return rows;
        }
        catch (Exception ex) when (ex is not ClassRuleException and not OperationCanceledException and not OutOfMemoryException)
        {
            throw InvalidFile("Invalid .xlsx file.");
        }
    }

    private static (int Column, bool IsEmail) FindColumn(string[]? header)
    {
        if (header is null) throw InvalidFile("File must start with a studentId or email header.");
        var studentId = Array.FindIndex(header, x => x.Trim().Equals("studentId", StringComparison.OrdinalIgnoreCase));
        var email = Array.FindIndex(header, x => x.Trim().Equals("email", StringComparison.OrdinalIgnoreCase));
        if ((studentId < 0) == (email < 0))
            throw InvalidFile("Header must contain exactly one of studentId or email.");
        return studentId >= 0 ? (studentId, false) : (email, true);
    }

    private static EnrollmentImportRow Row(int rowNumber, string value, bool isEmail, bool hasColumn)
    {
        var code = !hasColumn || string.IsNullOrWhiteSpace(value) ? "missing_student" : null;
        return isEmail ? new(rowNumber, null, value, code) : new(rowNumber, value, null, code);
    }

    private static ClassRuleException InvalidFile(string message) => new("invalid_import_file", message);
}
