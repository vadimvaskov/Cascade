using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    public static class CascadeApprovedUI
    {
        public const float Width=414,Height=850;
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Color C(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return c;}
        public static Font Font(bool bold=false)=>Resources.Load<Font>("Approved/Fonts/Nunito-"+(bold?"Bold":"Regular"))??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        public static Text Text(string name,Transform p,string value,float x,float y,float w,float h,int size=16,bool bold=true,string color="#FFF1D8",TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=Font(bold);t.text=value;t.fontSize=size;t.color=C(color);
            t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        public static Sprite Sprite(string key)
        {
            if(sprites.TryGetValue(key,out var s)&&s)return s;
            string file=key;Rect? crop=null;
            if(key=="fa")file="fa-pair";
            string[] menu={"Map","Events","Collection","Daily"},settings={"Headphones","Speaker","Gear","Haptic"};
            int index=Array.IndexOf(menu,key);if(index>=0)file="menu-icons";else{index=Array.IndexOf(settings,key);if(index>=0)file="settings-icons";}
            var tex=Resources.Load<Texture2D>("Approved/"+file);if(!tex)return null;
            if(index>=0)crop=new Rect((index%2)*tex.width/2f,(1-index/2)*tex.height/2f,tex.width/2f,tex.height/2f);
            if(key=="fa")crop=new Rect(0,0,tex.width/2f,tex.height);
            s=UnityEngine.Sprite.Create(tex,crop??new Rect(0,0,tex.width,tex.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
            s.name=key;sprites[key]=s;return s;
        }
        public static Image Image(string name,Transform p,string key,float x,float y,float w,float h,bool fit=true)
        {
            var i=Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>();i.sprite=Sprite(key);i.preserveAspect=fit;i.raycastTarget=false;return i;
        }
        public static CascadePanelGraphic Panel(string name,Transform p,float x,float y,float w,float h,string top="#B483CB",string bottom="#4B315F",float radius=17,bool border=true)
        {
            var g=Rect(name,p,x,y,w,h).gameObject.AddComponent<CascadePanelGraphic>();g.color=C(top);g.rightColor=C(bottom);g.verticalGradient=true;g.radius=radius;g.raycastTarget=false;
            if(border)Border(g.transform,w,h,"#EDC28A",radius,1.5f);return g;
        }
        public static void Border(Transform p,float w,float h,string color,float radius,float thickness)
        {
            var g=Rect("Border",p,0,0,w,h).gameObject.AddComponent<CascadePanelGraphic>();g.shape=CascadePanelGraphic.Shape.Border;g.color=g.rightColor=C(color);g.radius=radius;g.border=thickness;g.raycastTarget=false;
        }
        public static CascadePanelGraphic Glass(string name,Transform p,float x,float y,float w,float h,float radius=22,bool gold=false,bool dialog=false)
        {
            var g=Panel(name,p,x,y,w,h,gold?"#FFF4D5E8":dialog?"#795D9FD9":"#C9AFE076",gold?"#E8AA51D9":dialog?"#291D40E8":"#503363BA",radius,false);
            g.edgeFeather=.65f;
            GlassHighlights(g.rectTransform,gold);
            return g;
        }
        public static void GlassHighlights(RectTransform rect,bool gold=false)
        {
            float w=rect.rect.width,h=rect.rect.height,radius=rect.GetComponent<CascadePanelGraphic>().radius;
            var sheen=Panel("Glass reflection",rect,2,2,w-4,Mathf.Min(h*.42f,82),gold?"#FFFFFF4A":"#FFFFFF21","#FFFFFF00",Mathf.Max(0,radius-2),false);
            sheen.edgeFeather=.6f;
            sheen.rectTransform.anchorMax=Vector2.one;sheen.rectTransform.sizeDelta=new Vector2(-4,Mathf.Min(h*.42f,82));
            Border(rect,w,h,gold?"#FFF7DEA6":"#F3DCFF99",radius,1.1f);
            var rim=rect.Find("Border").GetComponent<CascadePanelGraphic>();rim.rightColor=C(gold?"#FFD7A14D":"#E8C99C55");rim.verticalGradient=true;rim.edgeFeather=.55f;
            rim.rectTransform.anchorMin=Vector2.zero;rim.rectTransform.anchorMax=Vector2.one;rim.rectTransform.pivot=Vector2.one*.5f;rim.rectTransform.anchoredPosition=Vector2.zero;rim.rectTransform.sizeDelta=Vector2.zero;
            var glint=Panel("Glass edge glint",rect,radius,1.5f,Mathf.Max(10,w-radius*2),1,gold?"#FFFFFFB0":"#FFFFFF75","#FFFFFF00",.5f,false);
            glint.rectTransform.anchorMax=Vector2.one;glint.rectTransform.sizeDelta=new Vector2(-radius*2,1);
        }
        public static Button Hit(string name,Transform p,float x,float y,float w,float h,Action action,bool enabled=true)
        {
            var i=Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>();i.color=Color.clear;i.raycastTarget=true;
            var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;b.transition=Selectable.Transition.None;
            var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;b.interactable=enabled;if(action!=null)b.onClick.AddListener(()=>action());return b;
        }
        public static Button Button(string name,Transform p,string label,float x,float y,float w,float h,Action action,bool gold=false,int size=18)
        {
            var plate=Glass(name+" surface",p,x,y,w,h,gold?24:17,gold);
            Text(name+" label",plate.transform,label,8,0,w-16,h,size,true,gold?"#5B342E":"#FFF1D8",TextAnchor.MiddleCenter);
            var b=Hit(name,p,x,y,w,h,action);b.gameObject.AddComponent<CascadeMenuPress>().visual=plate.rectTransform;return b;
        }
        public static void Cover(Image image,float w,float h)
        {
            if(!image.sprite)return;var s=image.sprite.rect.size;float scale=Mathf.Max(w/s.x,h/s.y);
            image.rectTransform.sizeDelta=s*scale;image.rectTransform.anchoredPosition=new Vector2((w-s.x*scale)/2,-(h-s.y*scale)/2);image.preserveAspect=false;
        }
    }
}
