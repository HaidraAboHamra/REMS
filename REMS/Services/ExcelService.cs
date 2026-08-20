using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using REMS.Enititys;

namespace REMS.Services
{
    public class ExcelService
    {
        private readonly IWebHostEnvironment _env;

        // =========================================================
        // HEMS / PREMIUM DESIGN SYSTEM
        // =========================================================

        private static readonly XLColor Black =
            XLColor.FromHtml("#030305");

        private static readonly XLColor Black2 =
            XLColor.FromHtml("#070811");

        private static readonly XLColor Black3 =
            XLColor.FromHtml("#0D1020");

        private static readonly XLColor Indigo =
            XLColor.FromHtml("#312E81");

        private static readonly XLColor Indigo2 =
            XLColor.FromHtml("#4338CA");

        private static readonly XLColor IndigoLight =
            XLColor.FromHtml("#6366F1");

        private static readonly XLColor Blue =
            XLColor.FromHtml("#1D4ED8");

        private static readonly XLColor Cyan =
            XLColor.FromHtml("#38BDF8");

        private static readonly XLColor White =
            XLColor.FromHtml("#F8FAFC");

        private static readonly XLColor Muted =
            XLColor.FromHtml("#A5B4FC");

        private static readonly XLColor TextMuted =
            XLColor.FromHtml("#CBD5E1");

        private static readonly XLColor Border =
            XLColor.FromHtml("#343758");

        private static readonly XLColor BorderSoft =
            XLColor.FromHtml("#20233A");

        private static readonly XLColor RowDark =
            XLColor.FromHtml("#090B15");

        private static readonly XLColor RowLight =
            XLColor.FromHtml("#101329");

        private static readonly XLColor Success =
            XLColor.FromHtml("#22C55E");

        private static readonly XLColor Warning =
            XLColor.FromHtml("#F59E0B");

        private static readonly XLColor Danger =
            XLColor.FromHtml("#EF4444");

        private static readonly XLColor Info =
            XLColor.FromHtml("#38BDF8");

        public ExcelService(IWebHostEnvironment env)
        {
            _env = env;
        }

        // =========================================================
        // GENERIC REPORT
        // =========================================================

