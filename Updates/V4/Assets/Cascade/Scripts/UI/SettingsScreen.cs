using System;
using Cascade.Audio;
using Cascade.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Настройки: громкость, вибрация, пульсация рамки, ритм-диаграмма, язык, повтор обучения.</summary>
    public sealed class SettingsScreen : ScreenBase
    {
        private RectTransform content;
        private float cursorY;

        protected override void Build()
        {
            CoverBackground("game/bg_game", 1f);
            Image shade = UiKit.Img("Shade", Root, null, new Color(0.04f, 0.02f, 0.08f, 0.55f));
            UiKit.Stretch(shade.rectTransform);
            shade.transform.SetSiblingIndex(2);

            Image back = UiKit.Img("Back", Safe, "ui/btn_back");
            UiKit.Top(back.rectTransform, 24f, 24f, 96f, 94f);
            UiKit.MakeButton(back, delegate { App.ShowMenu(); });
            Image logo = UiKit.Img("Logo", Safe, "menu/logo");
            logo.preserveAspect = true;
            UiKit.Top(logo.rectTransform, 290f, 16f, 500f, 201f);
            Image banner = UiKit.Img("Banner", Safe, "ui/banner_muted");
            banner.preserveAspect = true;
            UiKit.Top(banner.rectTransform, 250f, 206f, 580f, 190f);
            Text title = UiKit.FitLabel("Title", banner.transform, Loc.T("settings"), 74, UiKit.Ink, TextAnchor.MiddleCenter, true);
            UiKit.Center(title.rectTransform, 0f, 6f, 420f, 90f);

            // Прокручиваемый список
            RectTransform scrollRect = UiKit.Rect("Scroll", Safe);
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(0f, 20f);
            scrollRect.offsetMax = new Vector2(0f, -400f);
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", scrollRect));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            SaveData save = SaveSystem.Data;
            cursorY = 10f;

            // Панель из трёх строк: музыка, звуки, вибрация
            Image panel = UiKit.Img("Panel3", content, "settings/panel3");
            UiKit.Top(panel.rectTransform, 40f, cursorY, 1000f, 392f);
            float[] rows = { 0.205f, 0.507f, 0.808f };
            string[] keys = { "music", "sfx", "vibration" };
            for (int i = 0; i < 3; i++)
            {
                Text label = UiKit.FitLabel("Label", panel.transform, Loc.T(keys[i]), 40, UiKit.Cream, TextAnchor.MiddleLeft, true);
                UiKit.Center(label.rectTransform, -160f, 196f - rows[i] * 392f, 300f, 56f);
            }
            MakeSlider(panel.transform, 235f, 196f - rows[0] * 392f, 440f, save.musicVolume, delegate(float v)
            {
                save.musicVolume = v;
                App.Conductor.SetVolume(v);
            });
            MakeSlider(panel.transform, 235f, 196f - rows[1] * 392f, 440f, save.sfxVolume, delegate(float v) { save.sfxVolume = v; });
            MakeToggle(panel.transform, 392f, 196f - rows[2] * 392f, save.vibration, delegate(bool on)
            {
                save.vibration = on;
                Haptics.Enabled = on;
                if (on)
                {
                    Haptics.Tap();
                }
            });
            cursorY += 392f + 22f;

            Image pulseRow = Row("settings/row_blank", "frame_pulse", 80f);
            MakeToggle(pulseRow.transform, 392f, 0f, save.framePulse, delegate(bool on) { save.framePulse = on; });
            Image diagramRow = Row("settings/row_blank", "rhythm_diagram", 80f);
            MakeToggle(diagramRow.transform, 392f, 0f, save.rhythmDiagram, delegate(bool on) { save.rhythmDiagram = on; });

            Image syncRow = Row("settings/row_blank", "audio_sync", 80f);
            BuildSyncControls(syncRow.transform, save);
            Image timingRow = Row("settings/row_blank", "show_timing", 80f);
            MakeToggle(timingRow.transform, 392f, 0f, save.showTiming, delegate(bool on) { save.showTiming = on; });
            Text hint = UiKit.FitLabel("SyncHint", content, Loc.T("sync_hint"), 28, UiKit.Muted, TextAnchor.MiddleCenter, false);
            UiKit.Top(hint.rectTransform, 70f, cursorY - 8f, 940f, 40f);
            cursorY += 44f;

            Image languageRow = Row("settings/row_globe", "language", 200f);
            PillText(languageRow.transform, Loc.T("lang_name"));
            UiKit.MakeButton(languageRow, ToggleLanguage);

            Image tutorialRow = Row("settings/row_blank", "tutorial", 80f);
            RightText(tutorialRow.transform, Loc.T("replay") + "  >");
            UiKit.MakeButton(tutorialRow, delegate
            {
                SaveSystem.Save();
                App.StartTutorial();
            });

            Image graphicsRow = Row("settings/row_monitor", "graphics", 200f);
            PillText(graphicsRow.transform, Loc.T("high"));
            UiKit.MakeButton(graphicsRow, Soon);
            Image notificationsRow = Row("settings/row_bell", "notifications", 200f);
            RightText(notificationsRow.transform, Loc.T("coming_soon"));
            UiKit.MakeButton(notificationsRow, Soon);
            Image saveRow = Row("settings/row_cloud", "save_progress", 200f);
            RightText(saveRow.transform, Loc.T("local"));

            Image support = UiKit.Img("Support", content, "settings/btn_support");
            UiKit.Top(support.rectTransform, 60f, cursorY + 6f, 460f, 124f);
            Text supportText = UiKit.FitLabel("Label", support.transform, Loc.T("support"), 36, UiKit.Cream, TextAnchor.MiddleLeft, true);
            UiKit.Center(supportText.rectTransform, 128f, 0f, 180f, 50f);
            UiKit.MakeButton(support, Soon);
            Image about = UiKit.Img("About", content, "settings/btn_about");
            UiKit.Top(about.rectTransform, 560f, cursorY + 6f, 460f, 124f);
            Text aboutText = UiKit.FitLabel("Label", about.transform, Loc.T("about"), 36, UiKit.Cream, TextAnchor.MiddleLeft, true);
            UiKit.Center(aboutText.rectTransform, 105f, 0f, 200f, 50f);
            UiKit.MakeButton(about, delegate { App.Toast("Cascade " + Application.version); });
            cursorY += 160f;

            content.sizeDelta = new Vector2(UiKit.DesignWidth, cursorY + 40f);
        }

        public override void OnClose()
        {
            SaveSystem.Save();
        }

        private void Soon()
        {
            App.Toast(Loc.T("coming_soon"));
        }

        private void ToggleLanguage()
        {
            SaveData save = SaveSystem.Data;
            save.language = Loc.IsRussian ? "en" : "ru";
            Loc.SetLanguage(save.language);
            SaveSystem.Save();
            App.ShowSettings();
        }

        private Image Row(string sprite, string key, float labelLeft)
        {
            Image row = UiKit.Img(key, content, sprite);
            UiKit.Top(row.rectTransform, 40f, cursorY, 1000f, 111f);
            Text label = UiKit.FitLabel("Label", row.transform, Loc.T(key), 40, UiKit.Cream, TextAnchor.MiddleLeft, true);
            UiKit.Center(label.rectTransform, -500f + labelLeft + 190f, 0f, 380f, 56f);
            cursorY += 124f;
            return row;
        }

        /// <summary>Поправка синхронизации звука: кнопки «-» и «+» по 10 мс, от -200 до +200.</summary>
        private void BuildSyncControls(Transform row, SaveData save)
        {
            Text value = UiKit.Label("Value", row, "", 36, UiKit.Cream, TextAnchor.MiddleCenter, false);
            UiKit.Center(value.rectTransform, 300f, 0f, 150f, 50f);
            Action refresh = delegate
            {
                int ms = Mathf.RoundToInt(save.audioOffsetMs);
                value.text = (ms > 0 ? "+" : "") + ms + " " + Loc.T("ms");
            };
            Action<float> change = delegate(float delta)
            {
                save.audioOffsetMs = Mathf.Clamp(save.audioOffsetMs + delta, -200f, 200f);
                App.Conductor.SetUserOffsetMs(save.audioOffsetMs);
                refresh();
            };
            StepButton(row, 185f, "-", delegate { change(-10f); });
            StepButton(row, 415f, "+", delegate { change(10f); });
            refresh();
        }

        private static void StepButton(Transform row, float centerX, string symbol, Action onClick)
        {
            Image back = UiKit.Img("Step", row, "ui/circle");
            back.color = new Color(1f, 1f, 1f, 0.16f);
            UiKit.Center(back.rectTransform, centerX, 0f, 76f, 76f);
            Text label = UiKit.Label("Symbol", back.transform, symbol, 56, UiKit.Gold, TextAnchor.MiddleCenter, false);
            UiKit.Center(label.rectTransform, 0f, 4f, 76f, 76f);
            UiKit.MakeButton(back, onClick);
        }

        private static void PillText(Transform row, string value)
        {
            Text text = UiKit.FitLabel("Value", row, value, 34, UiKit.Cream, TextAnchor.MiddleCenter, false);
            UiKit.Center(text.rectTransform, 233f, 0f, 270f, 50f);
        }

        private static void RightText(Transform row, string value)
        {
            Text text = UiKit.FitLabel("Value", row, value, 34, UiKit.Muted, TextAnchor.MiddleRight, false);
            UiKit.Center(text.rectTransform, 250f, 0f, 380f, 50f);
        }

        private static void MakeSlider(Transform parent, float centerX, float centerY, float width, float value, Action<float> onChanged)
        {
            RectTransform root = UiKit.Rect("Slider", parent);
            UiKit.Center(root, centerX, centerY, width, 64f);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            Image track = UiKit.SlicedImg("Track", root, "ui/pill", 30f, new Color(1f, 1f, 1f, 0.18f));
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 16f);
            track.rectTransform.anchoredPosition = Vector2.zero;

            RectTransform fillArea = UiKit.Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 16f);
            fillArea.anchoredPosition = Vector2.zero;
            Image fill = UiKit.SlicedImg("Fill", fillArea, "ui/pill", 30f, UiKit.Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = UiKit.Rect("HandleArea", root);
            // Slider сам растягивает ручку по высоте области, поэтому высота области равна высоте ручки
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.sizeDelta = new Vector2(-56f, 62f);
            handleArea.anchoredPosition = Vector2.zero;
            Image handle = UiKit.Img("Handle", handleArea, "ui/knob");
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(62f, 0f);
            handle.rectTransform.anchoredPosition = Vector2.zero;

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = value;
            slider.onValueChanged.AddListener(delegate(float v) { onChanged(v); });
        }

        private static void MakeToggle(Transform parent, float centerX, float centerY, bool value, Action<bool> onChanged)
        {
            Image pill = UiKit.SlicedImg("Toggle", parent, "ui/pill", 30f, Color.white);
            UiKit.Center(pill.rectTransform, centerX, centerY, 132f, 68f);
            Image knob = UiKit.Img("Knob", pill.transform, "ui/knob");
            UiKit.Center(knob.rectTransform, 0f, 0f, 58f, 58f);
            bool state = value;
            Color onColor = UiKit.Gold;
            Color offColor = new Color(0.42f, 0.36f, 0.5f, 1f);
            Action<bool> apply = delegate(bool on)
            {
                pill.color = on ? onColor : offColor;
                knob.rectTransform.anchoredPosition = new Vector2(on ? 31f : -31f, 0f);
            };
            apply(state);
            UiKit.MakeButton(pill, delegate
            {
                state = !state;
                apply(state);
                onChanged(state);
            });
        }
    }
}
