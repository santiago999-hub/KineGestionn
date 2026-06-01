using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using KineGestion.Core;
using KineGestion.Core.Exceptions;
using KineGestion.Core.Interfaces;
using KineGestion.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KineGestion.Web.Controllers
{
    [Authorize(Roles = "Admin,Kinesiologo")]
    public class SessionsController : Controller
    {
        private const string IndexFiltersCookieKey = "kg.sessions.index.filters";
        private const string MyAgendaFiltersCookieKey = "kg.sessions.myagenda.filters";

        private readonly ISessionService _sessionService;
        private readonly IPatientService _patientService;
        private readonly IProfessionalService _professionalService;
        private readonly ITreatmentService _treatmentService;
        private readonly IOfficeService _officeService;

        public SessionsController(
            ISessionService sessionService,
            IPatientService patientService,
            IProfessionalService professionalService,
            ITreatmentService treatmentService,
            IOfficeService officeService)
        {
            _sessionService = sessionService;
            _patientService = patientService;
            _professionalService = professionalService;
            _treatmentService = treatmentService;
            _officeService = officeService;
        }

        // Listado administrativo: no incluye Evolution
        public async Task<IActionResult> Index(string? search, SessionStatus? status, PaymentStatus? paymentStatus, DateTime? dateFrom, DateTime? dateTo, string? sortBy = "fecha", string? sortDir = "desc", int page = 1, int pageSize = 10)
        {
            var actionStopwatch = Stopwatch.StartNew();

            if (HasNoQueryString() && TryReadFilters(IndexFiltersCookieKey, out var savedFilters))
            {
                search = savedFilters.Search;
                status = ParseEnumOrNull<SessionStatus>(savedFilters.Status);
                paymentStatus = ParseEnumOrNull<PaymentStatus>(savedFilters.PaymentStatus);
                dateFrom = ParseDateOrNull(savedFilters.DateFrom);
                dateTo = ParseDateOrNull(savedFilters.DateTo);
                sortBy = string.IsNullOrWhiteSpace(savedFilters.SortBy) ? sortBy : savedFilters.SortBy;
                sortDir = string.IsNullOrWhiteSpace(savedFilters.SortDir) ? sortDir : savedFilters.SortDir;
                if (savedFilters.PageSize.HasValue)
                    pageSize = savedFilters.PageSize.Value;
            }

            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            SaveFilters(IndexFiltersCookieKey, new SavedSessionFilters
            {
                Search = search,
                Status = status?.ToString(),
                PaymentStatus = paymentStatus?.ToString(),
                DateFrom = dateFrom?.ToString("yyyy-MM-dd"),
                DateTo = dateTo?.ToString("yyyy-MM-dd"),
                SortBy = sortBy,
                SortDir = sortDir,
                PageSize = pageSize
            });

            var (items, totalCount) = await _sessionService.GetPagedListForAdminAsync(page, pageSize, search, status, paymentStatus, dateFrom, dateTo, sortBy, sortDir);
            var viewModels = items.Select(SessionViewModel.FromDto).ToList();

            var model = new SessionIndexViewModel
            {
                Items = viewModels,
                Search = search,
                Status = status,
                PaymentStatus = paymentStatus,
                DateFrom = dateFrom,
                DateTo = dateTo,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "fecha" : sortBy,
                SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            actionStopwatch.Stop();
            if (HttpContext is not null)
                HttpContext.Items["kg.pipeline.actionMs"] = actionStopwatch.ElapsedMilliseconds;

            return View(model);
        }

        // GET: /Sessions/MyAgenda — Vista exclusiva del Kinesiologo autenticado
        [Authorize(Roles = "Kinesiologo")]
        public async Task<IActionResult> MyAgenda(
            string? search,
            SessionStatus? status,
            PaymentStatus? paymentStatus,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page = 1,
            int pageSize = 10)
        {
            if (HasNoQueryString() && TryReadFilters(MyAgendaFiltersCookieKey, out var savedFilters))
            {
                search = savedFilters.Search;
                status = ParseEnumOrNull<SessionStatus>(savedFilters.Status);
                paymentStatus = ParseEnumOrNull<PaymentStatus>(savedFilters.PaymentStatus);
                dateFrom = ParseDateOrNull(savedFilters.DateFrom);
                dateTo = ParseDateOrNull(savedFilters.DateTo);
                if (savedFilters.PageSize.HasValue)
                    pageSize = savedFilters.PageSize.Value;
            }

            var profIdClaim = User.FindFirstValue("ProfessionalId");
            if (!int.TryParse(profIdClaim, out var professionalId))
            {
                TempData["Error"] = "Tu usuario no está vinculado a ningún profesional. Solicitá al administrador que lo configure.";
                return RedirectToAction(nameof(Index));
            }

            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

            SaveFilters(MyAgendaFiltersCookieKey, new SavedSessionFilters
            {
                Search = search,
                Status = status?.ToString(),
                PaymentStatus = paymentStatus?.ToString(),
                DateFrom = dateFrom?.ToString("yyyy-MM-dd"),
                DateTo = dateTo?.ToString("yyyy-MM-dd"),
                PageSize = pageSize
            });

            var (items, totalCount) = await _sessionService.GetPagedListByProfessionalAsync(
                professionalId, page, pageSize, search, status, paymentStatus, dateFrom, dateTo);

            var viewModels = items.Select(SessionViewModel.FromDto).ToList();

            var model = new SessionIndexViewModel
            {
                Items = viewModels,
                Search = search,
                Status = status,
                PaymentStatus = paymentStatus,
                DateFrom = dateFrom,
                DateTo = dateTo,
                SortBy = "fecha",
                SortDir = "desc",
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(model);
        }

        private bool TryReadFilters(string cookieKey, out SavedSessionFilters filters)
        {
            filters = new SavedSessionFilters();

            if (HttpContext?.Request is null)
                return false;

            if (!Request.Cookies.TryGetValue(cookieKey, out var value) || string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                var parsed = JsonSerializer.Deserialize<SavedSessionFilters>(value);
                if (parsed is null)
                    return false;

                filters = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SaveFilters(string cookieKey, SavedSessionFilters filters)
        {
            if (HttpContext?.Request is null || HttpContext.Response is null)
                return;

            var payload = JsonSerializer.Serialize(filters);
            Response.Cookies.Append(cookieKey, payload, new CookieOptions
            {
                HttpOnly = true,
                Secure = !Request.IsHttps ? false : true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(14),
                IsEssential = true
            });
        }

        private bool HasNoQueryString()
        {
            return HttpContext?.Request?.QueryString.HasValue != true;
        }

        private static DateTime? ParseDateOrNull(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            return DateTime.TryParse(input, out var result) ? result : null;
        }

        private static TEnum? ParseEnumOrNull<TEnum>(string? input) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            return Enum.TryParse<TEnum>(input, out var parsed) ? parsed : null;
        }

        private sealed class SavedSessionFilters
        {
            public string? Search { get; set; }
            public string? Status { get; set; }
            public string? PaymentStatus { get; set; }
            public string? DateFrom { get; set; }
            public string? DateTo { get; set; }
            public string? SortBy { get; set; }
            public string? SortDir { get; set; }
            public int? PageSize { get; set; }
        }

        public async Task<IActionResult> Create(int? patientId = null)        {
            var viewModel = new SessionViewModel
            {
                FechaHora = DateTime.Now,
                PacienteId = patientId ?? 0
            };

            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> ByPatient(int patientId)
        {
            if (patientId <= 0)
                return Json(Array.Empty<object>());

            var treatments = await _treatmentService.GetByPatientForSelectAsync(patientId);
            var result = treatments
                .Select(t => new { id = t.Id, descripcion = t.Descripcion })
                .ToList();

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SessionViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var session = viewModel.ToEntity();
                await _sessionService.CreateAsync(session);
                TempData["Success"] = "Sesion registrada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? string.Empty : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }
        }

        // GET: /Sessions/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var session = await _sessionService.GetByIdAsync(id);
            if (session is null)
                return NotFound();

            var viewModel = SessionViewModel.FromEntity(session);
            await LoadSelectListsAsync(viewModel);
            return View(viewModel);
        }

        // POST: /Sessions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SessionViewModel viewModel)
        {
            if (id != viewModel.Id)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var session = viewModel.ToEntity();
                await _sessionService.UpdateAsync(session);
                TempData["Success"] = "Sesion actualizada correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessValidationException ex)
            {
                var key = string.IsNullOrWhiteSpace(ex.PropertyName) ? string.Empty : ex.PropertyName;
                ModelState.AddModelError(key, ex.Message);
                await LoadSelectListsAsync(viewModel);
                return View(viewModel);
            }
        }

        // GET: /Sessions/Details/5
        // Detalle para profesionales: incluye Evolution.
        public async Task<IActionResult> Details(int id)
        {
            var session = await _sessionService.GetByIdAsync(id);
            if (session is null)
                return NotFound();

            return View(SessionViewModel.FromEntity(session));
        }

        // GET: /Sessions/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var session = await _sessionService.GetByIdAsync(id);
            if (session is null)
                return NotFound();

            return View(SessionViewModel.FromEntityForAdmin(session));
        }

        // POST: /Sessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _sessionService.DeleteAsync(id);
            TempData["Success"] = "Sesion eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSelectListsAsync(SessionViewModel viewModel)
        {
            // Se ejecuta de forma secuencial para evitar operaciones concurrentes
            // sobre el mismo DbContext scoped del request.
            var patients = await _patientService.GetForSelectAsync();
            var professionals = await _professionalService.GetForSelectAsync();
            var treatments = await _treatmentService.GetForSelectAsync();
            var offices = await _officeService.GetActiveAsync();

            // El repositorio ya ordena por Apellido — no se necesita OrderBy en memoria
            viewModel.Pacientes = patients
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Apellido}, {p.Nombre} - DNI {p.DNI}"
                })
                .ToList();

            viewModel.Profesionales = professionals
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Apellido}, {p.Nombre} - Matricula {p.Matricula}"
                })
                .ToList();

            viewModel.Tratamientos = treatments
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Descripcion
                })
                .ToList();

            viewModel.Consultorios = offices
                .Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = o.Name
                })
                .ToList();

            viewModel.EstadosSesion = Enum.GetValues<SessionStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s switch
                    {
                        SessionStatus.Pending => "Pendiente",
                        SessionStatus.Completed => "Completada",
                        SessionStatus.Canceled => "Cancelada",
                        _ => s.ToString()
                    }
                })
                .ToList();

            viewModel.EstadosPago = Enum.GetValues<PaymentStatus>()
                .Select(p => new SelectListItem
                {
                    Value = p.ToString(),
                    Text = p == PaymentStatus.Paid ? "Pagada" : "Pendiente"
                })
                .ToList();
        }
    }
}
