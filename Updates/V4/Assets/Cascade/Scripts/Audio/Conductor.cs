using System;
using UnityEngine;

namespace Cascade.Audio
{
    /// <summary>
    /// Музыкальные часы. Время трека считается по аудиочасам Unity (dspTime), а не по кадрам,
    /// музыка запускается по расписанию (PlayScheduled). SongTime — это место в треке, которое игрок
    /// слышит прямо сейчас: из аудиочасов вычитается задержка вывода звука и ручная поправка из настроек.
    /// </summary>
    public sealed class Conductor : MonoBehaviour
    {
        /// <summary>Добавка к расчётной задержке: путь звука от микшера до динамика, секунды.</summary>
        private const double DeviceLatency = 0.015;

        private AudioSource source;
        private double dspStart;
        private double pausedAt;
        private bool started;
        private bool paused;
        private double clockOffset = double.MinValue;
        private double lastRealtime;
        private double bufferLatency;
        private double userOffset;

        public double Duration { get; private set; }
        public bool IsStarted { get { return started; } }
        public bool IsPaused { get { return paused; } }

        /// <summary>Полная учитываемая задержка звука, секунды.</summary>
        public double OutputLatency { get { return bufferLatency + DeviceLatency + userOffset; } }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            MeasureBufferLatency();
        }

        /// <summary>Задержка микшера: размер буфера × число буферов / частота.</summary>
        public void MeasureBufferLatency()
        {
            int bufferLength;
            int bufferCount;
            AudioSettings.GetDSPBufferSize(out bufferLength, out bufferCount);
            int sampleRate = AudioSettings.outputSampleRate;
            bufferLatency = sampleRate > 0 ? (double)bufferLength * bufferCount / sampleRate : 0.05;
            Debug.Log("[Cascade] Audio: buffer " + bufferLength + " x " + bufferCount + ", " + sampleRate + " Hz, mixer latency "
                + (bufferLatency * 1000.0).ToString("0") + " ms");
        }

        /// <summary>Ручная поправка синхронизации из настроек, миллисекунды. Больше — если игра пишет «поздно».</summary>
        public void SetUserOffsetMs(float milliseconds)
        {
            userOffset = milliseconds / 1000.0;
        }

        public void SetVolume(float volume)
        {
            source.volume = Mathf.Clamp01(volume);
        }

        public void Load(AudioClip clip)
        {
            source.Stop();
            source.clip = clip;
            started = false;
            paused = false;
            pausedAt = 0;
            Duration = clip == null ? 0 : (double)clip.samples / clip.frequency;
        }

        /// <summary>
        /// Плавные аудиочасы. dspTime меняется ступеньками (раз в буфер), поэтому между ступеньками
        /// время достраивается по системным часам.
        /// </summary>
        private double SmoothDspTime()
        {
            double realtime = Time.realtimeSinceStartupAsDouble;
            double candidate = AudioSettings.dspTime - realtime;
            if (clockOffset == double.MinValue)
            {
                clockOffset = candidate;
            }
            else
            {
                double elapsed = realtime - lastRealtime;
                if (elapsed < 0)
                {
                    elapsed = 0;
                }
                clockOffset = Math.Max(clockOffset - elapsed * 0.002, candidate);
            }
            lastRealtime = realtime;
            return realtime + clockOffset;
        }

        private void Update()
        {
            SmoothDspTime();
        }

        /// <summary>Запуск с задержкой: до старта SongTime отрицательное (идёт отсчёт).</summary>
        public void Play(double delaySeconds)
        {
            if (source.clip == null)
            {
                return;
            }
            source.timeSamples = 0;
            dspStart = AudioSettings.dspTime + delaySeconds;
            source.PlayScheduled(dspStart);
            started = true;
            paused = false;
        }

        /// <summary>Слышимое положение в треке, секунды. Отрицательное — музыка ещё не началась.</summary>
        public double SongTime
        {
            get
            {
                if (!started)
                {
                    return 0;
                }
                if (paused)
                {
                    return pausedAt;
                }
                return SmoothDspTime() - dspStart - OutputLatency;
            }
        }

        public void Pause()
        {
            if (!started || paused)
            {
                return;
            }
            pausedAt = SongTime;
            paused = true;
            source.Stop();
        }

        /// <summary>Продолжение с того же слышимого места через короткую задержку.</summary>
        public void Resume(double delaySeconds)
        {
            if (!started || !paused)
            {
                return;
            }
            paused = false;
            double startDsp = AudioSettings.dspTime + delaySeconds;
            if (pausedAt < 0)
            {
                source.timeSamples = 0;
                dspStart = startDsp - pausedAt;
                source.PlayScheduled(dspStart);
                return;
            }
            AudioClip clip = source.clip;
            int sample = (int)(pausedAt * clip.frequency);
            if (sample >= clip.samples)
            {
                sample = clip.samples - 1;
            }
            source.timeSamples = sample;
            dspStart = startDsp - (double)sample / clip.frequency;
            source.PlayScheduled(startDsp);
        }

        public void Stop()
        {
            source.Stop();
            started = false;
            paused = false;
        }
    }
}
