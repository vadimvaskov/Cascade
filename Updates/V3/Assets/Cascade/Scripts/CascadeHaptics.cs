using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
namespace Cascade.Prototype
{
    public static class CascadeHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CascadeLightImpact();
#endif
        public static void Tick()
        {
#if UNITY_IOS && !UNITY_EDITOR
            CascadeLightImpact();
#elif UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var vibrator=activity.Call<AndroidJavaObject>("getSystemService","vibrator"))
            using(var effect=new AndroidJavaClass("android.os.VibrationEffect"))
            using(var tick=effect.CallStatic<AndroidJavaObject>("createOneShot",10L,35))
                vibrator.Call("vibrate",tick);
#endif
        }
    }
}
