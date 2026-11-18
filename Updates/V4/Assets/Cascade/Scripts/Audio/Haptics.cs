using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Cascade.Audio
{
    /// <summary>Тактильная отдача: мягкий отклик на постановку и отдельный отклик ошибки.</summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _CascadeHapticLight();

        [DllImport("__Internal")]
        private static extern void _CascadeHapticError();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static bool vibratorChecked;

        private static void Vibrate(long milliseconds)
        {
            if (!vibratorChecked)
            {
                vibratorChecked = true;
                try
                {
                    using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }
                catch (System.Exception)
                {
                    vibrator = null;
                }
            }
            if (vibrator != null)
            {
                vibrator.Call("vibrate", milliseconds);
            }
            else
            {
                Handheld.Vibrate();
            }
        }
#endif

        public static void Tap()
        {
            if (!Enabled)
            {
                return;
            }
#if UNITY_IOS && !UNITY_EDITOR
            _CascadeHapticLight();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Vibrate(12);
#endif
        }

        public static void Error()
        {
            if (!Enabled)
            {
                return;
            }
#if UNITY_IOS && !UNITY_EDITOR
            _CascadeHapticError();
#elif UNITY_ANDROID && !UNITY_EDITOR
            Vibrate(35);
#endif
        }
    }
}
