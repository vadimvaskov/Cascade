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
public static class CascadeV3RuntimeChecks
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
        string path=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");byte[] journal=File.Exists(path)?File.ReadAllBytes(path):null;
        var originalStore=g.ProgressStore;
        string saveDirectory=Path.Combine(Path.GetTempPath(),"cascade-v3-runtime-"+Guid.NewGuid().ToString("N"));
        var testStore=new CascadeProgressStore(Path.Combine(saveDirectory,"save.json"),g.Catalog);
        foreach(var r in g.Catalog.records)testStore.Data.unlockedRecordIds.Add(r.record_id);
        Set(g,"progressStore",testStore);
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
            for(int level=0;level<8;level++)
            {
                Call(g,"ShowMenu");g.StartLevel(level);Require(Read<Text>(g,"trackText").text==g.Catalog.Tracks[level].title,"track title");
                Require(Read<Text>(g,"levelText").text=="УРОВЕНЬ "+(level+1),"level title");
            }
            // Exercise real placement and HUD at both sides of the new multiplier thresholds.
            int[] hitCounts={1,2,3,5,6,8,9,26,27,30};
            int[] multipliers={1,1,2,2,3,3,4,9,10,10};
            bool[] bonuses={false,false,false,false,false,true,true,false,true,true};
            string[] hints={"1/3 ДО ×2","2/3 ДО ×2","0/3 ДО ×3","2/3 ДО ×3","0/3 ДО ×4","2/3 ДО ×4","0/3 ДО ×5","2/3 ДО ×10","×10","×10"};
            for(int test=0;test<hitCounts.Length;test++)
            {
                int[] cells=new int[36];cells[8]=4;cells[14]=2;cells[15]=3;
                Fixture(g,cells,hitCounts[test]);var rhythm=Read<Rhythm>(g,"rhythm");
                for(int hit=1;hit<hitCounts[test];hit++)g.Bonus.Evaluate(rhythm.Evaluate(hit,0),hit,0);
                var expected=new Board(g.Model.config,731,cells).Place(9,multipliers[test],bonuses[test]);
                Require(g.PlaceCell(9,hitCounts[test]),"threshold placement accepted");
                Require(g.Multiplier==multipliers[test]&&g.Score==expected.points&&g.Model.cells.SequenceEqual(expected.final),"whole cascade uses threshold multiplier");
                Require(Read<Text>(g,"multiplierText").text=="×"+multipliers[test]&&Read<Text>(g,"statusText").text.StartsWith("В РИТМЕ · "+hints[test]+" · +"),"live multiplier and /3 HUD hint");
                Require(g.State==CascadeGame.Phase.Playing,"threshold placement keeps immediate input");
            }
            // Shared audio phase never inherits the touch calibration offset.
            Fixture(g,new int[36],1);Require(g.PlaceCell(0,1),"rhythmic placement");
            Require(g.Model.config.rhythmPrimaryBonus&&g.Model.config.maxMultiplier==10&&g.Model.config.hitsPerStep==3,"V3 balance is used");
            var screen=Read<CascadeGlowGraphic>(g,"screenGlow");
            Require(!screen.raycastTarget&&screen.rectTransform.anchorMin==Vector2.zero&&screen.rectTransform.anchorMax==Vector2.one,"Screen aura spans physical canvas");
            var frame=Read<CascadeGlowGraphic>(g,"frameGlow");
            Require(frame.transform.GetSiblingIndex()<Read<CascadeCell[]>(g,"views")[0].raster.transform.GetSiblingIndex(),"Rim behind orbs and HUD");
            double clock=Read<double>(g,"effectClock");
            cell.Explode(clock,false);cell.Animate(clock+.12,0,0,false,false);Require(cell.raster.burst.gameObject.activeSelf&&cell.raster.burst.color.a>0,"cascade effect");
            cell.Animate(clock+.3,0,0,false,false);Require(!cell.raster.burst.gameObject.activeSelf,"cascade ends");
            cell.Explode(clock,false);Call(g,"ShowMenu");g.StartLevel(0);Require(!cell.raster.burst.gameObject.activeSelf,"restart clears previous effects");
            Fixture(g,new int[36],1);var pause=Read<GameObject>(g,"hud").transform.Find("Pause").GetComponent<Button>();
            pause.onClick.Invoke();Require(g.State==CascadeGame.Phase.Paused,"pause callback");
            Canvas.ForceUpdateCanvases();int moves=g.MoveCount;Tap(probe,999,PixelToScreen(layout.Center(0)));Require(g.MoveCount==moves,"paused overlay blocks gameplay");
            Require(!Read<GameObject>(g,"hud").GetComponentsInChildren<Image>(true).Any(x=>x.sprite!=null&&x.sprite.name=="hud-overlay"),"static HUD sample is excluded");
            return "PASS: 36 native 36-byte touches at JSON coordinates; 4 separate sphere states and next preview; live HUD; 8 track titles; ×1–×10 / every3 hits / whole-cascade scoring / HUD thresholds / immediate input; V3 balance, screen aura/hit testing, pause, cascade and restart";
        }
        finally
        {
            if(probe!=null)InputSystem.RemoveDevice(probe);if(old!=null&&old.added)old.MakeCurrent();
            Set(g,"progressStore",originalStore);if(Directory.Exists(saveDirectory))Directory.Delete(saveDirectory,true);
            Call(g,"ShowMenu");source.mute=mute;Set(g,"offsetMs",offset);Set(g,"reduced",reduced);Read<HashSet<int>>(g,"downTouches").Clear();Set(g,"pointerTaken",false);
            if(journal!=null)File.WriteAllBytes(path,journal);else if(File.Exists(path))File.Delete(path);
        }
    }
    public static string RunLifecycle()
    {
        var g=Game();var originalStore=g.ProgressStore;bool testing=g.Catalog.testing_unlock_all_records;g.Catalog.testing_unlock_all_records=false;string dir=Path.Combine(Path.GetTempPath(),"cascade-v3-lifecycle-"+Guid.NewGuid().ToString("N"));
        var source=Read<AudioSource>(g,"music");bool mute=source.mute;int offset=Read<int>(g,"offsetMs");bool reduced=Read<bool>(g,"reduced");source.mute=true;
        string journalPath=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");byte[] prior=File.Exists(journalPath)?File.ReadAllBytes(journalPath):null;
        try
        {
            var store=new CascadeProgressStore(Path.Combine(dir,"win.json"),g.Catalog);Set(g,"progressStore",store);
            Call(g,"ShowMenu");g.StartLevel(1);Require(g.State==CascadeGame.Phase.Menu,"Locked direct launch refused");
            Fixture(g,new int[36],source.clip==null?1:source.clip.length);
            var clock=typeof(CascadeRuntimeChecks).GetMethod("Clock",BindingFlags.Static|BindingFlags.NonPublic);
            clock.Invoke(null,new object[]{g,(double)source.clip.length+.01});Call(g,"LateUpdate");
            Require(g.State==CascadeGame.Phase.Result&&g.MoveCount==0,"Zero-move song end wins");
            Require(new CascadeProgressStore(store.PathName,g.Catalog).IsOpen(g.Catalog.records[1]),"Unlock persisted before result");
            Call(g,"Finish",true);Require(store.Data.results.Count==1,"Duplicate terminal ignored");
            Set(g,"resultShownAt",Time.unscaledTimeAsDouble-1);Set(g,"resultShownFrame",Time.frameCount-1);Call(g,"UpdateResultActions");
            Read<GameObject>(g,"modal").transform.Find("Next").GetComponent<Button>().onClick.Invoke();Require(g.State==CascadeGame.Phase.Menu&&g.MenuUI.SelectedRecord==1&&g.MenuUI.CurrentPage==CascadeMenuUI.Page.Records,"Next selects the next record without auto-play");
            store=new CascadeProgressStore(Path.Combine(dir,"loss.json"),g.Catalog);Set(g,"progressStore",store);
            var full=Enumerable.Repeat(1,36).ToArray();full[0]=0;Fixture(g,full,source.clip.length-.01);
            Require(g.PlaceCell(0,source.clip.length-.005),"Final legal move accepted");
            clock.Invoke(null,new object[]{g,(double)source.clip.length+.01});Set(g,"reactionAge",1f);Call(g,"Update");
            Require(g.State==CascadeGame.Phase.Result&&g.Model.Occupied==36&&!Read<bool>(g,"wonAttempt"),"Full field beats simultaneous song end");
            Require(store.Data.results.Count==0&&store.Data.unlockedRecordIds.Count==1,"Loss cannot unlock");
            Require(!Read<GameObject>(g,"modal").transform.Find("Next").gameObject.activeSelf,"Loss has no next record");
            Fixture(g,new int[36]);g.Pause();double pos=g.SongPosition;Call(g,"Update");Require(g.SongPosition==pos,"Pause freezes song");g.Resume();Require(g.State==CascadeGame.Phase.Playing,"Explicit resume works");
            Fixture(g,new int[36],16);
            var map=Read<BeatMap>(g,"beatMap");var r=Read<Rhythm>(g,"rhythm");for(int i=1;i<=16;i++)r.Evaluate(i,0);
            var cfg=JsonUtility.FromJson<CascadeLightingConfig>(Resources.Load<TextAsset>("V3/lighting").text);
            var light=new CascadeRhythmLighting(map,cfg);Set(g,"lighting",light);
            Set(g,"<State>k__BackingField",CascadeGame.Phase.Paused);Set(g,"pausedPosition",16d);Set(g,"reduced",false);
            light.Tick(15.8,Time.unscaledTimeAsDouble-.13,16,true,false);
            Set(g,"offsetMs",0);Call(g,"UpdateV3Lighting",true,true);float alpha=Read<CascadeGlowGraphic>(g,"screenGlow").canvasRenderer.GetAlpha();
            Set(g,"offsetMs",142);Call(g,"UpdateV3Lighting",true,true);float calibrated=Read<CascadeGlowGraphic>(g,"screenGlow").canvasRenderer.GetAlpha();
            Require(Mathf.Abs(alpha-.4f)<.0001f&&Mathf.Abs(alpha-calibrated)<.0001f,"Touch calibration cannot shift visual beat peak");
            var preview=Read<CascadeCell>(g,"preview").raster;Require(preview.GetComponentInChildren<CascadeGlowGraphic>()==null,"Next orb has no rhythm glow");
            Set(g,"reduced",true);Call(g,"UpdateV3Lighting",true,true);Require(Read<CascadeGlowGraphic>(g,"screenGlow").canvasRenderer.GetAlpha()==0,"Soft mode disables aura in real renderer");
            foreach(var record in g.Catalog.records)if(!store.IsOpen(record))store.Data.unlockedRecordIds.Add(record.record_id);
            Call(g,"ShowMenu");g.StartLevel(7);Call(g,"Finish",true);
            Require(Read<Text>(g,"modalTitle").text!="КОЛЛЕКЦИЯ ПРОЙДЕНА","Opening last record cannot fake whole collection completion");
            foreach(var track in g.Catalog.Tracks)store.Complete(track,100);
            Call(g,"ShowMenu");g.StartLevel(7);Call(g,"Finish",true);
            Require(Read<Text>(g,"modalTitle").text=="КОЛЛЕКЦИЯ ПРОЙДЕНА"&&!Read<GameObject>(g,"modal").transform.Find("Next").gameObject.activeSelf,"Last record is collection complete, no ninth record");
            return "PASS: locked launch; zero-move win; persisted/idempotent unlock; next-record navigation; last-move full priority; loss result; pause/resume; actual renderer independent of touch offset; soft mode; final collection result";
        }
        finally
        {
            g.Catalog.testing_unlock_all_records=testing;Set(g,"progressStore",originalStore);Set(g,"offsetMs",offset);Set(g,"reduced",reduced);Call(g,"ShowMenu");source.mute=mute;
            if(Directory.Exists(dir))Directory.Delete(dir,true);
            if(prior==null){if(File.Exists(journalPath))File.Delete(journalPath);}else File.WriteAllBytes(journalPath,prior);
        }
    }

}
