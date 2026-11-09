using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    // One independently movable image per cell. Supplied pixels are never trimmed or tinted.
    public sealed class CascadeRasterOrb : MonoBehaviour
    {
        public Image image;
        public RectTransform content;
        public Sprite[] sprites;
        public Vector4[] rects;
        public float sizeMultiplier = 1;
        public CascadePanelGraphic rim, burst;
        double popAt = -100, burstAt = -100;
        Color burstColor;
        int charge;

        public void ResetEffects()
        {
            popAt=burstAt=-100;content.localScale=Vector3.one;
            if(burst!=null){burst.gameObject.SetActive(false);burst.rectTransform.localScale=Vector3.one;}
        }

        public void SetCharge(int value, double clock, bool animate)
        {
            charge = Mathf.Clamp(value,0,4);
            image.gameObject.SetActive(charge>0);
            if(charge==0)return;
            image.sprite=sprites[charge-1]; image.color=Color.white;
            Vector4 r=rects[charge-1]*sizeMultiplier;
            image.rectTransform.anchoredPosition=new Vector2(r.x,-r.y);
            image.rectTransform.sizeDelta=new Vector2(r.z,r.w);
            if(animate)popAt=clock;
            else {popAt=-100;content.localScale=Vector3.one;}
        }
        public void Explode(double clock,bool reduced,int originCharge=0)
        {
            burstAt=clock;burstColor=CascadeGame.Palette(originCharge>0?originCharge:Mathf.Max(1,charge));
            if(burst!=null)burst.gameObject.SetActive(true);
        }
        public void Animate(double clock,float beat,float light,bool reduced,bool paused)
        {
            float age=(float)(clock-popAt);
            float pop=!reduced&&age>=0&&age<.18f?Mathf.Sin(age/.18f*Mathf.PI)*.1f:0;
            content.localScale=Vector3.one*(1+pop);
            // No perpetual rescaling or extra bloom: the baked artwork stays intact.
            if(rim!=null)
            {
                float strength=beat*light*(reduced?.055f:.38f);
                rim.color=new Color(1,.65f,1,strength);rim.rightColor=rim.color;
            }
            if(burst!=null&&burst.gameObject.activeSelf)
            {
                float t=(float)(clock-burstAt)/.24f;
                if(t>=1)burst.gameObject.SetActive(false);
                else
                {
                    burst.rectTransform.localScale=Vector3.one*Mathf.Lerp(.45f,1.04f,t);
                    burstColor.a=(1-t)*(reduced?.18f:.65f);burst.color=burstColor;burst.rightColor=burstColor;
                }
            }
        }
    }
}