        public async Task<string> GenerateGenericReportExcelAsync<T>(
            List<T> items,
            string sheetName,
            DateTime date)
            where T : class
        {
            items ??=
                new List<T>();

            // =====================================================
            // Reflection
            // =====================================================

            var props =
                typeof(T)
                    .GetProperties(
                        BindingFlags.Public |
                        BindingFlags.Instance)
                    .Where(p => p.CanRead)
                    .ToList();

            var baseProps =
                props
                    .Where(p =>
                    {
                        var t = p.PropertyType;

                        if (t == typeof(string))
                            return true;

                        if (t.IsPrimitive)
                            return true;

                        if (t == typeof(DateTime) ||
                            t == typeof(DateTime?))
                            return true;

                        if (t == typeof(decimal) ||
                            t == typeof(decimal?))
                            return true;

                        if (t.IsEnum)
                            return true;

                        var nullable =
                            Nullable.GetUnderlyingType(t);

                        if (nullable != null &&
                            nullable.IsPrimitive)
                        {
                            return true;
                        }

                        return false;
                    })
                    .ToList();

            // =====================================================
            // Custom Fields
            // =====================================================

            var customFieldNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            var jsonProp =
                props.FirstOrDefault(
                    p =>
                        string.Equals(
                            p.Name,
                            "CustomFieldsJson",
                            StringComparison.OrdinalIgnoreCase));

            if (jsonProp != null)
            {
                foreach (var item in items)
                {
                    try
                    {
                        var json =
                            jsonProp
                                .GetValue(item)?
                                .ToString();

                        if (string.IsNullOrWhiteSpace(json))
                            continue;

                        var fields =
                            JsonSerializer.Deserialize<List<CustomField>>(
                                json);

                        if (fields == null)
                            continue;

                        foreach (var field in fields)
                        {
                            if (!string.IsNullOrWhiteSpace(field.Name))
                            {
                                customFieldNames.Add(
                                    field.Name.Trim());
                            }
                        }
                    }
                    catch
                    {
                        // Ignore broken JSON in a single item.
                    }
                }
            }

            var customFieldList =
                customFieldNames.ToList();

            // =====================================================
            // Workbook
            // =====================================================

            using var wb =
                new XLWorkbook();

            var finalSheetName =
                string.IsNullOrWhiteSpace(sheetName)
                    ? "تقرير"
                    : sheetName;

            var ws =
                wb.Worksheets.Add(
                    finalSheetName);

            // =====================================================
            // REPORT TITLE
            // =====================================================

            var totalColumns =
                baseProps.Count +
                customFieldList.Count;

            if (totalColumns == 0)
                totalColumns = 1;

            CreatePremiumTitle(
                ws,
                finalSheetName,
                date,
                totalColumns);

            // =====================================================
            // HEADER
            // =====================================================

            int headerRow = 4;

            int col = 1;

            foreach (var p in baseProps)
            {
                ws.Cell(
                    headerRow,
                    col)
                    .Value =
                    LocalizeHeader(
                        p.Name);

                col++;
            }

            foreach (var cf in customFieldList)
            {
                ws.Cell(
                    headerRow,
                    col)
                    .Value =
                    cf;

                col++;
            }

            ApplyPremiumHeaderStyle(
                ws.Range(
                    headerRow,
                    1,
                    headerRow,
                    totalColumns));

            // =====================================================
            // DATA
            // =====================================================

            int row = headerRow + 1;

            foreach (var item in items)
            {
                col = 1;

                foreach (var p in baseProps)
                {
                    var cell =
                        ws.Cell(
                            row,
                            col);

                    var raw =
                        p.GetValue(item);

                    if (raw == null)
                    {
                        cell.Value = "";
                    }
                    else if (raw is DateTime dt)
                    {
                        cell.Value = dt;

                        cell.Style.DateFormat.Format =
                            "yyyy-MM-dd HH:mm";
                    }
                    else if (raw is bool b)
                    {
                        cell.Value =
                            b
                                ? "نعم"
                                : "لا";
                    }
                    else
                    {
                        var value =
                            raw.ToString() ?? "";

                        if (IsUrlOrHttpPath(value))
                        {
                            cell.Value =
                                value;

                            ApplyHyperlinkStyle(
                                cell,
                                value);
                        }
                        else
                        {
                            cell.Value =
                                value;
                        }
                    }

                    ApplyPremiumDataCell(
                        cell);

                    col++;
                }

                // -------------------------------------------------
                // Custom Fields
                // -------------------------------------------------

                var fieldValues =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);

                if (jsonProp != null)
                {
                    try
                    {
                        var json =
                            jsonProp
                                .GetValue(item)?
                                .ToString();

                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var fields =
                                JsonSerializer.Deserialize<List<CustomField>>(
                                    json);

                            if (fields != null)
                            {
                                foreach (var field in fields)
                                {
                                    var key =
                                        field.Name?.Trim() ?? "";

                                    if (!fieldValues.ContainsKey(key))
                                    {
                                        fieldValues[key] =
                                            field.Value ?? "";
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                for (int i = 0;
                     i < customFieldList.Count;
                     i++)
                {
                    var fieldName =
                        customFieldList[i];

                    fieldValues.TryGetValue(
                        fieldName,
                        out var value);

                    var cell =
                        ws.Cell(
                            row,
                            baseProps.Count +
                            i +
                            1);

                    if (!string.IsNullOrEmpty(value) &&
                        IsUrlOrHttpPath(value))
                    {
                        cell.Value =
                            value;

                        ApplyHyperlinkStyle(
                            cell,
                            value);
                    }
                    else
                    {
                        cell.Value =
                            value ?? "";
                    }

                    ApplyPremiumDataCell(
                        cell);
                }

                ApplyPremiumRowStyle(
                    ws,
                    row,
                    totalColumns);

                row++;
            }

            // =====================================================
            // LAYOUT
            // =====================================================

            ApplyPremiumSheetLayout(
                ws,
                totalColumns,
                headerRow,
                row - 1);

            // =====================================================
            // SAVE
            // =====================================================

            var fileName =
                $"{MakeSafeFileName(finalSheetName)}_{date:yyyyMMdd_HHmmss}.xlsx";

            await SaveWorkbookAsync(
                wb,
                fileName);

            return
                $"/reports/{fileName}";
        }

        // =========================================================
        // REPORT WRAPPER
        // =========================================================

        public Task<string> GenerateReportExcelAsync(
            List<Report> reports,
            DateTime date)
        {
            return GenerateGenericReportExcelAsync(
                reports,
                "تقرير_العمل",
                date);
        }

        // =========================================================
        // FOLLOW UP REPORT
        // =========================================================

        public async Task<string> GenerateFollowUpReportExcelAsync(
            List<FollowUpReport> reports,
            DateTime date)
        {
            reports ??=
                new List<FollowUpReport>();

            using var wb =
                new XLWorkbook();

            var ws =
                wb.Worksheets.Add(
                    "تقرير قسم المتابعة");

            // =====================================================
            // COLUMNS
            // =====================================================

            string[] headers =
            {
                "المهمة",
                "النوع",
                "العميل / المشروع",
                "الموظف",
                "الأولوية",
                "التقدم",
                "المنجز",
                "المتبقي",
                "الحالة",
                "التسليم"
            };

            int totalColumns =
                headers.Length;

            // =====================================================
            // TITLE
            // =====================================================

            CreatePremiumTitle(
                ws,
                "تقرير قسم المتابعة",
                date,
                totalColumns);

            // =====================================================
            // HEADER
            // =====================================================

            int headerRow =
                4;

            for (int i = 0;
                 i < headers.Length;
                 i++)
            {
                ws.Cell(
                    headerRow,
                    i + 1)
                    .Value =
                    headers[i];
            }

            ApplyPremiumHeaderStyle(
                ws.Range(
                    headerRow,
                    1,
                    headerRow,
                    headers.Length));

            // =====================================================
            // DATA
            // =====================================================

            int row =
                headerRow + 1;

            foreach (var report in reports)
            {
                // -------------------------------------------------
                // المهمة
                // -------------------------------------------------

                ws.Cell(row, 1).Value =
                    report.Content ?? "";

                // -------------------------------------------------
                // النوع
                // -------------------------------------------------

                ws.Cell(row, 2).Value =
                    report.TaskType ?? "";

                // -------------------------------------------------
                // العميل / المشروع
                // -------------------------------------------------

                ws.Cell(row, 3).Value =
                    report.ClientOrProject ?? "";

                // -------------------------------------------------
                // الموظف
                // -------------------------------------------------

                ws.Cell(row, 4).Value =
                    report.AssignedEmployee ?? "";

                // -------------------------------------------------
                // الأولوية
                // -------------------------------------------------

                ws.Cell(row, 5).Value =
                    report.Priority ?? "";

                // -------------------------------------------------
                // التقدم
                // -------------------------------------------------

                int progress =
                    Math.Clamp(
                        report.ProgressPercentage,
                        0,
                        100);

                ws.Cell(
                    row,
                    6)
                    .Value =
                    progress / 100.0;

                ws.Cell(
                    row,
                    6)
                    .Style
                    .NumberFormat
                    .Format =
                    "0%";

                // -------------------------------------------------
                // المنجز
                // -------------------------------------------------

                ws.Cell(
                    row,
                    7)
                    .Value =
                    $"{report.CompletedItems} / {report.TotalItems}";

                // -------------------------------------------------
                // المتبقي
                // -------------------------------------------------

                ws.Cell(
                    row,
                    8)
                    .Value =
                    report.RemainingItems;

                // -------------------------------------------------
                // الحالة
                // -------------------------------------------------

                string status =
                    GetTaskStatus(
                        report);

                ws.Cell(
                    row,
                    9)
                    .Value =
                    status;

                // -------------------------------------------------
                // التسليم
                // -------------------------------------------------

                if (report.DueDate.HasValue)
                {
                    ws.Cell(
                        row,
                        10)
                        .Value =
                        report.DueDate.Value;

                    ws.Cell(
                        row,
                        10)
                        .Style
                        .DateFormat
                        .Format =
                        "yyyy-MM-dd";
                }
                else
                {
                    ws.Cell(
                        row,
                        10)
                        .Value =
                        "";
                }

                // =================================================
                // BASE STYLE
                // =================================================

                ApplyPremiumRowStyle(
                    ws,
                    row,
                    headers.Length);

                // =================================================
                // PROGRESS
                // =================================================

                ApplyProgressCellStyle(
                    ws.Cell(row, 6),
                    progress);

                // =================================================
                // STATUS
                // =================================================

                ApplyStatusCellStyle(
                    ws.Cell(row, 9),
                    status);

                // =================================================
                // PRIORITY
                // =================================================

                ApplyPriorityCellStyle(
                    ws.Cell(row, 5),
                    report.Priority);

                // =================================================
                // DUE DATE
                // =================================================

                ApplyDueDateStyle(
                    ws.Cell(row, 10),
                    report);

                row++;
            }

            // =====================================================
            // SHEET
            // =====================================================

            ApplyPremiumSheetLayout(
                ws,
                headers.Length,
                headerRow,
                row - 1);

            // =====================================================
            // SPECIAL WIDTHS
            // =====================================================

            ws.Column(1).Width = 33;
            ws.Column(2).Width = 18;
            ws.Column(3).Width = 27;
            ws.Column(4).Width = 23;
            ws.Column(5).Width = 16;
            ws.Column(6).Width = 13;
            ws.Column(7).Width = 17;
            ws.Column(8).Width = 13;
            ws.Column(9).Width = 19;
            ws.Column(10).Width = 17;

            // =====================================================
            // SAVE
            // =====================================================

            var fileName =
                $"تقرير_قسم_المتابعة_{date:yyyyMMdd_HHmmss}.xlsx";

            await SaveWorkbookAsync(
                wb,
                fileName);

            return
                $"/reports/{fileName}";
        }

        // =========================================================
        // PREMIUM TITLE
        // =========================================================

        private static void CreatePremiumTitle(
            IXLWorksheet ws,
            string title,
            DateTime date,
            int totalColumns)
        {
            if (totalColumns < 1)
                totalColumns = 1;

            // -----------------------------------------------------
            // Main Title
            // -----------------------------------------------------

            var titleRange =
                ws.Range(
                    1,
                    1,
                    1,
                    totalColumns);

            titleRange.Merge();

            titleRange.Value =
                $"◆  HEMS  |  {title}";

            titleRange.Style.Fill.BackgroundColor =
                Black;

            titleRange.Style.Font.FontColor =
                White;

            titleRange.Style.Font.Bold =
                true;

            titleRange.Style.Font.FontSize =
                20;

            titleRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            titleRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            titleRange.Style.Border.BottomBorder =
                XLBorderStyleValues.Medium;

            titleRange.Style.Border.BottomBorderColor =
                IndigoLight;

            ws.Row(1).Height =
                42;

            // -----------------------------------------------------
            // Subtitle
            // -----------------------------------------------------

            var subtitleRange =
                ws.Range(
                    2,
                    1,
                    2,
                    totalColumns);

            subtitleRange.Merge();

            subtitleRange.Value =
                $"HEMS • HEX STUDIO     |     تاريخ التقرير: {date:yyyy-MM-dd HH:mm}";

            subtitleRange.Style.Fill.BackgroundColor =
                Black2;

            subtitleRange.Style.Font.FontColor =
                Muted;

            subtitleRange.Style.Font.FontSize =
                10;

            subtitleRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            subtitleRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            ws.Row(2).Height =
                25;

            // -----------------------------------------------------
            // Premium Accent
            // -----------------------------------------------------

            var accentRange =
                ws.Range(
                    3,
                    1,
                    3,
                    totalColumns);

            accentRange.Merge();

            accentRange.Value =
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";

            accentRange.Style.Fill.BackgroundColor =
                Black3;

            accentRange.Style.Font.FontColor =
                IndigoLight;

            accentRange.Style.Font.FontSize =
                7;

            accentRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            accentRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            ws.Row(3).Height =
                12;
        }

        // =========================================================
        // PREMIUM HEADER
        // =========================================================

        private static void ApplyPremiumHeaderStyle(
            IXLRange range)
        {
            range.Style.Fill.BackgroundColor =
                Indigo;

            range.Style.Font.FontColor =
                White;

            range.Style.Font.Bold =
                true;

            range.Style.Font.FontSize =
                11;

            range.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            range.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            range.Style.Alignment.WrapText =
                true;

            range.Style.Border.TopBorder =
                XLBorderStyleValues.Medium;

            range.Style.Border.TopBorderColor =
                IndigoLight;

            range.Style.Border.BottomBorder =
                XLBorderStyleValues.Medium;

            range.Style.Border.BottomBorderColor =
                Blue;

            range.Style.Border.LeftBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.LeftBorderColor =
                Border;

            range.Style.Border.RightBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.RightBorderColor =
                Border;

            range.Worksheet.Row(
                range.RangeAddress.FirstAddress.RowNumber)
                .Height =
                34;
        }

        // =========================================================
        // PREMIUM DATA CELL
        // =========================================================

        private static void ApplyPremiumDataCell(
            IXLCell cell)
        {
            cell.Style.Font.FontColor =
                White;

            cell.Style.Font.FontSize =
                10;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            cell.Style.Alignment.WrapText =
                true;

            cell.Style.Border.BottomBorder =
                XLBorderStyleValues.Thin;

            cell.Style.Border.BottomBorderColor =
                BorderSoft;

            cell.Style.Border.LeftBorder =
                XLBorderStyleValues.Thin;

            cell.Style.Border.LeftBorderColor =
                BorderSoft;

            cell.Style.Border.RightBorder =
                XLBorderStyleValues.Thin;

            cell.Style.Border.RightBorderColor =
                BorderSoft;
        }

        // =========================================================
        // ROW DESIGN
        // =========================================================

        private static void ApplyPremiumRowStyle(
            IXLWorksheet ws,
            int row,
            int totalColumns)
        {
            var range =
                ws.Range(
                    row,
                    1,
                    row,
                    totalColumns);

            range.Style.Fill.BackgroundColor =
                row % 2 == 0
                    ? RowDark
                    : RowLight;

            range.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            range.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            range.Style.Alignment.WrapText =
                true;

            range.Style.Font.FontColor =
                White;

            range.Style.Font.FontSize =
                10;

            range.Style.Border.BottomBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.BottomBorderColor =
                BorderSoft;

            ws.Row(row).Height =
                30;
        }

        // =========================================================
        // HYPERLINK
        // =========================================================

        private static void ApplyHyperlinkStyle(
            IXLCell cell,
            string value)
        {
            try
            {
                cell.SetHyperlink(
                    new XLHyperlink(value));
            }
            catch
            {
                try
                {
                    var hyperlink =
                        cell.GetHyperlink();

                    if (hyperlink != null)
                    {
                        hyperlink.ExternalAddress =
                            new Uri(
                                value,
                                UriKind.RelativeOrAbsolute);
                    }
                }
                catch
                {
                }
            }

            cell.Style.Font.FontColor =
                Cyan;

            cell.Style.Font.Underline =
                XLFontUnderlineValues.Single;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
        }

        // =========================================================
        // PROGRESS
        // =========================================================

        private static void ApplyProgressCellStyle(
            IXLCell cell,
            int progress)
        {
            cell.Style.Font.Bold =
                true;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            if (progress >= 100)
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#10351F");

                cell.Style.Font.FontColor =
                    Success;
            }
            else if (progress >= 75)
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#102C3B");

                cell.Style.Font.FontColor =
                    Cyan;
            }
            else if (progress >= 50)
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#211F46");

                cell.Style.Font.FontColor =
                    IndigoLight;
            }
            else if (progress > 0)
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#342815");

                cell.Style.Font.FontColor =
                    Warning;
            }
            else
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#181A27");

                cell.Style.Font.FontColor =
                    TextMuted;
            }
        }

