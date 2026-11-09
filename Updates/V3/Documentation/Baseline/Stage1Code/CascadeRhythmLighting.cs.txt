using System;
using System.Linq;
using UnityEngine;

namespace Cascade.Prototype
{
    [Serializable] public sealed class CascadeLightingConfig
    {
        [Serializable] public class Knot { public int streak;public float[] gain; }
        public Knot[] peak_gain_knots;
        public int streak_cap_for_visuals=16,rise_transition_ms=120,reset_fade_ms=120;
    }
    // Shared song clock for phase; unscaled UI clock only for fades. No per-orb clocks or coroutines.
    public sealed class CascadeRhythmLighting
    {
        readonly CascadeLightingConfig config;
        readonly double[] peaks;
        Vector4 from,target,current,lastOutput,hiddenFrom;
        int streak=-1,localCell=-1;
        double gainAt,visibilityAt,localAt=-100;
        bool visible;
        float visibilityFrom;
        public Vector4 Output { get; private set; }
        public int PeakStride { get; }
        public double[] VisualPeaks => (double[])peaks.Clone();
        public CascadeRhythmLighting(BeatMap map,CascadeLightingConfig config)
        {
            this.config=config;
            if(config?.peak_gain_knots==null||config.peak_gain_knots.Length<2)throw new ArgumentException("Missing lighting knots");
            int stride=1;
            for(;stride<map.beats.Length;stride++)
            {
                bool valid=true;double previous=double.NegativeInfinity;
                for(int i=0;i<map.beats.Length;i+=stride)
                {if(map.IsExcluded(map.beats[i]))continue;if(map.beats[i]-previous<1.0/3-1e-9){valid=false;break;}previous=map.beats[i];}
                if(valid)break;
            }
            PeakStride=stride;
            peaks=map.beats.Where((t,i)=>i%stride==0&&!map.IsExcluded(t)).ToArray();
        }
        public Vector4 Gains(int count)
        {
            count=Mathf.Clamp(count,0,config.streak_cap_for_visuals);
            var knots=config.peak_gain_knots;
            for(int i=1;i<knots.Length;i++)if(count<=knots[i].streak)
                return Vector4.Lerp(V(knots[i-1].gain),V(knots[i].gain),Mathf.InverseLerp(knots[i-1].streak,knots[i].streak,count));
            return V(knots[knots.Length-1].gain);
        }
        static Vector4 V(float[] a)=>new Vector4(a[0],a[1],a[2],a[3]);
        static float Smooth(double value)=>Mathf.SmoothStep(0,1,Mathf.Clamp01((float)value));
        public static float Envelope(double time,double beat,double attack,double decay)
        {double d=time-beat;return d<0?Smooth(1+d/attack):1-Smooth(d/decay);}
        public float Pulse(double time,bool screen=false)
        {
            int index=Array.BinarySearch(peaks,time);if(index<0)index=~index;
            float value=0;double attack=screen?.065:.045,decay=screen?.220:.160;
            // MAX of envelopes; include upcoming attack, previous decays, never a missed-beat queue.
            for(int i=Math.Max(0,index-2);i<Math.Min(peaks.Length,index+2);i++)value=Mathf.Max(value,Envelope(time,peaks[i],attack,decay));
            return value;
        }
        public void LocalHit(int cell,double uiTime){localCell=cell;localAt=uiTime;}
        public float Local(int cell,double uiTime,bool soft,bool active)
            =>active&&cell==localCell?(soft?.15f:.25f)*(1-Smooth((uiTime-localAt)/.120)):0;
        public Vector4 Tick(double songTime,double uiTime,int newStreak,bool active,bool soft)
        {
            current=Vector4.Lerp(from,target,Smooth((uiTime-gainAt)/(config.rise_transition_ms/1000.0)));
            if(newStreak!=streak){streak=newStreak;from=current;target=Gains(newStreak);gainAt=uiTime;}
            bool show=active&&!soft;
            float gate=visible?Mathf.Lerp(visibilityFrom,1,Smooth((uiTime-visibilityAt)/.120)):0;
            if(show!=visible)
            {
                hiddenFrom=lastOutput;visibilityFrom=gate;visibilityAt=uiTime;visible=show;
                if(show)visibilityFrom=0;
            }
            if(!visible)Output=hiddenFrom*(1-Smooth((uiTime-visibilityAt)/(config.reset_fade_ms/1000.0)));
            else
            {
                gate=Mathf.Lerp(visibilityFrom,1,Smooth((uiTime-visibilityAt)/.120));
                float p=Pulse(songTime);Output=new Vector4(current.x*p,current.y*p,current.z*p,Mathf.Min(.4f,current.w*Pulse(songTime,true)))*gate;
            }
            if(soft)Output=Vector4.zero;
            lastOutput=Output;return Output;
        }
    }
}
