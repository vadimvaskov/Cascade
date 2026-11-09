using System;
using Cascade.Prototype;
using UnityEditor;
using UnityEngine;

// Pure timing fixtures: no audio device, scene, saved settings or PlayerPrefs are changed.
public static class CascadeCalibrationChecks
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Cascade calibration check failed: " + message);
    }

    static RhythmCalibration Samples(double[] errors)
    {
        var calibration = new RhythmCalibration();
        for (int i = 0; i < errors.Length; i++)
            Require(calibration.AddTap(i + 2 + errors[i]), "distinct fixture beat " + (i + 2));
        return calibration;
    }

    static double[] Repeated(double error, int count = 10)
    {
        var values = new double[count];
        for (int i = 0; i < count; i++) values[i] = error;
        return values;
    }

    [MenuItem("Cascade/Run rhythm calibration checks")]
    public static string Run()
    {
        var late = Samples(Repeated(.150));
        Require(late.IsComplete && late.TryEstimate(out int offset, out string reason) && offset == 150 && reason == "",
            "late taps estimate +150ms with Rhythm's subtraction convention");
        var early = Samples(Repeated(-.080));
        Require(early.TryEstimate(out offset, out reason) && offset == -80, "early taps estimate -80ms");

        var jitter = Samples(new[] { .138, .162, .142, .158, .146, .154, .140, .160, .144, .156 });
        Require(jitter.TryEstimate(out offset, out reason) && offset == 150, "balanced natural jitter retains the median");
        var outlierErrors = Repeated(.150); outlierErrors[4] = -.370;
        var outlier = Samples(outlierErrors);
        Require(outlier.TryEstimate(out offset, out reason) && offset == 150, "one accepted extreme outlier cannot shift the estimate");
        Require(outlier.Count == 10 && outlier.TryEstimate(out int repeatedOffset, out reason) && repeatedOffset == offset,
            "estimation is repeatable and never consumes or applies samples");

        var limits = Samples(Repeated(.400));
        Require(limits.TryEstimate(out offset, out reason) && offset == 400, "inclusive +400ms limit");
        limits = Samples(Repeated(-.400));
        Require(limits.TryEstimate(out offset, out reason) && offset == -400, "inclusive -400ms limit");

        var unique = new RhythmCalibration();
        Require(!unique.AddTap(.150) && !unique.AddTap(1.150), "first two scheduled beats are warmup");
        Require(!unique.AddTap(2.401) && !unique.AddTap(1.599), "outside timing window rejected before consuming beat");
        Require(unique.AddTap(2.150) && !unique.AddTap(2.170) && unique.Count == 1, "a beat contributes at most one sample");
        Require(!unique.AddTap(double.NaN) && !unique.AddTap(double.PositiveInfinity) && !unique.AddTap(-1) &&
            !unique.AddTap(16.150) && unique.Count == 1, "invalid times and events after the sequence are ignored");

        var silence = new RhythmCalibration();
        Require(!silence.IsComplete && !silence.TryEstimate(out offset, out reason) && offset == 0 && reason.Length > 0,
            "silence cannot produce a correction");
        var insufficient = Samples(Repeated(.150, 9));
        Require(!insufficient.IsComplete && !insufficient.TryEstimate(out offset, out reason), "nine taps are insufficient");
        var unstable = Samples(new[] { -.320, .250, -.240, .340, -.150, .180, .310, -.290, .060, -.070 });
        Require(unstable.IsComplete && !unstable.TryEstimate(out offset, out reason) && offset == 0,
            "enough random-phase taps are still rejected for inconsistency");
        var driftingErrors = new double[10];
        for (int i = 0; i < driftingErrors.Length; i++) driftingErrors[i] = .050 + i * .012;
        var drifting = Samples(driftingErrors);
        Require(!drifting.TryEstimate(out offset, out reason), "steady tempo drift is not mistaken for a fixed delay");

        var interval = new RhythmCalibration(16, .8, 2);
        for (int i = 2; i < 12; i++) Require(interval.AddTap(i * .8 + .150), "custom interval tap");
        Require(interval.TryEstimate(out offset, out reason) && offset == 150, "configured interval drives beat matching");
        bool invalidConfiguration = false;
        try { new RhythmCalibration(8, 1, 2); } catch (ArgumentOutOfRangeException) { invalidConfiguration = true; }
        Require(invalidConfiguration, "sequence must offer at least ten non-warmup beats");

        const string result = "PASS: signed calibration, jitter/outlier robustness, unique beats, warmup, limits, invalid/insufficient taps, random rhythm, tempo drift and custom interval";
        Debug.Log(result);
        return result;
    }
}
