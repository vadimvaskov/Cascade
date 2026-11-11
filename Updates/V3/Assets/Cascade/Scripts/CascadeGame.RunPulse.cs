using UnityEngine;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        CascadeRunPulse runPulse;
        CascadeBridgeLightPool bridgeLights;
        public CascadeRunPulse RunPulse=>runPulse;
        void BuildApprovedLighting()
        {
            if(!rasterRoot||!rasterFramePulse)return;
            // Broad halo under the opaque board, separate from its thin gold rim.
            var reference=rasterFramePulse.rectTransform;
            frameGlow=NewGlow("Approved board breathing halo",reference.parent,Vector2.zero,reference.sizeDelta,
                rasterFramePulse.radius,new Color(1,.55f,.24f),new Color(1,.22f,.86f));
            var frame=frameGlow.rectTransform;
            frame.anchorMin=reference.anchorMin;frame.anchorMax=reference.anchorMax;
            frame.pivot=reference.pivot;frame.anchoredPosition=reference.anchoredPosition;
            frameGlow.softness=22;
            var board=reference.parent.Find("Approved board 6 x 6");
            frame.SetSiblingIndex(board?board.GetSiblingIndex():reference.GetSiblingIndex());

            // Physical screen perimeter, above the opaque world viewport. Its centre
            // is transparent and neither aura can intercept taps or cover a menu.
            screenGlow=NewGlow("Approved screen breathing aura",rasterRoot.parent,Vector2.zero,Vector2.one,0,Color.white,Color.white);
            screenGlow.screenAura=true;
            var screen=screenGlow.rectTransform;screen.anchorMin=Vector2.zero;screen.anchorMax=Vector2.one;
            screen.offsetMin=screen.offsetMax=Vector2.zero;screen.SetSiblingIndex(rasterRoot.GetSiblingIndex()+1);
        }
        void UpdateApprovedLighting(bool active,bool frozen)
        {
            // Same calibrated DSP position as the visible guide. Muting does not
            // stop this clock, and sixteenth taps cannot speed up the light breath.
            runPulse?.Tick(Position-offsetMs/1000d,Time.unscaledDeltaTime,Streak,balance.hitsPerStep,active,frozen,reduced);
            float wave=runPulse?.Wave??0,gain=runPulse?.UiGain??.2f;
            lightStrength=gain;
            UpdateRasterPulse(wave,gain,reduced);
            float growth=runPulse?.Progress??0;
            float boardGain=reduced?Mathf.Lerp(.15f,.5f,growth):Mathf.Lerp(.28f,.95f,growth);
            float screenGain=reduced?Mathf.Lerp(.08f,.22f,growth):Mathf.Lerp(.16f,.42f,growth);
            if(frameGlow)frameGlow.canvasRenderer.SetAlpha(wave*boardGain);
            if(screenGlow)screenGlow.canvasRenderer.SetAlpha(wave*screenGain);
            foreach(var view in views)view.Animate(effectClock,wave,gain,reduced,frozen);
            preview.Animate(effectClock,wave,gain,reduced,frozen);
            if(!bridgeLights&&dreamWorld)bridgeLights=dreamWorld.GetComponent<CascadeBridgeLightPool>();
            if(bridgeLights)bridgeLights.PresentRhythm(runPulse?.Value??0);
        }
    }
}
