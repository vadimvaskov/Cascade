using System;
using System.Collections.Generic;
using System.Linq;
using Cascade.Prototype;
using UnityEditor;
using UnityEngine;

// Deterministic checks use artificial beat times to test rules, not to certify any music map.
public static class CascadeRhythmChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Cascade rhythm check failed: " + message);
    }
    static BeatMap TestMap()
    {
        return new BeatMap
        {
            trackId = "test-only", version = "synthetic-test", duration = 100,
            audioSha256 = new string('a', 64), verified = true,
            beats = Enumerable.Range(1, 90).Select(x => (double)x).ToArray()
        };
    }
    [MenuItem("Cascade/Run rhythm x10 checks")]
    public static string Run()
    {
        var passed = new List<string>();
        var settings = new Balance();
        var map = TestMap();
        var rhythm = new Rhythm(map, settings);
        for (int i = 1; i <= 44; i++)
        {
            var hit = rhythm.Evaluate(i, 0);
            int multiplier = Math.Min(10, 1 + i / 4);
            Check(hit.kind == "hit" && hit.multiplier == multiplier && rhythm.Multiplier == multiplier,
                "step " + i + " must give x" + multiplier);
        }
        Check(rhythm.Streak == 44 && rhythm.Best == 44 && rhythm.Hits == 44 && rhythm.UsedBeats == 44, "capped x10 preserves hit statistics");
        passed.Add("all x1-x10 thresholds and cap");

        var fixture = new int[36]; fixture[8] = fixture[13] = 4; fixture[14] = 3;
        var board = new Board(settings, 7, fixture);
        var turn = board.Place(7, rhythm.Multiplier);
        Check(turn.waves.Count == 2 && turn.destroyed == 3 && turn.points == 1600, "whole two-wave turn receives fixed x10");
        Check(rhythm.Hits == 44 && rhythm.Streak == 44, "cascade and auto sphere cannot award rhythm hits");
        passed.Add("one multiplier for whole cascade");

        var miss = rhythm.Evaluate(44.4, 0);
        Check(miss.kind == "miss" && miss.multiplier == 1 && rhythm.Streak == 0 && rhythm.Multiplier == 1 && rhythm.Best == 44, "miss resets live multiplier only");
        rhythm.Evaluate(45, 0);
        var repeat = rhythm.Evaluate(45.05, 0);
        Check(repeat.kind == "repeat" && repeat.multiplier == 1 && rhythm.Streak == 1 && rhythm.Hits == 45, "one reward per beat");
        rhythm.Advance(55);
        Check(rhythm.Streak == 0, "repeat does not refresh ten-second timeout");
        Check(rhythm.Evaluate(55, 0).multiplier == 1 && rhythm.Streak == 1, "hit exactly at timeout starts a new streak");
        passed.Add("miss, repeat, timeout and restart");

        // A render update has already crossed the timeout, but the queued touch occurred before it.
        var lateMap = TestMap(); lateMap.beats = new[] { 1.0, 2.0, 3.0, 12.99, 13.5 };
        rhythm = new Rhythm(lateMap, settings);
        rhythm.Evaluate(1, 0); rhythm.Evaluate(2, 0); rhythm.Evaluate(3, 0);
        rhythm.Advance(13.2);
        Check(rhythm.Streak == 0, "display times out independently");
        var delayed = rhythm.Evaluate(12.99, 0);
        Check(delayed.kind == "hit" && delayed.multiplier == 2 && rhythm.Streak == 4, "pre-timeout original event preserves x2 despite later render frame");
        int hitsBefore = rhythm.Hits;
        Check(rhythm.Evaluate(2.5, 0).kind == "stale" && rhythm.Hits == hitsBefore && rhythm.Streak == 4, "out-of-order evaluation cannot corrupt state");
        rhythm.Advance(2);
        Check(rhythm.Streak == 4, "presentation clock cannot regress");
        passed.Add("original event time versus display time");

        Check(new Rhythm(map, settings).Evaluate(.88, 0).kind == "hit", "left inclusive boundary");
        Check(new Rhythm(map, settings).Evaluate(1.12, 0).kind == "hit", "right inclusive boundary");
        Check(new Rhythm(map, settings).Evaluate(.879, 0).kind == "miss", "left outside window");
        Check(new Rhythm(map, settings).Evaluate(1.121, 0).kind == "miss", "right outside window");
        var dense = TestMap(); dense.beats = new[] { .1, .3 };
        Check(new Rhythm(dense, settings).Evaluate(.19, 0).kind == "hit", "45 percent capped boundary");
        Check(new Rhythm(dense, settings).Evaluate(.191, 0).kind == "miss", "beyond capped boundary");
        Check(new Rhythm(dense, settings).Evaluate(.2, 0).beat == 0, "equal distance chooses earlier beat");
        var calibrated = new Rhythm(map, settings).Evaluate(10.08, .08);
        Check(calibrated.kind == "hit" && Math.Abs(calibrated.error) < 1e-8, "positive calibration compensates late touch");
        Check(new Rhythm(map, settings).Evaluate(map.duration, .2).kind == "invalid", "offset never extends input beyond song end");
        passed.Add("window boundaries, tie break and offset");

        var excluded = TestMap(); excluded.beats = new[] { 1.0, 20.0, 21.0, 30.0 };
        excluded.excluded = new[] { new Exclusion { start = 2, end = 20 } };
        rhythm = new Rhythm(excluded, settings); rhythm.Evaluate(1, 0);
        Check(rhythm.Evaluate(2, 0).kind == "excluded", "excluded interval begins inclusively");
        Check(rhythm.Evaluate(10, 0).multiplier == 1 && rhythm.Streak == 1, "excluded moves neither award nor reset streak");
        rhythm.Advance(20);
        Check(rhythm.Streak == 1, "excluded time is not active timeout time");
        Check(rhythm.Evaluate(20, 0).kind == "hit" && rhythm.Streak == 2, "excluded interval ends exclusively");
        Check(rhythm.Evaluate(20.01, .1).kind == "excluded", "calibration cannot cross an excluded interval");
        passed.Add("excluded intervals and frozen timeout");

        map = TestMap();
        Check(map.Valid(100) && map.Valid(100, new string('a',64), false), "verified map with exact audio identity");
        Check(!map.Valid(100, new string('b',64), false), "audio hash mismatch rejected");
        Check(!map.Valid(101, map.audioSha256, false), "duration mismatch rejected");
        map.verified = false; map.analysisPassed = true; map.reviewStatus = BeatMap.ProvisionalReview; map.analysisMethod = "detected-onsets-v1";
        Check(map.IsProvisional && !map.Valid(100) && !map.Valid(100,map.audioSha256,false) && map.Valid(100,map.audioSha256,true), "provisional maps require explicit opt-in and remain unverified");
        map.analysisPassed = false;
        Check(!map.Valid(100,map.audioSha256,true), "missing analysis approval rejected");
        map.analysisPassed = true; map.reviewStatus = "unknown";
        Check(!map.Valid(100,map.audioSha256,true), "unknown review provenance rejected");
        map = TestMap(); map.beats = new[] { 1.0,1.0 };
        Check(!map.Valid(100), "duplicate beat timestamp rejected");
        map = TestMap(); map.beats = new[] { 1.0,double.NaN };
        Check(!map.Valid(100), "NaN beat rejected");
        map = TestMap(); map.excluded = new[] { new Exclusion { start=1,end=3 },new Exclusion { start=2,end=4 } };
        Check(!map.Valid(100), "overlapping exclusions rejected");
        map = TestMap(); map.audioSha256 = "not-a-sha256";
        Check(!map.Valid(100), "invalid audio checksum rejected");
        passed.Add("map integrity and honest analysis provenance");

        string result = "PASS: " + string.Join("; ", passed);
        Debug.Log(result);
        return result;
    }
}
