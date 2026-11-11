using System;
using System.Linq;

namespace Cascade.Prototype
{
    public struct RhythmBonusView
    {
        public int charge,remaining;
        public bool pending;
    }
    // Event-time state only. Reading the HUD at a later render time never expires a delayed touch.
    public sealed class RhythmBonus
    {
        readonly BeatMap map;
        readonly Balance balance;
        readonly int[] eligible;
        readonly int ticksPerBeat;
        int charge,first=-1,count;
        double lastCharge=double.NaN,lastEvent=double.NegativeInfinity;
        public RhythmBonus(BeatMap map,Balance balance,int ticksPerBeat=1)
        {
            this.map=map??throw new ArgumentNullException(nameof(map));
            this.ticksPerBeat=Math.Max(1,ticksPerBeat);
            this.balance=balance??throw new ArgumentNullException(nameof(balance));balance.Validate();
            eligible=Enumerable.Range(0,map.beats.Length).Where(i=>!map.IsExcluded(map.beats[i])).ToArray();
        }
        bool ActiveAt(double songTime,double offset)=>first>=0&&songTime-offset<=map.HitWindowEnd(eligible[first+count-1],balance.rhythmWindow)+1e-9;
        int ChargeAt(double time)=>double.IsNaN(lastCharge)||map.Active(lastCharge,time)>=balance.streakTimeout?0:charge;
        public RhythmBonusView View(double songTime,double offset)
        {
            if(!balance.TimedRhythmBonus)return default;
            if(ActiveAt(songTime,offset))
            {
                int remaining=0;
                for(int n=first;n<first+count;n++)if(songTime-offset<=map.HitWindowEnd(eligible[n],balance.rhythmWindow)+1e-9)remaining++;
                return new RhythmBonusView{remaining=(remaining+ticksPerBeat-1)/ticksPerBeat,pending=songTime-offset<map.beats[eligible[first]]};
            }
            return new RhythmBonusView{charge=ChargeAt(songTime)};
        }
        // Call once for each accepted manual move, using its original input timestamp and judgement.
        public bool Evaluate(RhythmResult judgement,double songTime,double offset)
        {
            if(!balance.TimedRhythmBonus||double.IsNaN(songTime)||double.IsInfinity(songTime)||songTime<0||songTime>=map.duration||
                double.IsNaN(offset)||double.IsInfinity(offset)||songTime<lastEvent||judgement.kind=="invalid"||judgement.kind=="stale")return false;
            lastEvent=songTime;
            if(ActiveAt(songTime,offset))
                return judgement.kind=="hit"&&Array.BinarySearch(eligible,first,count,judgement.beat)>=0;
            if(first>=0){first=-1;count=0;charge=0;lastCharge=double.NaN;}
            charge=ChargeAt(songTime);
            if(judgement.kind=="miss"){charge=0;lastCharge=double.NaN;return false;}
            if(judgement.kind!="hit")return false;
            charge++;lastCharge=songTime;
            if(charge>=balance.rhythmBonusHits)
            {
                int index=Array.BinarySearch(eligible,judgement.beat);
                first=index>=0?index+1:~index;
                count=Math.Min(balance.rhythmBonusBeats*ticksPerBeat,eligible.Length-first);
                if(count==0)first=-1;
                charge=0;lastCharge=double.NaN;
            }
            // The charging hit itself is ordinary +1; only subsequent eligible musical beats get +2.
            return false;
        }
    }
}
