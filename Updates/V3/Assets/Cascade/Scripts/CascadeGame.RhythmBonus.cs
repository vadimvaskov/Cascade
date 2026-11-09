using UnityEngine;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        RhythmBonus rhythmBonus;
        public RhythmBonus Bonus=>rhythmBonus;
        void ResetRhythmBonus(){rhythmBonus=useV3&&balance.TimedRhythmBonus?new RhythmBonus(beatMap,balance):null;}
        void RefreshRhythmBonusHud()
        {
            if(rhythmBonus==null)return;
            var view=rhythmBonus.View(Position,offsetMs/1000d);
            string prefix=Streak>0?"СЕРИЯ "+Streak+" · ":"";
            streakText.text=prefix+(view.remaining>0
                ?"+2 · "+(view.pending?(view.remaining==1?"СЛЕДУЮЩАЯ ":"СЛЕДУЮЩИЕ "):(view.remaining==1?"ОСТАЛАСЬ ":"ОСТАЛОСЬ "))+view.remaining+" "+(view.remaining==1?"ДОЛЯ":"ДОЛИ")
                :"ЗАРЯД +2 · "+view.charge+"/"+balance.rhythmBonusHits);
            streakText.color=view.remaining>0?new Color(1,.84f,.48f):new Color(.76f,.64f,.86f);
        }
    }
}
