using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Cascade.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

// These checks isolate recovery state from audible beat accuracy. Hits at map timestamps
// prove that the model can recover; they do not approve the provisional music map.
// No scene, PlayerPrefs, AudioSource or production resource is modified.
public static class CascadeRecoveryChecks
{
    // Independent raw-WAV high-frequency attacks, not values read from the map under test.
    // WAV SHA256: 38910171809015c75e7a591472397d724ad8f3c2e308dbe12eb48abe4f2fc7c2.
    // FFmpeg highpass=1500 -> 12 kHz mono -> positive 10 ms RMS differences -> local maxima.
    // Analysis provenance: Builds/Audio/DarkPulseCandidate/report.json.
    static readonly double[] AudioPulses =
    {
        20.375,20.715,21.055,21.395,21.735,22.075,22.415,22.755,
        23.105,23.445,23.775,24.125,24.465,24.805,25.145,25.485,
        25.825,26.165,26.505,26.845,27.195,27.535,27.875,28.215
    };

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Cascade recovery check failed: " + message);
    }

    static BeatMap Synthetic()
    {
        return new BeatMap
        {
            trackId = "recovery-test-only", version = "synthetic",
            duration = 100, audioSha256 = new string('a', 64),
            beats = Enumerable.Range(1, 90).Select(i => (double)i).ToArray()
        };
    }

    static void SyntheticCases(Balance settings)
    {
        var map = Synthetic();
        var rhythm = new Rhythm(map, settings);
        for (int beat = 1; beat <= 4; beat++)
            Check(rhythm.Evaluate(beat, 0).kind == "hit", "initial hit " + beat);
        Check(rhythm.Multiplier == 2, "initial series reaches x2");
        Check(rhythm.Evaluate(4.4, 0).kind == "miss" && rhythm.Streak == 0 && rhythm.UsedBeats == 4,
            "a miss clears the series without consuming another beat");
        Check(rhythm.Evaluate(5, 0).kind == "hit" && rhythm.Streak == 1 && rhythm.Multiplier == 1,
            "the next beat immediately starts a new series after a miss");
        Check(rhythm.Evaluate(5.02, 0).kind == "repeat" && rhythm.Streak == 1,
            "only the already rewarded beat remains unavailable");
        for (int beat = 6; beat <= 8; beat++) rhythm.Evaluate(beat, 0);
        Check(rhythm.Streak == 4 && rhythm.Multiplier == 2 && rhythm.Best == 4,
            "four recovered hits restore x2 without a restart");

        rhythm = new Rhythm(map, settings);
        Check(rhythm.Evaluate(.879, 0).kind == "miss" && rhythm.UsedBeats == 0,
            "an early miss does not reserve its nearest future beat");
        Check(rhythm.Evaluate(1, 0).kind == "hit" && rhythm.UsedBeats == 1,
            "the same nearest beat remains rewardable after the early miss");
        for (int beat = 1; beat < 60; beat++)
            Check(rhythm.Evaluate(beat + .4, 0).kind == "miss", "consecutive miss " + beat);
        Check(rhythm.Evaluate(60, 0).kind == "hit" && rhythm.Streak == 1 && rhythm.UsedBeats == 2,
            "a long run of misses cannot lock all subsequent beats");

        rhythm = new Rhythm(map, settings);
        rhythm.Evaluate(1, 0); rhythm.Evaluate(1.4, 0);
        Check(rhythm.Evaluate(1.3, 0).kind == "stale", "old input is rejected independently of the miss");
        rhythm.Advance(2.35);
        Check(rhythm.Evaluate(2, 0).kind == "hit" && rhythm.Streak == 1,
            "a delayed original event recovers after a miss despite a newer presentation clock");
        rhythm.Advance(20);
        Check(rhythm.Streak == 0 && rhythm.Evaluate(20, 0).kind == "hit" && rhythm.Streak == 1,
            "expired presentation state can start a new series");

        var excluded = Synthetic();
        excluded.excluded = new[] { new Exclusion { start = 1.5, end = 2 } };
        rhythm = new Rhythm(excluded, settings);
        Check(rhythm.Evaluate(1.142, .142).kind == "hit", "calibrated hit before exclusion");
        Check(rhythm.Evaluate(1.6, .142).kind == "excluded", "raw timestamp inside exclusion is not scored");
        Check(rhythm.Evaluate(2.1, .142).kind == "excluded", "corrected timestamp inside exclusion is not scored");
        Check(rhythm.Evaluate(2.142, .142).kind == "hit" && rhythm.Streak == 2,
            "positive offset releases the exclusion at its corrected end boundary");
        Check(rhythm.Evaluate(2.542, .142).kind == "miss", "a real miss after the exclusion resets the series");
        Check(rhythm.Evaluate(3.142, .142).kind == "hit" && rhythm.Streak == 1,
            "a later calibrated beat recovers after exclusion plus miss");
        rhythm = new Rhythm(excluded, settings);
        Check(rhythm.Evaluate(1.858, -.142).kind == "excluded", "negative offset cannot bypass a raw exclusion");
        Check(rhythm.Evaluate(2.858, -.142).kind == "hit", "negative offset also recovers beyond the exclusion");
    }

    static bool Eligible(BeatMap map, double beat, double offset)
    {
        double raw = beat + offset;
        return raw >= 0 && raw < map.duration && !map.IsExcluded(beat) && !map.IsExcluded(raw);
    }

    static string RealMapCases(BeatMap map, Balance settings, double offset)
    {
        var eligible = map.beats.Where(beat => Eligible(map, beat, offset)).ToArray();
        Check(eligible.Length > 100, "Dark has enough testable beats for offset " + offset);
        int recoveries = 0;
        for (int i = 0; i + 1 < eligible.Length; i++)
        {
            double first = eligible[i], next = eligible[i + 1], miss = double.NaN;
            // Find a genuine offbeat, outside both exclusion clocks, before the recovery beat.
            for (double candidate = first + settings.rhythmWindow + .01;
                candidate < next - settings.rhythmWindow - .01; candidate += .025)
            {
                if (new Rhythm(map, settings).Evaluate(candidate + offset, offset).kind != "miss") continue;
                miss = candidate;
                break;
            }
            if (double.IsNaN(miss)) continue;
            var rhythm = new Rhythm(map, settings);
            Check(rhythm.Evaluate(first + offset, offset).kind == "hit", "real map setup beat " + first);
            Check(rhythm.Evaluate(miss + offset, offset).kind == "miss" && rhythm.Streak == 0 && rhythm.UsedBeats == 1,
                "real map miss does not reserve the following beat at " + next);
            rhythm.Advance(next + offset + .05);
            var recovered = rhythm.Evaluate(next + offset, offset);
            Check(recovered.kind == "hit" && recovered.multiplier == 1 && rhythm.Streak == 1 &&
                rhythm.Hits == 2 && rhythm.Misses == 1 && rhythm.UsedBeats == 2,
                "real map hit/miss/hit recovery at " + next + " with offset " + offset);
            recoveries++;
        }
        Check(recoveries > 100, "tested more than 100 independent recovery locations for offset " + offset);

        int exclusionExits = 0;
        foreach (var exclusion in map.excluded)
        {
            var before = eligible.Where(beat => beat + offset < exclusion.start).ToArray();
            var after = eligible.Where(beat => beat >= exclusion.end && beat + offset >= exclusion.end).ToArray();
            if (before.Length == 0 || after.Length == 0) continue;
            double rawExcluded = (exclusion.start + exclusion.end) * .5;
            var rhythm = new Rhythm(map, settings);
            Check(rhythm.Evaluate(before[before.Length - 1] + offset, offset).kind == "hit", "pre-exclusion hit");
            Check(rhythm.Evaluate(rawExcluded, offset).kind == "excluded", "real excluded interval " + exclusion.start);
            Check(rhythm.Evaluate(after[0] + offset, offset).kind == "hit" && rhythm.Streak > 0,
                "real exclusion releases at " + exclusion.end + " with offset " + offset);
            exclusionExits++;
        }
        // A reviewed replacement may need only leading/trailing exclusions. SyntheticCases
        // still verifies internal boundaries; report zero exits honestly for such a map.
        return (offset * 1000).ToString("0") + " ms: " + recoveries + " recoveries, " + exclusionExits + " exclusion exits";
    }

    [MenuItem("Cascade/Run Dark rhythm recovery checks")]
    public static string Run()
    {
        var balanceAsset = Resources.Load<TextAsset>("Data/balance");
        var mapAsset = Resources.Load<TextAsset>("Data/Dark Bass Drop");
        Check(balanceAsset != null && mapAsset != null, "runtime balance and Dark map resources exist");
        var map = JsonUtility.FromJson<BeatMap>(mapAsset.text);
        return RunMap(map);
    }

    public static string RunMap(BeatMap map)
    {
        var balanceAsset = Resources.Load<TextAsset>("Data/balance");
        Check(balanceAsset != null && map != null, "balance and supplied map exist");
        var settings = JsonUtility.FromJson<Balance>(balanceAsset.text);
        Check(map.Valid(map.duration, map.audioSha256, true), "real map has valid structure and explicit provisional provenance");
        SyntheticCases(settings);
        var summaries = new List<string>();
        foreach (double offset in new[] { 0d, .142, -.080 }) summaries.Add(RealMapCases(map, settings, offset));
        string result = "PASS: miss/repeat/stale/timeout recovery and calibrated exclusion boundaries; Dark " +
            string.Join("; ", summaries) + ". This checks model recovery, not audible map accuracy or device input.";
        Debug.Log(result);
        return result;
    }

    static double[] Cadence(int parity)
    {
        var times = new List<double>();
        for (int i = 0; i < 4; i++) times.Add(AudioPulses[parity + i * 2]);
        times.Add((AudioPulses[parity + 7] + AudioPulses[parity + 8]) * .5);
        for (int i = 0; i < 4; i++) times.Add(AudioPulses[parity + 9 + i * 2]);
        return times.ToArray();
    }

    [MenuItem("Cascade/Run independent Dark audio cadence regression")]
    public static string RunAudioCadence()
    {
        var map = JsonUtility.FromJson<BeatMap>(Resources.Load<TextAsset>("Data/Dark Bass Drop").text);
        return RunAudioCadenceMap(map);
    }

    public static string RunAudioCadenceMap(BeatMap map)
    {
        var settings = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("Data/balance").text);
        // Start with the parity admitted by the old half-time map. That baseline should
        // demonstrate four hits, an offpulse miss, then failure on the opposite parity.
        foreach (int parity in new[] { 1, 0 })
            foreach (double offset in new[] { 0d, .142 })
            {
                var rhythm = new Rhythm(map, settings);
                var times = Cadence(parity);
                for (int i = 0; i < times.Length; i++)
                {
                    if (i > 0) Check(times[i] - times[i - 1] > settings.presentationSeconds + .02,
                        "independent cadence respects the existing reaction interval");
                    string expected = i == 4 ? "miss" : "hit";
                    var result = rhythm.Evaluate(times[i] + offset, offset);
                    Check(result.kind == expected,
                        "independent PCM pulse parity " + parity + ", step " + i + ", time " + times[i] +
                        ", offset " + offset + ": expected " + expected + ", got " + result.kind +
                        ", error " + result.error);
                    Check(rhythm.Streak == (i < 4 ? i + 1 : i == 4 ? 0 : i - 4),
                        "independent cadence restores the series after changing pulse parity");
                    if (i == 4) Check(rhythm.UsedBeats == 4, "offpulse miss cannot consume the next audio pulse");
                }
                Check(rhythm.Hits == 8 && rhythm.Misses == 1 && rhythm.Multiplier == 2,
                    "both audio pulse parities produce eight hits and recover x2");
            }
        const string summary = "PASS: independent Dark PCM pulses, both half-time parities, offpulse miss and recovery at offsets 0/+142 ms; all accepted taps respect the 350 ms reaction gate. This is not a device playback test.";
        Debug.Log(summary);
        return summary;
    }

    [Serializable]
    sealed class ReplayReport
    {
        public string track, journal, map, recordedMapVersion, replayMapVersion;
        public bool recordedFieldsVerified;
        public int accepted, rejected, hits, misses, excluded, repeats, score, recordedScore;
        public int maximumStreak, maximumMultiplier, recoveriesAfterMiss, changedRhythmKinds;
        public double medianSignedErrorMs;
        public string[] changes;
        public string scope;
    }

    static string ProjectPath(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Application.dataPath,"..",path));

    // With verifyRecorded=true, independently reconstruct the original Board/Rhythm outputs.
    // With false, keep the same inputs/seed/board and report a counterfactual map replay.
    // Neither mode claims that previously unrecorded or reaction-blocked inputs were tested.
    public static string ReplayJournal(string journalPath, string mapPath, bool verifyRecorded)
    {
        var attempt=JsonUtility.FromJson<CascadeGame.AttemptLog>(File.ReadAllText(ProjectPath(journalPath)));
        var map=JsonUtility.FromJson<BeatMap>(File.ReadAllText(ProjectPath(mapPath)));
        var settings=JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("Data/balance").text);
        Check(attempt!=null&&attempt.moves!=null,"device journal has moves");
        Check(attempt.config==settings.version,"journal balance version matches the model settings");
        Check(map.Valid(map.duration,map.audioSha256,true),"replay map structure and provisional provenance are valid");
        if(verifyRecorded)Check(attempt.mapVersion==map.version,"strict replay uses the recorded map version");
        var board=new Board(settings,attempt.seed);
        var rhythm=new Rhythm(map,settings);
        var report=new ReplayReport
        {
            track=attempt.track,journal=journalPath,map=mapPath,
            recordedMapVersion=attempt.mapVersion,replayMapVersion=map.version,
            recordedFieldsVerified=verifyRecorded,recordedScore=attempt.score,
            scope=verifyRecorded?"Model replay of recorded device inputs; no new device run or unrecorded input coverage.":
                "Counterfactual model replay using identical recorded inputs and seeded Board; not a new device result."
        };
        var changes=new List<string>();var errors=new List<double>();bool waitingForRecovery=false;
        for(int i=0;i<attempt.moves.Length;i++)
        {
            var row=attempt.moves[i];
            if(!string.IsNullOrEmpty(row.rejection))
            {
                Check(row.rejection=="occupied","recognized rejection at record "+i);
                Check(row.cell<0||row.cell>=board.cells.Length||board.cells[row.cell]!=0,
                    "rejected cell really is occupied/outside the board at record "+i);
                Check(row.points==0&&string.IsNullOrEmpty(row.rhythm),"rejected tap contributes no score or rhythm assessment");
                report.rejected++;continue;
            }
            var assessment=rhythm.Evaluate(row.time,row.offsetMs/1000d);
            Check(assessment.kind!="invalid"&&assessment.kind!="stale","accepted input remains valid at record "+i);
            var turn=board.Place(row.cell,assessment.multiplier);
            Check(turn!=null,"recorded accepted placement reaches a free cell at record "+i);
            // Board state and cascades must remain identical even when the rhythm map changes.
            Check(turn.waves.Count==row.waves&&board.Occupied==row.occupied,
                "seeded board, cascade waves and occupied count match at record "+i);
            Check(row.multiplier>0&&row.points%row.multiplier==0&&
                turn.points/assessment.multiplier==row.points/row.multiplier,
                "base placement/explosion points remain unchanged at record "+i);
            report.accepted++;report.score+=turn.points;
            report.maximumMultiplier=Math.Max(report.maximumMultiplier,rhythm.Multiplier);
            double error=double.IsNaN(assessment.error)?0:assessment.error;
            if(assessment.kind=="hit")
            {
                report.hits++;
                if(waitingForRecovery){report.recoveriesAfterMiss++;waitingForRecovery=false;}
            }
            else if(assessment.kind=="miss"){report.misses++;waitingForRecovery=true;}
            else if(assessment.kind=="excluded")report.excluded++;
            else if(assessment.kind=="repeat")report.repeats++;
            else Check(false,"unexpected rhythm result "+assessment.kind);
            if(assessment.kind=="hit"||assessment.kind=="miss"||assessment.kind=="repeat")errors.Add(error*1000);
            if(assessment.kind!=row.rhythm)
            {
                report.changedRhythmKinds++;
                changes.Add("record "+i+", t="+row.time.ToString("F6")+": "+row.rhythm+" -> "+assessment.kind+", errorMs="+(error*1000).ToString("F3"));
            }
            if(verifyRecorded)
                Check(assessment.kind==row.rhythm&&Math.Abs(error-row.error)<1e-7&&
                    rhythm.Streak==row.streak&&assessment.multiplier==row.multiplier&&turn.points==row.points,
                    "exact kind/error/streak/multiplier/points match at record "+i+" (t="+row.time+")");
        }
        report.maximumStreak=rhythm.Best;
        errors.Sort();report.medianSignedErrorMs=errors.Count==0?0:
            (errors[(errors.Count-1)/2]+errors[errors.Count/2])*.5;
        report.changes=changes.ToArray();
        if(verifyRecorded)Check(report.score==attempt.score,"strict replay total score equals the device journal");
        string result=JsonUtility.ToJson(report,true);Debug.Log(result);return result;
    }

    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Read<T>(CascadeGame game, string field) => (T)typeof(CascadeGame).GetField(field, PrivateInstance).GetValue(game);
    static void Write(CascadeGame game, string field, object value) => typeof(CascadeGame).GetField(field, PrivateInstance).SetValue(game, value);
    static void Call(CascadeGame game, string method, params object[] args) => typeof(CascadeGame).GetMethod(method, PrivateInstance).Invoke(game, args);

    static void SetClock(CascadeGame game, double position)
    {
        double dsp = AudioSettings.dspTime;
        Write(game,"startDsp",dsp-position); Write(game,"scheduledDsp",dsp-1);
        Write(game,"dspAnchor",dsp); Write(game,"eventAnchor",InputState.currentTime);
    }

    static void RuntimeFixture(CascadeGame game, double position, int offset)
    {
        game.CloseCalibration(); Call(game,"ShowMenu"); game.StartLevel(2);
        Check(game.State == CascadeGame.Phase.Countdown, "real Dark level prepares before fixture injection");
        Read<AudioSource>(game,"music").Stop();
        var settings = Read<Balance>(game,"balance");
        var cells = new int[settings.size * settings.size];
        Write(game,"<Model>k__BackingField",new Board(settings,731,cells));
        Write(game,"rhythm",new Rhythm(Read<BeatMap>(game,"beatMap"),settings));
        Write(game,"score",0); Write(game,"moves",0); Write(game,"pending",null);
        Write(game,"lastAcceptedTime",-1d); Write(game,"inputOpenTime",0d);
        Write(game,"offsetMs",offset); Write(game,"effectClock",0d); Write(game,"reactionAge",0f);
        Write(game,"lightStrength",0f); Write(game,"fadeRemaining",0f);
        Write(game,"<State>k__BackingField",CascadeGame.Phase.Playing);
        Read<GameObject>(game,"modal").SetActive(false);
        Read<UnityEngine.UI.Text>(game,"countdownText").text="";
        Read<HashSet<int>>(game,"downTouches").Clear(); Write(game,"pointerTaken",false);
        SetClock(game,position); Call(game,"Render",cells); Call(game,"RefreshHud"); Call(game,"Update");
        Canvas.ForceUpdateCanvases(); Physics.SyncTransforms();
    }

    [StructLayout(LayoutKind.Explicit, Size=36)]
    struct NativePrefix : IInputStateTypeInfo
    {
        [FieldOffset(0)] public int touchId;
        [FieldOffset(4)] public Vector2 position;
        [FieldOffset(12)] public Vector2 delta;
        [FieldOffset(20)] public float pressure;
        [FieldOffset(24)] public Vector2 radius;
        [FieldOffset(32)] public byte phase;
        [FieldOffset(33)] public byte tapCount;
        [FieldOffset(34)] public byte displayIndex;
        [FieldOffset(35)] public byte flags;
        public FourCC format => TouchState.Format;
    }

    static void NativeTap(CascadeGame game, Touchscreen device, int cell, double songTime, int id)
    {
        SetClock(game,songTime);
        double eventTime=Read<double>(game,"eventAnchor");
        var view=Read<CascadeCell[]>(game,"views")[cell];
        Vector2 point=Read<Camera>(game,"gameCamera").WorldToScreenPoint(view.transform.position);
        InputSystem.QueueStateEvent(device,new NativePrefix {touchId=id,position=point,pressure=1,phase=(byte)UnityEngine.InputSystem.TouchPhase.Began},eventTime);
        InputSystem.QueueStateEvent(device,new NativePrefix {touchId=id,position=point,pressure=0,phase=(byte)UnityEngine.InputSystem.TouchPhase.Ended},eventTime+.001);
        InputSystem.Update();
    }

    static void FinishRuntimeReaction(CascadeGame game, double originalTime)
    {
        Check(game.State==CascadeGame.Phase.Reaction,"accepted native tap enters reaction");
        var settings=Read<Balance>(game,"balance");
        SetClock(game,originalTime+settings.presentationSeconds+.015);
        Write(game,"reactionAge",settings.presentationSeconds+.001f); Call(game,"Update");
        Check(game.State==CascadeGame.Phase.Playing,"the finite reaction reopens input without a restart");
    }

    [MenuItem("Cascade/Run Dark native cadence recovery (Play Mode)")]
    public static string RunRuntimeCadence()
    {
        Check(Application.isPlaying,"start Play Mode before native cadence checks");
        var game=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();
        Check(game!=null,"CascadeGame exists");
        var source=Read<AudioSource>(game,"music"); var originalClip=source.clip;
        int originalSample=originalClip==null?0:source.timeSamples;
        bool originalMute=source.mute,originalLoop=source.loop,originalReduced=Read<bool>(game,"reduced"),originalPointer=Read<bool>(game,"pointerTaken");
        float originalVolume=source.volume,originalPitch=source.pitch;
        int originalOffset=Read<int>(game,"offsetMs"),originalSeed=Read<int>(game,"testSeed");
        var originalTouches=Read<HashSet<int>>(game,"downTouches").ToArray();
        bool hadPreference=PlayerPrefs.HasKey("cascade.offset");int originalPreference=PlayerPrefs.GetInt("cascade.offset",0);
        string journalPath=Path.Combine(Application.persistentDataPath,"cascade-last-attempt.json");
        byte[] originalJournal=File.Exists(journalPath)?File.ReadAllBytes(journalPath):null;
        Touchscreen probe=null,previousTouch=Touchscreen.current;
        try
        {
            source.mute=true;Write(game,"testSeed",731);
            probe=InputSystem.AddDevice<Touchscreen>("Cascade Dark recovery native check");
            foreach(int parity in new[]{1,0})
                foreach(int offsetMs in new[]{0,142})
                {
                    double offset=offsetMs/1000d;var times=Cadence(parity);
                    RuntimeFixture(game,times[0]+offset,offsetMs);
                    Check(Math.Abs(Read<Balance>(game,"balance").presentationSeconds-.35f)<.00001,
                        "runtime cadence keeps the production 350 ms reaction interval");
                    for(int i=0;i<times.Length;i++)
                    {
                        int free=Array.IndexOf(game.Model.cells,0),before=game.MoveCount;
                        NativeTap(game,probe,free,times[i]+offset,700+i*2);
                        Check(game.MoveCount==before+1,"one native tap reaches the actual Dark board input route at "+times[i]);
                        var last=Read<List<CascadeGame.MoveLog>>(game,"journal").Last();
                        string expected=i==4?"miss":"hit";
                        Check(last.rhythm==expected,"runtime independent audio parity "+parity+", step "+i+", offset "+offsetMs+": expected "+expected+", got "+last.rhythm+", error "+last.error);
                        Check(game.Streak==(i<4?i+1:i==4?0:i-4),"native recovery updates the visible streak");
                        if(i==4)Check(Read<Rhythm>(game,"rhythm").UsedBeats==4,"native miss does not reserve the next valid beat");
                        if(i==0||i==4)
                        {
                            NativeTap(game,probe,Array.IndexOf(game.Model.cells,0),times[i]+offset+.2,701+i*2);
                            Check(game.MoveCount==before+1&&game.State==CascadeGame.Phase.Reaction,
                                "an extra native tap during reaction remains rejected for both hit and miss");
                        }
                        FinishRuntimeReaction(game,times[i]+offset);
                    }
                    var rhythm=Read<Rhythm>(game,"rhythm");
                    Check(game.MoveCount==9&&rhythm.Hits==8&&rhythm.Misses==1&&game.Streak==4&&game.Multiplier==2,
                        "actual native input recovers x2 on both pulse parities without reducing the reaction gate");
                }
            const string summary="PASS: real Dark resource, independent PCM pulse parities, native 36-byte input, hit/miss reaction rejection, recovery and x2 at offsets 0/+142 ms. Audio playback accuracy still requires device listening.";
            Debug.Log(summary);return summary;
        }
        finally
        {
            if(probe!=null)InputSystem.RemoveDevice(probe);
            if(previousTouch!=null&&previousTouch.added)previousTouch.MakeCurrent();
            game.CloseCalibration();Call(game,"ShowMenu");
            Write(game,"offsetMs",originalOffset);Write(game,"testSeed",originalSeed);Write(game,"reduced",originalReduced);Write(game,"pointerTaken",originalPointer);
            var touches=Read<HashSet<int>>(game,"downTouches");touches.Clear();foreach(int id in originalTouches)touches.Add(id);
            source.Stop();source.clip=originalClip;source.mute=originalMute;source.loop=originalLoop;source.volume=originalVolume;source.pitch=originalPitch;
            if(originalClip!=null)source.timeSamples=Mathf.Clamp(originalSample,0,Math.Max(0,originalClip.samples-1));
            if(hadPreference)PlayerPrefs.SetInt("cascade.offset",originalPreference);else PlayerPrefs.DeleteKey("cascade.offset");PlayerPrefs.Save();
            if(originalJournal!=null)File.WriteAllBytes(journalPath,originalJournal);else if(File.Exists(journalPath))File.Delete(journalPath);
        }
    }
}