        // =========================================================
        // STATUS
        // =========================================================

        private static void ApplyStatusCellStyle(
            IXLCell cell,
            string? status)
        {
            var value =
                status?.Trim() ?? "";

            cell.Style.Font.Bold =
                true;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            if (
                value.Contains(
                    "مكتملة",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "منتهية",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#10351F");

                cell.Style.Font.FontColor =
                    Success;
            }
            else if (
                value.Contains(
                    "متأخرة",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#39161B");

                cell.Style.Font.FontColor =
                    Danger;
            }
            else
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#211D45");

                cell.Style.Font.FontColor =
                    IndigoLight;
            }
        }

        // =========================================================
        // PRIORITY
        // =========================================================

        private static void ApplyPriorityCellStyle(
            IXLCell cell,
            string? priority)
        {
            var value =
                priority?.Trim() ?? "";

            cell.Style.Font.Bold =
                true;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            if (
                value.Contains(
                    "عالي",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "عاجل",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "مرتفع",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#39161B");

                cell.Style.Font.FontColor =
                    Danger;
            }
            else if (
                value.Contains(
                    "متوسط",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#382912");

                cell.Style.Font.FontColor =
                    Warning;
            }
            else if (
                value.Contains(
                    "منخفض",
                    StringComparison.OrdinalIgnoreCase))
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#10351F");

                cell.Style.Font.FontColor =
                    Success;
            }
            else
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#181A29");

                cell.Style.Font.FontColor =
                    Muted;
            }
        }

        // =========================================================
        // DUE DATE
        // =========================================================

        private static void ApplyDueDateStyle(
            IXLCell cell,
            FollowUpReport report)
        {
            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            if (!report.DueDate.HasValue)
                return;

            var dueDate =
                report.DueDate.Value.Date;

            if (
                dueDate <
                DateTime.Today &&
                !report.IsDone)
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#39161B");

                cell.Style.Font.FontColor =
                    Danger;

                cell.Style.Font.Bold =
                    true;
            }
            else if (
                dueDate ==
                DateTime.Today)
            {
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#382912");

                cell.Style.Font.FontColor =
                    Warning;

                cell.Style.Font.Bold =
                    true;
            }
            else
            {
                cell.Style.Font.FontColor =
                    White;
            }
        }

