using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;
using Sistema_Classificacao_Seca.Services;

namespace Sistema_Classificacao_Seca.Controllers;

public sealed record DroughtDashboard(DroughtInput Input, DroughtEvaluation? Evaluation,
    IReadOnlyList<DroughtCount> Counts, IReadOnlyList<MoisturePoint> Moisture,
    IReadOnlyList<DroughtForecast> Forecasts, DroughtPrediction? Prediction, DroughtForecast? Forecast);
public sealed record MoisturePoint(string Region, DateTime Date, double Sentinel1, double Ascat);
public sealed record DroughtCount(string Region, string Label, int Count);
public sealed record ForecastRequest(DroughtInput Input, int HorizonDays = 30);

public sealed class DroughtController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public Task<IActionResult> Index(CancellationToken ct) => Dashboard(new DroughtInput(), false, ct);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Index([Bind(Prefix = "Input")] DroughtInput input, CancellationToken ct) =>
        Dashboard(input, ModelState.IsValid, ct);

    [HttpPost("/api/forecast")]
    public async Task<IActionResult> Forecast([FromBody] ForecastRequest? request, CancellationToken ct)
    {
        if (request is null || request.Input is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(ForecastRequest.Input)] = ["Forneça os dados para a previsão."]
            }));

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        using var classifier = await TrainAsync(ct);
        if (classifier is null)
            return Problem("Dados insuficientes para treinar o modelo de risco.", statusCode: 503);

        var horizon = request.HorizonDays <= 0 ? 30 : request.HorizonDays;
        return Ok(classifier.Forecast(request.Input, horizon));
    }

    [HttpGet("/api/models/metrics")]
    public async Task<IActionResult> Evaluation(CancellationToken ct)
    {
        using var classifier = await TrainAsync(ct);
        return classifier is null
            ? Problem("Dados insuficientes para treinar e avaliar o modelo.", statusCode: 503)
            : Ok(classifier.Evaluation);
    }

    [HttpPost("/api/predictions")]
    public async Task<IActionResult> Predict([FromBody] DroughtInput? input, CancellationToken ct)
    {
        if (input is null)
            ModelState.AddModelError(nameof(input), "Forneça os dados da previsão.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        using var classifier = await TrainAsync(ct);
        return classifier is null
            ? Problem("Dados insuficientes para treinar o modelo.", statusCode: 503)
            : Ok(classifier.Predict(input!));
    }

    [HttpGet("/api/regions")]
    public async Task<IActionResult> Regions(CancellationToken ct) =>
        Ok(await db.Regions.AsNoTracking().OrderBy(region => region.Name)
            .Select(region => new { region.Id, region.Name, region.Province }).ToListAsync(ct));

    [HttpGet("/api/predictions")]
    public async Task<IActionResult> PredictPeriod(int regionId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        if (regionId <= 0 || from is null || to is null || from.Value > to.Value)
            return BadRequest("Indique uma região e um intervalo válido (from e to).");

        var start = from.Value.ToDateTime(TimeOnly.MinValue);
        var end = to.Value.ToDateTime(TimeOnly.MaxValue);
        var observations = await db.SoilObservations.AsNoTracking()
            .Where(row => row.RegionId == regionId && row.Date >= start && row.Date <= end)
            .OrderBy(row => row.Date).ThenBy(row => row.Point).ToListAsync(ct);
        if (observations.Count == 0)
            return NotFound();

        using var classifier = await TrainAsync(ct);
        if (classifier is null)
            return Problem("Dados insuficientes para treinar o modelo.", statusCode: 503);

        return Ok(observations.Select(row => new
        {
            row.RegionId, row.Date, row.Point, row.Latitude, row.Longitude,
            Prediction = classifier.Predict(DroughtInput.From(row))
        }).ToArray());
    }

    private async Task<IActionResult> Dashboard(DroughtInput input, bool predict, CancellationToken ct)
    {
        var counts = await db.SoilObservations.AsNoTracking()
            .GroupBy(row => new { row.Region.Name, row.DroughtClass })
            .OrderBy(group => group.Key.Name).ThenBy(group => group.Key.DroughtClass)
            .Select(group => new DroughtCount(group.Key.Name, group.Key.DroughtClass, group.Count()))
            .ToListAsync(ct);
        var moisture = await db.SoilObservations.AsNoTracking()
            .GroupBy(row => new { row.Region.Name, row.Date })
            .OrderBy(group => group.Key.Date)
            .Select(group => new MoisturePoint(group.Key.Name, group.Key.Date,
                group.Average(row => (double)row.Sentinel1Moisture) * 100,
                group.Average(row => (double)row.AscatMoisture)))
            .ToListAsync(ct);
        var recent = await db.SoilObservations.AsNoTracking()
            .OrderByDescending(row => row.Date)
            .Take(10)
            .ToListAsync(ct);

        using var classifier = await TrainAsync(ct);
        var forecastList = classifier is null
            ? []
            : recent.Select(row => classifier.Forecast(DroughtInput.From(row), 30)).ToList();
        var currentForecast = predict && classifier is not null ? classifier.Forecast(input, 30) : null;

        return View("Index", new DroughtDashboard(input, classifier?.Evaluation, counts, moisture,
            forecastList, predict ? classifier?.Predict(input) : null, currentForecast));
    }

    private async Task<DroughtClassifier?> TrainAsync(CancellationToken ct) =>
        DroughtClassifier.Train(await db.SoilObservations.AsNoTracking().ToListAsync(ct));
}
