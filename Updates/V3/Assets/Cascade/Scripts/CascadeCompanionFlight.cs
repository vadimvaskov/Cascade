using System;
using UnityEngine;

namespace Cascade.Prototype
{
    public static class CascadeCompanionFlight
    {
        // Faster escape, slower return. Both turnarounds have zero forward velocity.
        public static Vector3 Offset(double time,bool gentle)
        {
            float t=(float)time,phase=(float)(time%4.2)/4.2f;
            float away=phase<.36f?Mathf.SmoothStep(0,1,phase/.36f):1-Mathf.SmoothStep(0,1,(phase-.36f)/.64f);
            float amount=gentle?.65f:1;
            return new Vector3(.32f*Mathf.Sin(t*1.5f)+.07f*Mathf.Sin(t*3),
                .18f*Mathf.Sin(t*2.1f)+.055f*Mathf.Sin(t*3.8f),2.2f*away)*amount;
        }
    }
}
