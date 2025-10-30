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

        public ExcelService(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Generate Excel for a list of generic items (Report / FollowUpReport / etc.)
        /// Returns the public relative URL (e.g. /reports/file.xlsx)
        /// </summary>
        public async Task<string> GenerateGenericReportExcelAsync<T>(List<T> items, string sheetName, DateTime date) where T : class
        {
            if (items == null) items = new List<T>();

            // Properties via reflection
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToList();

            // اختر الخصائص الأساسية المناسبة (string, primitive, DateTime, decimal, enum, nullable primitive)
            var baseProps = props.Where(p =>
            {
                var t = p.PropertyType;
                if (t == typeof(string)) return true;
                if (t.IsPrimitive) return true;
                if (t == typeof(DateTime) || t == typeof(DateTime?)) return true;
                if (t == typeof(decimal) || t == typeof(decimal?)) return true;
                if (t.IsEnum) return true;
                if (Nullable.GetUnderlyingType(t) != null && Nullable.GetUnderlyingType(t).IsPrimitive) return true;
                return false;
            }).ToList();

            // اجمع أسماء الحقول المخصّصة (من CustomFieldsJson) في قائمة ثابتة الترتيب
            var customFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var jsonProp = props.FirstOrDefault(p => string.Equals(p.Name, "CustomFieldsJson", StringComparison.OrdinalIgnoreCase));
            if (jsonProp != null)
            {
                foreach (var item in items)
                {
                    try
                    {
                        var json = jsonProp.GetValue(item)?.ToString();
                        if (string.IsNullOrWhiteSpace(json)) continue;
                        var fields = JsonSerializer.Deserialize<List<CustomField>>(json);
                        if (fields == null) continue;
                        foreach (var f in fields)
                        {
                            if (!string.IsNullOrWhiteSpace(f.Name))
                                customFieldNames.Add(f.Name.Trim());
                        }
                    }
                    catch
                    {
                        // تجاهل عنصر واحد إذا كان الـ JSON مكسورًا
                    }
                }
            }

            var customFieldList = customFieldNames.ToList(); // ترتيب ثابت للاستخدام لاحقًا

            // Prepare workbook
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "تقرير" : sheetName);

            int col = 1;
            // Header row: base props (نستخدم LocalizeHeader لتحويل أسماء الحقول إن رغبت)
            foreach (var p in baseProps)
            {
                var header = LocalizeHeader(p.Name);
                ws.Cell(1, col).Value = header;
                col++;
            }

            // Custom field headers
            foreach (var cf in customFieldList)
            {
                ws.Cell(1, col).Value = cf;
                col++;
            }

            // Style header
            var totalColumns = baseProps.Count + customFieldList.Count;
            if (totalColumns == 0) totalColumns = 1;
            var headerRange = ws.Range(1, 1, 1, totalColumns);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

            // Rows
            int row = 2;
            foreach (var item in items)
            {
                col = 1;

                // base props
                foreach (var p in baseProps)
                {
                    var cell = ws.Cell(row, col);
                    var raw = p.GetValue(item);

                    if (raw == null)
                    {
                        cell.Value = "";
                    }
                    else if (raw is DateTime dt)
                    {
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                    }
                    else if (raw is bool b)
                    {
                        cell.Value = b ? "نعم" : "لا";
                    }
                    else
                    {
                        var s = raw.ToString() ?? "";

                        if (IsUrlOrHttpPath(s))
                        {
                            // ضع الرابط كـ hyperlink باستخدام API المتوافق مع الإصدارات الحديثة
                            cell.Value = s;
                            try
                            {
                                cell.SetHyperlink(new XLHyperlink(s));
                                cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                            }
                            catch
                            {
                                // محاولة بديلة: حاول إنشاء hyperlink عبر ExternalAddress لو أمكن
                                try
                                {
                                    var gh = cell.GetHyperlink();
                                    if (gh != null)
                                        gh.ExternalAddress = new Uri(s, UriKind.RelativeOrAbsolute);
                                }
                                catch { /* تجاهل إذا فشل */ }
                            }
                        }
                        else
                        {
                            cell.Value = s;
                        }
                    }

                    cell.Style.Alignment.WrapText = true;
                    // محتوى عربي: محاذاة إلى اليمين أفضل، لكن للخلايا العامة نجعلها وسطًا ثم نجري تعديلًا لاحقًا
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    col++;
                }

                // custom fields values for this item
                var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (jsonProp != null)
                {
                    try
                    {
                        var json = jsonProp.GetValue(item)?.ToString();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var fields = JsonSerializer.Deserialize<List<CustomField>>(json);
                            if (fields != null)
                            {
                                foreach (var f in fields)
                                {
                                    var key = f.Name?.Trim() ?? "";
                                    if (!fieldValues.ContainsKey(key))
                                        fieldValues[key] = f.Value ?? "";
                                }
                            }
                        }
                    }
                    catch { }
                }

                // ضع قيم الحقول المخصّصة بحسب الترتيب في customFieldList
                for (int i = 0; i < customFieldList.Count; i++)
                {
                    var headerName = customFieldList[i];
                    fieldValues.TryGetValue(headerName, out var val);
                    var cell = ws.Cell(row, baseProps.Count + i + 1);

                    if (!string.IsNullOrEmpty(val) && IsUrlOrHttpPath(val))
                    {
                        cell.Value = val;
                        try
                        {
                            cell.SetHyperlink(new XLHyperlink(val));
                            cell.Style.Font.Underline = XLFontUnderlineValues.Single;
                        }
                        catch
                        {
                            try
                            {
                                var gh = cell.GetHyperlink();
                                if (gh != null)
                                    gh.ExternalAddress = new Uri(val, UriKind.RelativeOrAbsolute);
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        cell.Value = val ?? "";
                    }

                    cell.Style.Alignment.WrapText = true;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                row++;
            }

            // Formatting: autofilter, freeze header, adjust columns, set right-to-left alignment for used cells
            try
            {
                if (ws.RangeUsed() != null)
                {
                    ws.SheetView.FreezeRows(1);
                    ws.RangeUsed().SetAutoFilter();
                    ws.Columns(1, totalColumns).AdjustToContents();

                    // لجعل العرض متوافقًا مع اللغة العربية، نجعل المحاذاة العامة يميناً (ماعدا الرأس نجعلها مركز)
                    ws.Range(2, 1, row - 1, totalColumns).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    // Header نتركه مركزًا — سبق ضبطه
                }
            }
            catch
            {
                // تجاهل أخطاء تنسيق صغيرة
            }

            // Create reports folder if missing
            var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var reportsFolder = Path.Combine(wwwroot, "reports");
            if (!Directory.Exists(reportsFolder))
                Directory.CreateDirectory(reportsFolder);

            // Unique file name to avoid collisions
            var safeSheetName = MakeSafeFileName(sheetName);
            var fileName = $"{safeSheetName}_{date:yyyyMMdd_HHmmss}.xlsx";
            var filePath = Path.Combine(reportsFolder, fileName);

            // Save workbook asynchronously
            await Task.Run(() => wb.SaveAs(filePath));

            // Return web relative path
            var relative = $"/reports/{fileName}";
            return relative;
        }

        // Convenience wrappers for your specific types
        public Task<string> GenerateReportExcelAsync(List<Report> reports, DateTime date)
            => GenerateGenericReportExcelAsync(reports, "تقرير_العمل", date);

        public Task<string> GenerateFollowUpReportExcelAsync(List<FollowUpReport> reports, DateTime date)
            => GenerateGenericReportExcelAsync(reports, "تقرير_قسم_المتابعة", date);


        // ---------- helpers ----------
        private static bool IsUrlOrHttpPath(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                // treat common file paths as links (relative web path)
                || s.StartsWith("/") || s.StartsWith("\\");
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "report";
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        private static string LocalizeHeader(string propName)
        {
            // هنا يمكنك إضافة قواعد تحويل لأسماء الخصائص إلى عناوين عربية أكثر وضوحاً
            return propName switch
            {
                "FullName" => "اسم الموظف",
                "WorkDate" => "تاريخ العمل",
                "DateTime" => "تاريخ الإدخال",
                "Region" => "المنطقة",
                "Governorate" => "المحافظة",
                "StoreName" => "اسم المتجر",
                "StoreType" => "نوع المتجر",
                "Content" => "ملاحظات",
                "IsDone" => "إنجاز",
                "ProductsCount" => "عدد المنتجات",
                "ContractFilePath" => "توقيع العقد",
                "Path" => "الملف",
                _ => propName // fallback to property name
            };
        }

        private class CustomField
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string Value { get; set; } = "";
        }
    }
}
