using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Cascade.Prototype;

public static class CascadeChecks
{
    static void Check(bool ok,string name) { if (!ok) throw new Exception("FAILED: " + name); }
    [MenuItem("Cascade/Run model checks")]
    public static string Run()
    {
        var passed=new List<string>();var cfg=new Balance();
        var f=new int[36]; f[8]=1;f[13]=2;f[15]=3;f[20]=1;f[7]=2;
        var b=new Board(cfg,1,f);var t=b.Place(14);
        Check(t.afterPlacement[8]==2&&t.afterPlacement[13]==3&&t.afterPlacement[15]==4&&t.afterPlacement[20]==2&&t.afterPlacement[7]==2&&t.afterPlacement[14]==1,"G01 side neighbours");passed.Add("G01");
        f=new int[36];f[8]=4; b=new Board(cfg,1,f);t=b.Place(14);Check(t.destroyed==1&&t.waves.Count==1,"G02 threshold");passed.Add("G02");
        f=new int[36];f[14]=3;f[8]=4;f[13]=4;b=new Board(cfg,1,f);t=b.Place(7);
        Check(t.waves.Count==2&&t.waves[0].SequenceEqual(new[]{8,13})&&t.waves[1].SequenceEqual(new[]{14})&&t.destroyed==3,"G03 simultaneous impulses");passed.Add("G03");
        Check(t.autoCell>=0&&t.final.Count(x=>x>0)==2&&t.nextCharge==b.NextCharge&&t.points==160,"G04 one passive auto");passed.Add("G04");
        f=Enumerable.Repeat(1,36).ToArray();f[0]=0;f[1]=4;b=new Board(cfg,1,f);t=b.Place(0);Check(t.waves.Count==1&&t.full,"G05 cascade before full check then passive auto refills");passed.Add("G05");
        f=Enumerable.Repeat(1,36).ToArray();f[0]=f[35]=0;b=new Board(cfg,1,f);t=b.Place(0);Check(t.autoCell==35&&t.full&&t.points==10,"G06 last auto");passed.Add("G06");
        int next=b.NextCharge;var snapshot=(int[])b.cells.Clone();Check(b.Place(0)==null&&b.NextCharge==next&&b.cells.SequenceEqual(snapshot),"G07 invalid input");passed.Add("G07");
        var a=new Board(cfg,87);var c=new Board(cfg,87);for(int i=0;i<15;i++) { Check(a.cells.SequenceEqual(c.cells),"G08 repeat seed");int j=Array.IndexOf(a.cells,0);if(j<0)break;var at=a.Place(j);var ct=c.Place(j);Check(at.final.SequenceEqual(ct.final)&&at.waves.Count==ct.waves.Count,"G08 repeat waves"); }passed.Add("G08");
        var map=new BeatMap {duration=100,beats=Enumerable.Range(1,60).Select(x=>(double)x).ToArray()};
        Check(new Rhythm(map,cfg).Evaluate(.88,0).kind=="hit"&&new Rhythm(map,cfg).Evaluate(1.12,0).kind=="hit"&&new Rhythm(map,cfg).Evaluate(.879,0).kind=="miss"&&new Rhythm(map,cfg).Evaluate(1.121,0).kind=="miss","R01 boundaries");passed.Add("R01");
        var r=new Rhythm(map,cfg);r.Evaluate(1,0);var repeat=r.Evaluate(1.05,0);Check(repeat.kind=="repeat"&&repeat.multiplier==1&&r.Streak==1&&r.Hits==1,"R02 repeated beat");passed.Add("R02");
        r=new Rhythm(map,cfg);for(int i=1;i<=8;i++){var rr=r.Evaluate(i,0);if(i==4)Check(110*rr.multiplier==220,"R03 fourth");if(i==8)Check(110*rr.multiplier==330,"R03 eighth");}passed.Add("R03");
        r.Evaluate(8.4,0);Check(r.Streak==0&&r.Multiplier==1,"R04 miss");passed.Add("R04");
        r=new Rhythm(map,cfg);r.Evaluate(1,0);r.Advance(10.999);Check(r.Streak==1,"R05 before timeout");r.Evaluate(11,0);Check(r.Streak==1,"R05 exact timeout then hit");passed.Add("R05");
        var rr6=new Rhythm(map,cfg).Evaluate(10.08,.08);Check(rr6.kind=="hit"&&Math.Abs(rr6.error)<1e-8,"R06 offset sign");passed.Add("R06");
        r=new Rhythm(map,cfg);r.Evaluate(1,0);b=new Board(cfg,3);b.Place(Array.IndexOf(b.cells,0));Check(r.Hits==1&&r.Streak==1,"R07 automatic not rhythm");passed.Add("R07");
        var ex=new BeatMap {duration=100,beats=new[]{1.0,20.0,21.0},excluded=new[]{new Exclusion{start=2,end=20}}};r=new Rhythm(ex,cfg);r.Evaluate(1,0);var er=r.Evaluate(10,0);r.Advance(20);Check(er.kind=="excluded"&&r.Streak==1,"excluded active timeout");passed.Add("Exclusions");
        for(int seed=1;seed<=100;seed++) { b=new Board(cfg,seed);for(int turn=0;turn<30;turn++) {int j=Array.IndexOf(b.cells,0);if(j<0)break;t=b.Place(j);Check(b.cells.All(x=>x>=0&&x<=4),"stable board");} }passed.Add("100 seeded simulations");
        string result="PASS: "+string.Join(", ",passed);Debug.Log(result);return result;
    }
}
