using System.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Localization;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AuditController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Index(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            NormalizeFilters(ref entityName, ref changedBy, ref action, ref dateFrom, ref dateTo);

            var (items, totalCount) = await _auditLogService.GetPagedAsync(entityName, entityId, changedBy, action, dateFrom, dateTo, page, pageSize);
            var viewModels = MapToViewModels(items);

            var model = new AuditIndexViewModel
            {
                Items = viewModels,
                EntityOptions = AuditIndexViewModel.DefaultEntityOptions,
                ActionOptions = AuditIndexViewModel.DefaultActionOptions,
                EntityName = entityName,
                EntityId = entityId,
                ChangedBy = changedBy,
                Action = action,
                DateFrom = dateFrom,
                DateTo = dateTo,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }

        public async Task<IActionResult> Export(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            NormalizeFilters(ref entityName, ref changedBy, ref action, ref dateFrom, ref dateTo);

            var items = await _auditLogService.GetAllAsync(entityName, entityId, changedBy, action, dateFrom, dateTo);
            var viewModels = MapToViewModels(items);
            var csv = BuildCsv(viewModels);
            var fileName = BuildExportFileName("csv");
            return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
        }

        public async Task<IActionResult> ExportExcel(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            NormalizeFilters(ref entityName, ref changedBy, ref action, ref dateFrom, ref dateTo);

            var items = await _auditLogService.GetAllAsync(entityName, entityId, changedBy, action, dateFrom, dateTo);
            var viewModels = MapToViewModels(items);
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(AuditText.Get("Audit.Export.SheetName", "Auditoria"));

            var headers = GetExportHeaders();
            for (var i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            var row = 2;
            foreach (var item in viewModels)
            {
                WriteExcelRow(worksheet, row, item);
                row++;
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = BuildExportFileName("xlsx");
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static string BuildCsv(IEnumerable<AuditLogViewModel> items)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", GetExportHeaders()));

            foreach (var item in items)
            {
                builder.AppendLine(BuildCsvRow(item));
            }

            return builder.ToString();
        }

        private static string BuildCsvRow(AuditLogViewModel item)
        {
            var (entityLabel, actionLabel) = GetLocalizedLabels(item);

            return string.Join(",",
                item.Id,
                Csv(item.ChangedAt.ToLocalTime().ToString(GetExportDateFormat(), CultureInfo.CurrentCulture)),
                Csv(item.ChangedBy),
                Csv(entityLabel),
                Csv(item.EntityId),
                Csv(actionLabel),
                Csv(item.OldValuesJson),
                Csv(item.NewValuesJson));
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string[] GetExportHeaders()
        {
            return new[]
            {
                AuditText.Get("Audit.Export.Column.Id", "Id"),
                AuditText.Get("Audit.Export.Column.ChangedAt", "ChangedAt"),
                AuditText.Get("Audit.Export.Column.ChangedBy", "ChangedBy"),
                AuditText.Get("Audit.Export.Column.EntityName", "EntityName"),
                AuditText.Get("Audit.Export.Column.EntityId", "EntityId"),
                AuditText.Get("Audit.Export.Column.Action", "Action"),
                AuditText.Get("Audit.Export.Column.OldValuesJson", "OldValuesJson"),
                AuditText.Get("Audit.Export.Column.NewValuesJson", "NewValuesJson")
            };
        }

        private static string GetExportFilePrefix()
        {
            return AuditText.Get("Audit.Export.FilePrefix", "audit-log");
        }

        private static string BuildExportFileName(string extension)
        {
            return $"{GetExportFilePrefix()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{extension}";
        }

        private static string GetExportDateFormat()
        {
            return AuditText.Get("Audit.Export.DateFormat", "dd/MM/yyyy HH:mm");
        }

        private static List<AuditLogViewModel> MapToViewModels(IEnumerable<AuditLog> items)
        {
            return items.Select(AuditLogViewModel.FromEntity).ToList();
        }

        private static (string EntityLabel, string ActionLabel) GetLocalizedLabels(AuditLogViewModel item)
        {
            return (
                AuditIndexViewModel.GetEntityLabel(item.EntityName),
                AuditIndexViewModel.GetActionLabel(item.Action));
        }

        private static void WriteExcelRow(IXLWorksheet worksheet, int row, AuditLogViewModel item)
        {
            var (entityLabel, actionLabel) = GetLocalizedLabels(item);

            worksheet.Cell(row, 1).Value = item.Id;
            worksheet.Cell(row, 2).Value = item.ChangedAt.ToLocalTime();
            worksheet.Cell(row, 2).Style.DateFormat.Format = GetExportDateFormat();
            worksheet.Cell(row, 3).Value = item.ChangedBy;
            worksheet.Cell(row, 4).Value = entityLabel;
            worksheet.Cell(row, 5).Value = item.EntityId;
            worksheet.Cell(row, 6).Value = actionLabel;
            worksheet.Cell(row, 7).Value = item.OldValuesJson;
            worksheet.Cell(row, 8).Value = item.NewValuesJson;
        }

        private static void NormalizeFilters(
            ref string? entityName,
            ref string? changedBy,
            ref string? action,
            ref DateTime? dateFrom,
            ref DateTime? dateTo)
        {
            entityName = NormalizeEnumFilter<AuditEntityType>(entityName);
            action = NormalizeEnumFilter<AuditActionType>(action);
            changedBy = NormalizeTextFilter(changedBy);

            if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
            {
                (dateFrom, dateTo) = (dateTo, dateFrom);
            }
        }

        private static string? NormalizeTextFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static string? NormalizeEnumFilter<TEnum>(string? value)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            return Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed)
                ? parsed.ToString()
                : null;
        }
    }
}