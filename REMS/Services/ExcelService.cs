using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting;
using REMS.Enititys;

namespace REMS.Services
{
    public class ExcelService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ExcelService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> GenerateReportExcel(List<Report> filteredReportList,DateTime Date)
        {
            string reportName = "تقرير العمل";
            string filePath = await GenerateExcelAsync(filteredReportList, reportName,Date);

            
            string fileUrl = $"/reports/{Path.GetFileName(filePath)}";
            return fileUrl;
        }
        public async Task<string> GenerateFollowUpReportExcel(List<FollowUpReport> filteredReportList, DateTime Date)
        {
            string reportName = "تقرير قسم المتابعة";
            string filePath = await GenerateExcelAsync(filteredReportList, reportName, Date);


            string fileUrl = $"/reports/{Path.GetFileName(filePath)}";
            return fileUrl;
        }

        private async Task<string> GenerateExcelAsync<T>(List<T> modelList, string reportName,DateTime Date) where T : class
        {
            string htmlTable = GenerateHtmlTable(modelList, reportName);
            string filePath = await GenerateExcelFromHtmlAsync(htmlTable, reportName,Date);
            return filePath;
        }

        private string GenerateHtmlTable<T>(List<T> data, string reportName) where T : class
        {
            if (data == null || data.Count == 0)
                return "<h3>لا توجد بيانات لعرضها</h3>";

            // الخصائص الأساسية
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead &&
                    (p.PropertyType.IsPrimitive ||
                     p.PropertyType == typeof(string) ||
                     p.PropertyType == typeof(DateTime) ||
                     p.PropertyType == typeof(decimal) ||
                     p.PropertyType.IsEnum ||
                     Nullable.GetUnderlyingType(p.PropertyType) != null))
                .ToList();

            // استخراج كل أسماء الحقول المخصصة
            var customFieldNames = new HashSet<string>();
            foreach (var item in data)
            {
                var jsonProp = typeof(T).GetProperty("CustomFieldsJson");
                if (jsonProp == null) continue;

                var jsonValue = jsonProp.GetValue(item)?.ToString();
                if (string.IsNullOrWhiteSpace(jsonValue)) continue;

                try
                {
                    var fields = System.Text.Json.JsonSerializer.Deserialize<List<CustomField>>(jsonValue);
                    if (fields != null)
                    {
                        foreach (var f in fields)
                            customFieldNames.Add(f.Name);
                    }
                }
                catch { }
            }

            var html = new StringWriter();
            html.WriteLine("<html><head><meta charset='UTF-8'/><style>");
            html.WriteLine(@"
        table { width:100%; border-collapse:collapse; direction:rtl; font-family:Arial; }
        th, td { border:1px solid #ccc; text-align:center; padding:6px; vertical-align:middle; }
        th { background:#f2f2f2; }
        tr:nth-child(even) { background:#fafafa; }
        h1 { text-align:center; margin:10px; }
    ");
            html.WriteLine("</style></head><body>");
            html.WriteLine($"<h1>{reportName}</h1>");
            html.WriteLine("<table><tr>");

            // رؤوس الأعمدة الأساسية
            foreach (var prop in properties)
                html.WriteLine($"<th>{prop.Name}</th>");

            // رؤوس الحقول المخصصة
            foreach (var fieldName in customFieldNames)
                html.WriteLine($"<th>{fieldName}</th>");

            html.WriteLine("</tr>");

            // الصفوف
            foreach (var item in data)
            {
                html.WriteLine("<tr>");
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item);
                    string text = value switch
                    {
                        null => "",
                        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
                        bool b => b ? "نعم" : "لا",
                        _ => value.ToString()
                    };
                    html.WriteLine($"<td>{System.Net.WebUtility.HtmlEncode(text)}</td>");
                }

                // تعبئة الحقول المخصصة
                var fieldValues = new Dictionary<string, string>();
                try
                {
                    var jsonProp = typeof(T).GetProperty("CustomFieldsJson");
                    if (jsonProp != null)
                    {
                        var jsonValue = jsonProp.GetValue(item)?.ToString();
                        if (!string.IsNullOrEmpty(jsonValue))
                        {
                            var fields = System.Text.Json.JsonSerializer.Deserialize<List<CustomField>>(jsonValue);
                            if (fields != null)
                            {
                                foreach (var f in fields)
                                {
                                    string val = f.Value ?? "";
                                    if (f.Type == "File" || f.Type == "Image")
                                        val = f.Value; // يمكن وضع رابط مباشر هنا
                                    fieldValues[f.Name] = val;
                                }
                            }
                        }
                    }
                }
                catch { }

                foreach (var fieldName in customFieldNames)
                {
                    fieldValues.TryGetValue(fieldName, out var val);
                    html.WriteLine($"<td>{System.Net.WebUtility.HtmlEncode(val ?? "")}</td>");
                }

                html.WriteLine("</tr>");
            }

            html.WriteLine("</table></body></html>");
            return html.ToString();
        }

        private class CustomField
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string Value { get; set; } = "";
        }



        private async Task<string> GenerateExcelFromHtmlAsync(string html, string sheetName, DateTime Date)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(sheetName);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var table = htmlDoc.DocumentNode.SelectSingleNode("//table");

            if (table == null)
            {
                throw new InvalidOperationException("No table found in HTML.");
            }

            int rowIndex = 1;
            foreach (var row in table.SelectNodes("tr"))
            {
                int colIndex = 1;
                foreach (var cell in row.SelectNodes("th|td"))
                {
                    var xlCell = worksheet.Cell(rowIndex, colIndex);
                    xlCell.Value = cell.InnerText.Trim();
                    xlCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    xlCell.Style.Border.OutsideBorderColor = XLColor.Black;
                    xlCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    xlCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    var pageSetup = worksheet.PageSetup;
                    pageSetup.CenterHorizontally = true;
                    pageSetup.PageOrientation = XLPageOrientation.Landscape;
                    pageSetup.AdjustTo(100);
                    pageSetup.PagesWide = 1;
                    pageSetup.PagesTall = 0;
                    pageSetup.Margins.Top = 0.2;
                    pageSetup.Margins.Bottom = 0.2;
                    pageSetup.Margins.Left = 0.2;
                    pageSetup.Margins.Right = 0.2;
                  
                    colIndex++;
                }
                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();

            string wwwrootPath = _webHostEnvironment.WebRootPath;
            string reportsFolderPath = Path.Combine(wwwrootPath, "reports");

            if (!Directory.Exists(reportsFolderPath))
            {
                Directory.CreateDirectory(reportsFolderPath);
            }

            string fileName = $"{sheetName}_{Date:yyyyMMdd}.xlsx";
            string filePath = Path.Combine(reportsFolderPath, fileName);

            workbook.SaveAs(filePath);

            return filePath; 
        }
    }
}
