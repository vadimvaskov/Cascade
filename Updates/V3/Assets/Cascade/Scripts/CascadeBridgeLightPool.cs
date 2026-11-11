using UnityEngine;

namespace Cascade.Prototype
{
    // Lights travel with the recycled bridge sections. Only the nearest eight are active.
    // No light or scene object is created while the runner is moving.
    public sealed class CascadeBridgeLightPool : MonoBehaviour
    {
        public Light[] lamps;
        public Transform viewer;
        public int maximumActive=8;
        float nextUpdate;
        float[] fullIntensity;
        static readonly int PulseId=Shader.PropertyToID("_CascadeWorldPulse");
        public float RhythmPulse { get; private set; }
        public int ActiveCount { get; private set; }
        public void PresentRhythm(float value)
        {RhythmPulse=Mathf.Clamp(value,0,.5f);Shader.SetGlobalFloat(PulseId,RhythmPulse);}
        void OnEnable(){nextUpdate=0;PresentRhythm(0);Refresh();}
        void OnDisable(){PresentRhythm(0);}
        void LateUpdate()
        {
            if(Time.unscaledTime>=nextUpdate){nextUpdate=Time.unscaledTime+.1f;Refresh();}
            if(lamps==null||!viewer||fullIntensity==null)return;
            for(int i=0;i<lamps.Length;i++)
            {
                if(!lamps[i]||!lamps[i].enabled)continue;
                float z=lamps[i].transform.position.z-viewer.position.z;
                float fade=Mathf.SmoothStep(0,1,(z+3)/4)*Mathf.SmoothStep(0,1,(26-z)/4);
                lamps[i].intensity=fullIntensity[i]*fade*(1+RhythmPulse);
            }
        }
        void Refresh()
        {
            if(lamps==null||!viewer)return;ActiveCount=0;
            if(fullIntensity==null||fullIntensity.Length!=lamps.Length)
            {
                fullIntensity=new float[lamps.Length];
                for(int i=0;i<lamps.Length;i++)if(lamps[i])fullIntensity[i]=lamps[i].intensity;
            }
            for(int i=0;i<lamps.Length;i++)if(lamps[i])lamps[i].enabled=false;
            for(int selected=0;selected<maximumActive;selected++)
            {
                Light best=null;float bestDistance=float.PositiveInfinity;
                foreach(var lamp in lamps)
                {
                    if(!lamp||lamp.enabled)continue;
                    float z=lamp.transform.position.z-viewer.position.z;
                    if(z< -3||z>26)continue;
                    float d=(lamp.transform.position-viewer.position).sqrMagnitude;
                    if(d<bestDistance){best=lamp;bestDistance=d;}
                }
                if(!best)break;best.enabled=true;ActiveCount++;
            }
        }
    }
}
