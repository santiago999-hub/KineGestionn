using System;
using System.Linq;
using System.Security.Claims;
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
            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

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
            var profIdClaim = User.FindFirstValue("ProfessionalId");
            if (!int.TryParse(profIdClaim, out var professionalId))
            {
                TempData["Error"] = "Tu usuario no está vinculado a ningún profesional. Solicitá al administrador que lo configure.";
                return RedirectToAction(nameof(Index));
            }

            if (page < 1) page = 1;
            if (pageSize is < 5 or > 50) pageSize = 10;

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
            // Proyecciones mínimas: solo los campos que necesita cada dropdown
            var patientsTask      = _patientService.GetForSelectAsync();
            var professionalsTask = _professionalService.GetForSelectAsync();
            var treatmentsTask    = _treatmentService.GetForSelectAsync();
            var officesTask       = _officeService.GetActiveAsync();
            await Task.WhenAll(patientsTask, professionalsTask, treatmentsTask, officesTask);
            var patients      = patientsTask.Result;
            var professionals = professionalsTask.Result;
            var treatments    = treatmentsTask.Result;
            var offices       = officesTask.Result;

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
