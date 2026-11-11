using System.ComponentModel.DataAnnotations;
using Sistema_Classificacao_Seca.Model;
using Microsoft.ML;

namespace Sistema_Classificacao_Seca.Services;

public sealed record DroughtInput
{
    [Required, Range(0, 1)]
    public double? Sentinel1Moisture { get; init; }

    [Required, Range(0, 100)]
    public double? AscatMoisture { get; init; }

    [Required, Range(1, 12)]
    public int? Month { get; init; }

    [Required, Range(0, 100)]
    public float? Clay { get; init; }

    [Required, Range(0, 100)]
    public float? Sand { get; init; }

    [Required, Range(0, 100)]
    public float? Silt { get; init; }

    [Required, Range(0, float.MaxValue)]
    public float? OrganicCarbon { get; init; }

    [Required, Range(0.001, 10)]
    public float? BulkDensity { get; init; }

    internal static DroughtInput From(SoilObservation row) => new()
    {
        Sentinel1Moisture = row.Sentinel1Moisture,
        AscatMoisture = row.AscatMoisture,
        Month = row.Date.Month,
        Clay = row.Clay, Sand = row.Sand, Silt = row.Silt,
        OrganicCarbon = row.OrganicCarbon, BulkDensity = row.BulkDensity
    };
}

public sealed record ClassMetrics(double? Precision, double? Recall, double? F1, int Support);
public sealed record ModelMetrics(string Algorithm, double Accuracy, double? MacroF1,
    Dictionary<string, ClassMetrics> Classes, Dictionary<string, Dictionary<string, int>> ConfusionMatrix);
public sealed record DroughtEvaluation(int TrainingCount, int TestCount, DateTime TestFrom,
    string PrimaryAlgorithm, double MajorityBaselineAccuracy, IReadOnlyList<ModelMetrics> Models);
public sealed record DroughtPrediction(string DroughtClass, float Confidence, string Algorithm, DroughtInput Inputs);

public sealed class DroughtClassifier : IDisposable
{
    public const string PrimaryAlgorithm = "SDCA Maximum Entropy";
    private const string ComparisonAlgorithm = "L-BFGS Maximum Entropy";
    private static readonly string[] Labels = ["Extrema", "Severa", "Moderada", "Normal"];
    private readonly PredictionEngine<TrainingRow, Output> engine;
    public DroughtEvaluation Evaluation { get; }

    private DroughtClassifier(PredictionEngine<TrainingRow, Output> engine, DroughtEvaluation evaluation)
    {
        this.engine = engine;
        Evaluation = evaluation;
    }

    public static DroughtClassifier? Train(IReadOnlyList<SoilObservation> rows)
    {
        var dates = rows.Select(row => row.Date.Date).Distinct().Order().ToArray();
        if (dates.Length < 2)
            return null;

        var cutoff = dates[Math.Clamp((int)(dates.Length * 0.8), 1, dates.Length - 1)];
        var train = rows.Where(row => row.Date < cutoff && Labels.Contains(row.DroughtClass)).ToArray();
        var test = rows.Where(row => row.Date >= cutoff && Labels.Contains(row.DroughtClass)).ToArray();
        if (test.Length == 0 || Labels.Any(label => train.Count(row => row.DroughtClass == label) < 2))
            return null;

        var ml = new MLContext(seed: 42);
        var trainingData = ml.Data.LoadFromEnumerable(train.OrderBy(row => row.Date)
            .ThenBy(row => row.Point, StringComparer.Ordinal)
            .Select(row => TrainingRow.From(DroughtInput.From(row), row.DroughtClass)));
        var prepare = ml.Transforms.Conversion.MapValueToKey("Label", nameof(TrainingRow.DroughtClass))
            .Append(ml.Transforms.Concatenate("Features", nameof(TrainingRow.Sentinel1),
                nameof(TrainingRow.Ascat), nameof(TrainingRow.MonthSin), nameof(TrainingRow.MonthCos),
                nameof(TrainingRow.Clay), nameof(TrainingRow.Sand), nameof(TrainingRow.Silt),
                nameof(TrainingRow.OrganicCarbon), nameof(TrainingRow.BulkDensity)))
            .Append(ml.Transforms.NormalizeMeanVariance("Features"));
        var primary = prepare.Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy())
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel")).Fit(trainingData);
        var comparison = prepare.Append(ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy())
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel")).Fit(trainingData);
        var primaryEngine = ml.Model.CreatePredictionEngine<TrainingRow, Output>(primary);
        using var comparisonEngine = ml.Model.CreatePredictionEngine<TrainingRow, Output>(comparison);