        // =========================================================
        // SHEET / PRINT SETTINGS
        // =========================================================

        private static void ApplyPremiumSheetLayout(
            IXLWorksheet ws,
            int totalColumns,
            int headerRow,
            int lastRow)
        {
            if (totalColumns <= 0)
                totalColumns = 1;

            if (lastRow < headerRow)
                lastRow = headerRow;

            var usedRange =
                ws.Range(
                    1,
                    1,
                    lastRow,
                    totalColumns);

            // -----------------------------------------------------
            // Background
            // -----------------------------------------------------

            usedRange.Style.Fill.PatternType =
                XLFillPatternValues.Solid;

            // Keep existing premium row/title colors.

            // -----------------------------------------------------
            // Borders
            // -----------------------------------------------------

            usedRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            usedRange.Style.Border.OutsideBorderColor =
                Indigo;

            // -----------------------------------------------------
            // Freeze panes
            // -----------------------------------------------------

            ws.SheetView.FreezeRows(
                headerRow);

            // -----------------------------------------------------
            // Filter
            // -----------------------------------------------------

            if (lastRow >= headerRow)
            {
                ws.Range(
                    headerRow,
                    1,
                    lastRow,
                    totalColumns)
                    .SetAutoFilter();
            }

            // -----------------------------------------------------
            // Gridlines
            // -----------------------------------------------------

         

            // -----------------------------------------------------
            // Default widths
            // -----------------------------------------------------

            ws.Columns(
                1,
                totalColumns)
                .AdjustToContents();

            for (
                int i = 1;
                i <= totalColumns;
                i++)
            {
                if (ws.Column(i).Width < 12)
                {
                    ws.Column(i).Width =
                        12;
                }

                if (ws.Column(i).Width > 42)
                {
                    ws.Column(i).Width =
                        42;
                }
            }

            // -----------------------------------------------------
            // Center Everything
            // -----------------------------------------------------

            if (lastRow >= headerRow)
            {
                ws.Range(
                    headerRow,
                    1,
                    lastRow,
                    totalColumns)
                    .Style
                    .Alignment
                    .Horizontal =
                    XLAlignmentHorizontalValues.Center;

                ws.Range(
                    headerRow,
                    1,
                    lastRow,
                    totalColumns)
                    .Style
                    .Alignment
                    .Vertical =
                    XLAlignmentVerticalValues.Center;

                ws.Range(
                    headerRow,
                    1,
                    lastRow,
                    totalColumns)
                    .Style
                    .Alignment
                    .WrapText =
                    true;
            }

            // -----------------------------------------------------
            // Print Area
            // -----------------------------------------------------

            ws.PageSetup.PrintAreas.Clear();

            ws.PageSetup.PrintAreas.Clear();

            var printRange =
                ws.Range(
                    1,
                    1,
                    lastRow,
                    totalColumns);

            ws.PageSetup.PrintAreas.Add(
                printRange.RangeAddress.ToString());

            // -----------------------------------------------------
            // Landscape
            // -----------------------------------------------------

            ws.PageSetup.PageOrientation =
                XLPageOrientation.Landscape;

            // -----------------------------------------------------
            // A4
            // -----------------------------------------------------

            ws.PageSetup.PaperSize =
                XLPaperSize.A4Paper;

            // -----------------------------------------------------
            // Fit to one page wide
            // -----------------------------------------------------

            ws.PageSetup.FitToPages(
                1,
                0);

            ws.PageSetup.PagesWide =
                1;

            ws.PageSetup.PagesTall =
                0;

            // -----------------------------------------------------
            // Margins
            // -----------------------------------------------------

            ws.PageSetup.Margins.Top =
                0.35;

            ws.PageSetup.Margins.Bottom =
                0.35;

            ws.PageSetup.Margins.Left =
                0.25;

            ws.PageSetup.Margins.Right =
                0.25;

            ws.PageSetup.Margins.Header =
                0.15;

            ws.PageSetup.Margins.Footer =
                0.15;

            // -----------------------------------------------------
            // Center on page
            // -----------------------------------------------------

            ws.PageSetup.CenterHorizontally =
                true;

            ws.PageSetup.CenterVertically =
                false;

            // -----------------------------------------------------
            // Repeat Header Rows
            // -----------------------------------------------------

            ws.PageSetup.SetRowsToRepeatAtTop(
                headerRow,
                headerRow);

            // -----------------------------------------------------
            // Print Options
            // -----------------------------------------------------

         

            // -----------------------------------------------------
            // Row Heights
            // -----------------------------------------------------

            ws.Row(1).Height =
                42;

            ws.Row(2).Height =
                25;

            ws.Row(3).Height =
                12;

            ws.Row(headerRow).Height =
                36;

            if (lastRow > headerRow)
            {
                for (
                    int i = headerRow + 1;
                    i <= lastRow;
                    i++)
                {
                    ws.Row(i).Height =
                        30;
                }
            }

            // -----------------------------------------------------
            // Page Header / Footer
            // -----------------------------------------------------

            ws.PageSetup.Header.Left.AddText(
                "Hex");

            ws.PageSetup.Header.Center.AddText(
                "HEMS • HEX STUDIO");

            ws.PageSetup.Header.Right.AddText(
                "&D");

            ws.PageSetup.Footer.Left.AddText(
                "HEMS");

            ws.PageSetup.Footer.Center.AddText(
                "صفحة &P من &N");

            ws.PageSetup.Footer.Right.AddText(
                "&T");
        }

