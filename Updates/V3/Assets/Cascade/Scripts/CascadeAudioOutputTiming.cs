using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cascade.Prototype
{
    // DSP time describes the mixer, not when its queued samples reach the speaker.
    // This estimate affects presentation only; input calibration and scoring keep their clock.
    [Serializable]
    public sealed class CascadeAudioOutputTiming
    {
        public string source = "editor-uncompensated";
        public int sampleRate, bufferLength, bufferCount;
        public double outputLatency, ioBufferDuration, visualDelay;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int CascadeReadAudioOutputTiming(out double outputLatency, out double ioBufferDuration);
#endif

        public static double Estimate(int frames, int buffers, int rate, double hardware, double io)
        {
            double queue = frames > 0 && buffers > 0 && rate > 0 ? (double)frames * (buffers - 1) / rate : 0;
            return Math.Min(2, Valid(queue) + Valid(hardware) + Valid(io));
        }

        static double Valid(double value) => double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? 0 : Math.Min(2, value);
        public double SongTime(double mixerTime) => mixerTime - visualDelay;

        public static CascadeAudioOutputTiming Capture()
        {
            var timing = new CascadeAudioOutputTiming { sampleRate = AudioSettings.outputSampleRate };
            AudioSettings.GetDSPBufferSize(out timing.bufferLength, out timing.bufferCount);
#if UNITY_IOS && !UNITY_EDITOR
            timing.source = "ios-unity-queue-estimate";
            try
            {
                if (CascadeReadAudioOutputTiming(out double output, out double io) != 0)
                {
                    timing.outputLatency = Valid(output);
                    timing.ioBufferDuration = Valid(io);
                    timing.source = "ios-session-and-unity-queue-estimate";
                }
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            timing.visualDelay = Estimate(timing.bufferLength, timing.bufferCount, timing.sampleRate, timing.outputLatency, timing.ioBufferDuration);
#endif
            return timing;
        }
    }
}
