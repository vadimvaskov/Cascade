using UnityEngine;
using UnityEngine.AdaptivePerformance;

namespace Cascade.Prototype
{
    public static class CascadeThermalController
    {
        public const int SeriousFps = 30;
        public const float SeriousRenderScale = 0.75f;
        public const int CriticalFps = 15;

        public static bool ComplexAnimationsEnabled { get; private set; } = true;

        static IAdaptivePerformance bound;
        static WarningLevel applied = WarningLevel.NoWarning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Holder.LifecycleEvent += OnLifecycle;
            Bind(Holder.Instance);
        }

        static void OnLifecycle(IAdaptivePerformance instance, LifecycleChangeType change)
        {
            if (change == LifecycleChangeType.Created) Bind(instance);
            else Unbind(instance);
        }

        static void Bind(IAdaptivePerformance instance)
        {
            if (instance == null || bound == instance) return;
            Unbind(bound);
            bound = instance;
            instance.ThermalStatus.ThermalEvent += OnThermal;
            Apply(instance.ThermalStatus.ThermalMetrics.WarningLevel);
        }

        static void Unbind(IAdaptivePerformance instance)
        {
            if (instance == null) return;
            instance.ThermalStatus.ThermalEvent -= OnThermal;
            if (bound == instance) bound = null;
        }

        static void OnThermal(ThermalMetrics metrics) => Apply(metrics.WarningLevel);

        static void Apply(WarningLevel level)
        {
            if (level == applied && bound != null) return;
            applied = level;
            switch (level)
            {
                case WarningLevel.Throttling:
                    ComplexAnimationsEnabled = false;
                    CascadeFramePacing.Apply(CriticalFps, SeriousRenderScale);
                    break;
                case WarningLevel.ThrottlingImminent:
                    ComplexAnimationsEnabled = true;
                    CascadeFramePacing.Apply(SeriousFps, SeriousRenderScale);
                    break;
                default:
                    ComplexAnimationsEnabled = true;
                    CascadeFramePacing.Apply();
                    break;
            }
            Debug.Log("[Cascade] Thermal " + level + ", fps " + Application.targetFrameRate + ", complex animations " + ComplexAnimationsEnabled);
        }
    }
}
