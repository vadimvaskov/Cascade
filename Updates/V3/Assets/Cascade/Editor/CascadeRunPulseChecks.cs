using System;
using System.Linq;
using Cascade.Prototype;
using UnityEngine;

public static class CascadeRunPulseChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Run pulse: "+message);}
    static void Near(float a,float b,string message)=>Check(Math.Abs(a-b)<.0001f,message);
    public static string Run()
    {
        var source=new BeatMap{duration=16,nominalBpm=120,beats=Enumerable.Range(0,32).Select(i=>i*.5).ToArray(),excluded=Array.Empty<Exclusion>()};
        var grid=new RhythmGrid(source);var pulse=new CascadeRunPulse(grid);
        var balance=new Balance{hitsPerStep=3,maxMultiplier=10};var judge=new Rhythm(grid.Map,balance);
        pulse.Tick(1,1,0,3,true,false,false);Near(pulse.Value,.10f,"base pulse exists before any hit");
        Near(pulse.WaveAt(1.25),0,"breath has a soft trough between quarters");
        float previous=pulse.Value;
        for(int i=1;i<=27;i++)
        {
            Check(judge.Evaluate(i*.125,0).kind=="hit","sixteenth accepted "+i);
            pulse.Tick(1,1,judge.Streak,3,true,false,false);
            Check(pulse.Value>previous,"each correct tap increases amplitude "+i);
            if(judge.Multiplier<10)Check(pulse.Value<.5f,"no early maximum below x10");
            Near(pulse.Wave,1,"hits do not shift pulse phase");previous=pulse.Value;
        }
        Check(judge.Multiplier==10,"maximum corresponds to actual x10 multiplier");Near(pulse.Value,.5f,"maximum amplitude at x10");
        pulse.Tick(1,1,1000,3,true,false,false);Near(pulse.Value,.5f,"longer streak stays capped");
        pulse.Tick(1.25,.01f,0,3,false,true,false);Near(pulse.Value,.5f,"pause freezes light phase and amplitude");
        pulse.Tick(1,.05f,0,3,true,false,false);Check(pulse.Value>.1f&&pulse.Value<.5f,"a miss eases down instead of snapping");
        pulse.Tick(1,1,0,3,true,false,false);Near(pulse.Value,.1f,"miss returns to base pulse, never disables it");
        pulse.Tick(1,.01f,27,3,true,false,false);Check(pulse.Value>.1f&&pulse.Value<.5f,"amplitude increase has a transition");
        pulse.Tick(1,1,27,3,true,false,true);Near(pulse.Value,.125f,"soft effects still grows to a restrained maximum");
        pulse.Tick(1.25,1,0,3,false,true,false);Near(pulse.Value,.5f,"changing effects while paused retains the frozen phase");
        source.excluded=new[]{new Exclusion{start=2,end=3}};pulse=new CascadeRunPulse(new RhythmGrid(source));
        pulse.Tick(2.5,1,0,3,true,false,false);Near(pulse.Value,.1f,"baseline breath continues through musical rests");
        pulse.Tick(2.5,1,0,3,false,false,false);Near(pulse.Value,0,"leaving a run clears the effect");
        return "PASS: continuous baseline pulse, each-hit growth to actual x10, cap, smooth reset, quarter phase, pause, soft mode and musical rests";
    }
}
