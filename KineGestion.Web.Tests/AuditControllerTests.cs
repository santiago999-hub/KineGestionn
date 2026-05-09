using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Controllers;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ClosedXML.Excel;
using System.IO;
using KineGestion.Core;

namespace KineGestion.Web.Tests
{
    public class AuditControllerTests
    {
        [Fact]
        public async Task Index_ShouldNormalizeInvalidPaging_AndPopulateModel()
        {
            var auditService = new Mock<IAuditLogService>();
            var dateFrom = new DateTime(2026, 5, 1);
            var dateTo = new DateTime(2026, 5, 9);

            auditService
                .Setup(s => s.GetPagedAsync("Patient", "10", "admin", "Update", dateFrom, dateTo, 1, 10))
                .ReturnsAsync((
                    new[]
                    {
                        new AuditLog
                        {
                            Id = 21,
                            EntityName = "Patient",
                            EntityId = "10",
                            Action = "Update",
                            ChangedBy = "admin@local",
                            ChangedAt = new DateTime(2026, 5, 9, 14, 0, 0),
                            OldValuesJson = "{\"Nombre\":\"Ana\"}",
                            NewValuesJson = "{\"Nombre\":\"Ana Maria\"}"
                        }
                    },
                    1));

            var controller = new AuditController(auditService.Object);

            var result = await controller.Index("Patient", "10", "admin", "Update", dateFrom, dateTo, 0, 100);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AuditIndexViewModel>(view.Model);

            Assert.Equal(1, model.Page);
            Assert.Equal(10, model.PageSize);
            Assert.Equal("Patient", model.EntityName);
            Assert.Equal("10", model.EntityId);
            Assert.Equal("admin", model.ChangedBy);
            Assert.Equal("Update", model.Action);
            Assert.Equal(dateFrom, model.DateFrom);
            Assert.Equal(dateTo, model.DateTo);
            Assert.Equal(1, model.TotalCount);

            var item = Assert.Single(model.Items);
            Assert.Equal("Patient", item.EntityName);
            Assert.Equal("10", item.EntityId);
            Assert.Equal("Update", item.Action);
            Assert.Equal("admin@local", item.ChangedBy);
            Assert.Equal("{\"Nombre\":\"Ana\"}", item.OldValuesJson);
            Assert.Equal("{\"Nombre\":\"Ana Maria\"}", item.NewValuesJson);

            auditService.Verify(s => s.GetPagedAsync("Patient", "10", "admin", "Update", dateFrom, dateTo, 1, 10), Times.Once);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(51)]
        public async Task Index_ShouldFallbackToDefaultPageSize_WhenOutOfRange(int pageSize)
        {
            var auditService = new Mock<IAuditLogService>();

            auditService
                .Setup(s => s.GetPagedAsync(null, null, null, null, null, null, 2, 10))
                .ReturnsAsync((Array.Empty<AuditLog>(), 0));

            var controller = new AuditController(auditService.Object);

            var result = await controller.Index(null, null, null, null, null, null, 2, pageSize);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AuditIndexViewModel>(view.Model);

            Assert.Equal(2, model.Page);
            Assert.Equal(10, model.PageSize);
            auditService.Verify(s => s.GetPagedAsync(null, null, null, null, null, null, 2, 10), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldForwardActionAndDateFilters_ToService()
        {
            var auditService = new Mock<IAuditLogService>();
            var dateFrom = new DateTime(2026, 5, 7);
            var dateTo = new DateTime(2026, 5, 8);

            auditService
                .Setup(s => s.GetPagedAsync("Session", null, "admin", "Delete", dateFrom, dateTo, 3, 20))
                .ReturnsAsync((Array.Empty<AuditLog>(), 0));

            var controller = new AuditController(auditService.Object);

            var result = await controller.Index("Session", null, "admin", "Delete", dateFrom, dateTo, 3, 20);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AuditIndexViewModel>(view.Model);

            Assert.Equal("Delete", model.Action);
            Assert.Equal(dateFrom, model.DateFrom);
            Assert.Equal(dateTo, model.DateTo);
            auditService.Verify(s => s.GetPagedAsync("Session", null, "admin", "Delete", dateFrom, dateTo, 3, 20), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldPopulatePredefinedEntityOptions()
        {
            var auditService = new Mock<IAuditLogService>();

            auditService
                .Setup(s => s.GetPagedAsync(null, null, null, null, null, null, 1, 10))
                .ReturnsAsync((Array.Empty<AuditLog>(), 0));

            var controller = new AuditController(auditService.Object);

            var result = await controller.Index(null, null, null, null, null, null, 1, 10);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AuditIndexViewModel>(view.Model);

            Assert.Contains(AuditEntityType.Patient, model.EntityOptions);
            Assert.Contains(AuditEntityType.Professional, model.EntityOptions);
            Assert.Contains(AuditEntityType.Treatment, model.EntityOptions);
            Assert.Contains(AuditEntityType.Session, model.EntityOptions);
            Assert.Contains(AuditEntityType.Office, model.EntityOptions);
            Assert.Contains(AuditEntityType.Equipment, model.EntityOptions);
            Assert.Contains(AuditActionType.Create, model.ActionOptions);
            Assert.Contains(AuditActionType.Update, model.ActionOptions);
            Assert.Contains(AuditActionType.Delete, model.ActionOptions);
        }

        [Fact]
        public async Task Export_ShouldReturnCsvFile_WithFilteredRows()
        {
            var auditService = new Mock<IAuditLogService>();
            var dateFrom = new DateTime(2026, 5, 7);
            var dateTo = new DateTime(2026, 5, 8);

            auditService
                .Setup(s => s.GetAllAsync("Patient", null, "admin", "Update", dateFrom, dateTo))
                .ReturnsAsync(new[]
                {
                    new AuditLog
                    {
                        Id = 9,
                        EntityName = "Patient",
                        EntityId = "12",
                        Action = "Update",
                        ChangedBy = "admin@local",
                        ChangedAt = new DateTime(2026, 5, 8, 10, 15, 0),
                        OldValuesJson = "{\"DNI\":\"123\"}",
                        NewValuesJson = "{\"DNI\":\"456\"}"
                    }
                });

            var controller = new AuditController(auditService.Object);

            var result = await controller.Export("Patient", null, "admin", "Update", dateFrom, dateTo);

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv; charset=utf-8", file.ContentType);
            Assert.EndsWith(".csv", file.FileDownloadName, StringComparison.OrdinalIgnoreCase);

            var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
            var expectedEntityLabel = AuditIndexViewModel.GetEntityLabel("Patient");
            var expectedActionLabel = AuditIndexViewModel.GetActionLabel("Update");
            Assert.Contains("Id,ChangedAt,ChangedBy,EntityName,EntityId,Action,OldValuesJson,NewValuesJson", csv);
            Assert.Contains("\"admin@local\"", csv);
            Assert.Contains($"\"{expectedEntityLabel}\"", csv);
            Assert.Contains($"\"{expectedActionLabel}\"", csv);

            auditService.Verify(s => s.GetAllAsync("Patient", null, "admin", "Update", dateFrom, dateTo), Times.Once);
        }

        [Fact]
        public async Task ExportExcel_ShouldReturnXlsxFile_WithWorksheetAndHeaders()
        {
            var auditService = new Mock<IAuditLogService>();
            var dateFrom = new DateTime(2026, 5, 7);
            var dateTo = new DateTime(2026, 5, 8);

            auditService
                .Setup(s => s.GetAllAsync("Patient", null, "admin", "Update", dateFrom, dateTo))
                .ReturnsAsync(new[]
                {
                    new AuditLog
                    {
                        Id = 9,
                        EntityName = "Patient",
                        EntityId = "12",
                        Action = "Update",
                        ChangedBy = "admin@local",
                        ChangedAt = new DateTime(2026, 5, 8, 10, 15, 0),
                        OldValuesJson = "{\"DNI\":\"123\"}",
                        NewValuesJson = "{\"DNI\":\"456\"}"
                    }
                });

            var controller = new AuditController(auditService.Object);

            var result = await controller.ExportExcel("Patient", null, "admin", "Update", dateFrom, dateTo);

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
            Assert.EndsWith(".xlsx", file.FileDownloadName, StringComparison.OrdinalIgnoreCase);

            using var stream = new MemoryStream(file.FileContents);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet("Auditoria");
            var expectedEntityLabel = AuditIndexViewModel.GetEntityLabel("Patient");
            var expectedActionLabel = AuditIndexViewModel.GetActionLabel("Update");

            Assert.Equal("Id", worksheet.Cell(1, 1).GetString());
            Assert.Equal("ChangedAt", worksheet.Cell(1, 2).GetString());
            Assert.Equal(expectedEntityLabel, worksheet.Cell(2, 4).GetString());
            Assert.Equal(expectedActionLabel, worksheet.Cell(2, 6).GetString());

            auditService.Verify(s => s.GetAllAsync("Patient", null, "admin", "Update", dateFrom, dateTo), Times.Once);
        }

        [Fact]
        public async Task Index_ShouldExposePredefinedEntityOptions_AndExportActions()
        {
            var auditService = new Mock<IAuditLogService>();

            auditService
                .Setup(s => s.GetPagedAsync(null, null, null, null, null, null, 1, 10))
                .ReturnsAsync((Array.Empty<AuditLog>(), 0));

            var controller = new AuditController(auditService.Object);

            var result = await controller.Index(null, null, null, null, null, null, 1, 10);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AuditIndexViewModel>(view.Model);

            Assert.Contains(AuditEntityType.Patient, model.EntityOptions);
            Assert.Contains(AuditEntityType.Office, model.EntityOptions);
            Assert.Contains(AuditActionType.Create, model.ActionOptions);
            Assert.Contains(AuditActionType.Update, model.ActionOptions);
            Assert.Contains(AuditActionType.Delete, model.ActionOptions);
        }
    }
}
