using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame : MonoBehaviour
    {
        static readonly System.Globalization.CultureInfo ScoreCulture = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        public enum Phase { Menu, Countdown, Playing, Reaction, Paused, Result, Error }
        [SerializeField] Camera gameCamera;
        [SerializeField] AudioSource music;
        [SerializeField] CascadeCell[] views;
        [SerializeField] Transform boardRoot;
        [SerializeField] RectTransform safeRoot;
        [SerializeField] GameObject menu, hud, modal;
        [SerializeField] Text scoreText, multiplierText, timeText, levelText, statusText, nextText, modalTitle, modalBody;
        [SerializeField] Image progress;
        [SerializeField] Text streakText, countdownText;
        [SerializeField] bool allowProvisionalMaps = true;
        [SerializeField] Material[] orbMaterials;
        [SerializeField] LineRenderer perimeter;
        [SerializeField] Text[] recordTexts;
        [SerializeField] CascadeCell preview;
        [SerializeField] string[] trackNames = { "Midnight Floor", "Drop Bass Princess", "Dark Bass Drop" };
        public Phase State { get; private set; } = Phase.Menu;
        public Board Model { get; private set; }
        Balance balance; Rhythm rhythm; BeatMap beatMap; RhythmGrid rhythmGrid;
        int level, score, moves, bestOccupied;
        double startDsp, pausedPosition, scheduledDsp, eventAnchor, dspAnchor;
        float reactionAge, countdown;
        Turn pending;
        Phase resumePhase;
        bool pointerTaken, reduced;
        readonly HashSet<int> downTouches = new HashSet<int>();
        double inputOpenTime, lastAcceptedTime = -1, effectClock, feedbackUntil, lastKnownPosition;
        bool audioNeedsReschedule;
        float lightStrength, fadeRemaining;
        int lastStage;
        public int Score => score;
        public int Streak => rhythm?.Streak ?? 0;
        public int Multiplier => rhythm?.Multiplier ?? 1;
        public int MoveCount => moves;
        public double SongPosition => Position;
        public string MapStatus => beatMap == null ? "missing" : beatMap.verified ? "verified" : beatMap.IsProvisional ? "provisional" : "invalid";
        public float LightStrength => lightStrength;
        [Serializable] class AudioManifest { public string version; public TrackSignature[] tracks; }
        [Serializable] class TrackSignature { public string trackId, resourceName, audioSha256; public double duration; }
        float volume = .7f;
        int offsetMs, testSeed;
        bool devPanel;
        double Position => State == Phase.Paused || State == Phase.Result ? pausedPosition : State == Phase.Playing || State == Phase.Reaction ? (AudioSettings.dspTime < scheduledDsp ? pausedPosition : Math.Max(0, AudioSettings.dspTime - startDsp)) : 0;
        readonly List<MoveLog> journal = new List<MoveLog>();
        [Serializable] public class MoveLog { public double time, error; public int cell, points, waves, occupied, streak, multiplier, offsetMs, bonusCharge, bonusBeatsRemaining; public bool bonusApplied; public string rhythm, rejection; }
        [Serializable] public class AttemptLog { public string config, track, mapVersion, mapStatus, result; public int seed, score, offsetMs, sampleRate, dspBufferLength, dspBufferCount; public CascadeAudioOutputTiming visualTiming; public MoveLog[] moves; }
        void Awake()
        {
            if (!Application.isPlaying) return;
            balance = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>(useV3?"V3/balance":"Data/balance").text);
            volume = PlayerPrefs.GetFloat("cascade.volume", .7f); reduced = PlayerPrefs.GetInt("cascade.reduced", 0) != 0;
            offsetMs = PlayerPrefs.GetInt("cascade.offset", 0);
            InitializeV3(); BindUI(); SetupCalibration(); ConfigureRasterFeedback(); InitializeApprovedRuntime(); music.volume = volume; Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            AudioSettings.OnAudioConfigurationChanged += AudioChanged;
            InputSystem.onEvent += InputEvent;
            ShowMenu();
        }
        void OnDestroy() { AudioSettings.OnAudioConfigurationChanged -= AudioChanged; InputSystem.onEvent -= InputEvent; DisposeCalibration(); DisposeApproved(); }
        void OnApplicationPause(bool pause) { if (pause) { CloseCalibration(); Pause(); } }
        void OnApplicationFocus(bool focus) { if (!focus) { CloseCalibration(); Pause(); } }
        void AudioChanged(bool changed)
        {
            outputTiming = CascadeAudioOutputTiming.Capture();
            CloseCalibration();
            if (State == Phase.Menu || State == Phase.Result || State == Phase.Error) return;
            bool beforeStart = State == Phase.Countdown || (State == Phase.Paused && resumePhase == Phase.Countdown);
            double snapshot = beforeStart ? 0 : State == Phase.Paused ? pausedPosition : lastKnownPosition;
            // Device changes can reset both DSP time and the source playhead before this callback.
            // A zero playhead must not erase an already running or explicitly paused attempt.
            if (!beforeStart && State != Phase.Paused && music != null && music.clip != null)
            {
                double sample = (double)music.timeSamples / music.clip.frequency;
                if (sample > 0 && Math.Abs(sample - lastKnownPosition) < .5) snapshot = sample;
            }
            Pause();
            if (State != Phase.Paused) return;
            pausedPosition = snapshot; lastKnownPosition = snapshot;
            audioNeedsReschedule = !beforeStart;
            if (beforeStart) { music.Stop(); if (music.clip != null) music.timeSamples = 0; }
        }
        void ShowMenu()
        {
            RecordApprovedAttempt(false);
            RestoreResultActions();
            rhythmBonus=null;
            music.Stop(); fadeRemaining=0; State = Phase.Menu; menu.SetActive(!useV3); hud.SetActive(false); modal.SetActive(false); boardRoot.gameObject.SetActive(false);
            developerPreview=false;RefreshV3Menu();
            if(!useV3)for (int i = 0; i < recordTexts.Length; i++) recordTexts[i].text = "ЛУЧШИЙ РЕЗУЛЬТАТ  " + PlayerPrefs.GetInt(BestKey(i), 0).ToString("N0",ScoreCulture);
        }
        string BestKey(int i) { var a = Resources.Load<TextAsset>("Data/"+trackNames[i]); var m = a == null ? null : JsonUtility.FromJson<BeatMap>(a.text); return "cascade.best."+balance.version+"."+(m?.version ?? "none")+"."+i; }
        public void StartLevel(int index)
        {
            if (State == Phase.Countdown || index < 0 || index >= trackNames.Length) return;
            if(useV3&&!developerPreview&&!progressStore.IsOpen(catalog.RecordFor(catalogTracks[index].track_id)))return;
            RecordApprovedAttempt(false);
            RestoreResultActions();
            outputTiming = CascadeAudioOutputTiming.Capture();
            if(useV3)
            {
                var settings=Resources.Load<TextAsset>(catalogTracks[index].level_settings_path);
                if(settings==null){Error("Не удалось загрузить настройки уровня.");return;}
                balance=JsonUtility.FromJson<Balance>(settings.text);balance.Validate();
                if(balance.size!=6){Error("Этот экран рассчитан на поле 6×6.");return;}
            }
            ClearCascadePresentation();wonAttempt=false;level = index; music.Stop(); fadeRemaining=0; lastKnownPosition=0; audioNeedsReschedule=false; pending = null; lastAcceptedTime=-1; effectClock=0; lightStrength=0; score = moves = 0; bestOccupied = balance.initialCount; journal.Clear();
            int seed = testSeed != 0 ? testSeed : unchecked((int)DateTime.UtcNow.Ticks);
            Model = new Board(balance, seed); rhythm = null; beatMap = null;rhythmBonus=null;
            music.clip = Resources.Load<AudioClip>(useV3?catalogTracks[level].runtime_audio_path:"Audio/" + trackNames[level]);
            if (music.clip == null) { Error("Не удалось загрузить музыку. Попробуй ещё раз."); return; }
            var mapAsset = Resources.Load<TextAsset>(useV3?catalogTracks[level].beatmap_path:"Data/" + trackNames[level]);
            if (mapAsset != null) beatMap = JsonUtility.FromJson<BeatMap>(mapAsset.text);
            var manifestAsset=Resources.Load<TextAsset>("Data/audio-manifest");
            var manifest=manifestAsset==null?null:JsonUtility.FromJson<AudioManifest>(manifestAsset.text);
            var signature=manifest?.tracks?.FirstOrDefault(x=>x.resourceName==trackNames[level]);
            if(useV3){var t=catalogTracks[level];signature=new TrackSignature{trackId=t.track_id,audioSha256=t.runtime_audio_sha256,duration=t.runtime_duration_seconds};}
            if(beatMap==null||signature==null||signature.trackId!=beatMap.trackId||!beatMap.Valid(music.clip.length,signature.audioSha256,allowProvisionalMaps)) { Error("Разметка не соответствует музыке. Попробуй загрузить уровень ещё раз."); return; }
            rhythmGrid=useV3?new RhythmGrid(beatMap):null;
            rhythm=new Rhythm(rhythmGrid?.Map??beatMap,balance);
            ResetRhythmBonus();
            if(useV3)lighting=new CascadeRhythmLighting(beatMap,lightingConfig);
            music.loop = false; music.pitch = 1; music.volume = volume; music.time = 0; music.clip.LoadAudioData();
            menu.SetActive(false);if(useV3)menuUI.gameObject.SetActive(false); hud.SetActive(true); modal.SetActive(false); boardRoot.gameObject.SetActive(true);
            levelText.text = "УРОВЕНЬ " + (level + 1); trackText.text=TrackTitle;
            statusText.text = "";
            countdown = 3; State = Phase.Countdown; pausedPosition = 0;
            ApprovedStart();
            foreach(var v in views) { v.charge=-1; v.raster?.ResetEffects(); if(v.burst!=null)v.burst.gameObject.SetActive(false); if(v.sparks!=null)v.sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); }
            Render(Model.cells); RefreshHud();
            UnityEngine.Debug.Log("Cascade seed=" + Model.seed + " config=" + balance.version + " track=" + trackNames[level]);
        }
        void Error(string message)
        {
            RestoreResultActions();
            bool loadFailure=useV3&&menuUI!=null&&(menuUI.Loading||State==Phase.Countdown);
            music.Stop();fadeRemaining=0;ClearCascadePresentation();menu.SetActive(false);hud.SetActive(false);boardRoot.gameObject.SetActive(false);
            if(loadFailure){State=Phase.Menu;modal.SetActive(false);menuUI.LoadFailed(message);return;}
            State=Phase.Error;modal.SetActive(true);modalTitle.text="НЕ УДАЛОСЬ ПОДГОТОВИТЬ";modalBody.text=message;SetModalButtons(false);
        }
        void Update()
        {
            if (safeRoot == null) return;
            if(Keyboard.current!=null && Keyboard.current.f1Key.wasPressedThisFrame) devPanel=!devPanel;
            if(fadeRemaining>0){fadeRemaining=Mathf.Max(0,fadeRemaining-Time.unscaledDeltaTime);music.volume=volume*fadeRemaining/.12f;if(fadeRemaining==0)music.Stop();}
            UpdateScreenLayout();
            UpdateCalibration();
            if (calibrationVisible) {if(useV3)UpdateV3Lighting(false,true);return;}
            if (State == Phase.Countdown)
            {
                if (music.clip.loadState == AudioDataLoadState.Failed) { Error("Не удалось декодировать трек."); return; }
                if (music.clip.loadState != AudioDataLoadState.Loaded) return;
                countdown -= Time.unscaledDeltaTime;
                countdownText.text = countdown > 0 ? Mathf.CeilToInt(countdown).ToString() : "";
                if (countdown <= 0)
                {
                    startDsp = AudioSettings.dspTime + .12; scheduledDsp = startDsp; music.PlayScheduled(startDsp);
                    eventAnchor = InputState.currentTime; dspAnchor = AudioSettings.dspTime; inputOpenTime=0; State = Phase.Playing;
                    statusText.text = "";
                }
            }
            if ((State == Phase.Playing || State == Phase.Reaction) && AudioSettings.dspTime >= scheduledDsp)
            {
                lastKnownPosition = Math.Min(Position, music.clip.length);
                effectClock+=Time.unscaledDeltaTime; rhythm?.Advance(Position);
                if(useV3)PresentCascadeBursts();
                if (State == Phase.Reaction)
                {
                    reactionAge += Time.unscaledDeltaTime;
                    float duration = balance.presentationSeconds;
                    int stages = pending.waves.Count + 2;
                    int stage = Mathf.Min(stages - 1, Mathf.FloorToInt(reactionAge / duration * stages));
                    for(int k=lastStage+1;k<=stage;k++) {
                        if(k>0&&k<=pending.waves.Count) { foreach(int cell in pending.waves[k-1])views[cell].Explode(effectClock,reduced);Render(pending.afterWaves[k-1]); }
                        else if(k==stages-1)Render(pending.final);
                    }
                    lastStage=stage;
                    if (reactionAge >= duration)
                    {
                        Render(pending.final);
                        if (pending.full) Finish(false); else if (Position >= music.clip.length) Finish(true); else { State = Phase.Playing; inputOpenTime=Position; }
                        pending = null;
                    }
                }

                RefreshHud();
            }
            double rhythmPosition = Position - offsetMs / 1000.0;
            bool active=(State==Phase.Playing||State==Phase.Reaction)&&AudioSettings.dspTime>=scheduledDsp;bool excluded=beatMap==null||beatMap.IsExcluded(Position)||beatMap.IsExcluded(rhythmPosition);
            bool frozenVisuals=State==Phase.Paused||((State==Phase.Playing||State==Phase.Reaction)&&!active);
            if(useV3)UpdateV3Lighting(active,frozenVisuals);
            else
            {
            float pulse=active&&!excluded?BeatPulse(rhythmPosition):frozenVisuals&&Position>0&&!excluded?BeatPulse(rhythmPosition):0;
            float target=active&&!excluded&&Streak>0?.2f+.8f*Mathf.Clamp01((Multiplier-1)/9f):0;
            if(!frozenVisuals)lightStrength=Mathf.MoveTowards(lightStrength,target,Time.unscaledDeltaTime*10);
            Shader.SetGlobalFloat("_CascadeSongTime",(float)Math.Max(0,rhythmPosition));Shader.SetGlobalFloat("_CascadeLight",reduced?pulse*.06f:pulse*(.12f+.6f*lightStrength));
            UpdateRasterPulse(pulse,lightStrength,reduced);
            foreach(var view in views)view.Animate(effectClock,pulse,lightStrength,reduced,frozenVisuals);
            preview.Animate(effectClock,pulse,lightStrength,reduced,frozenVisuals);
            }
            if(active&&effectClock>feedbackUntil)statusText.text=excluded?T("СВОБОДНЫЙ РИТМ","FREE RHYTHM"):"";
        }
        void LateUpdate()
        {
            if(State==Phase.Playing&&AudioSettings.dspTime>=scheduledDsp&&Position>=music.clip.length)Finish(true);
            UpdateResultActions();
            UpdateApprovedRuntime();
        }
        public float BeatPulse(double t)
        {
            if (beatMap == null || beatMap.beats.Length == 0) return 0;
            int i = Array.BinarySearch(beatMap.beats, t); if (i < 0) i = ~i - 1;
            return i < 0 ? 0 : Mathf.Exp((float)(-(t - beatMap.beats[i]) * 12));
        }
        void InputEvent(InputEventPtr evt, InputDevice device)
        {
            if (!Application.isPlaying || (!evt.IsA<StateEvent>() && !evt.IsA<DeltaStateEvent>())) return;
            // The original Input System event time is mapped to the DSP clock, before Update ends a song.
            if (device is Touchscreen touch)
            {
                // Native mobile events contain only the touch's native prefix; the full
                // managed TouchState also has fields that Input System appends later.
                // Read the required controls individually so short iOS events remain valid.
                var primary = touch.primaryTouch;
                if (!primary.phase.ReadValueFromEvent(evt, out var phase) ||
                    !primary.touchId.ReadValueFromEvent(evt, out int touchId)) return;
                if(phase==UnityEngine.InputSystem.TouchPhase.Ended||phase==UnityEngine.InputSystem.TouchPhase.Canceled){downTouches.Remove(touchId);return;}
                if(phase!=UnityEngine.InputSystem.TouchPhase.Began)return;
                if (!primary.position.ReadValueFromEvent(evt, out Vector2 position)) return;
                // A focus interruption may drop an End callback while the device state has reset.
                // Reconcile only before a new Began, never turn movement into a fresh placement.
                downTouches.RemoveWhere(id => !touch.touches.Any(t => t.press.isPressed && t.touchId.ReadValue() == id));
                bool first=downTouches.Count==0;if(!downTouches.Add(touchId)||!first)return;
                AcceptPointer(position,evt.time);
            }
            else if (device is Mouse mouse)
            {
                if (!mouse.leftButton.ReadValueFromEvent(evt, out float press)) return;
                if (press <= .5f) { pointerTaken = false; return; }
                if (pointerTaken) return;
                if (!mouse.position.ReadValueFromEvent(evt, out Vector2 pos)) pos = mouse.position.ReadValue();
                pointerTaken = true; AcceptPointer(pos, evt.time);
            }
        }
        void AcceptPointer(Vector2 point, double eventTime)
        {
            if (calibrationVisible) { AcceptCalibrationPointer(point,eventTime); return; }
            if (State != Phase.Playing || AudioSettings.dspTime < scheduledDsp) return;
            double songTime = dspAnchor + eventTime - eventAnchor - startDsp;
            if (songTime < inputOpenTime || songTime < lastAcceptedTime || songTime < 0 || songTime >= music.clip.length) return;
            if (EventSystem.current != null)
            {
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                if (hits.Count > 0) return;
            }
            if (!Physics.Raycast(gameCamera.ScreenPointToRay(point), out RaycastHit hit,100,1<<8)) return;
            var cell = hit.collider.GetComponent<CascadeCell>(); if (cell == null) return;
            PlaceCell(cell.index, songTime);
        }
        public bool PlaceCell(int cell, double songTime)
        {
            if (State != Phase.Playing || AudioSettings.dspTime < scheduledDsp || songTime < inputOpenTime || songTime < lastAcceptedTime || songTime >= music.clip.length || songTime < 0) return false;
            if (cell < 0 || cell >= Model.cells.Length || Model.cells[cell] != 0)
            { journal.Add(new MoveLog { time = songTime, cell = cell, rejection = "occupied" }); return false; }
            var assessment = rhythm == null ? new RhythmResult { multiplier = 1, kind = "unmapped", error = double.NaN } : rhythm.Evaluate(songTime, offsetMs / 1000.0);
            if(assessment.kind=="invalid"||assessment.kind=="stale")return false; lastAcceptedTime=songTime;
            ApprovedHit(assessment);
            bool bonusApplied=rhythmBonus!=null?rhythmBonus.Evaluate(assessment,songTime,offsetMs/1000d):balance.rhythmPrimaryBonus&&assessment.kind=="hit";
            pending = Model.Place(cell, assessment.multiplier, bonusApplied);
            EmitMusicEnergy(pending,assessment.kind=="hit");
            if(useV3&&assessment.kind=="hit")lighting?.LocalHit(cell,Time.unscaledTimeAsDouble); score += pending.points; moves++; bestOccupied = Math.Max(bestOccupied, Model.Occupied);
            var bonusState=rhythmBonus?.View(songTime,offsetMs/1000d)??default;
            journal.Add(new MoveLog { time = songTime, cell = cell, points = pending.points, waves = pending.waves.Count, occupied = Model.Occupied, streak = rhythm?.Streak ?? 0, rhythm = assessment.kind, multiplier=assessment.multiplier, offsetMs=offsetMs, error = double.IsNaN(assessment.error) ? 0 : assessment.error,bonusApplied=bonusApplied,bonusCharge=bonusState.charge,bonusBeatsRemaining=bonusState.remaining });
            string advance = Multiplier >= balance.maxMultiplier ? "×"+Multiplier : (Streak % balance.hitsPerStep)+"/"+balance.hitsPerStep+" ДО ×"+(Multiplier+1);
            statusText.text=assessment.kind=="hit"?"В РИТМЕ · "+advance+" · +"+pending.points:assessment.kind=="miss"?(assessment.error<0?"РАНО НА ":"ПОЗДНО НА ")+Math.Round(Math.Abs(assessment.error)*1000)+" мс · +"+pending.points:assessment.kind=="repeat"?"ДОЛЯ УЖЕ УЧТЕНА · ×1 · +"+pending.points:assessment.kind=="excluded"?"СВОБОДНЫЙ РИТМ · ×1 · +"+pending.points:"+"+pending.points;
            statusText.color=assessment.kind=="miss"?new Color(.9f,.53f,.65f):new Color(1,.8f,.45f);feedbackUntil=effectClock+1.2;
            if(IsEnglish)statusText.text=assessment.kind=="hit"?"ON BEAT · ×"+Multiplier+" · +"+pending.points:assessment.kind=="miss"?(assessment.error<0?"EARLY BY ":"LATE BY ")+Math.Round(Math.Abs(assessment.error)*1000)+" ms · +"+pending.points:assessment.kind=="repeat"?"TICK ALREADY COUNTED · +"+pending.points:"FREE RHYTHM · +"+pending.points;
            if(useV3){PresentImmediate(pending);return true;}
            reactionAge=0;lastStage=0;State=Phase.Reaction;Render(pending.afterPlacement);RefreshHud();return true;
        }
        void Render(int[] values){for(int i=0;i<views.Length;i++)views[i].SetCharge(values[i],orbMaterials,effectClock);}
        void RefreshHud()
        {
            scoreText.text = score.ToString("N0",ScoreCulture); multiplierText.text = "×" + (rhythm?.Multiplier ?? 1); streakText.text=Streak>0?"СЕРИЯ  "+Streak:"";
            RefreshRhythmBonusHud();
            double t = Math.Min(Position, music.clip.length); if (State == Phase.Countdown) t = 0;
            timeText.text = Clock(t) + " / " + Clock(music.clip.length); progress.fillAmount = (float)(t / music.clip.length);
            
            nextText.text = T("СЛЕДУЮЩАЯ","NEXT");
            preview.SetCharge(Model.NextCharge,orbMaterials,effectClock,false);
        }
        static string Clock(double t) => ((int)t / 60) + ":" + ((int)t % 60).ToString("00");
        public void Pause()
        {
            if (State != Phase.Playing && State != Phase.Reaction && State != Phase.Countdown) return;
            RestoreResultActions();
            resumePhase = State; pausedPosition = State == Phase.Countdown ? 0 : Position;
            lastKnownPosition = pausedPosition;
            if (State != Phase.Countdown && AudioSettings.dspTime < scheduledDsp) audioNeedsReschedule = true;
            music.Pause(); State = Phase.Paused; foreach(var v in views)if(v.sparks!=null)v.sparks.Pause(); RestoreContinue(); modal.SetActive(true); modalTitle.text = T("ПАУЗА","PAUSED");
            modalBody.text = T("Музыка и поле ждут тебя","Your music and board are waiting"); SetModalButtons(true);
            Export("paused");
        }
        public void Resume()
        {
            if (State != Phase.Paused) return;
            outputTiming = CascadeAudioOutputTiming.Capture();
            modal.SetActive(false); State = resumePhase;
            downTouches.Clear();
            if (Touchscreen.current != null)
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) downTouches.Add(touch.touchId.ReadValue());
            pointerTaken = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (State != Phase.Countdown)
            {
                double now = AudioSettings.dspTime;
                scheduledDsp = now; startDsp = now - pausedPosition;
                if (pausedPosition <= 0 || audioNeedsReschedule)
                {
                    music.Stop();
                    if (music.clip != null)
                        music.timeSamples = Mathf.Clamp((int)Math.Round(pausedPosition * music.clip.frequency), 0, Math.Max(0, music.clip.samples - 1));
                    scheduledDsp = now + .12; startDsp = scheduledDsp - pausedPosition;
                    music.PlayScheduled(scheduledDsp);
                }
                else music.UnPause();
                eventAnchor = InputState.currentTime; dspAnchor = AudioSettings.dspTime; inputOpenTime = pausedPosition;
                lastKnownPosition = pausedPosition; audioNeedsReschedule = false;
            }

        }
        void Finish(bool won)
        {
            if (State == Phase.Result || State == Phase.Error) return;
            pausedPosition = Math.Min(Position, music.clip.length); State = Phase.Result; fadeRemaining=.12f; countdownText.text="";
            wonAttempt=won;
            if(useV3&&won&&!developerPreview)
            {
                try{progressStore.Complete(catalogTracks[level],score);}
                catch(Exception e){Debug.LogError("Cascade progress save: "+e.Message);Error("Не удалось сохранить результат. Освободи место и попробуй снова.");return;}
            }
            PrepareResultActions();modal.SetActive(true);
            if (!useV3 && won && rhythm != null) { int best = PlayerPrefs.GetInt(BestKey(level), 0); if (score > best) PlayerPrefs.SetInt(BestKey(level), score); PlayerPrefs.Save(); }
            modalTitle.text = won ? T("ТРЕК ЗАВЕРШЁН","TRACK COMPLETE") : T("ПОЛЕ ЗАПОЛНЕНО","BOARD FULL");
            modalBody.text = score.ToString("N0",ScoreCulture) + " ОЧКОВ\n\nХодов: " + moves + "   •   Макс. занято: " + bestOccupied + "\nЛучшая серия: " + (rhythm?.Best ?? 0) + "\nПопаданий: " + (rhythm?.Hits ?? 0) + "   •   Промахов: " + (rhythm?.Misses ?? 0) + (rhythm == null ? "\n\nТест механики: ритм и рекорды отключены" : "");
            if(useV3&&won&&IsLastRecordComplete())modalTitle.text="КОЛЛЕКЦИЯ ПРОЙДЕНА";
            if(IsEnglish)modalBody.text=score.ToString("N0",ScoreCulture)+" POINTS\n\nMoves: "+moves+"   ·   Peak occupied: "+bestOccupied+"\nBest streak: "+(rhythm?.Best??0)+"\nHits: "+(rhythm?.Hits??0)+"   ·   Misses: "+(rhythm?.Misses??0);
            SetModalButtons(false); Export(won ? "completed" : "full");
        }
        void Export(string result)
        {
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
            var log = new AttemptLog { config = balance.version, track = trackNames[level], mapVersion = beatMap?.version ?? "unmapped", mapStatus=MapStatus, seed = Model.seed, score = score, offsetMs = offsetMs, sampleRate = AudioSettings.outputSampleRate, dspBufferLength = bufferLength, dspBufferCount = bufferCount, visualTiming=outputTiming, result = result, moves = journal.ToArray() };
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, useV3?"cascade-v3-last-attempt.json":"cascade-last-attempt.json"), JsonUtility.ToJson(log, true)); }
            catch (Exception e) { UnityEngine.Debug.LogWarning(e.Message); }
        }
        void SetModalButtons(bool paused)
        {
            modal.transform.Find("Restart").GetComponentInChildren<Text>().text = State == Phase.Result ? T("ПОПРОБОВАТЬ ЕЩЁ РАЗ","TRY AGAIN") : T("НАЧАТЬ ЗАНОВО","RESTART");
            modal.transform.Find("Menu").GetComponentInChildren<Text>().text=T("В МЕНЮ","HOME");
            modal.transform.Find("Next").GetComponentInChildren<Text>().text=T("СЛЕДУЮЩИЙ УРОВЕНЬ","NEXT RECORD");
            modal.transform.Find("Settings").GetComponentInChildren<Text>().text=T(approvedDesign?"НАСТРОЙКИ":"ГРОМКОСТЬ",approvedDesign?"SETTINGS":"VOLUME");
            modal.transform.Find("Effects").GetComponentInChildren<Text>().text=T("ЭФФЕКТЫ","EFFECTS");
            modal.transform.Find("Cancel").GetComponentInChildren<Text>().text=T("ОТМЕНА","CANCEL");
            if(calibrationPauseButton)calibrationPauseButton.GetComponentInChildren<Text>().text=T("НАСТРОЙКА РИТМА","RHYTHM CALIBRATION");
            modal.transform.Find("Continue").gameObject.SetActive(paused);
            modal.transform.Find("Next").gameObject.SetActive(!paused && State == Phase.Result && (useV3?HasNextOpenRecord():level < trackNames.Length-1) && !developerPreview && wonAttempt && Model.Occupied < Model.cells.Length);
            modal.transform.Find("Settings").gameObject.SetActive(paused);
            modal.transform.Find("Effects").gameObject.SetActive(paused);
            modal.transform.Find("Cancel").gameObject.SetActive(false);
            if (calibrationPauseButton != null) calibrationPauseButton.gameObject.SetActive(paused);
            LayoutApprovedModal();
        }
        public void Restart() { if(State==Phase.Result&&!resultActionsReady)return; if (State == Phase.Paused) Confirm(true); else StartLevel(level); }
        public void ExitMenu() { if(State==Phase.Result&&!resultActionsReady)return; if (State == Phase.Paused) Confirm(false); else ShowMenu(); }
        void Confirm(bool restart)
        {
            calibrationPauseButton.gameObject.SetActive(false);
            modal.transform.Find("Cancel").gameObject.SetActive(true); modalTitle.text = T("ЗАВЕРШИТЬ ПОПЫТКУ?","END THIS ATTEMPT?"); modalBody.text = T("Текущее поле и очки будут потеряны.","The current board and score will be lost.");
            var btn = modal.transform.Find("Continue").GetComponent<Button>(); var label = btn.GetComponentInChildren<Text>();
            label.text = restart ? T("ДА, НАЧАТЬ ЗАНОВО","YES, RESTART") : T("ДА, В МЕНЮ","YES, GO HOME"); btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => { RestoreContinue(); if (restart) StartLevel(level); else ShowMenu(); });
            LayoutApprovedModal(true);
        }
        void RestoreContinue() { var b = modal.transform.Find("Continue").GetComponent<Button>(); b.GetComponentInChildren<Text>().text = T("ПРОДОЛЖИТЬ","CONTINUE"); b.onClick.RemoveAllListeners(); b.onClick.AddListener(Resume); }
        public void CycleSettings()
        {
            volume = volume >= .99f ? 0 : Mathf.Min(1,volume + .25f); music.volume = volume;
            SaveSettings();
            modalBody.text = "Громкость: " + Mathf.RoundToInt(volume * 100) + "%\nМягкие эффекты: " + (reduced ? "да" : "нет");
        }
        public void ToggleEffects()
        {
            reduced = !reduced; SaveSettings();
            modalBody.text = "Мягкие эффекты: " + (reduced ? "да" : "нет");
        }
        void OnGUI()
        {
            if (!Application.isPlaying) return;

            if (!devPanel) return;
            GUI.Box(new Rect(8,44,340,useV3?360:240), "Панель тестирования · F1");
            GUI.Label(new Rect(20,74,310,24), "Seed (0 = новый):"); string s = GUI.TextField(new Rect(170,74,160,24), testSeed.ToString()); int.TryParse(s,out testSeed);
            GUI.Label(new Rect(20,105,310,24), "Offset: " + offsetMs + " мс");
            if (GUI.Button(new Rect(190,105,60,28), "−10")) offsetMs = Math.Max(-400,offsetMs-10);
            if (GUI.Button(new Rect(260,105,60,28), "+10")) offsetMs = Math.Min(400,offsetMs+10);
            if(!useV3)PlayerPrefs.SetInt("cascade.offset",offsetMs);
            GUI.Label(new Rect(20,142,310,40), "Поле: 6×6 · версия " + balance.version + "\nРитм: " + (rhythm == null ? "нет проверенной карты" : "карта загружена"));
            GUI.Label(new Rect(20,190,310,50), "Seed попытки: " + (Model?.seed.ToString() ?? "—") + "\nЭкспорт: persistentDataPath/cascade-last-attempt.json");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(useV3)for(int i=0;i<8;i++){int n=i;if(GUI.Button(new Rect(20+(i%4)*77,285+(i/4)*38,72,32),"Трек "+(i+1)))PreviewV3Track(n);}
            if(useV3)GUI.Label(new Rect(20,364,310,30),"Тест трека: прогресс не записывается");
#endif
            if (State == Phase.Paused && GUI.Button(new Rect(20,244,300,28), "Экспортировать текущую попытку")) Export("paused");
        }
        public static Color Palette(int charge) => charge == 1 ? new Color(.04f,.64f,1) : charge == 2 ? new Color(.12f,1,.4f) : charge == 3 ? new Color(1,.53f,.06f) : new Color(.75f,.2f,1);
        void BindUI()
        {
            for (int i = 0; i < 3; i++) { int selected = i; menu.transform.Find("Level" + i).GetComponent<Button>().onClick.AddListener(() => StartLevel(selected)); }
            hud.transform.Find("Pause").GetComponent<Button>().onClick.AddListener(Pause);
            RestoreContinue();
            modal.transform.Find("Restart").GetComponent<Button>().onClick.AddListener(Restart);
            modal.transform.Find("Menu").GetComponent<Button>().onClick.AddListener(ExitMenu);
            modal.transform.Find("Next").GetComponent<Button>().onClick.AddListener(NextRecord);
            modal.transform.Find("Settings").GetComponent<Button>().onClick.AddListener(CycleSettings);
            modal.transform.Find("Effects").GetComponent<Button>().onClick.AddListener(ToggleEffects);
            modal.transform.Find("Cancel").GetComponent<Button>().onClick.AddListener(() => { RestoreContinue(); modalTitle.text=T("ПАУЗА","PAUSED"); modalBody.text=T("Музыка и поле ждут тебя","Your music and board are waiting"); SetModalButtons(true); });
        }
    }
}
