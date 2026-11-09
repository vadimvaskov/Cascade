using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Cascade.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

// Play Mode fixtures only. Reference placement never becomes the game's starting board.
public static class CascadeRasterChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Read<T>(CascadeGame g,string n)=>(T)typeof(CascadeGame).GetField(n,F).GetValue(g);
    static void Set(CascadeGame g,string n,object v)=>typeof(CascadeGame).GetField(n,F).SetValue(g,v);
    static object Call(CascadeGame g,string n,params object[] args)=>typeof(CascadeGame).GetMethod(n,F).Invoke(g,args);
    static void Require(bool ok,string message){if(!ok)throw new Exception("Raster check: "+message);}
    static CascadeGame Game()
    {
        Require(Application.isPlaying,"Play Mode is required");
        var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();Require(g!=null&&g.RasterRoot!=null,"Raster scene is required");return g;
    }
    static CascadeRasterLayout Layout(CascadeGame g)=>JsonUtility.FromJson<CascadeRasterLayout>(g.RasterLayoutAsset.text);
    static Vector2 PixelToScreen(Vector2 p)
    {
        Rect safe=Screen.safeArea;float scale=Mathf.Min(safe.width/941f,safe.height/1672f);
        return safe.center+new Vector2(p.x-470.5f,836-p.y)*scale;
    }
    static void Fixture(CascadeGame g,int[] cells,double time=1)
    {
        typeof(CascadeRuntimeChecks).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g,cells,time});
        Call(g,"UpdateScreenLayout");Canvas.ForceUpdateCanvases();Physics.SyncTransforms();
        Read<HashSet<int>>(g,"downTouches").Clear();Set(g,"pointerTaken",false);
    }
    [StructLayout(LayoutKind.Explicit,Size=36)]
    struct NativeTouch:IInputStateTypeInfo
    {
        [FieldOffset(0)]public int id;[FieldOffset(4)]public Vector2 position;
        [FieldOffset(20)]public float pressure;[FieldOffset(32)]public byte phase;
        public FourCC format=>TouchState.Format;
    }
    static void Tap(Touchscreen touch,int id,Vector2 p)
    {
        double time=InputState.currentTime;
        InputSystem.QueueStateEvent(touch,new NativeTouch{id=id,position=p,pressure=1,phase=(byte)UnityEngine.InputSystem.TouchPhase.Began},time);
        InputSystem.QueueStateEvent(touch,new NativeTouch{id=id,position=p,phase=(byte)UnityEngine.InputSystem.TouchPhase.Ended},time+.001);
        InputSystem.Update();
    }
    public static string Run()
    {
        var g=Game();var layout=Layout(g);layout.Validate();
        var source=Read<AudioSource>(g,"music");bool mute=source.mute,reduced=Read<bool>(g,"reduced");int offset=Read<int>(g,"offsetMs");
        string path=Path.Combine(Application.persistentDataPath,"cascade-last-attempt.json");byte[] journal=File.Exists(path)?File.ReadAllBytes(path):null;
        Touchscreen old=Touchscreen.current,probe=null;
        try
        {
            source.mute=true;probe=InputSystem.AddDevice<Touchscreen>("Raster native touch check");
            // Input positions come from the delivered JSON and screen safe area, not collider transforms.
            for(int i=0;i<36;i++)
            {
                Fixture(g,new int[36]);Vector2 p=PixelToScreen(layout.Center(i));
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);
                Require(hits.Count==0,"decorative UI blocks cell "+i);
                var view=Read<CascadeCell[]>(g,"views")[i];
                Vector2 world=Read<Camera>(g,"gameCamera").WorldToScreenPoint(view.transform.position);
                Require(Vector2.Distance(p,world)<.5f,"collider alignment "+i);
                Tap(probe,100+i,p);Require(g.MoveCount==1&&g.Model.cells[i]>0,"native tap in layout cell "+i);
                Require(view.raster.image.gameObject.activeSelf&&view.raster.image.sprite.name.EndsWith(new[]{"cyan","green","amber","purple"}[view.charge-1]),"placed state sprite "+i);
                Require(Read<Text>(g,"scoreText").text==g.Score.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("ru-RU")),"live score");
            }
            // Dynamic states, preview, track and actual time/progress (not the sample overlay).
            Fixture(g,new int[36],74);
            Set(g,"<State>k__BackingField",CascadeGame.Phase.Paused);Set(g,"pausedPosition",74d);
            var cell=Read<CascadeCell[]>(g,"views")[0];
            for(int state=1;state<=4;state++)
            {
                cell.SetCharge(state,Read<Material[]>(g,"orbMaterials"),0,false);
                Require(cell.raster.image.sprite.name=="sphere-"+state+"-"+new[]{"cyan","green","amber","purple"}[state-1],"state "+state);
                Set(g,"<Model>k__BackingField",new Board(Read<Balance>(g,"balance"),731,new int[36],state));Call(g,"RefreshHud");
                Require(Read<CascadeCell>(g,"preview").raster.image.sprite==cell.raster.image.sprite,"next state "+state);
            }
            Require(Math.Abs(Read<Image>(g,"progress").fillAmount-74/source.clip.length)<.0001,"live progress ratio");
            Require(Read<Text>(g,"timeText").text.StartsWith("1:14 / "),"live song time");
            for(int level=0;level<3;level++)
            {
                Call(g,"ShowMenu");g.StartLevel(level);Require(Read<Text>(g,"trackText").text==Read<string[]>(g,"trackNames")[level],"track title");
                Require(Read<Text>(g,"levelText").text=="УРОВЕНЬ "+(level+1),"level title");
            }
            // Real Update drives the raster rims from corrected song time, and reduced mode attenuates them.
            Fixture(g,new int[36],1);Require(g.PlaceCell(0,1),"rhythmic placement");
            Set(g,"<State>k__BackingField",CascadeGame.Phase.Paused);Set(g,"pausedPosition",1d);Set(g,"lightStrength",1f);Set(g,"reduced",false);Call(g,"Update");
            var frame=Read<CascadePanelGraphic>(g,"rasterFramePulse");float frameAlpha=frame.color.a,cellAlpha=cell.raster.rim.color.a;
            Require(frameAlpha>.2f&&cellAlpha>.3f,"hit-driven frame and cell pulse");
            double clock=Read<double>(g,"effectClock");Call(g,"Update");Require(Read<double>(g,"effectClock")==clock&&frame.color.a==frameAlpha,"paused visual clock");
            Set(g,"reduced",true);Call(g,"Update");Require(frame.color.a<frameAlpha*.2f&&cell.raster.rim.color.a<cellAlpha*.2f,"reduced effects");
            cell.Explode(clock,false);cell.Animate(clock+.12,0,0,false,false);Require(cell.raster.burst.gameObject.activeSelf&&cell.raster.burst.color.a>0,"cascade effect");
            cell.Animate(clock+.3,0,0,false,false);Require(!cell.raster.burst.gameObject.activeSelf,"cascade ends");
            cell.Explode(clock,false);Call(g,"ShowMenu");g.StartLevel(0);Require(!cell.raster.burst.gameObject.activeSelf,"restart clears previous effects");
            Fixture(g,new int[36],1);var pause=Read<GameObject>(g,"hud").transform.Find("Pause").GetComponent<Button>();
            pause.onClick.Invoke();Require(g.State==CascadeGame.Phase.Paused,"pause callback");
            Canvas.ForceUpdateCanvases();int moves=g.MoveCount;Tap(probe,999,PixelToScreen(layout.Center(0)));Require(g.MoveCount==moves,"paused overlay blocks gameplay");
            Require(!Read<GameObject>(g,"hud").GetComponentsInChildren<Image>(true).Any(x=>x.sprite!=null&&x.sprite.name=="hud-overlay"),"static HUD sample is excluded");
            return "PASS: 36 native 36-byte touches at JSON coordinates; 4 separate sphere states and next preview; live HUD; 3 track titles; corrected hit pulses, reduced effects, pause, cascade and restart";
        }
        finally
        {
            if(probe!=null)InputSystem.RemoveDevice(probe);if(old!=null&&old.added)old.MakeCurrent();
            Call(g,"ShowMenu");source.mute=mute;Set(g,"offsetMs",offset);Set(g,"reduced",reduced);Read<HashSet<int>>(g,"downTouches").Clear();Set(g,"pointerTaken",false);
            if(journal!=null)File.WriteAllBytes(path,journal);else if(File.Exists(path))File.Delete(path);
        }
    }
    [MenuItem("Cascade/Show supplied layout fixture (Play Mode only)")]
    public static void ShowReference()
    {
        var g=Game();g.CloseCalibration();Call(g,"ShowMenu");g.StartLevel(0);
        Read<AudioSource>(g,"music").Stop();Set(g,"<State>k__BackingField",CascadeGame.Phase.Paused);Set(g,"pausedPosition",74d);
        Set(g,"score",8120);Set(g,"lightStrength",0f);Set(g,"effectClock",10d);
        var cells=Layout(g).ReferenceCells();Set(g,"<Model>k__BackingField",new Board(Read<Balance>(g,"balance"),731,cells,1));
        Read<GameObject>(g,"modal").SetActive(false);
        foreach(var view in Read<CascadeCell[]>(g,"views")){view.raster.ResetEffects();view.SetCharge(cells[view.index],Read<Material[]>(g,"orbMaterials"),10,false);}
        Call(g,"RefreshHud");Read<Text>(g,"multiplierText").text="×3";Read<Text>(g,"streakText").text="";Read<Text>(g,"countdownText").text="";
        // The handoff's sample bar is 44.5% while its sample time is 74/187. Override only this visual fixture.
        Read<Image>(g,"progress").fillAmount=.445f;Call(g,"UpdateScreenLayout");Canvas.ForceUpdateCanvases();
    }
}
