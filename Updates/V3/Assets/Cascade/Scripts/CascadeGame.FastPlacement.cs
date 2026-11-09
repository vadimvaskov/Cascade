using System;
using UnityEngine;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        // Only presentation is delayed. The authoritative final board is ready before the next touch.
        readonly double[] cascadeBurstAt=new double[36];
        void ClearCascadePresentation(){for(int i=0;i<cascadeBurstAt.Length;i++)cascadeBurstAt[i]=double.PositiveInfinity;}
        void PresentImmediate(Turn turn)
        {
            ClearCascadePresentation();
            for(int wave=0;wave<turn.waves.Count;wave++)
                foreach(int cell in turn.waves[wave])cascadeBurstAt[cell]=effectClock+.20*wave/Math.Max(1,turn.waves.Count);
            Render(turn.final);RefreshHud();
            PresentCascadeBursts();
            // No reaction phase, input cooldown, or queued logical moves in V3.
            pending=null;State=Phase.Playing;
            if(turn.full)Finish(false);
        }
        void PresentCascadeBursts()
        {
            for(int i=0;i<cascadeBurstAt.Length;i++)if(cascadeBurstAt[i]<=effectClock)
            {
                cascadeBurstAt[i]=double.PositiveInfinity;
                if(views[i].raster!=null)views[i].raster.Explode(effectClock,reduced,4);
                else views[i].Explode(effectClock,reduced);
            }
        }
    }
}