        // =========================================================
        // TASK STATUS
        // =========================================================

        private static string GetTaskStatus(
            FollowUpReport report)
        {
            if (
                report.IsDone ||
                report.CompletedItems >=
                report.TotalItems)
            {
                return "مكتملة";
            }

            if (
                report.DueDate.HasValue &&
                report.DueDate.Value.Date <
                DateTime.Today &&
                !report.IsDone)
            {
                return "متأخرة";
            }

            if (!string.IsNullOrWhiteSpace(
                report.IsDoneOrNot))
            {
                return report.IsDoneOrNot!;
            }

            return "قيد التنفيذ";
        }

        // =========================================================
        // SAVE
        // =========================================================

        private async Task<string> SaveWorkbookAsync(
            XLWorkbook workbook,
            string fileName)
        {
            var wwwroot =
                _env.WebRootPath ??
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot");

            var reportsFolder =
                Path.Combine(
                    wwwroot,
                    "reports");

            if (!Directory.Exists(
                reportsFolder))
            {
                Directory.CreateDirectory(
                    reportsFolder);
            }

            var filePath =
                Path.Combine(
                    reportsFolder,
                    fileName);

            await Task.Run(
                () =>
                    workbook.SaveAs(
                        filePath));

            return filePath;
        }

        // =========================================================
        // URL
        // =========================================================

