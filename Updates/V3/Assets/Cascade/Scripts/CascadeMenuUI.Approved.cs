using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using A=Cascade.Prototype.CascadeApprovedUI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeMenuUI
    {
        bool approvedDesign,pauseSettings;
        string chosenHero,infoTitle,infoBody;
        int mapPage;
        bool English=>Store.Data.language=="en";
        string L(string ru,string en)=>English?en:ru;
        public void InitializeApproved(CascadeGame owner)
        {
            approvedDesign=true;game=owner;records=game.Catalog.records.OrderBy(r=>r.order).ToArray();
            regular=A.Font();bold=A.Font(true);
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            gameObject.AddComponent<GraphicRaycaster>();
            background=A.Image("World background",transform,"city-off",0,0,414,850,false).rectTransform;
            background.anchorMin=background.anchorMax=background.pivot=Vector2.one*.5f;
            safe=A.Rect("Menu safe composition",transform,0,0,414,850);safe.anchorMin=safe.anchorMax=safe.pivot=Vector2.one*.5f;
            ShowRecords(0);UpdateApprovedLayout();
        }
        void UpdateApprovedLayout()
        {
            var area=Screen.safeArea;float scale=Mathf.Max(.01f,Mathf.Min(area.width/414,area.height/850));scaler.scaleFactor=scale;
            safe.anchoredPosition=(area.center-new Vector2(Screen.width,Screen.height)*.5f)/scale;
            var image=background.GetComponent<Image>();var size=image.sprite.rect.size;
            background.sizeDelta=size*Mathf.Max(Screen.width/(size.x*scale),Screen.height/(size.y*scale));background.anchoredPosition=Vector2.zero;
        }
        void Go(Page page){if(Busy)return;CurrentPage=page;error="";Draw();}
        public void OpenPauseSettings(){pauseSettings=true;settingsOrigin=Page.Records;CurrentPage=Page.Settings;game.SetPauseSettingsVisible(true);gameObject.SetActive(true);Draw();}
        void DrawApproved()
        {
            if(screen){screen.gameObject.SetActive(false);Destroy(screen.gameObject);}elements.Clear();recordGroups.Clear();
            screen=A.Rect("Approved screen",safe,0,0,414,850);Store.RefreshDaily();
            bool lit=records.Any(Completed);
            background.gameObject.SetActive(!pauseSettings);
            background.GetComponent<Image>().sprite=A.Sprite(CurrentPage==Page.World?"map":lit?"city-on":"city-off");
            if(CurrentPage==Page.World){DrawWorld();return;}
            if(!pauseSettings)DrawHome();
            if(CurrentPage==Page.Records)return;
            A.Panel("Modal dim",screen,-1000,-1000,2414,2850,"#16102B22","#16102B38",0,false).raycastTarget=true;
            switch(CurrentPage)
            {
                case Page.Settings:DrawSettings();break;
                case Page.Language:DrawLanguage();break;
                case Page.Heroes:DrawHeroes();break;
                case Page.Tracks:DrawTracks();break;
                case Page.Collection:DrawCollection();break;
                case Page.Daily:DrawDaily();break;
                case Page.Events:InfoPanel(L("События","Events"),L("Сейчас активных событий нет.\nНовые музыкальные приключения появятся здесь.","No events are running right now.\nNew musical adventures will appear here."));break;
                case Page.Information:InfoPanel(infoTitle,infoBody);break;
            }
        }
        void DrawHome()
        {
            A.Panel("Header readability",screen,-1000,-80,2414,230,"#10173255","#10173200",0,false);
            A.Panel("Bottom readability",screen,-1000,630,2414,350,"#120D2D00","#120D2DEE",0,false);
            var pick=A.Glass("Hero picker",screen,18,31,111,57,22);
            var portrait=A.Panel("Portrait",pick.transform,5,7,43,43,"#593974","#593974",22,false).rectTransform;portrait.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            bool mi=Store.Data.selectedHero!="Fa";
            A.Image("Selected portrait",portrait,mi?"mi":"fa",mi?-38:-39,mi?-7:-3,mi?132:100,mi?150:134);
            A.Text("Heroes caption",pick.transform,L("Герои","Heroes"),55,8,50,16,10);
            A.Text("Hero name",pick.transform,mi?L("Ми","Mi"):L("Фа","Fa"),55,22,42,29,16);
            A.Image("Open heroes",pick.transform,"Icons/ChevronDown",91,30,12,12);
            A.Hit("Choose hero",screen,18,31,111,57,()=>{chosenHero=Store.Data.selectedHero;Go(Page.Heroes);});
            var brand=A.Text("Cascade",screen,"Cascade",144,31,190,57,33,false,"#FFF8D5",TextAnchor.MiddleCenter);
            brand.font=Resources.Load<Font>("Approved/Fonts/LibreBaskerville-Italic")??regular;
            A.Button("Settings",screen,"",350,36,46,46,Settings);A.Image("Settings icon",screen,"Icons/Settings",361,47,24,24);
            string state=L("Глава ","Chapter ")+(SelectedRecord+1)+L(records.Any(Completed)?" · Город оживает":" · Город спит",records.Any(Completed)?" · City awakens":" · City sleeps");
            Pill(screen,"Chapter state",state,111,109,192,26,11);
            ObjectButton("World map",L("Карта мира","World map"),"Map",308,157,()=>Go(Page.World));
            ObjectButton("Events",L("События","Events"),"Events",308,274,()=>Go(Page.Events));
            A.Image("Selected hero",screen,mi?"mi":"fa",mi?111:129.5f,mi?416.5f:397,mi?250:213,mi?258:284);
            Pill(screen,"Current record",L("Текущая пластинка","Current record"),24,426,120,22,11);
            var disc=A.Panel("Vinyl",screen,43,460,82,82,"#20162E","#20162E",41);
            for(int i=0;i<8;i++){float d=72-i*5;var r=A.Rect("Groove",disc.transform,(82-d)/2,(82-d)/2,d,d);A.Border(r,d,d,i%2==0?"#A980B3AA":"#FFF1D880",d/2,.65f);}
            A.Panel("Record centre",disc.transform,25.5f,25.5f,31,31,"#B483CB","#795192",16,false);
            A.Image("Music",disc.transform,"Icons/Music2",32,32,18,18);
            A.Panel("Plinth",screen,36.5f,547,95,12,"#B483CB","#4B315F",6);
            Pill(screen,"Record title",Record.display_name,24,576,120,26,11);
            A.Text("Open record hint",screen,L("Открыть пластинку","Open record"),22,603,125,24,11,true,"#FFF1D8",TextAnchor.MiddleCenter);
            A.Hit("Open record",screen,20,426,130,204,OpenTracks);
            ObjectButton("Collection",L("Коллекция\nинструментов","Instrument\ncollection"),"Collection",19,719,()=>Go(Page.Collection));
            ObjectButton("Daily",L("Ежедневные\nзадания","Daily\nquests"),"Daily",303,719,()=>Go(Page.Daily));
            var play=A.Button("Play",screen,L("Играть","Play"),121,719,172,64,PlayHome,true,22);
            var playSurface=play.transform.parent.Find("Play surface");var label=playSurface.Find("Play label").GetComponent<RectTransform>();label.anchoredPosition=new Vector2(50,0);label.sizeDelta=new Vector2(112,64);
            A.Panel("Play medallion",playSurface,24,17,30,30,"#B483CB","#4B315F",15,false);A.Image("Play icon",playSurface,"Icons/Play",31,24,16,16);
            var track=CurrentTrack();
            A.Text("Current level",screen,L("Уровень ","Level ")+(SelectedRecord+1)+"\n"+track.title,121,790,172,40,11,true,"#FFF1D8",TextAnchor.MiddleCenter);
        }
        CascadeTrack CurrentTrack()=>Record.tracks.FirstOrDefault(t=>t.track_id==SelectedTrackId)??Record.tracks.FirstOrDefault(t=>Store.Result(t)?.completed!=true)??Record.tracks[0];
        void PlayHome(){if(!Open(Record)){OpenLocked(Record);return;}SelectedTrackId=CurrentTrack().track_id;CurrentPage=Page.Tracks;StartCoroutine(Launch());}
        void ObjectButton(string key,string label,string art,float x,float y,Action action)
        {
            A.Glass(key+" key",screen,x+14.5f,y,63,60,22);
            A.Image(key+" artwork",screen,art,x+3.5f,y-12,85,85);
            Pill(screen,key+" caption",label,x+1,y+70,90,30,11);A.Hit(key,screen,x,y-12,92,112,action);
        }
        void Pill(Transform p,string name,string value,float x,float y,float w,float h,int font)
        {var panel=A.Glass(name,p,x,y,w,h,9);A.Text(name+" text",panel.transform,value,3,0,w-6,h,font,true,"#FFF1D8",TextAnchor.MiddleCenter);}
        RectTransform Dialog(string title,float height,float y,bool gear=false)
        {
            float width=pauseSettings?348:384;
            var p=A.Glass("Dialog",screen,(414-width)/2,y,width,height,28,false,true).rectTransform;
            if(gear)A.Image("Settings artwork",p,"Gear",pauseSettings?20:23,pauseSettings?17:28,pauseSettings?48:61,pauseSettings?48:61);
            A.Text("Dialog title",p,title,gear?(pauseSettings?76:88):23,pauseSettings?13:24,gear?(pauseSettings?190:220):width-100,pauseSettings?61:69,pauseSettings?23:gear?27:24,true,"#FFF8D5");
            float closeX=width-65,closeY=pauseSettings?24:38;
            A.Button("Close",p,"",closeX,closeY,44,44,Back,false,27);A.Image("Close icon",p,"Icons/X",closeX+10,closeY+10,24,24);return p;
        }
        void DrawSettings()
        {
            var p=Dialog(L("Настройки","Settings"),pauseSettings?520:656,pauseSettings?324:97,true);
            A.Panel("Toggle well",p,pauseSettings?18:26,pauseSettings?82:110,pauseSettings?312:332,pauseSettings?190:244,"#53346558","#38254778",22,false);
            Toggle(p,0,"Headphones",L("Музыка","Music"),Store.Data.musicEnabled,()=>game.MenuSetMusic(!Store.Data.musicEnabled));
            Toggle(p,1,"Speaker",L("Звуки","Sounds"),Store.Data.soundsEnabled,()=>game.MenuSetSounds(!Store.Data.soundsEnabled));
            Toggle(p,2,"Haptic",L("Вибрация","Vibration"),Store.Data.hapticsEnabled,()=>game.MenuSetHaptics(!Store.Data.hapticsEnabled));
            NavigationRow(p,"Language",L("Язык","Language"),English?"English":"Русский",pauseSettings?282:384,()=>Go(Page.Language));
            NavigationRow(p,"Support",L("Поддержка","Support"),"",pauseSettings?344:460,()=>OpenInfo("support"));
            float legalWidth=p.rect.width-44;
            A.Text("Terms link",p,L("Условия использования","Terms of Use"),22,pauseSettings?410:546,legalWidth,44,12,true,"#ECE0F5",TextAnchor.MiddleCenter);
            A.Hit("Terms",p,22,pauseSettings?410:546,legalWidth,44,()=>OpenInfo("terms"));
            A.Text("Privacy link",p,L("Конфиденциальность","Privacy Policy"),22,pauseSettings?458:590,legalWidth,44,12,true,"#ECE0F5",TextAnchor.MiddleCenter);
            A.Hit("Privacy",p,22,pauseSettings?458:590,legalWidth,44,()=>OpenInfo("privacy"));
        }
        void Toggle(Transform p,int index,string art,string label,bool on,Action action)
        {
            float step=pauseSettings?62:80,y=(pauseSettings?84:112)+index*step;
            A.Image(label+" icon",p,art,pauseSettings?25:34,y+(pauseSettings?9:12),pauseSettings?44:56,pauseSettings?44:56);
            A.Text(label,p,label,pauseSettings?78:94,y+(pauseSettings?5:10),pauseSettings?164:170,27,pauseSettings?16:17);
            A.Text(label+" state",p,on?L("Вкл.","On"):L("Выкл.","Off"),pauseSettings?78:94,y+(pauseSettings?31:37),160,22,12,false,"#ECE0F5");
            var sw=A.Panel(label+" switch",p,pauseSettings?254:280,y+(pauseSettings?14:25),65,34,on?"#64D8BA":"#493754",on?"#248F81":"#392B45",17);
            A.Panel("Thumb",sw.transform,on?32:5,3,28,28,on?"#FFF8D5":"#DDC8E8",on?"#FFDE7F":"#AC92B6",14,false);
            A.Text("Switch state",sw.transform,on?"✓":"−",on?7:41,2,20,29,17,true,"#FFF1D8",TextAnchor.MiddleCenter);
            A.Hit("Toggle "+label,p,18,y,((RectTransform)p).rect.width-36,step,()=>{action();Draw();});if(index<2)A.Panel("Divider",p,30,y+step,((RectTransform)p).rect.width-60,1,"#DDC8E830","#DDC8E830",0,false);
        }
        void NavigationRow(Transform p,string name,string label,string value,float y,Action action)
        {
            float width=((RectTransform)p).rect.width;
            A.Glass(name+" icon",p,26,y+4,44,44,15);
            A.Image(name+" glyph",p,name=="Language"?"Icons/Globe":"Icons/MessagesSquare",35,y+13,26,26);
            A.Text(name+" label",p,label,81,y,width-200,53,16);A.Text(name+" value",p,value,width-139,y,85,53,14,true,"#FFDE7F",TextAnchor.MiddleRight);
            A.Text(name+" arrow",p,"›",width-47,y,20,53,25);A.Panel(name+" divider",p,26,y+(pauseSettings?54:60),width-52,1,"#DDC8E840","#DDC8E840",0,false);
            A.Hit(name,p,26,y,width-52,pauseSettings?56:66,action);
        }
        void DrawLanguage()
        {
            var p=Dialog(L("Язык","Language"),325,pauseSettings?390:263);
            string[] values={"Русский","English"};for(int i=0;i<2;i++){int n=i;A.Button("Language "+i,p,values[i]+((English?1:0)==i?"   ✓":""),26,111+i*87,p.rect.width-52,74,()=>{game.MenuSetLanguage(n==0?"ru":"en");Go(Page.Settings);});}
        }
        void DrawHeroes()
        {
            var p=Dialog(L("Выбери героя","Choose your hero"),420,418);
            for(int i=0;i<2;i++)
            {
                string hero=i==0?"Fa":"Mi";float x=23+i*174;
                var card=A.Panel(hero+" card",p,x,92,164,215,"#B483CB","#4B315F",24);
                if(chosenHero==hero)A.Border(card.transform,164,215,"#FFDE7F",24,3);
                A.Image(hero,card.transform,i==0?"fa":"mi",i==0?21:11,10,i==0?121.5f:142.6f,162);
                A.Text(hero+" name",card.transform,i==0?L("Фа","Fa"):L("Ми","Mi"),0,177,164,30,18,true,"#FFF1D8",TextAnchor.MiddleCenter);
                A.Hit("Choose "+hero,p,x,92,164,215,()=>{chosenHero=hero;Draw();});
            }
            A.Button("Confirm hero",p,L("Выбрать","Select"),23,329,338,54,()=>{game.MenuSetHero(chosenHero);Go(Page.Records);},true,20);
        }
        void DrawTracks()
        {
            var p=Dialog(Record.display_name,540,170);var t=CurrentTrack();SelectedTrackId=t.track_id;
            A.Text("Track subtitle",p,L("Пластинка ","Record ")+(SelectedRecord+1)+" / "+records.Length,26,92,332,28,13,false,"#DDC8E8");
            for(int i=0;i<Record.tracks.Length;i++)
            {
                var track=Record.tracks[i];var result=Store.Result(track);float y=136+i*94;
                A.Button("Track "+track.track_id,p,track.title+"\n"+Clock(track.runtime_duration_seconds)+(result?.completed==true?"   ✓":""),26,y,332,80,()=>{SelectedTrackId=track.track_id;Draw();},track==t,16);
            }
            A.Text("Best",p,L("Рекорд: ","Best: ")+(Store.Result(t)?.bestScore.ToString("N0")??"—"),26,300,332,34,14,false,"#FFF1D8",TextAnchor.MiddleCenter);
            A.Text("Track error",p,error,26,342,332,60,13,false,"#FFF1D8",TextAnchor.MiddleCenter);
            var play=A.Button("Play selected",p,L("▶ Играть","▶ Play"),26,430,332,64,PlaySelected,true,22);
            elements["Primary_Button_Label"]=play.transform.parent.Find("Play selected surface/Play selected label").GetComponent<RectTransform>();
        }
        void DrawWorld()
        {
            A.Panel("Map header shade",screen,0,0,414,150,"#101732AA","#10173200",0,false);
            A.Button("Map back",screen,"‹",20,49,46,46,Back,false,29);
            A.Text("Map title",screen,L("Музыкальные миры","Musical worlds"),78,42,320,39,23);
            A.Text("Map subtitle",screen,L("Верни музыку парящим городам","Bring music back to the floating cities"),78,80,320,25,11,false);
            float[] xs={69,237,18,225},ys={634,440,286,160};
            string[] titles={L("Город ритма","Rhythm city"),L("Струнные острова","String islands"),L("Город ударных","Percussion city"),L("Электро-небеса","Electro skies")};
            for(int i=0;i<4;i++)
            {
                int index=mapPage*4+i;if(index>=records.Length)break;var record=records[index];bool open=Open(record);
                string label=(Completed(record)?"✓ ":open?"♪ ":"● ")+titles[i]+"\n"+L("Глава ","Chapter ")+(index+1)+" · "+record.display_name;
                A.Button("City "+index,screen,label,xs[i],ys[i],i==0?186:156,58,()=>{if(open){SelectedRecord=index;SelectedTrackId=null;ShowRecords(index);}else OpenLocked(record);},open&&index==SelectedRecord,11);
            }
            A.Button("More worlds",screen,mapPage==0?L("Дальше · Главы 5–8  ›","Next · Chapters 5–8  ›"):L("‹  Главы 1–4","‹  Chapters 1–4"),107,771,200,46,()=>{mapPage=1-mapPage;Draw();},false,14);
        }
        void OpenLocked(CascadeRecord record){infoTitle=L("Город пока спит","The city is still asleep");var before=records.FirstOrDefault(r=>r.record_id==record.prerequisite_record_id);infoBody=L("Сначала заверши пластинку ","First complete ")+(before?.display_name??"—");Go(Page.Information);}
        void DrawCollection()
        {
            var p=Dialog(L("Коллекция инструментов","Instrument collection"),650,100);
            A.Text("Collection hint",p,L("Завершай пластинки, чтобы собирать инструменты.","Complete records to collect instruments."),26,92,332,48,13,false,"#DDC8E8");
            string[] names=English?new[]{"Trumpet","Synthesizer","Saxophone","Bass","Drums","Violin","Keys","Guitar"}:new[]{"Труба","Синтезатор","Саксофон","Бас","Барабаны","Скрипка","Клавиши","Гитара"};
            for(int i=0;i<records.Length;i++){bool earned=Completed(records[i]);float y=155+i*52;A.Text("Instrument "+i,p,(earned?"✓ ":"○ ")+names[i]+" · "+records[i].display_name,30,y,324,44,14,true,earned?"#FFDE7F":"#DDC8E8");}
        }
        void DrawDaily()
        {
            var p=Dialog(L("Ежедневные задания","Daily quests"),470,220);
            A.Text("Daily reset",p,L("Обновляются каждый день","Refresh every day"),26,94,332,32,13,false,"#DDC8E8");
            string[] names={L("Попади в ритм 10 раз","Hit the rhythm 10 times"),L("Заверши один трек","Complete one track"),L("Запусти 3 каскада","Trigger 3 cascades")};int[] values={Store.Data.dailyHits,Store.Data.dailyTracks,Store.Data.dailyCascades},targets={10,1,3};
            for(int i=0;i<3;i++){var row=A.Panel("Quest "+i,p,26,146+i*88,332,72,"#795192","#4B315F",17,false);A.Text("Task",row.transform,names[i],15,8,290,30,15);A.Text("Progress",row.transform,Math.Min(targets[i],values[i])+" / "+targets[i]+(values[i]>=targets[i]?"  ✓":""),15,38,290,24,13,true,"#FFDE7F");}
        }
        void OpenInfo(string key)
        {
            infoTitle=key=="support"?L("Поддержка","Support"):key=="terms"?L("Условия использования","Terms of Use"):L("Конфиденциальность","Privacy Policy");
            if(game.OpenServiceLink(key))return;
            infoBody=key=="support"?L("Канал поддержки ещё не подключён к этой сборке.","The support channel is not connected in this build yet."):L("Документ ещё не добавлен в эту сборку.","This document has not been added to this build yet.");Go(Page.Information);
        }
        void InfoPanel(string title,string body){var p=Dialog(title,340,pauseSettings?390:255);A.Text("Information",p,body,27,116,p.rect.width-54,152,17,false,"#FFF1D8",TextAnchor.MiddleCenter);}
    }
}
