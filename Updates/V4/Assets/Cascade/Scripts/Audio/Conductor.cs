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
        private double lastSongTime;

        /// <summary>На сколько плавные часы могут обгонять аудиочасы. Больше — значит, аудиочасы стояли или сбросились.</summary>
        private double maxClockLead = 0.06;
        /// <summary>Допустимое расхождение между расчётным местом в треке и тем, что реально играет источник.</summary>
        private const double MaxSourceDrift = 0.2;

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
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        /// <summary>Сверить плавные часы с аудиочасами заново (после сворачивания игры или смены аудиоустройства).</summary>
        public void ResyncClock()
        {
            clockOffset = double.MinValue;
        }

        private void OnApplicationPause(bool pausedByOs)
        {
            if (!pausedByOs)
            {
                ResyncClock();
            }
        }

        /// <summary>
        /// Телефон сменил аудиоустройство (наушники, Bluetooth) или звук перезапустился: Unity останавливает
        /// все источники и обнуляет аудиочасы. Меряем задержку заново и продолжаем трек с того же места.
        /// </summary>
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            ResyncClock();
            MeasureBufferLatency();
            Debug.Log("[Cascade] Audio configuration changed, device changed: " + deviceWasChanged);
            if (started && !paused && source != null && source.clip != null)
            {
                pausedAt = lastSongTime;
                paused = true;
                Resume(0.1);
            }
        }

        /// <summary>Задержка микшера: размер буфера × число буферов / частота.</summary>
        public void MeasureBufferLatency()
        {
            int bufferLength;
            int bufferCount;
            AudioSettings.GetDSPBufferSize(out bufferLength, out bufferCount);
            int sampleRate = AudioSettings.outputSampleRate;
            bufferLatency = sampleRate > 0 ? (double)bufferLength * bufferCount / sampleRate : 0.05;
            // Аудиочасы идут ступеньками размером в один буфер; три ступеньки — запас на неровную работу телефона
            maxClockLead = Math.Max(0.06, sampleRate > 0 ? 3.0 * bufferLength / sampleRate : 0.06);
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
                // Аудиочасы стоят, пока игра свёрнута, и обнуляются при смене аудиоустройства, а системные
                // часы идут дальше. Без этой проверки игра считала бы, что трек давно закончился.
                if (clockOffset - candidate > maxClockLead)
                {
                    clockOffset = candidate;
                }
            }
            lastRealtime = realtime;
            return realtime + clockOffset;
        }

        private void Update()
        {
            double now = SmoothDspTime();
            if (!started || paused)
            {
                return;
            }
            AudioClip clip = source.clip;
            if (clip != null && source.isPlaying && source.timeSamples > 0)
            {
                // Источник — истина: если расчёт ушёл от того, что реально играет, привязываемся к источнику
                double actual = (double)source.timeSamples / clip.frequency;
                double expected = now - dspStart;
                if (Math.Abs(expected - actual) > MaxSourceDrift)
                {
                    Debug.LogWarning("[Cascade] Song clock corrected by " + ((actual - expected) * 1000.0).ToString("0") + " ms");
                    dspStart = now - actual;
                }
            }
            lastSongTime = now - dspStart - OutputLatency;
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
            lastSongTime = -delaySeconds;
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

        /// <summary>Только для проверки: сдвигает плавные часы вперёд, как будто аудиочасы стояли.</summary>
        public void DevShiftClock(double seconds)
        {
            SmoothDspTime();
            clockOffset += seconds;
        }

        public void Stop()
        {
            source.Stop();
            started = false;
            paused = false;
        }
    }
}
