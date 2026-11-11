using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeMenuUI:MonoBehaviour
    {
        const string Root="V3/Menu/";
        CascadeGame game;CascadeMenuLayout layout;CascadeRecord[] records;
        CascadeMenuCompositor compositor;
        CanvasScaler scaler;RectTransform safe,background,screen;
        Font regular,bold;
        readonly Dictionary<string,RectTransform> elements=new Dictionary<string,RectTransform>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        readonly List<RectTransform> recordGroups=new List<RectTransform>();
        public enum Page { Records,Tracks,Settings,World,Heroes,Language,Collection,Daily,Events,Information }
        public Page CurrentPage{get;private set;}
        Page settingsOrigin;
        public int SelectedRecord{get;private set;}
        public string SelectedTrackId{get;private set;}
        public bool Busy{get;private set;}
        public bool Loading{get;private set;}
        public RectTransform SafeRoot=>safe;
        public CascadeMenuScreen CurrentLayout{get;private set;}
        public IReadOnlyDictionary<string,RectTransform> Elements=>elements;
        CascadeRecord Record=>records[SelectedRecord];
        CascadeProgressStore Store=>game.ProgressStore;
        bool Open(CascadeRecord r)=>Store.IsOpen(r);
        bool Completed(CascadeRecord r)=>r.tracks.All(t=>Store.Result(t)?.completed==true);
        string error="";
        public void Initialize(CascadeGame owner)
        {
            if(owner.UsesApprovedDesign){InitializeApproved(owner);return;}
            gameObject.layer=29;game=owner;layout=JsonUtility.FromJson<CascadeMenuLayout>(Resources.Load<TextAsset>(Root+"Layout_941x1672").text);
            records=game.Catalog.records.OrderBy(r=>r.order).ToArray();
            regular=Resources.Load<Font>(Root+"Fonts/Montserrat-Regular");bold=Resources.Load<Font>(Root+"Fonts/Montserrat-Bold");
            if(regular==null||bold==null)throw new InvalidOperationException("Montserrat fonts missing");
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=11;
            scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            compositor=gameObject.AddComponent<CascadeMenuCompositor>();compositor.Initialize(canvas);
            background=Image("World background",transform,"Assets/background.png",new Rect(0,0,941,1672)).rectTransform;
            background.anchorMin=background.anchorMax=background.pivot=Vector2.one*.5f;background.anchoredPosition=Vector2.zero;
            safe=Rect("Menu safe composition",transform,new Rect(0,0,941,1672));safe.anchorMin=safe.anchorMax=safe.pivot=Vector2.one*.5f;
            ShowRecords(0);
        }
        void Update()
        {
            if(approvedDesign){UpdateApprovedLayout();return;}
            var area=Screen.safeArea;float s=Mathf.Max(.01f,Mathf.Min(area.width/941,area.height/1672));scaler.scaleFactor=s;
            safe.anchoredPosition=(area.center-new Vector2(Screen.width*.5f,Screen.height*.5f))/s;
            background.sizeDelta=new Vector2(941,1672)*Mathf.Max(Screen.width/(941*s),Screen.height/(1672*s));
        }
        public Vector2 ReferencePoint(Vector2 pixel)
        {RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,pixel,GetComponent<Canvas>().worldCamera,out var p);return new Vector2(p.x+470.5f,836-p.y);}
        public void ShowRecords(int index=-1)
        {
            StopAllCoroutines();Busy=Loading=false;error="";
            if(index>=0&&index<records.Length&&index!=SelectedRecord){SelectedRecord=index;SelectedTrackId=null;}
            gameObject.SetActive(true);CurrentPage=Page.Records;Draw();
        }
        public void OpenTracks()
        {
            if(Busy||!Open(Record))return;
            CurrentPage=Page.Tracks;if(!Record.tracks.Any(t=>t.track_id==SelectedTrackId))SelectedTrackId=Record.tracks.OrderBy(t=>t.order).First().track_id;
            error="";Draw();
        }
        public void Settings()
        {if(Busy||CurrentPage==Page.Settings)return;settingsOrigin=CurrentPage;CurrentPage=Page.Settings;Draw();}
        public void Back()
        {
            if(Busy)return;
            if(approvedDesign&&CurrentPage==Page.Language){CurrentPage=Page.Settings;Draw();return;}
            if(approvedDesign&&pauseSettings&&CurrentPage==Page.Information){CurrentPage=Page.Settings;Draw();return;}
            if(approvedDesign&&pauseSettings){gameObject.SetActive(false);pauseSettings=false;game.SetPauseSettingsVisible(false);return;}
            if(CurrentPage==Page.Settings){CurrentPage=settingsOrigin;Draw();}
            else ShowRecords();
        }
        public void LoadFailed(string message)
        {StopAllCoroutines();Busy=Loading=false;gameObject.SetActive(true);CurrentPage=Page.Tracks;error=message;Draw();}
        public void PlaySelected()
        {if(Busy||CurrentPage!=Page.Tracks||!Open(Record))return;StartCoroutine(Launch());}
        IEnumerator Launch()
        {
            Busy=Loading=true;TextValue("Primary_Button_Label","ЗАГРУЗКА…");SetButtons(false);
            yield return null; // Paint the disabled/loading state before resource preparation.
            game.StartMenuTrack(SelectedTrackId);
            if(game.State==CascadeGame.Phase.Countdown){Busy=Loading=false;gameObject.SetActive(false);}
        }
        void Draw()
        {
            if(approvedDesign){DrawApproved();return;}
            if(screen!=null){screen.gameObject.SetActive(false);Destroy(screen.gameObject);}
            elements.Clear();recordGroups.Clear();
            screen=Rect("Menu screen",safe,new Rect(0,0,941,1672));
            CurrentLayout=CurrentPage==Page.Settings?layout.screens.First(s=>s.kind=="settings"):
                layout.screens.First(s=>s.record_id==Record.record_id&&s.kind==(CurrentPage==Page.Records?"record_menu":"record_tracks")&&s.name.EndsWith(CurrentPage==Page.Records?"_open":Record.record_id));
            bool multi=CurrentPage==Page.Tracks&&Record.tracks.Length>1;
            foreach(var e in CurrentLayout.elements_back_to_front)
            {
                if(e.id=="Background_World")continue;
                if(e.id.StartsWith("Previous_Record_")||e.id.StartsWith("Next_Record_")||e.id.StartsWith("Selected_Record_"))
                {
                    if(!e.id.EndsWith("_Vinyl"))continue;
                    int index=SelectedRecord+(e.id.StartsWith("Previous")?-1:e.id.StartsWith("Next")?1:0);
                    var cover=CurrentLayout.elements_back_to_front.First(c=>c.id==e.id.Replace("_Vinyl","_Cover"));
                    var group=BuildRecord(screen,e.id.Replace("_Vinyl",""),index,cover.clip_circle.center[0],cover.clip_circle.center[1],e.width,index==SelectedRecord);
                    recordGroups.Add(group);continue;
                }
                if(multi&&(e.id.StartsWith("Track_01_")||e.id=="Track_Count"))continue;
                var rect=BuildElement(e,screen);if(rect!=null)elements[e.id]=rect;
            }
            if(CurrentPage==Page.Records)
            {
                bool open=Open(Record);TextValue("Record_Counter",(SelectedRecord+1)+" / "+records.Length);
                TextValue("Completion_Progress",open?"Пройдено "+Record.tracks.Count(t=>Store.Result(t)?.completed==true)+" из "+Record.tracks.Length:"Сначала пройди "+records.First(r=>r.record_id==Record.prerequisite_record_id).display_name);
                var progress=elements["Completion_Progress"].GetComponent<Text>();progress.color=Tint(open?"#FFF2D6":"#D7B9F0");
                if(!open){progress.fontSize=27;progress.rectTransform.anchoredPosition+=new Vector2(0,-2);}
                TextValue("Primary_Button_Label",open?"ОТКРЫТЬ ТРЕКИ":"ПЛАСТИНКА ЗАКРЫТА");
                elements["Primary_Button_Label"].GetComponent<Text>().color=Tint(open?"#FFF2D6":"#C6ACDE");
                SetImageAlpha("Primary_Button_Artwork",open?1:.33f);
                if(Completed(Record))elements["Completed_Check"]=Image("Completed_Check",screen,"Assets/UI/Completed_Check.png",new Rect(696,1200,60,60)).rectTransform;
                var gesture=Image("Carousel gesture",screen,null,new Rect(0,480,941,714));gesture.color=Color.clear;gesture.raycastTarget=true;gesture.gameObject.AddComponent<CascadeMenuGesture>().owner=this;
            }
            if(CurrentPage==Page.Tracks)
            {
                if(multi)BuildTrackList();else UpdateSingleTrack();
                if(!string.IsNullOrEmpty(error))TextAt("Load error",screen,error,470.5f,1366,22,false,"middle","#FFF2D6");
            }
            foreach(var a in CurrentLayout.actions)
            {
                if(a.id=="central_record"||a.id=="music_volume"||a.id=="reduced_effects"||(multi&&a.id.StartsWith("track_")))continue;
                bool enabled=true;
                if(a.id=="previous")enabled=SelectedRecord>0;
                if(a.id=="next")enabled=SelectedRecord<records.Length-1;
                if(a.id=="primary"&&CurrentPage==Page.Records)enabled=Open(Record);
                var button=ActionButton(a.id,screen,new Rect(a.rect[0],a.rect[1],a.rect[2],a.rect[3]),()=>Act(a.id),enabled);
                string visual=a.id=="settings"?"Settings_Button":a.id=="previous"?"Previous_Arrow":a.id=="next"?"Next_Arrow":a.id=="back"?"Back":null;
                if(visual!=null&&elements.TryGetValue(visual,out var v))button.gameObject.AddComponent<CascadeMenuPress>().visual=v;
                if(a.id=="primary")
                {
                    var group=Rect("Primary visual group",screen,new Rect(470.5f,1513.3333f,0,0));
                    foreach(var key in new[]{"Primary_Button_Artwork","Primary_Button_Label","Primary_Button_Secondary"})if(elements.TryGetValue(key,out var child))child.SetParent(group,true);
                    button.gameObject.AddComponent<CascadeMenuPress>().visual=group;
                }
            }
            ApplyComposition();
        }
        void ApplyComposition()
        {
            foreach(var graphic in GetComponentsInChildren<Graphic>(true))if(!(graphic is RawImage))graphic.material=compositor.CompositionMaterial;
        }
        void Act(string id)
        {
            if(Busy)return;
            switch(id)
            {
                case "previous":Step(-1);break;case "next":Step(1);break;
                case "settings":Settings();break;case "back":Back();break;
                case "primary":if(CurrentPage==Page.Records)OpenTracks();else if(CurrentPage==Page.Tracks)PlaySelected();else Back();break;
            }
        }
        void UpdateSingleTrack()
        {
            var t=Record.tracks.First(t=>t.track_id==SelectedTrackId);var result=Store.Result(t);
            TextValue("Track_01_Title",t.title);TextValue("Track_01_Best","Рекорд: "+(result?.completed==true?result.bestScore.ToString("N0",CultureInfo.GetCultureInfo("ru-RU")):"—"));
            TextValue("Track_01_Duration",Clock(t.runtime_duration_seconds));TextValue("Track_Count",Record.tracks.Length+" трек");
            TextValue("Primary_Button_Secondary","Уровень "+Record.order);
        }
        void BuildTrackList()
        {
            foreach(string id in new[]{"Record_Header_Vinyl","Record_Header_Cover"})
            {
                var rect=elements[id];Vector2 centre=new Vector2(470.5f,-706);rect.anchoredPosition=centre+(rect.anchoredPosition-centre)*.67f+new Vector2(0,135);rect.localScale=Vector3.one*.67f;
            }
            foreach(var kv in elements)if(kv.Key.StartsWith("Style_Title")||kv.Key=="Track_List_Heading")kv.Value.anchoredPosition+=new Vector2(0,220);
            var viewport=Rect("Track scroll viewport",screen,new Rect(25,925,891,432));var bg=viewport.gameObject.AddComponent<Image>();bg.color=Color.clear;bg.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Track rows",viewport,new Rect(0,0,891,Mathf.Max(432,Record.tracks.Length*132)));
            content.pivot=new Vector2(.5f,1);content.anchoredPosition=new Vector2(445.5f,0);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            for(int i=0;i<Record.tracks.Length;i++)
            {
                var track=Record.tracks[i];float shift=925-1164+16+i*132;
                foreach(var template in CurrentLayout.elements_back_to_front.Where(e=>e.id.StartsWith("Track_01_")))
                {
                    var e=JsonUtility.FromJson<CascadeMenuElement>(JsonUtility.ToJson(template));e.id="Track_"+i+"_"+template.id.Substring(9);e.x-=25;e.y+=shift-925;e.figma_text_top_y+=shift-925;
                    if(template.id.EndsWith("Artwork"))e.asset=track.track_id==SelectedTrackId?"Assets/track-selected.png":"Assets/track-normal.png";
                    if(template.id.EndsWith("Number"))e.text=(i+1).ToString();if(template.id.EndsWith("Title"))e.text=track.title;
                    if(template.id.EndsWith("Best"))e.text="Рекорд: "+(Store.Result(track)?.completed==true?Store.Result(track).bestScore.ToString():"—");
                    if(template.id.EndsWith("Duration"))e.text=Clock(track.runtime_duration_seconds);
                    BuildElement(e,content);
                }
                ActionButton("Select "+track.track_id,content,new Rect(27,16+i*132,835,112),()=>{SelectedTrackId=track.track_id;Draw();},true);
            }
            TextValue("Primary_Button_Secondary","Уровень "+Record.order);
        }
        public void Step(int direction)
        {
            if(Busy||CurrentPage!=Page.Records||direction==0)return;
            int next=SelectedRecord+Math.Sign(direction);if(next<0||next>=records.Length)return;
            if(approvedDesign){ShowRecords(next);return;}
            StartCoroutine(Slide(next));
        }
        IEnumerator Slide(int next)
        {
            Busy=true;SetButtons(false);
            foreach(var group in recordGroups)group.gameObject.SetActive(false);
            var groups=new List<(int index,RectTransform rect,CanvasGroup alpha)>();
            for(int i=Math.Max(0,Math.Min(next,SelectedRecord)-1);i<=Math.Min(records.Length-1,Math.Max(next,SelectedRecord)+1);i++)
            {
                var root=BuildRecord(screen,"Transition "+i,i,470.5f+(i-SelectedRecord)*510.5f,805,750,true);
                root.SetSiblingIndex(3);groups.Add((i,root,root.gameObject.AddComponent<CanvasGroup>()));
            }
            ApplyComposition();
            float at=Time.unscaledTime;
            while(Time.unscaledTime-at<.28f)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-at)/.28f);float ease=1-Mathf.Pow(1-t,3);
                foreach(var g in groups)
                {
                    float old=g.index-SelectedRecord,to=g.index-next;
                    g.rect.anchoredPosition=new Vector2(470.5f+Mathf.Lerp(old,to,ease)*510.5f,-805);
                    g.rect.localScale=Vector3.one*Mathf.Lerp(old==0?1:.68f,to==0?1:.68f,ease);
                    g.alpha.alpha=Mathf.Lerp(Mathf.Abs(old)>1?0:1,Mathf.Abs(to)>1?0:1,ease);
                    bool open=Open(records[g.index]);float main=open?1:.58f,side=open?.48f:.35f;
                    float opacity=Mathf.Lerp(old==0?main:side,to==0?main:side,ease);
                    foreach(var im in g.rect.GetComponentsInChildren<Image>())
                        if(im.name.EndsWith("_Vinyl")||im.name=="Cover pixels"){var c=im.color;c.a=opacity;im.color=c;}
                }
                // Keep the record closest to the centre above its neighbours throughout the slide.
                foreach(var g in groups.OrderByDescending(g=>Mathf.Abs(Mathf.Lerp(g.index-SelectedRecord,g.index-next,ease))))g.rect.SetSiblingIndex(3+groups.Count);
                yield return null;
            }
            // Commit the record, label, progress and primary action together at the snap point.
            SelectedRecord=next;SelectedTrackId=null;Busy=false;Draw();
        }
        void SetButtons(bool enabled){foreach(var button in screen.GetComponentsInChildren<Button>())button.interactable=enabled&&button.interactable;}
        RectTransform BuildRecord(Transform parent,string name,int index,float cx,float cy,float size,bool central)
        {
            var group=Rect(name,parent,new Rect(cx,cy,0,0));float scale=size/1254;bool open=Open(records[index]);float alpha=central?(open?1:.58f):(open?.48f:.35f);
            // Local coordinates use a zero-sized parent located at the exact label-ring centre.
            Image(name+"_Vinyl",group,"Assets/vinyl.png",new Rect(-619.791f*scale,-627.169f*scale,size,size),alpha);
            float radius=256.554f*scale;
            var cover=Image(name+"_Cover",group,records[index].cover_file,new Rect(-radius,-radius,2*radius,2*radius),alpha,true);
            if(!open)
            {
                var shade=Image(name+"_LockShade",group,"Assets/UI/Circle_Mask.png",new Rect(-radius,-radius,2*radius,2*radius));shade.color=Tint("#110A2B",.62f);
                float lockSize=size*.12f;Image(name+"_Lock",group,"Assets/UI/Lock.png",new Rect(-lockSize/2,-lockSize/2,lockSize,lockSize));
            }
            return group;
        }
        RectTransform BuildElement(CascadeMenuElement e,Transform parent)
        {
            if(e.type=="image")return Image(e.id,parent,e.asset,new Rect(e.x,e.y,e.width,e.height),e.opacity,!string.IsNullOrEmpty(e.clip)).rectTransform;
            if(e.type=="text")return TextAt(e.id,parent,e.text,e.x,e.figma_text_top_y,e.font_size,e.font_weight==700,e.anchor,e.color).rectTransform;
            if(e.geometry!=null&&!string.IsNullOrEmpty(e.geometry.direction))
            {
                float alpha=e.id=="Previous_Arrow"&&SelectedRecord==0||e.id=="Next_Arrow"&&SelectedRecord==records.Length-1?.28f:1;
                return Image(e.id,parent,"Assets/UI/Arrow_"+(e.geometry.direction=="left"?"Left":"Right")+".png",new Rect(e.geometry.center[0]-50,e.geometry.center[1]-50,100,100),alpha).rectTransform;
            }
            if(e.id=="Settings_Panel")
            {
                var xml=XElement.Parse(e.svg_markup);float x=Num(xml,"x"),y=Num(xml,"y"),w=Num(xml,"width"),h=Num(xml,"height"),r=Num(xml,"rx");
                var panel=Panel(e.id,parent,new Rect(x,y,w,h),Tint("#160B35",.93f),r);
                var outline=Panel("Settings outline",parent,new Rect(x-1,y-1,w+2,h+2),Tint("#8C51B6"),r+1);outline.shape=CascadePanelGraphic.Shape.Border;outline.border=2;
                return panel.rectTransform;
            }
            if(e.id=="Music_Slider"){BuildVolume(parent);return null;}
            if(e.id.StartsWith("Reduced_Toggle"))
            {
                var image=Image(e.id,parent,"Assets/UI/Toggle_"+(game.MenuSoftEffects?"On":"Off")+".png",new Rect(698,906,120,62));
                ActionButton("Toggle soft",parent,new Rect(686,883,144,112),()=>{game.MenuSetSoftEffects(!game.MenuSoftEffects);image.sprite=Sprite("Assets/UI/Toggle_"+(game.MenuSoftEffects?"On":"Off")+".png");},true);return image.rectTransform;
            }
            throw new InvalidOperationException("Unmapped menu element "+e.id);
        }
        void BuildVolume(Transform parent)
        {
            var root=Rect("Volume control",parent,new Rect(108,745,724,104));var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var range=Rect("Volume range",root,new Rect(17,0,690,104));
            Panel("Volume rail",range,new Rect(0,46,690,12),Tint("#41255A"),6);
            var fillRange=Rect("Volume fill range",range,new Rect(0,46,690,12));
            var fill=Panel("Volume fill",fillRange,new Rect(0,0,690,12),Tint("#FFD37B"),6).rectTransform;
            fill.sizeDelta=Vector2.zero;fill.anchoredPosition=Vector2.zero;
            var handleRange=Rect("Volume handle range",range,new Rect(0,52,690,0));
            var handle=Image("Volume thumb",handleRange,"Assets/UI/Slider_Thumb.png",new Rect(-24,-24,48,48));
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.anchoredPosition=Vector2.zero;
            var slider=root.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.transition=Selectable.Transition.None;
            slider.SetValueWithoutNotify(game.MenuVolume);slider.onValueChanged.AddListener(value=>{game.MenuSetVolume(value);TextValue("Music_Value",Mathf.RoundToInt(value*100)+"%");});
            // Music_Value is already created before this element in the supplied back-to-front list.
            TextValue("Music_Value",Mathf.RoundToInt(game.MenuVolume*100)+"%");
        }
        static float Num(XElement x,string name)=>float.Parse(x.Attribute(name).Value,CultureInfo.InvariantCulture);
        public static RectTransform Rect(string name,Transform parent,Rect box)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.gameObject.layer=29;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=Vector2.one*.5f;rt.anchoredPosition=new Vector2(box.x+box.width/2,-box.y-box.height/2);rt.sizeDelta=box.size;return rt;
        }
        Sprite Sprite(string path)
        {
            if(!sprites.TryGetValue(path,out var sprite)){sprite=Resources.Load<Sprite>(Root+path.Substring(0,path.LastIndexOf('.')));if(sprite==null)throw new InvalidOperationException("Missing menu sprite "+path);sprites[path]=sprite;}return sprite;
        }
        Image Image(string name,Transform parent,string path,Rect box,float alpha=1,bool circle=false)
        {
            var rect=Rect(name,parent,box);
            if(circle)
            {
                var mask=rect.gameObject.AddComponent<Image>();mask.sprite=Sprite("Assets/UI/Circle_Mask.png");mask.raycastTarget=false;rect.gameObject.AddComponent<Mask>().showMaskGraphic=false;
                var child=Rect("Cover pixels",rect,new Rect(0,0,box.width,box.height));var im=child.gameObject.AddComponent<Image>();im.sprite=Sprite(path);im.color=new Color(1,1,1,alpha);im.raycastTarget=false;im.preserveAspect=true;return mask;
            }
            var image=rect.gameObject.AddComponent<Image>();if(path!=null)image.sprite=Sprite(path);image.color=new Color(1,1,1,alpha);image.raycastTarget=false;image.preserveAspect=true;return image;
        }
        Text TextAt(string name,Transform parent,string value,float x,float top,float size,bool heavy,string anchor,string tint)
        {
            float width=anchor=="middle"?900:anchor=="end"?800:Mathf.Max(1,891-x);
            float left=anchor=="middle"?x-width/2:anchor=="end"?x-width:x;
            var text=Rect(name,parent,new Rect(left,top,width,size*1.5f)).gameObject.AddComponent<Text>();text.font=heavy?bold:regular;text.fontSize=Mathf.RoundToInt(size);text.fontStyle=FontStyle.Normal;text.text=value;text.color=Tint(tint);text.raycastTarget=false;text.supportRichText=false;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.alignment=anchor=="middle"?TextAnchor.UpperCenter:anchor=="end"?TextAnchor.UpperRight:TextAnchor.UpperLeft;return text;
        }
        static CascadePanelGraphic Panel(string name,Transform parent,Rect box,Color color,float radius)
        {var p=Rect(name,parent,box).gameObject.AddComponent<CascadePanelGraphic>();p.color=p.rightColor=color;p.radius=radius;p.raycastTarget=false;return p;}
        static Button ActionButton(string name,Transform parent,Rect box,UnityEngine.Events.UnityAction callback,bool enabled)
        {
            // Enlarge only the transparent hit area; keep the supplied artwork geometry intact.
            Vector2 centre=box.center;box.size=new Vector2(Mathf.Max(112,box.width),Mathf.Max(112,box.height));box.center=centre;
            var image=Rect("Action "+name,parent,box).gameObject.AddComponent<Image>();image.color=Color.clear;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.interactable=enabled;button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(callback);return button;
        }
        void TextValue(string id,string value){if(elements.TryGetValue(id,out var r))r.GetComponent<Text>().text=value;}
        void SetImageAlpha(string id,float alpha){var im=elements[id].GetComponent<Image>();var c=im.color;c.a=alpha;im.color=c;}
        static Color Tint(string text,float alpha=1){ColorUtility.TryParseHtmlString(text,out var c);c.a=alpha;return c;}
        static string Clock(double t)=>((int)t/60)+":"+((int)t%60).ToString("00");
    }
}
