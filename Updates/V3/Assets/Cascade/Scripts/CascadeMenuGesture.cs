using UnityEngine;
using UnityEngine.EventSystems;
namespace Cascade.Prototype
{
    public sealed class CascadeMenuGesture:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler,IInitializePotentialDragHandler
    {
        public CascadeMenuUI owner;
        int pointer;Vector2 start,last;double started,lastTime;float velocity;bool tracking,dragged;
        public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData e)
        {
            if(tracking||owner.Busy||e.button!=PointerEventData.InputButton.Left)return;
            pointer=e.pointerId;tracking=true;dragged=false;start=last=owner.ReferencePoint(e.position);started=lastTime=Time.unscaledTimeAsDouble;velocity=0;
        }
        public void OnDrag(PointerEventData e)
        {
            if(!tracking||pointer!=e.pointerId)return;Vector2 p=owner.ReferencePoint(e.position);double now=Time.unscaledTimeAsDouble;
            dragged|=(p-start).magnitude>=12;
            if(now-lastTime>.001)velocity=(p.x-last.x)/(float)(now-lastTime);
            last=p;lastTime=now;
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(!tracking||pointer!=e.pointerId)return;tracking=false;Vector2 p=owner.ReferencePoint(e.position);float dx=p.x-start.x,dy=p.y-start.y;
            dragged|=(p-start).magnitude>=12;
            if(dragged)
            {
                float speed=Time.unscaledTimeAsDouble-lastTime<.1?velocity:0;
                if(Mathf.Abs(dx)>Mathf.Abs(dy)&&(Mathf.Abs(dx)>=80||Mathf.Abs(speed)>=600))owner.Step(dx<0?1:-1);
            }
            else if(new Rect(230,565,481,481).Contains(p))owner.OpenTracks();
        }
        void OnDisable(){tracking=false;}
    }
    public sealed class CascadeMenuPress:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        public RectTransform visual;
        float from=1,target=1,at,duration=.12f;
        void Set(float value,float seconds){from=visual==null?1:visual.localScale.x;target=value;at=Time.unscaledTime;duration=seconds;}
        public void OnPointerDown(PointerEventData e){if(GetComponent<UnityEngine.UI.Button>()?.interactable==true)Set(.97f,.08f);}
        public void OnPointerUp(PointerEventData e){Set(1,.12f);}
        public void OnPointerExit(PointerEventData e){Set(1,.12f);}
        void Update(){if(visual!=null)visual.localScale=Vector3.one*Mathf.Lerp(from,target,Mathf.SmoothStep(0,1,(Time.unscaledTime-at)/duration));}
        void OnDisable(){if(visual!=null)visual.localScale=Vector3.one;target=from=1;}
    }
}
