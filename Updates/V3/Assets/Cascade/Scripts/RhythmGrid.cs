using System;
using System.Collections.Generic;
using System.Linq;

namespace Cascade.Prototype
{
    // One musical clock for judgement, bonus and anticipatory visual cues.
    // Dense detected attacks are not themselves quarter beats: follow the local
    // quarter pulse near nominal BPM before subdividing it. Source provenance stays unchanged.
    public sealed class RhythmGrid
    {
        public const int TicksPerQuarter = 4;
        public BeatMap Map { get; }
        public double[] Quarters { get; }
        public RhythmGrid(BeatMap source)
        {
            if (source == null || source.beats == null || source.beats.Length == 0)
                throw new ArgumentException("A non-empty beat map is required");
            double nominal = source.nominalBpm > 30 && source.nominalBpm < 300 ? 60 / source.nominalBpm :
                source.beats.Skip(1).Select((b,i)=>b-source.beats[i]).OrderBy(x=>x).DefaultIfEmpty(.5).ElementAt(Math.Max(0,(source.beats.Length-2)/2));
            double period=nominal;
            var quarters=new List<double>{source.beats[0]};
            while(quarters[0]-period>=0)quarters.Insert(0,quarters[0]-period);
            double at=source.beats[0];int search=1;
            while(at < source.duration)
            {
                double expected=at+period, best=expected, error=period*.22;
                while(search<source.beats.Length&&source.beats[search]<expected-error)search++;
                for(int n=search;n<source.beats.Length&&source.beats[n]<=expected+error;n++)
                {
                    double distance=Math.Abs(source.beats[n]-expected);
                    if(distance<error){error=distance;best=source.beats[n];}
                }
                period=Math.Max(nominal*.85,Math.Min(nominal*1.15,period*.88+(best-at)*.12));
                quarters.Add(best);at=best;
            }
            Quarters=quarters.ToArray();
            var ticks=new List<double>();
            for(int q=0;q+1<Quarters.Length;q++)for(int part=0;part<TicksPerQuarter;part++)
            {
                double t=Quarters[q]+(Quarters[q+1]-Quarters[q])*part/TicksPerQuarter;
                if(t>=0&&t<source.duration)ticks.Add(t);
            }
            Map=new BeatMap{trackId=source.trackId,version=source.version+"/free-16ths-v1",audioSha256=source.audioSha256,
                verified=source.verified,analysisPassed=source.analysisPassed,reviewStatus=source.reviewStatus,
                analysisMethod=source.analysisMethod,nominalBpm=source.nominalBpm,duration=source.duration,
                beats=ticks.ToArray(),excluded=source.excluded};
        }
        public int Nearest(double time)
        {
            int i=Array.BinarySearch(Map.beats,time);if(i>=0)return i;
            int right=~i;
            return right==0?0:right==Map.beats.Length?right-1:
                time-Map.beats[right-1]<=Map.beats[right]-time+1e-9?right-1:right;
        }
        public double Spacing(int tick)
        {
            if(Map.beats.Length<2)return .125;
            return tick+1<Map.beats.Length?Map.beats[tick+1]-Map.beats[tick]:Map.beats[tick]-Map.beats[tick-1];
        }
    }
}
