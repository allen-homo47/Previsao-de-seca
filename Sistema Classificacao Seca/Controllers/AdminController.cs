using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;
using Sistema_Classificacao_Seca.Services;

namespace Sistema_Classificacao_Seca.Controllers;

[Authorize(Roles = "Administrador")]
public class AdminController(
    ApplicationDbContext db,
    CsvImportService importer) : Controller
{
    private const int MaxCsvBytes = 2 * 1024 * 1024;

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxCsvBytes + 64 * 1024)]
    public async Task<IActionResult> Importar(IFormFile? ficheiro, CancellationToken ct)
    {
        if (ficheiro is null || ficheiro.Length == 0 || ficheiro.Length > MaxCsvBytes ||
            !string.Equals(Path.GetExtension(ficheiro.FileName), ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "Escolhe um ficheiro CSV com até 2 MB.");
            return View("Index");
        }

        try
        {
            using var reader = new StreamReader(ficheiro.OpenReadStream());
            var rows = await CsvImportService.ReadAsync(reader, ct);
            var inserted = await importer.ImportAsync(rows, ct);

            TempData["Mensagem"] =
                $"Validadas: {rows.Count}. Inseridas: {inserted}. Já existentes: {rows.Count - inserted}.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidDataException error)
        {
            ModelState.AddModelError("", error.Message);
        }
        catch (Exception error) when (error is CsvFormatException or KeyNotFoundException)
        {
            ModelState.AddModelError("", "O CSV tem colunas em falta ou valores num formato inválido.");
        }

        return View("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Treinar(CancellationToken ct)
    {
        var rows = await db.SoilObservations.AsNoTracking().ToListAsync(ct);
        using var classifier = DroughtClassifier.Train(rows);

        if (classifier is null)
        {
            ModelState.AddModelError("", "Dados insuficientes para treinar e avaliar os modelos.");
            return View("Index");
        }

        return View("Index", classifier.Evaluation);
    }
}