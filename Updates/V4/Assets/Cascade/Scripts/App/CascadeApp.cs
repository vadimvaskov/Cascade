using Cascade.Audio;
using Cascade.Core;
using Cascade.Data;
using Cascade.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cascade
{
    /// <summary>Точка входа: создаёт холст, звук и переключает экраны. Запускается сам в любой сцене.</summary>
    public sealed class CascadeApp : MonoBehaviour
    {
        public static CascadeApp Instance { get; private set; }

        public BalanceConfig Config { get; private set; }
        public Conductor Conductor { get; private set; }
        public RectTransform ScreenRoot { get; private set; }
        public RectTransform OverlayRoot { get; private set; }
        public float TopInset { get; private set; }
        public float BottomInset { get; private set; }
        public float CanvasHeight { get; private set; }
        public float CanvasWidth { get; private set; }
        public ScreenBase Current { get; private set; }

        private RectTransform toastRoot;
        private RectTransform energyOffer;
        private LevelDef energyOfferLevel;

        /// <summary>Открыто окно «Энергия закончилась».</summary>
        public bool EnergyOfferOpen { get { return energyOffer != null; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null)
            {
                return;
            }
            GameObject go = new GameObject("CascadeApp");
            DontDestroyOnLoad(go);
            go.AddComponent<CascadeApp>();
        }

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            // Игра не замирает, когда окно редактора теряет фокус (на телефоне настройка ни на что не влияет)
            Application.runInBackground = true;
            Config = new BalanceConfig();
            SaveSystem.Load();
            Loc.SetLanguage(SaveSystem.Data.language);
            Haptics.Enabled = SaveSystem.Data.vibration;
            SaveSystem.RefreshEnergy(Config);
            PrepareCamera();
            BuildCanvas();
            UseLowLatencyAudio();
            Conductor = gameObject.AddComponent<Conductor>();
            Conductor.SetVolume(SaveSystem.Data.musicVolume);
            Conductor.SetUserOffsetMs(SaveSystem.Data.audioOffsetMs);
            Debug.Log("[Cascade] App started, canvas " + CanvasWidth + "x" + CanvasHeight + ", language " + Loc.Language);
            ShowMenu();
        }

        /// <summary>Маленький аудиобуфер: звук выходит быстрее, попадание в ритм точнее.</summary>
        private static void UseLowLatencyAudio()
        {
            try
            {
                AudioConfiguration configuration = AudioSettings.GetConfiguration();
                if (configuration.dspBufferSize > 256)
                {
                    configuration.dspBufferSize = 256;
                    if (!AudioSettings.Reset(configuration))
                    {
                        Debug.LogWarning("[Cascade] Could not switch audio to the small buffer");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Cascade] Audio buffer setup failed: " + e.Message);
            }
        }

        private void PrepareCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("CascadeCamera");
                DontDestroyOnLoad(go);
                cam = go.AddComponent<Camera>();
                cam.orthographic = true;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Night;
            if (cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }
        }

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("CascadeCanvas", typeof(RectTransform));
            canvasGo.layer = 5;
            DontDestroyOnLoad(canvasGo);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiKit.DesignWidth, UiKit.DesignHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.UI.InputSystemUIInputModule module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            UpdateInsets();
            ScreenRoot = UiKit.Stretch(UiKit.Rect("Screens", canvasGo.transform));
            OverlayRoot = UiKit.Stretch(UiKit.Rect("Overlays", canvasGo.transform));
            toastRoot = UiKit.Stretch(UiKit.Rect("Toasts", canvasGo.transform));
        }

        private void UpdateInsets()
        {
            float scale = Mathf.Min(Screen.width / UiKit.DesignWidth, Screen.height / UiKit.DesignHeight);
            if (scale <= 0f)
            {
                scale = 1f;
            }
            Rect safe = Screen.safeArea;
            CanvasWidth = Screen.width / scale;
            CanvasHeight = Screen.height / scale;
            TopInset = Mathf.Max(0f, (Screen.height - safe.yMax) / scale);
            BottomInset = Mathf.Max(0f, safe.yMin / scale);
        }

        private T Show<T>() where T : ScreenBase
        {
            if (Current != null)
            {
                Current.OnClose();
                Destroy(Current.gameObject);
                Current = null;
            }
            for (int i = OverlayRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(OverlayRoot.GetChild(i).gameObject);
            }
            energyOffer = null;
            energyOfferLevel = null;
            Time.timeScale = 1f;
            UpdateInsets();
            RectTransform root = UiKit.Stretch(UiKit.Rect(typeof(T).Name, ScreenRoot));
            T screen = root.gameObject.AddComponent<T>();
            Current = screen;
            screen.Init(this, root);
            return screen;
        }

        public void ShowMenu()
        {
            Conductor.Stop();
            Show<MainMenuScreen>();
        }

        public void ShowWorldMap()
        {
            Conductor.Stop();
            Show<WorldMapScreen>();
        }

        public void ShowSettings()
        {
            Show<SettingsScreen>();
        }

        /// <summary>
        /// Запуск уровня. Без энергии уровень не начинается: открывается окно с предложением пополнить её
        /// (см. EnergyOfferOpen), и метод возвращает false.
        /// </summary>
        public bool StartLevel(LevelDef level)
        {
            if (level == null)
            {
                return false;
            }
            if (!LevelCatalog.IsUnlocked(level))
            {
                Toast(Loc.T("locked"));
                return false;
            }
            if (SaveSystem.RefreshEnergy(Config) <= 0)
            {
                if (Config.freeEnergyRefill)
                {
                    ShowEnergyOffer(level);
                }
                else
                {
                    Toast(Loc.T("no_energy"));
                }
                return false;
            }
            GameScreen game = Show<GameScreen>();
            game.Begin(level, false);
            return true;
        }

        /// <summary>Окно «Энергия закончилась»: бесплатное пополнение и сразу запуск выбранного уровня.</summary>
        private void ShowEnergyOffer(LevelDef level)
        {
            CloseEnergyOffer();
            RectTransform panel;
            energyOffer = Modal.Create(OverlayRoot, "EnergyOffer", 900f, 900f, 0.76f, out panel);
            energyOfferLevel = level;
            // Окно может открыться поверх итога уровня, поэтому фон непрозрачный: текст снизу не просвечивает
            Image solid = UiKit.SlicedImg("Solid", panel, "ui/round16", 24f, new Color32(30, 22, 46, 255));
            UiKit.Stretch(solid.rectTransform);
            solid.transform.SetAsFirstSibling();
            float top = panel.sizeDelta.y * 0.5f;
            Text title = UiKit.FitLabel("Title", panel, Loc.T("energy_out_title"), 70, UiKit.Gold, TextAnchor.MiddleCenter, true);
            UiKit.Center(title.rectTransform, 0f, top - 100f, 800f, 100f);
            Image icon = UiKit.Img("Icon", panel, "ui/energy");
            icon.preserveAspect = true;
            UiKit.Center(icon.rectTransform, 0f, top - 260f, 150f, 150f);
            Text count = UiKit.Label("Count", panel, SaveSystem.Data.energy + " / " + Config.maxEnergy, 56, UiKit.Cream, TextAnchor.MiddleCenter, true);
            UiKit.Center(count.rectTransform, 0f, top - 372f, 500f, 64f);
            Text note = UiKit.FitLabel("Note", panel, Loc.T("energy_out_text"), 36, UiKit.Muted, TextAnchor.MiddleCenter, false);
            UiKit.Center(note.rectTransform, 0f, top - 446f, 800f, 50f);
            Modal.GoldButton(panel, 0f, -top + 250f, 560f, Loc.T("refill"), AcceptEnergyOffer);
            Modal.PlainButton(panel, 0f, -top + 90f, 620f, Loc.T("later"), CloseEnergyOffer);
            Debug.Log("[Cascade] Out of energy, free refill offered");
        }

        /// <summary>Игрок согласился: энергия полная, выбранный уровень запускается.</summary>
        public void AcceptEnergyOffer()
        {
            LevelDef level = energyOfferLevel;
            CloseEnergyOffer();
            SaveSystem.RefillEnergy(Config);
            Debug.Log("[Cascade] Energy refilled for free");
            if (level != null)
            {
                StartLevel(level);
            }
        }

        public void CloseEnergyOffer()
        {
            if (energyOffer != null)
            {
                Destroy(energyOffer.gameObject);
            }
            energyOffer = null;
            energyOfferLevel = null;
        }

        public void StartTutorial()
        {
            GameScreen game = Show<GameScreen>();
            game.Begin(LevelCatalog.Levels[0], true);
        }

        public void Toast(string message)
        {
            for (int i = toastRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(toastRoot.GetChild(i).gameObject);
            }
            Image plate = UiKit.SlicedImg("Toast", toastRoot, "ui/round16", 24f, new Color(0.06f, 0.04f, 0.1f, 0.92f));
            RectTransform rt = plate.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(820f, 96f);
            rt.anchoredPosition = new Vector2(0f, 330f + BottomInset);
            Text label = UiKit.FitLabel("Text", rt, message, 38, UiKit.Cream, TextAnchor.MiddleCenter, false);
            UiKit.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(30f, 8f);
            label.rectTransform.offsetMax = new Vector2(-30f, -8f);
            CanvasGroup group = plate.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0f;
            Anim.Run(plate, 0f, 0.15f, delegate(float t) { group.alpha = t; }, null, true);
            Anim.Run(plate, 1.7f, 0.35f, delegate(float t) { group.alpha = 1f - t; }, delegate { Destroy(plate.gameObject); }, true);
        }

        private void OnApplicationPause(bool paused)
        {
            if (Config == null || Conductor == null)
            {
                return;
            }
            if (paused)
            {
                if (Current != null)
                {
                    Current.OnAppPaused();
                }
                SaveSystem.Save();
            }
            else
            {
                SaveSystem.RefreshEnergy(Config);
            }
        }

        private void OnApplicationQuit()
        {
            SaveSystem.Save();
        }
    }
}
