using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cascade.Prototype
{
    public static class CascadeFramePacing
    {
        public const int TargetFps = 60;
        public const float IosRenderScale = 0.9f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => Apply();

        public static void Apply() => Apply(TargetFps, IosRenderScale);

        public static void Apply(int framesPerSecond, float iosRenderScale)
        {
            // vSyncCount above 0 makes Unity ignore targetFrameRate and wait another vblank, which stutters a locked 60.
            // iOS still presents on the display vblank; this only removes that extra wait.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = framesPerSecond;
            if (Application.platform != RuntimePlatform.IPhonePlayer) return;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = iosRenderScale;
        }
    }
}