        var majority = train.GroupBy(row => row.DroughtClass).OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal).First().Key;
        var evaluation = new DroughtEvaluation(train.Length, test.Length, cutoff, PrimaryAlgorithm,
            test.Count(row => row.DroughtClass == majority) / (double)test.Length,
            [Evaluate(PrimaryAlgorithm, primaryEngine, test), Evaluate(ComparisonAlgorithm, comparisonEngine, test)]);
        return new DroughtClassifier(primaryEngine, evaluation);
    }

    public void Dispose() => engine.Dispose();

    public DroughtPrediction Predict(DroughtInput input)
    {
        var output = engine.Predict(TrainingRow.From(input));
        return new DroughtPrediction(output.PredictedLabel, output.Score.Max(), PrimaryAlgorithm, input);
    }

    private static ModelMetrics Evaluate(string algorithm, PredictionEngine<TrainingRow, Output> model,
        SoilObservation[] test)
    {
        var matrix = Labels.ToDictionary(actual => actual,
            _ => Labels.ToDictionary(predicted => predicted, _ => 0));
        foreach (var row in test)
        {
            var prediction = model.Predict(TrainingRow.From(DroughtInput.From(row)));
            matrix[row.DroughtClass][prediction.PredictedLabel]++;
        }

        var classes = Labels.ToDictionary(label => label, label =>
        {
            var correct = matrix[label][label];
            var actual = matrix[label].Values.Sum();
            var predicted = matrix.Values.Sum(row => row[label]);
            return new ClassMetrics(predicted == 0 ? null : (double)correct / predicted,
                actual == 0 ? null : (double)correct / actual,
                actual == 0 ? null : 2.0 * correct / (actual + predicted), actual);
        });
        return new ModelMetrics(algorithm, Labels.Sum(label => matrix[label][label]) / (double)test.Length,
            classes.Values.All(metrics => metrics.F1.HasValue) ? classes.Values.Average(metrics => metrics.F1) : null,
            classes, matrix);
    }

    public sealed class TrainingRow
    {
        public string DroughtClass { get; set; } = "";
        public float Sentinel1 { get; set; }
        public float Ascat { get; set; }
        public float MonthSin { get; set; }
        public float MonthCos { get; set; }
        public float Clay { get; set; }
        public float Sand { get; set; }
        public float Silt { get; set; }
        public float OrganicCarbon { get; set; }
        public float BulkDensity { get; set; }

        public static TrainingRow From(DroughtInput input, string label = "")
        {
            var angle = 2 * Math.PI * (input.Month!.Value - 1) / 12;
            return new TrainingRow
            {
                DroughtClass = label, Sentinel1 = (float)input.Sentinel1Moisture!.Value,
                Ascat = (float)input.AscatMoisture!.Value, MonthSin = (float)Math.Sin(angle),
                MonthCos = (float)Math.Cos(angle), Clay = input.Clay!.Value, Sand = input.Sand!.Value,
                Silt = input.Silt!.Value, OrganicCarbon = input.OrganicCarbon!.Value,
                BulkDensity = input.BulkDensity!.Value
            };
        }
    }

    public sealed class Output
    {
        public string PredictedLabel { get; set; } = "";
        public float[] Score { get; set; } = [];
    }
}
