using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KineGestion.Core.Interfaces;
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

            var (items, totalCount) = await _auditLogService.GetPagedAsync(entityName, entityId, changedBy, action, dateFrom, dateTo, page, pageSize);

            var model = new AuditIndexViewModel
            {
                Items = items.Select(AuditLogViewModel.FromEntity).ToList(),
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
            var items = await _auditLogService.GetAllAsync(entityName, entityId, changedBy, action, dateFrom, dateTo);
            var csv = BuildCsv(items.Select(AuditLogViewModel.FromEntity));
            var fileName = $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
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
            var items = await _auditLogService.GetAllAsync(entityName, entityId, changedBy, action, dateFrom, dateTo);
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Auditoria");

            var headers = new[] { "Id", "ChangedAt", "ChangedBy", "EntityName", "EntityId", "Action", "OldValuesJson", "NewValuesJson" };
            for (var i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            var row = 2;
            foreach (var item in items.Select(AuditLogViewModel.FromEntity))
            {
                var entityLabel = AuditIndexViewModel.GetEntityLabel(item.EntityName);
                var actionLabel = AuditIndexViewModel.GetActionLabel(item.Action);

                worksheet.Cell(row, 1).Value = item.Id;
                worksheet.Cell(row, 2).Value = item.ChangedAt.ToLocalTime();
                worksheet.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                worksheet.Cell(row, 3).Value = item.ChangedBy;
                worksheet.Cell(row, 4).Value = entityLabel;
                worksheet.Cell(row, 5).Value = item.EntityId;
                worksheet.Cell(row, 6).Value = actionLabel;
                worksheet.Cell(row, 7).Value = item.OldValuesJson;
                worksheet.Cell(row, 8).Value = item.NewValuesJson;
                row++;
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static string BuildCsv(IEnumerable<AuditLogViewModel> items)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Id,ChangedAt,ChangedBy,EntityName,EntityId,Action,OldValuesJson,NewValuesJson");

            foreach (var item in items)
            {
                var entityLabel = AuditIndexViewModel.GetEntityLabel(item.EntityName);
                var actionLabel = AuditIndexViewModel.GetActionLabel(item.Action);

                builder.AppendLine(string.Join(",",
                    item.Id,
                    Csv(item.ChangedAt.ToString("o")),
                    Csv(item.ChangedBy),
                    Csv(entityLabel),
                    Csv(item.EntityId),
                    Csv(actionLabel),
                    Csv(item.OldValuesJson),
                    Csv(item.NewValuesJson)));
            }

            return builder.ToString();
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}