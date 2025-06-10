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
            var properties = typeof(T).GetProperties()
                                      .Where(prop => prop.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), false)
                                                         .Any())
                                      .ToList();

            var html = new StringWriter();

            html.WriteLine("<html>");
            html.WriteLine("<head>");
            html.WriteLine("<style>");
            html.WriteLine(
                "*{\r\n    padding: 0;\r\n    margin: 0;\r\n} " +
                "table {\r\n" +
                "font-family: Arial, sans-serif;\r\n        border-collapse: collapse;\r\n        width: 100%;\r\n    }\r\n    \r\n    th, td {\r\n        border: 1px solid #ddd;\r\n\r\n        text-align: center;\r\n    }\r\n    \r\n    th {\r\n        background-color: #f2f2f2;\r\n    }\r\n    \r\n    tr:nth-child(even) {\r\n        background-color: #f9f9f9;\r\n    }\r\n    \r\n    tr:hover {\r\n        background-color: #e9e9e9;\r\n    }\r\n    \r\n    h1 {\r\n        text-align: center;\r\n    }\r\n    \r\n    table th:first-child,\r\n    table td:first-child {\r\n        text-align: center;\r\n    }"
                );
            html.WriteLine("</style>");
            html.WriteLine("</head>");
            html.WriteLine("<body>");
            html.WriteLine($"<h1>{reportName}</h1>");
            html.WriteLine("<table>");

            html.WriteLine("<tr>");
            foreach (var prop in properties)
            {
                var displayName = prop.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), false)
                                      .Cast<System.ComponentModel.DisplayNameAttribute>()
                                      .FirstOrDefault()?.DisplayName;

                if (!string.IsNullOrEmpty(displayName))
                {
                    html.WriteLine($"<th>{displayName}</th>");
                }
            }
            html.WriteLine("</tr>");

            foreach (var item in data)
            {
                html.WriteLine("<tr>");
                foreach (var prop in properties)
                {
                    var displayName = prop.GetCustomAttributes(typeof(System.ComponentModel.DisplayNameAttribute), false)
                                          .Cast<System.ComponentModel.DisplayNameAttribute>()
                                          .FirstOrDefault()?.DisplayName;

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        var value = prop.GetValue(item);
                        html.WriteLine($"<td>{value?.ToString() ?? string.Empty}</td>");
                    }
                }
                html.WriteLine("</tr>");
            }

            html.WriteLine("</table>");
            html.WriteLine("</body>");
            html.WriteLine("</html>");

            return html.ToString();
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
