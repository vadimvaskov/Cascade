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
using UnityEngine.UI;

// Controlled Play Mode checks. These deliberately replace the current attempt with fixtures,
// then return to Menu. No saved scene is edited; settings and the previous attempt log are restored.
// Synthetic beat maps verify integration, not the musical accuracy of the supplied tracks.
public static class CascadeRuntimeChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type GameType = typeof(CascadeGame);
    static T Read<T>(CascadeGame game, string field) => (T)GameType.GetField(field, Flags).GetValue(game);
    static void Write(CascadeGame game, string field, object value) => GameType.GetField(field, Flags).SetValue(game, value);
    static object Call(CascadeGame game, string method, params object[] args) => GameType.GetMethod(method, Flags).Invoke(game, args);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Cascade runtime check failed: " + message);
    }
    sealed class OffsetPreferenceSnapshot
    {
        readonly bool existed;
        readonly int value;
        public OffsetPreferenceSnapshot()
        {
            existed=PlayerPrefs.HasKey("cascade.offset"); value=PlayerPrefs.GetInt("cascade.offset",0);
        }
        public void Restore()
        {
            if(existed)PlayerPrefs.SetInt("cascade.offset",value);else PlayerPrefs.DeleteKey("cascade.offset");
            PlayerPrefs.Save();
        }
    }
    sealed class AudioSnapshot
    {
        readonly AudioSource source;
        readonly AudioClip clip;
        readonly int samples;
        readonly float volume,pitch;
        readonly bool mute,loop;
        public AudioSnapshot(AudioSource value)
        {
            source=value;clip=value.clip;samples=clip==null?0:value.timeSamples;
            volume=value.volume;pitch=value.pitch;mute=value.mute;loop=value.loop;
        }
        public void Restore()
        {
            // The checks return to Menu, so restore source settings without restarting audio.
            source.Stop();source.clip=clip;source.volume=volume;source.pitch=pitch;source.mute=mute;source.loop=loop;
            if(clip!=null)source.timeSamples=Mathf.Clamp(samples,0,Math.Max(0,clip.samples-1));
        }
    }
    static void Clock(CascadeGame game, double songTime)
    {
        double dsp = AudioSettings.dspTime;
        Write(game, "startDsp", dsp - songTime);
        Write(game, "scheduledDsp", dsp - 1);
        Write(game, "dspAnchor", dsp);
        Write(game, "eventAnchor", InputState.currentTime);
    }
    static BeatMap SyntheticMap(double duration)
    {
        return new BeatMap
        {
            trackId = "runtime-check-only", version = "synthetic-runtime-check",
            audioSha256 = new string('a',64), duration = duration,
            beats = Enumerable.Range(1, (int)duration - 1).Select(x => (double)x).ToArray(),
            verified = false, analysisPassed = true, reviewStatus = BeatMap.ProvisionalReview,
            analysisMethod = "synthetic-test-only"
        };
    }
    static void Fixture(CascadeGame game, int[] cells, double time = 1)
    {
        // Start the real level first so the actual UI, materials, views and audio references are used.
        game.CloseCalibration(); Call(game,"ShowMenu"); game.StartLevel(0);
        Require(game.State == CascadeGame.Phase.Countdown, "level preparation succeeds before fixture injection");
        var source = Read<AudioSource>(game,"music"); source.Stop();
        var balance = Read<Balance>(game,"balance");
        var map = SyntheticMap(source.clip.length);
        Write(game,"<Model>k__BackingField",new Board(balance,731,cells));
        Write(game,"beatMap",map); Write(game,"rhythm",new Rhythm(map,balance));
        Write(game,"score",0); Write(game,"moves",0); Write(game,"pending",null);
        Write(game,"lastAcceptedTime",-1d); Write(game,"inputOpenTime",0d);
        Write(game,"offsetMs",0); Write(game,"effectClock",0d); Write(game,"reactionAge",0f);
        Write(game,"lightStrength",0f); Write(game,"fadeRemaining",0f);
        Write(game,"<State>k__BackingField",CascadeGame.Phase.Playing);
        Read<GameObject>(game,"modal").SetActive(false);
        Read<Text>(game,"countdownText").text="";
        Clock(game,time);
        Call(game,"Render",cells); Call(game,"RefreshHud");
    }
    static void FinishReaction(CascadeGame game, double time)
    {
        Require(game.State==CascadeGame.Phase.Reaction,"a calculated reaction exists");
        Clock(game,time);
        Write(game,"reactionAge",Read<Balance>(game,"balance").presentationSeconds+.001f);
        Call(game,"Update");
    }
    static Vector2 ScreenCell(CascadeGame game, int index)
    {
        var view = Read<CascadeCell[]>(game,"views")[index];
        return Read<Camera>(game,"gameCamera").WorldToScreenPoint(view.transform.position);
    }
    static void Touch(Touchscreen device, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(device,new TouchState {touchId=id,phase=phase,position=position},InputState.currentTime);
        InputSystem.Update();
    }

    // The native prefix ends before managed updateStepCount/startTime/startPosition.
    // QueueStateEvent(TouchState) instead sends all 56 bytes and misses this regression.
    [StructLayout(LayoutKind.Explicit, Size=36)]
    struct NativeTouchPrefix : IInputStateTypeInfo
    {
        [FieldOffset(0)] public int touchId;
        [FieldOffset(4)] public Vector2 position;
        [FieldOffset(12)] public Vector2 delta;
        [FieldOffset(20)] public float pressure;
        [FieldOffset(24)] public Vector2 radius;
        [FieldOffset(32)] public byte phaseId;
        [FieldOffset(33)] public byte tapCount;
        [FieldOffset(34)] public byte displayIndex;
        [FieldOffset(35)] public byte flags;
        public FourCC format => TouchState.Format;
    }

    static void QueueNativeTouch(Touchscreen device, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position, double? eventTime=null)
    {
        InputSystem.QueueStateEvent(device,new NativeTouchPrefix
        {
            touchId=id,position=position,phaseId=(byte)phase,
            pressure=phase==UnityEngine.InputSystem.TouchPhase.Ended||phase==UnityEngine.InputSystem.TouchPhase.Canceled?0:1
        },eventTime??InputState.currentTime);
    }

    static void NativeTouch(Touchscreen device, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position, double? eventTime=null)
    {
        QueueNativeTouch(device,id,phase,position,eventTime);
        InputSystem.Update();
    }

    static void QueueCalibrationTap(Touchscreen device,int id,Vector2 position,double eventTime)
    {
        QueueNativeTouch(device,id,UnityEngine.InputSystem.TouchPhase.Began,position,eventTime);
        QueueNativeTouch(device,id,UnityEngine.InputSystem.TouchPhase.Ended,position,eventTime+.005);
    }

    static void CalibrationCases(CascadeGame game,Touchscreen probe)
    {
        var source=Read<AudioSource>(game,"music");
        var clickSource=Read<AudioSource>(game,"calibrationAudio");
        Fixture(game,new int[36],5);
        Write(game,"offsetMs",-60);PlayerPrefs.SetInt("cascade.offset",-60);
        Read<HashSet<int>>(game,"downTouches").Clear();Write(game,"pointerTaken",false);
        game.Pause();double frozen=game.SongPosition;
        var originalCells=(int[])game.Model.cells.Clone();var originalClip=source.clip;int originalSample=source.timeSamples;
        game.OpenCalibration();
        Require(game.State==CascadeGame.Phase.Paused&&Read<bool>(game,"calibrationVisible")&&Read<GameObject>(game,"calibrationPanel").activeSelf&&!Read<GameObject>(game,"modal").activeSelf,
            "calibration opens over a paused attempt without resuming it");
        Call(game,"StartCalibration");Canvas.ForceUpdateCanvases();
        var tap=Read<RectTransform>(game,"calibrationTap");
        Vector2 point=RectTransformUtility.WorldToScreenPoint(null,tap.TransformPoint(tap.rect.center));

        // Replay twelve seconds of original timestamps in one update. Handling-time based
        // scoring would collapse these into one beat instead of estimating +150 ms.
        // QueueStateEvent reserves negative time for "now". A fresh Play session therefore
        // uses nonnegative synthetic event times; this project's Dynamic updates do not
        // timeslice future events. Older sessions replay delayed events.
        double eventStart=Math.Max(0,InputState.currentTime-12),dspStart=AudioSettings.dspTime-12;
        Write(game,"calibrationEventAnchor",eventStart);Write(game,"calibrationDspAnchor",dspStart);Write(game,"calibrationStartDsp",dspStart);
        QueueCalibrationTap(probe,601,point,eventStart+.150);
        QueueCalibrationTap(probe,602,point,eventStart+1.150);
        QueueCalibrationTap(probe,603,new Vector2(-999,-999),eventStart+2.100);
        for(int beat=2;beat<=10;beat++)
        {
            QueueCalibrationTap(probe,610+beat,point,eventStart+beat+.150);
            if(beat==2)QueueCalibrationTap(probe,604,point,eventStart+beat+.200);
        }
        InputSystem.Update();
        int accepted=Read<RhythmCalibration>(game,"calibrationSession").Count;
        bool running=Read<bool>(game,"calibrationRunning");
        Require(accepted==9&&running,
            "native calibration uses original timestamps, ignores warmup/outside taps and counts a beat only once; count="+accepted+", running="+running+", eventStart="+eventStart+", inputNow="+InputState.currentTime);
        Require(Read<int>(game,"offsetMs")==-60&&PlayerPrefs.GetInt("cascade.offset")==-60,
            "collecting calibration samples does not apply or save an offset");
        QueueCalibrationTap(probe,621,point,eventStart+11.150);InputSystem.Update();
        Require(Read<RhythmCalibration>(game,"calibrationSession").Count==10&&!Read<bool>(game,"calibrationRunning")&&Read<int>(game,"calibrationCandidate")==150,
            "ten native 36-byte taps at +150 ms produce a +150 ms candidate");
        Require(Read<int>(game,"offsetMs")==-60&&PlayerPrefs.GetInt("cascade.offset")==-60&&!clickSource.isPlaying,
            "a completed estimate stops clicks and waits for explicit Apply");
        Require(game.MoveCount==0&&game.Score==0&&game.Model.cells.SequenceEqual(originalCells),"calibration taps never place gameplay spheres");
        Call(game,"ApplyCalibration");
        Require(Read<int>(game,"offsetMs")==150&&PlayerPrefs.GetInt("cascade.offset")==150&&!Read<bool>(game,"calibrationVisible"),"Apply saves the candidate and closes calibration");
        Require(game.State==CascadeGame.Phase.Paused&&game.SongPosition==frozen&&Read<GameObject>(game,"modal").activeSelf&&!source.isPlaying&&source.clip==originalClip&&source.timeSamples==originalSample,
            "Apply returns to the same paused music position without resuming gameplay");

        Call(game,"ShowMenu");game.OpenCalibration();
        Require(game.State==CascadeGame.Phase.Menu&&Read<bool>(game,"calibrationVisible")&&!Read<GameObject>(game,"menu").activeSelf,"calibration also opens from the menu");
        Call(game,"AdjustCalibration",70);Call(game,"StartCalibration");game.CloseCalibration();
        Require(game.State==CascadeGame.Phase.Menu&&Read<GameObject>(game,"menu").activeSelf&&!Read<bool>(game,"calibrationVisible")&&!Read<bool>(game,"calibrationRunning")&&!clickSource.isPlaying,
            "Cancel stops an active calibration and returns to the menu");
        Require(Read<int>(game,"offsetMs")==150&&PlayerPrefs.GetInt("cascade.offset")==150,"Cancel preserves the previously applied offset");

        game.OpenCalibration();Call(game,"StartCalibration");Call(game,"OnApplicationFocus",false);
        Require(game.State==CascadeGame.Phase.Menu&&!Read<bool>(game,"calibrationVisible")&&!Read<bool>(game,"calibrationRunning")&&!clickSource.isPlaying,
            "focus interruption cancels calibration without starting a level");
        game.OpenCalibration();Call(game,"StartCalibration");Call(game,"OnApplicationPause",true);
        Require(!Read<bool>(game,"calibrationVisible")&&!Read<bool>(game,"calibrationRunning")&&!clickSource.isPlaying,"application suspension cancels scheduled clicks");
        Fixture(game,new int[36],5);Write(game,"offsetMs",150);game.Pause();frozen=game.SongPosition;
        game.OpenCalibration();Call(game,"StartCalibration");Call(game,"AudioChanged",true);
        Require(game.State==CascadeGame.Phase.Paused&&game.SongPosition==frozen&&!Read<bool>(game,"calibrationVisible")&&!Read<bool>(game,"calibrationRunning")&&!clickSource.isPlaying&&!source.isPlaying,
            "audio interruption cancels calibration and preserves the paused attempt");
        Require(Read<int>(game,"offsetMs")==150&&PlayerPrefs.GetInt("cascade.offset")==150&&game.MoveCount==0,"interruptions cannot apply a new offset or create a move");

        // Exercise the actual Update shader path at a deterministic frozen song position.
        // This avoids a frame/DSP race while still checking the runtime offset sign.
        Fixture(game,new int[36],2.15);Write(game,"reduced",false);Write(game,"offsetMs",150);game.Pause();
        Write(game,"pausedPosition",2.15d);Write(game,"lightStrength",0f);Call(game,"Update");
        float aligned=Shader.GetGlobalFloat("_CascadeLight");
        Require(Math.Abs(Shader.GetGlobalFloat("_CascadeSongTime")-2)<.0001&&aligned>.119f,"positive offset aligns the actual Update light pulse with the corrected beat");
        Write(game,"offsetMs",0);Call(game,"Update");
        Require(Shader.GetGlobalFloat("_CascadeLight")<aligned*.2f,"the uncorrected light is already decaying 150 ms after the same beat");
        Write(game,"offsetMs",150);Write(game,"pausedPosition",2.14d);Call(game,"Update");
        Require(Shader.GetGlobalFloat("_CascadeLight")<.001f,"corrected pulse does not fire before the aligned beat");
    }

    [MenuItem("Cascade/Run calibration integration checks (Play Mode)")]
    public static string RunCalibrationIntegration()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Start Play Mode in CascadePrototype before running calibration checks.");
        var game=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();
        if(game==null)throw new InvalidOperationException("CascadeGame was not found in the running scene.");
        int originalOffset=Read<int>(game,"offsetMs"),originalSeed=Read<int>(game,"testSeed");
        bool originalReduced=Read<bool>(game,"reduced"),originalPointer=Read<bool>(game,"pointerTaken");
        var originalTouches=Read<HashSet<int>>(game,"downTouches").ToArray();
        var preference=new OffsetPreferenceSnapshot();
        var source=Read<AudioSource>(game,"music");var originalSource=new AudioSnapshot(source);
        var clickSource=Read<AudioSource>(game,"calibrationAudio");var originalClickSource=new AudioSnapshot(clickSource);
        float originalShaderTime=Shader.GetGlobalFloat("_CascadeSongTime"),originalShaderLight=Shader.GetGlobalFloat("_CascadeLight");
        string journalPath=Path.Combine(Application.persistentDataPath,"cascade-last-attempt.json");
        byte[] originalJournal=File.Exists(journalPath)?File.ReadAllBytes(journalPath):null;
        Touchscreen probe=null,previousTouch=Touchscreen.current;
        try
        {
            game.CloseCalibration();source.mute=true;clickSource.mute=true;Write(game,"testSeed",731);
            probe=InputSystem.AddDevice<Touchscreen>("Cascade calibration native touch check");CalibrationCases(game,probe);
            const string result="PASS: calibration menu/pause, native timestamp +150 ms, warmup/duplicate exclusion, explicit Apply/Cancel, no gameplay moves, focus/audio interruption and corrected Update light pulse";
            Debug.Log(result);return result;
        }
        finally
        {
            if(probe!=null)InputSystem.RemoveDevice(probe);
            if(previousTouch!=null&&previousTouch.added)previousTouch.MakeCurrent();
            game.CloseCalibration();Call(game,"ShowMenu");
            Write(game,"offsetMs",originalOffset);Write(game,"testSeed",originalSeed);Write(game,"reduced",originalReduced);Write(game,"pointerTaken",originalPointer);
            var touches=Read<HashSet<int>>(game,"downTouches");touches.Clear();foreach(int id in originalTouches)touches.Add(id);
            originalSource.Restore();originalClickSource.Restore();preference.Restore();
            Shader.SetGlobalFloat("_CascadeSongTime",originalShaderTime);Shader.SetGlobalFloat("_CascadeLight",originalShaderLight);
            if(originalJournal!=null)File.WriteAllBytes(journalPath,originalJournal);else if(File.Exists(journalPath))File.Delete(journalPath);
        }
    }

    static void NativeTouchCases(CascadeGame game, Touchscreen probe)
    {
        Fixture(game,new int[36],1);
        Read<HashSet<int>>(game,"downTouches").Clear(); Write(game,"pointerTaken",false);
        Canvas.ForceUpdateCanvases(); Physics.SyncTransforms();
        var position=ScreenCell(game,0);
        NativeTouch(probe,101,UnityEngine.InputSystem.TouchPhase.Began,position);
        Require(game.MoveCount==1,"36-byte native TOUC Began must place a sphere through the full board input route");
        FinishReaction(game,1.4);
        position=ScreenCell(game,Array.IndexOf(game.Model.cells,0));
        NativeTouch(probe,202,UnityEngine.InputSystem.TouchPhase.Began,position);
        NativeTouch(probe,202,UnityEngine.InputSystem.TouchPhase.Ended,position);
        NativeTouch(probe,101,UnityEngine.InputSystem.TouchPhase.Moved,position);
        Require(game.MoveCount==1,"native secondary Ended cannot turn held primary movement into another placement");
        NativeTouch(probe,203,UnityEngine.InputSystem.TouchPhase.Began,position);
        NativeTouch(probe,203,UnityEngine.InputSystem.TouchPhase.Canceled,position);
        NativeTouch(probe,101,UnityEngine.InputSystem.TouchPhase.Moved,position);
        Require(game.MoveCount==1,"native secondary Canceled cannot unlock a held primary finger");
        NativeTouch(probe,101,UnityEngine.InputSystem.TouchPhase.Ended,position);
        Require(Read<HashSet<int>>(game,"downTouches").Count==0,"native Ended clears the final held finger");

        QueueNativeTouch(probe,301,UnityEngine.InputSystem.TouchPhase.Began,position);
        QueueNativeTouch(probe,301,UnityEngine.InputSystem.TouchPhase.Ended,position);
        InputSystem.Update();
        Require(game.MoveCount==2&&Read<HashSet<int>>(game,"downTouches").Count==0,"native rapid Began/Ended in one update produces exactly one placement and releases input");
        FinishReaction(game,1.8);
        position=ScreenCell(game,Array.IndexOf(game.Model.cells,0));
        QueueNativeTouch(probe,401,UnityEngine.InputSystem.TouchPhase.Began,position);
        QueueNativeTouch(probe,401,UnityEngine.InputSystem.TouchPhase.Canceled,position);
        InputSystem.Update();
        Require(game.MoveCount==3&&Read<HashSet<int>>(game,"downTouches").Count==0,"native rapid Began/Canceled keeps one placement and releases input");
        FinishReaction(game,2.2);
        position=ScreenCell(game,Array.IndexOf(game.Model.cells,0));
        NativeTouch(probe,501,UnityEngine.InputSystem.TouchPhase.Began,position);
        NativeTouch(probe,501,UnityEngine.InputSystem.TouchPhase.Ended,position);
        Require(game.MoveCount==4,"native fresh touch remains accepted after cancellation");
    }

    [MenuItem("Cascade/Run native touch regression (Play Mode)")]
    public static string RunNativeTouch()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Start Play Mode in CascadePrototype before running native touch checks.");
        var game=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();
        if(game==null)throw new InvalidOperationException("CascadeGame was not found in the running scene.");
        int originalOffset=Read<int>(game,"offsetMs"),originalSeed=Read<int>(game,"testSeed");
        bool originalReduced=Read<bool>(game,"reduced"),originalPointer=Read<bool>(game,"pointerTaken");
        var originalTouches=Read<HashSet<int>>(game,"downTouches").ToArray();
        var source=Read<AudioSource>(game,"music");var originalSource=new AudioSnapshot(source);
        var preference=new OffsetPreferenceSnapshot();
        string journalPath=Path.Combine(Application.persistentDataPath,"cascade-last-attempt.json");
        byte[] originalJournal=File.Exists(journalPath)?File.ReadAllBytes(journalPath):null;
        Touchscreen probe=null,previousTouch=Touchscreen.current;
        try
        {
            source.mute=true;Write(game,"testSeed",731);
            probe=InputSystem.AddDevice<Touchscreen>("Cascade native prefix touch check");
            NativeTouchCases(game,probe);
            const string result="PASS: native 36-byte TOUC first placement, secondary Ended/Canceled, held primary, rapid same-update taps and cancellation recovery";
            Debug.Log(result);return result;
        }
        finally
        {
            if(probe!=null)InputSystem.RemoveDevice(probe);
            if(previousTouch!=null&&previousTouch.added)previousTouch.MakeCurrent();
            game.CloseCalibration();Call(game,"ShowMenu");
            Write(game,"offsetMs",originalOffset);Write(game,"testSeed",originalSeed);
            Write(game,"reduced",originalReduced);Write(game,"pointerTaken",originalPointer);
            var touches=Read<HashSet<int>>(game,"downTouches");touches.Clear();foreach(int id in originalTouches)touches.Add(id);
            originalSource.Restore();preference.Restore();
            if(originalJournal!=null)File.WriteAllBytes(journalPath,originalJournal);else if(File.Exists(journalPath))File.Delete(journalPath);
        }
    }
    [MenuItem("Cascade/Run runtime integration checks (Play Mode)")]
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Start Play Mode in CascadePrototype before running runtime checks.");
        var game = UnityEngine.Object.FindAnyObjectByType<CascadeGame>();
        if (game==null) throw new InvalidOperationException("CascadeGame was not found in the running scene.");
        int originalOffset=Read<int>(game,"offsetMs");
        int originalSeed=Read<int>(game,"testSeed");
        bool originalReduced=Read<bool>(game,"reduced");
        bool originalPointer=Read<bool>(game,"pointerTaken");
        var originalTouches=Read<HashSet<int>>(game,"downTouches").ToArray();
        var source=Read<AudioSource>(game,"music");var originalSource=new AudioSnapshot(source);
        var preference=new OffsetPreferenceSnapshot();
        string journalPath=Path.Combine(Application.persistentDataPath,"cascade-last-attempt.json");
        byte[] originalJournal=File.Exists(journalPath)?File.ReadAllBytes(journalPath):null;
        var passed=new List<string>(); Touchscreen probe=null; Touchscreen previousTouch=Touchscreen.current;
        try
        {
            source.mute=true; Write(game,"testSeed",731);
            var fixture=new int[36]; fixture[8]=fixture[13]=4; fixture[14]=3;
            Fixture(game,fixture,36);
            var rhythm=Read<Rhythm>(game,"rhythm");
            for(int i=1;i<=35;i++) rhythm.Evaluate(i,0);
            Write(game,"lastAcceptedTime",35d); Write(game,"inputOpenTime",35.5d);
            Require(game.PlaceCell(7,36),"36th successful rhythmic placement is accepted");
            Require(game.Streak==36&&game.Multiplier==10&&game.Score==1600&&game.MoveCount==1,"x10 applies to all three explosions in a two-wave cascade");
            int blockedScore=game.Score;
            Require(!game.PlaceCell(0,36.01)&&game.Score==blockedScore&&rhythm.Hits==36,"reaction blocks input without changing score or streak");
            FinishReaction(game,36.4);
            Require(game.State==CascadeGame.Phase.Playing,"reaction returns to playing");
            Require(game.Model.Occupied==2,"one surviving placement and exactly one passive auto sphere");
            int free=Array.IndexOf(game.Model.cells,0);
            Require(game.PlaceCell(free,36.6)&&game.Streak==0&&game.Multiplier==1,"offbeat placement resets multiplier");
            Require(Read<Turn>(game,"pending").points==10+50*Read<Turn>(game,"pending").destroyed,"miss keeps mechanical points at x1");
            passed.Add("x10/cascade/miss/locked input");

            Fixture(game,new int[36],5.2);
            Write(game,"inputOpenTime",5d);
            Require(!game.PlaceCell(0,4.999)&&game.MoveCount==0&&game.Model.Occupied==0,"delayed event from a closed input interval is rejected");
            Require(game.PlaceCell(0,5)&&game.MoveCount==1,"event at reopened boundary is accepted");
            passed.Add("original input availability boundary");

            Fixture(game,new int[36],1);
            Write(game,"offsetMs",200);
            double length=source.clip.length;
            Require(!game.PlaceCell(0,length)&&!game.PlaceCell(0,length+.001)&&game.MoveCount==0,"song endpoint is exclusive regardless calibration");
            Require(game.BeatPulse(1)>.99f&&game.BeatPulse(1.1)<game.BeatPulse(1)&&game.BeatPulse(.5)==0,"beat envelope follows beat timestamps");
            passed.Add("song end boundary and beat envelope");

            // A beat at zero must not flash before PlayScheduled has actually started.
            Fixture(game,new int[36],0);
            Read<BeatMap>(game,"beatMap").beats=new[]{0d,1d,2d};
            Write(game,"scheduledDsp",AudioSettings.dspTime+10);
            Write(game,"startDsp",Read<double>(game,"scheduledDsp"));
            double effectAtStart=Read<double>(game,"effectClock");
            Call(game,"Update");
            Require(game.SongPosition==0&&Shader.GetGlobalFloat("_CascadeLight")==0,"prestart beat-zero pulse is gated until scheduled audio starts");
            Require(Read<double>(game,"effectClock")==effectAtStart&&!game.PlaceCell(0,0),"scheduled lead-in freezes effects and rejects placement");
            passed.Add("scheduled start gating");

            Fixture(game,new int[36],10);
            double beforePause=game.SongPosition;
            double effectBefore=Read<double>(game,"effectClock");
            game.Pause(); double frozen=game.SongPosition;
            Write(game,"startDsp",Read<double>(game,"startDsp")-5);
            Call(game,"Update"); Call(game,"Update");
            Require(game.State==CascadeGame.Phase.Paused&&game.SongPosition==frozen&&Math.Abs(frozen-beforePause)<.1,"pause freezes song position despite wall-clock advance");
            Require(Read<double>(game,"effectClock")==effectBefore&&!source.isPlaying,"pause freezes effect clock and audio source");
            Require(!game.PlaceCell(0,10)&&game.Score==0,"paused input is rejected");
            game.Resume();
            Require(game.State==CascadeGame.Phase.Playing&&Math.Abs(game.SongPosition-frozen)<.1,"resume reanchors song clock without adding paused time");
            passed.Add("pause and resume clocks");

            Call(game,"ShowMenu"); game.StartLevel(0);
            float remainingCountdown=Read<float>(game,"countdown");
            source.timeSamples=source.clip.frequency*2;
            Call(game,"AudioChanged",false);
            Require(game.State==CascadeGame.Phase.Paused&&game.SongPosition==0,"audio change during countdown preserves a zero song position");
            source.timeSamples=source.clip.frequency*3;
            Call(game,"AudioChanged",true);
            Require(game.SongPosition==0&&Read<float>(game,"countdown")==remainingCountdown,"repeated audio changes cannot replace paused countdown with a source playhead");
            game.Resume();
            Require(game.State==CascadeGame.Phase.Countdown&&game.SongPosition==0&&Read<float>(game,"countdown")==remainingCountdown,"countdown resumes from its remaining time");
            passed.Add("audio device change during paused countdown");

            Fixture(game,new int[36],10);
            game.Pause(); frozen=game.SongPosition;
            source.timeSamples=0;
            Call(game,"AudioChanged",true);
            Require(game.State==CascadeGame.Phase.Paused&&game.SongPosition==frozen,"audio reset cannot erase an existing paused song position");
            game.Resume();
            Require(game.State==CascadeGame.Phase.Playing&&Read<double>(game,"scheduledDsp")>AudioSettings.dspTime,"audio reset uses a fresh scheduled start");
            // Extend the tiny lead-in deterministically to check frozen visuals without a timing race.
            Write(game,"scheduledDsp",AudioSettings.dspTime+10);
            Write(game,"startDsp",Read<double>(game,"scheduledDsp")-frozen);
            Write(game,"lightStrength",.75f); effectBefore=Read<double>(game,"effectClock");
            Call(game,"Update");
            Require(game.SongPosition==frozen&&Read<double>(game,"effectClock")==effectBefore&&game.LightStrength==.75f,"scheduled audio resume freezes song and visual clocks");
            Require(!game.PlaceCell(0,frozen)&&game.MoveCount==0,"reschedule lead-in rejects new moves");

            Fixture(game,new int[36],12);
            Write(game,"lastKnownPosition",12d); source.timeSamples=0;
            Call(game,"AudioChanged",true);
            Require(game.State==CascadeGame.Phase.Paused&&game.SongPosition==12,"a device-reset zero playhead uses the last known active position");
            passed.Add("paused and active audio reset recovery");

            Fixture(game,Enumerable.Range(0,36).Select(i=>i==0||i==35?0:1).ToArray(),1);
            Require(game.PlaceCell(0,1),"last available cells fixture accepts move");
            Require(Read<Turn>(game,"pending").full&&game.Score==10,"auto sphere fills last cell after scoring");
            FinishReaction(game,length+1);
            Require(game.State==CascadeGame.Phase.Result&&Read<Text>(game,"modalTitle").text=="ПОЛЕ ЗАПОЛНЕНО","full field wins over simultaneous song endpoint");
            Call(game,"LateUpdate");
            Require(game.Score==10&&Read<Text>(game,"modalTitle").text=="ПОЛЕ ЗАПОЛНЕНО","terminal state is idempotent");
            game.StartLevel(0);
            Require(game.State==CascadeGame.Phase.Countdown&&game.Score==0&&game.Streak==0&&game.Multiplier==1&&game.Model.Occupied==6,"restart resets attempt state");
            passed.Add("full-field priority, result idempotence and restart");

            Fixture(game,new int[36],1);
            Write(game,"pointerTaken",false); Read<HashSet<int>>(game,"downTouches").Clear();
            probe=InputSystem.AddDevice<Touchscreen>("Cascade runtime checks touch");
            Canvas.ForceUpdateCanvases(); Physics.SyncTransforms();
            Touch(probe,101,UnityEngine.InputSystem.TouchPhase.Began,ScreenCell(game,0));
            Require(game.MoveCount==1,"first touch reaches the board through real Input System events");
            FinishReaction(game,1.4);
            Read<HashSet<int>>(game,"downTouches").Add(999); // An End callback lost on focus change.
            game.Pause(); game.Resume();
            Require(Read<HashSet<int>>(game,"downTouches").Contains(101)&&!Read<HashSet<int>>(game,"downTouches").Contains(999),"resume preserves held fingers and discards stale touch IDs");
            free=Array.IndexOf(game.Model.cells,0);var secondPosition=ScreenCell(game,free);
            Touch(probe,202,UnityEngine.InputSystem.TouchPhase.Began,secondPosition);
            Touch(probe,202,UnityEngine.InputSystem.TouchPhase.Ended,secondPosition);
            Touch(probe,101,UnityEngine.InputSystem.TouchPhase.Moved,secondPosition);
            Require(game.MoveCount==1,"secondary release followed by held primary motion cannot duplicate a move");
            Touch(probe,101,UnityEngine.InputSystem.TouchPhase.Ended,secondPosition);
            Touch(probe,303,UnityEngine.InputSystem.TouchPhase.Began,secondPosition);
            Require(game.MoveCount==2,"new touch after all fingers lifted accepts one move");
            Touch(probe,303,UnityEngine.InputSystem.TouchPhase.Ended,secondPosition);
            FinishReaction(game,1.8);
            Read<HashSet<int>>(game,"downTouches").Add(999);
            free=Array.IndexOf(game.Model.cells,0);secondPosition=ScreenCell(game,free);
            Touch(probe,404,UnityEngine.InputSystem.TouchPhase.Began,secondPosition);
            Require(game.MoveCount==3,"fresh touch recovers a lost End callback even if device state reset after resume");
            Touch(probe,404,UnityEngine.InputSystem.TouchPhase.Ended,secondPosition);
            passed.Add("real touch phases, held-touch resume and lost End recovery");

            NativeTouchCases(game,probe);
            passed.Add("native 36-byte touch prefix, secondary fingers, rapid taps and cancellation");

            string result="PASS: "+string.Join("; ",passed);
            Debug.Log(result); return result;
        }
        finally
        {
            if(probe!=null)InputSystem.RemoveDevice(probe);
            if(previousTouch!=null&&previousTouch.added)previousTouch.MakeCurrent();
            game.CloseCalibration();Call(game,"ShowMenu");
            Write(game,"offsetMs",originalOffset); Write(game,"testSeed",originalSeed);
            Write(game,"reduced",originalReduced); Write(game,"pointerTaken",originalPointer);
            var touches=Read<HashSet<int>>(game,"downTouches");touches.Clear();foreach(int id in originalTouches)touches.Add(id);
            originalSource.Restore();preference.Restore();
            if(originalJournal!=null)File.WriteAllBytes(journalPath,originalJournal);else if(File.Exists(journalPath))File.Delete(journalPath);
        }
    }
}
