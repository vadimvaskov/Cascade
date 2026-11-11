using System;
using UnityEngine;

namespace Cascade.Prototype
{
    // A continuous quarter-note breath. Hits change amplitude, never its musical phase.
    public sealed class CascadeRunPulse
    {
        readonly double[] quarters;
        public float Progress { get; private set; }
        public float Wave { get; private set; }
        public float Value { get; private set; }
        public float UiGain=>Mathf.Lerp(.2f,1,Progress);
        public CascadeRunPulse(RhythmGrid grid){quarters=grid.Quarters;}

        public static float ProgressFor(int streak,int hitsPerStep)
            =>Mathf.Clamp01(Mathf.Max(0,streak)/(9f*Mathf.Max(1,hitsPerStep)));

        public float WaveAt(double time)
        {
            int index=Array.BinarySearch(quarters,time);if(index<0)index=~index-1;
            index=Mathf.Clamp(index,0,quarters.Length-2);
            double interval=Math.Max(.001,quarters[index+1]-quarters[index]);
            double phase=(time-quarters[index])/interval;
            // Continuous value and slope across beat boundaries; no step or flash on a tap.
            float breath=(float)(.5+.5*Math.Cos(phase*Math.PI*2));
            return breath*breath;
        }
        public void Tick(double songTime,float delta,int streak,int hitsPerStep,bool active,bool frozen,bool reduced)
        {
            if(frozen){ApplyAmplitude(reduced);return;}
            if(!active){Progress=Wave=Value=0;return;}
            float target=ProgressFor(streak,hitsPerStep);
            float duration=target<Progress?.65f:.35f;
            Progress=Mathf.MoveTowards(Progress,target,Mathf.Max(0,delta)/duration);
            Wave=WaveAt(songTime);
            ApplyAmplitude(reduced);
        }
        void ApplyAmplitude(bool reduced)=>Value=Wave*Mathf.Lerp(.10f,.50f,Progress)*(reduced?.25f:1);
    }
}
