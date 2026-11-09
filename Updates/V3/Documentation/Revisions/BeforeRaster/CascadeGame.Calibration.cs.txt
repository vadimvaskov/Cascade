using System;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        GameObject calibrationPanel;
        Button calibrationMenuButton, calibrationPauseButton, calibrationStartButton, calibrationApplyButton;
        Text calibrationInstruction, calibrationCount, calibrationValue;
        RectTransform calibrationTap;
        AudioSource calibrationAudio;
        AudioClip calibrationClip;
        RhythmCalibration calibrationSession;
        bool calibrationVisible, calibrationRunning;
        int calibrationCandidate;
        double calibrationStartDsp, calibrationEventAnchor, calibrationDspAnchor;

        void SetupCalibration()
        {
            calibrationMenuButton = CopyCalibrationButton("Calibrate rhythm", menu.transform, "НАСТРОЙКА РИТМА", new Vector2(0, -570), new Vector2(440, 58), OpenCalibration);
            var line = menu.transform.Find("Menu lower line");
            if (line != null) line.gameObject.SetActive(false);
            calibrationPauseButton = CopyCalibrationButton("Calibrate rhythm", modal.transform, "НАСТРОЙКА РИТМА", new Vector2(0, -445), new Vector2(540, 65), OpenCalibration);
            calibrationPauseButton.gameObject.SetActive(false);

            calibrationPanel = new GameObject("Rhythm calibration", typeof(RectTransform));
            var panel = calibrationPanel.GetComponent<RectTransform>();
            panel.SetParent(safeRoot, false); panel.sizeDelta = new Vector2(720, 1280);
            var shade = Instantiate(modal.transform.Find("Shade").gameObject, panel, false);
            shade.name = "Calibration shade";
            CopyCalibrationText(modalTitle, panel, "НАСТРОЙКА РИТМА", new Vector2(0, 420), new Vector2(660, 100), 36);
            calibrationInstruction = CopyCalibrationText(modalBody, panel, "Слушайте щелчки и касайтесь кнопки в такт.\nИспользуйте тот же динамик или наушники,\nс которыми будете играть.", new Vector2(0, 270), new Vector2(620, 180), 23);
            calibrationCount = CopyCalibrationText(modalBody, panel, "Первые два щелчка — разминка", new Vector2(0, 125), new Vector2(620, 80), 23);
            var tap = CopyCalibrationButton("Calibration tap", panel, "КАСАЙТЕСЬ\nВ ТАКТ", new Vector2(0, -30), new Vector2(380, 180), null);
            tap.GetComponentInChildren<Text>().fontSize = 30;
            calibrationTap = tap.GetComponent<RectTransform>();
            // Scoring uses the original press event, not Button.onClick on release.
            CopyCalibrationButton("Earlier correction", panel, "−10", new Vector2(-220, -205), new Vector2(110, 64), () => AdjustCalibration(-10));
            CopyCalibrationButton("Later correction", panel, "+10", new Vector2(220, -205), new Vector2(110, 64), () => AdjustCalibration(10));
            calibrationValue = CopyCalibrationText(modalBody, panel, "", new Vector2(0, -205), new Vector2(300, 64), 23);
            calibrationStartButton = CopyCalibrationButton("Start calibration", panel, "НАЧАТЬ ПРОВЕРКУ", new Vector2(0, -305), new Vector2(540, 70), StartCalibration);
            calibrationApplyButton = CopyCalibrationButton("Apply calibration", panel, "ПРИМЕНИТЬ", new Vector2(0, -400), new Vector2(540, 70), ApplyCalibration);
            CopyCalibrationButton("Close calibration", panel, "НАЗАД", new Vector2(0, -495), new Vector2(540, 65), CloseCalibration);
            calibrationAudio = gameObject.AddComponent<AudioSource>();
            calibrationAudio.playOnAwake = false; calibrationAudio.spatialBlend = 0;
            calibrationPanel.SetActive(false);
            statusText.fontSize = 24;
            statusText.rectTransform.sizeDelta = new Vector2(670, 48);
            streakText.fontSize = 16;
        }

        Text CopyCalibrationText(Text source, Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Instantiate(source, parent, false);
            label.name = text; label.text = text; label.fontSize = fontSize; label.raycastTarget = false;
            label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
            return label;
        }

        Button CopyCalibrationButton(string name, Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var source = modal.transform.Find("Settings").GetComponent<Button>();
            var button = Instantiate(source, parent, false);
            button.name = name; button.gameObject.SetActive(true); button.interactable = true;
            var rect = button.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = button.GetComponentInChildren<Text>(); text.text = label; text.fontSize = 22; text.rectTransform.sizeDelta = size - Vector2.one * 12;
            var rim = button.transform.Find("Fine glass rim") as RectTransform;
            if (rim != null) rim.sizeDelta = size;
            button.onClick = new Button.ButtonClickedEvent();
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        public void OpenCalibration()
        {
            if (State != Phase.Menu && State != Phase.Paused) return;
            calibrationVisible = true; calibrationCandidate = offsetMs;
            menu.SetActive(false); modal.SetActive(false); calibrationPanel.SetActive(true);
            calibrationCount.text = "Первые два щелчка — разминка";
            calibrationInstruction.text = "Слушайте щелчки и касайтесь кнопки в такт.\nНужно 10 ровных касаний.\nНастройка сохранится после «Применить».";
            calibrationApplyButton.interactable = true;
            RefreshCalibrationValue();
        }

        void StartCalibration()
        {
            calibrationAudio.Stop();
            if (calibrationClip == null)
            {
                int rate = AudioSettings.outputSampleRate;
                var samples = new float[rate * 16];
                int clickSamples = (int)(rate * .035);
                for (int beat = 0; beat < 16; beat++)
                    for (int i = 0; i < clickSamples; i++)
                    {
                        double t = (double)i / rate;
                        samples[beat * rate + i] = (float)(.65 * Math.Sin(2 * Math.PI * 1200 * t) * Math.Exp(-t * 110) * Math.Min(1, i / (rate * .001)));
                    }
                calibrationClip = AudioClip.Create("Cascade calibration clicks", samples.Length, 1, rate, false);
                calibrationClip.SetData(samples, 0);
            }
            calibrationSession = new RhythmCalibration();
            calibrationAudio.clip = calibrationClip; calibrationAudio.volume = volume > .01f ? volume : .7f;
            calibrationEventAnchor = InputState.currentTime; calibrationDspAnchor = AudioSettings.dspTime;
            calibrationStartDsp = calibrationDspAnchor + .4;
            calibrationAudio.PlayScheduled(calibrationStartDsp);
            calibrationRunning = true; calibrationApplyButton.interactable = false; calibrationStartButton.interactable = false;
            calibrationCount.text = "Слушайте щелчки…";
        }

        void AcceptCalibrationPointer(Vector2 point, double eventTime)
        {
            if (!calibrationRunning || !RectTransformUtility.RectangleContainsScreenPoint(calibrationTap, point)) return;
            double elapsed = calibrationDspAnchor + eventTime - calibrationEventAnchor - calibrationStartDsp;
            if (!calibrationSession.AddTap(elapsed)) return;
            calibrationCount.text = "Принято касаний: " + calibrationSession.Count + " / 10";
            if (calibrationSession.IsComplete) FinishCalibration();
        }

        void UpdateCalibration()
        {
            if (calibrationRunning && AudioSettings.dspTime >= calibrationStartDsp + 16) FinishCalibration();
        }

        void FinishCalibration()
        {
            calibrationAudio.Stop(); calibrationRunning = false; calibrationStartButton.interactable = true;
            if (calibrationSession.TryEstimate(out int estimate, out string reason))
            {
                calibrationCandidate = estimate; calibrationApplyButton.interactable = true;
                calibrationCount.text = "Готово — нажмите «Применить»";
            }
            else
            {
                calibrationApplyButton.interactable = false;
                calibrationCount.text = reason;
            }
            calibrationStartButton.GetComponentInChildren<Text>().text = "ПОВТОРИТЬ ПРОВЕРКУ";
            RefreshCalibrationValue();
        }

        void AdjustCalibration(int amount)
        {
            if (calibrationRunning) return;
            calibrationCandidate = Math.Max(-400, Math.Min(400, calibrationCandidate + amount));
            calibrationApplyButton.interactable = true; RefreshCalibrationValue();
        }

        void RefreshCalibrationValue() => calibrationValue.text = "Поправка: " + (calibrationCandidate > 0 ? "+" : "") + calibrationCandidate + " мс";

        void ApplyCalibration()
        {
            if (calibrationRunning) return;
            offsetMs = calibrationCandidate;
            PlayerPrefs.SetInt("cascade.offset", offsetMs); PlayerPrefs.Save();
            CloseCalibration();
        }

        public void CloseCalibration()
        {
            if (!calibrationVisible) return;
            calibrationAudio.Stop(); calibrationRunning = false; calibrationVisible = false;
            calibrationStartButton.interactable = true; calibrationPanel.SetActive(false);
            menu.SetActive(State == Phase.Menu); modal.SetActive(State == Phase.Paused);
        }

        void DisposeCalibration()
        {
            if (calibrationClip != null) Destroy(calibrationClip);
        }
    }
}