        private static bool IsUrlOrHttpPath(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value =
                value.Trim();

            return
                value.StartsWith(
                    "http://",
                    StringComparison.OrdinalIgnoreCase)
                ||
                value.StartsWith(
                    "https://",
                    StringComparison.OrdinalIgnoreCase)
                ||
                value.StartsWith("/")
                ||
                value.StartsWith("\\");
        }

        // =========================================================
        // SAFE FILE NAME
        // =========================================================

        private static string MakeSafeFileName(
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "report";

            foreach (
                var c in
                Path.GetInvalidFileNameChars())
            {
                name =
                    name.Replace(
                        c,
                        '_');
            }

            return
                name.Replace(
                    ' ',
                    '_');
        }

        // =========================================================
        // LOCALIZE HEADER
        // =========================================================

        private static string LocalizeHeader(
            string propName)
        {
            return propName switch
            {
                "FullName" =>
                    "اسم الموظف",

                "WorkDate" =>
                    "تاريخ العمل",

                "DateTime" =>
                    "تاريخ الإدخال",

                "Region" =>
                    "المنطقة",

                "Governorate" =>
                    "المحافظة",

                "StoreName" =>
                    "اسم المتجر",

                "StoreType" =>
                    "نوع المتجر",

                "Content" =>
                    "ملاحظات",

                "IsDone" =>
                    "إنجاز",

                "ProductsCount" =>
                    "عدد المنتجات",

                "ContractFilePath" =>
                    "توقيع العقد",

                "Path" =>
                    "الملف",

                "TaskDetails" =>
                    "تفاصيل المهمة",

                "TaskType" =>
                    "نوع المهمة",

                "ClientOrProject" =>
                    "العميل / المشروع",

                "AssignedEmployee" =>
                    "الموظف المسؤول",

                "Priority" =>
                    "الأولوية",

                "ProgressPercentage" =>
                    "التقدم",

                "CompletedItems" =>
                    "المنجز",

                "RemainingItems" =>
                    "المتبقي",

                "IsDoneOrNot" =>
                    "الحالة",

                "StartDate" =>
                    "تاريخ البدء",

                "DueDate" =>
                    "تاريخ التسليم",

                "CompletedDetails" =>
                    "ما تم إنجازه",

                "TotalItems" =>
                    "إجمالي البنود",

                _ =>
                    propName
            };
        }

        // =========================================================
        // CUSTOM FIELD
        // =========================================================

        private class CustomField
        {
            public string Name { get; set; } =
                "";

            public string Type { get; set; } =
                "";

            public string Value { get; set; } =
                "";
        }
    }
}