using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cascade.Prototype;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class CascadeIntegrationPlayChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Pending="CascadeIntegration.PlayCheck";
    static CascadeGame game;
    static CascadeProgressStore original;
    static bool originalReduced;
    static readonly System.Collections.Generic.List<string> passed=new System.Collections.Generic.List<string>();
    static CascadeIntegrationPlayChecks(){EditorApplication.playModeStateChanged+=Changed;}
    static T Read<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);passed.Add("PASS: "+text);}
    public static void Size(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var viewType=assembly.GetType("UnityEditor.GameView");var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance").GetValue(null);
        var getGroup=sizesType.GetMethod("GetGroup");var kind=getGroup.GetParameters()[0].ParameterType;var group=getGroup.Invoke(sizes,new[]{Enum.ToObject(kind,1)});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var sizeKind=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,new object[]{Enum.ToObject(sizeKind,1),width,height,"Cascade "+width+"×"+height});
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var view=EditorWindow.GetWindow(viewType);viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1);view.Focus();
    }
    public static void Start()
    {
        Directory.CreateDirectory(CascadeIntegrationTools.Output);Size(828,1700);
        if(EditorApplication.isPlaying){Begin();return;}
        EditorSceneManager.OpenScene(CascadeIntegrationTools.ScenePath);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(CascadeIntegrationTools.ScenePath);
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);EditorApplication.delayCall+=Begin;}}
    static void Begin(){new GameObject("Integration test frame").AddComponent<CascadeEditorFrame>().action=()=>{game=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();game.StartCoroutine(Guarded());};}
    static IEnumerator Guarded()
    {
        passed.Clear();original=game.ProgressStore;originalReduced=Read<bool>(game,"reduced");var run=Run();Exception failure=null;
        while(true)
        {
            object value=null;bool more=false;
            try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){failure=e;}
            if(failure!=null||!more)break;yield return value;
        }
        try
        {
            Call(game,"ShowMenu");Set(game,"progressStore",original);Set(game,"reduced",originalReduced);game.MenuUI.ShowRecords(0);
            Read<AudioSource>(game,"music").mute=!original.Data.musicEnabled;
        }
        catch(Exception e){failure=failure??e;}
        File.WriteAllText(CascadeIntegrationTools.Output+"/play-checks.txt",string.Join("\n",passed)+"\n"+(failure==null?"ALL PLAY CHECKS PASSED":failure.ToString()));
        if(failure!=null)Debug.LogException(failure);else Debug.Log("CASCADE_INTEGRATION_PLAY_PASS");
    }
    static void Capture(string name){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(CascadeIntegrationTools.Output+"/"+name+".png");}
    static void CheckScreenAura()
    {
        Canvas.ForceUpdateCanvases();var aura=Read<CascadeGlowGraphic>(game,"screenGlow");
        var corners=new Vector3[4];aura.rectTransform.GetWorldCorners(corners);
        Check(Math.Abs(corners[0].x)<1&&Math.Abs(corners[0].y)<1&&Math.Abs(corners[2].x-Screen.width)<1&&Math.Abs(corners[2].y-Screen.height)<1,"aura follows physical screen edges at "+Screen.width+"x"+Screen.height);
        var mesh=aura.canvasRenderer.GetMesh();var colours=mesh.colors32;
        Check(mesh.vertices.Select((v,i)=>new{v,i}).Any(p=>p.v.sqrMagnitude<.1f&&colours[p.i].a==0),"screen aura leaves gameplay centre fully transparent");
    }
    static void Click(string name)
    {
        var b=game.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.name==name&&x.gameObject.activeInHierarchy);Check(b&&b.interactable,"button available: "+name);b.onClick.Invoke();
    }
    static IEnumerator Run()
    {
        yield return null;
        Check(game.UsesApprovedDesign&&game.World,"integrated game scene loaded");
        var isolated=new CascadeProgressStore(Path.Combine(Path.GetTempPath(),"CascadePlay-"+Guid.NewGuid()+"/progress.json"),game.Catalog);
        isolated.Data.musicEnabled=false;isolated.Data.soundsEnabled=false;isolated.Data.hapticsEnabled=false;isolated.Data.softEffects=false;Set(game,"progressStore",isolated);Set(game,"reduced",false);
        game.MenuUI.ShowRecords(0);yield return new WaitForEndOfFrame();Capture("01-home-mi");yield return null;
        Click("Settings");yield return new WaitForEndOfFrame();Capture("02-settings");yield return null;
        Click("Toggle Музыка");Check(isolated.Data.musicEnabled,"music toggle enabled");Click("Toggle Музыка");Check(!isolated.Data.musicEnabled,"music toggle disabled");
        Click("Language");yield return new WaitForEndOfFrame();Capture("03-language");yield return null;Click("Language 1");Check(game.IsEnglish,"language persisted");
        Click("Language");Click("Language 0");game.MenuUI.Back();
        Click("Choose hero");Click("Choose Fa");Click("Confirm hero");Check(isolated.Data.selectedHero=="Fa","Fa selected on home");yield return new WaitForEndOfFrame();Capture("04-home-fa");yield return null;
        Click("Choose hero");Click("Choose Mi");yield return new WaitForEndOfFrame();Capture("05-heroes");yield return null;Click("Confirm hero");
        Click("World map");yield return new WaitForEndOfFrame();Capture("06-world-map");yield return null;game.MenuUI.Back();
        Click("Open record");yield return new WaitForEndOfFrame();Capture("07-tracks");yield return null;Click("Play selected");
        yield return new WaitForSeconds(3.6f);
        Check(game.State==CascadeGame.Phase.Playing,"home → record → countdown → gameplay");
        Check(Read<AudioSource>(game,"music").mute,"music muted without stopping song");
        Check(game.World.hero.name.Contains("Mi"),"selected Mi is running");
        double before=game.SongPosition, distance=game.World.Distance;yield return new WaitForSeconds(.25f);
        Check(game.SongPosition>before&&game.World.Distance>distance,"muted clock and world continue");
        var screenAura=Read<CascadeGlowGraphic>(game,"screenGlow");var boardAura=Read<CascadeGlowGraphic>(game,"frameGlow");
        Check(screenAura&&boardAura&&!screenAura.raycastTarget&&!boardAura.raycastTarget,"screen and board halos exist without blocking input");
        CheckScreenAura();
        float lowestPulse=1,highestPulse=0,screenPeak=0,boardPeak=0,closestOrb=10,farthestOrb=0;bool emissionInSync=true;
        for(int frame=0;frame<40;frame++)
        {
            yield return new WaitForSeconds(.02f);
            float pulse=game.World.GetComponent<CascadeBridgeLightPool>().RhythmPulse;
            lowestPulse=Mathf.Min(lowestPulse,pulse);highestPulse=Mathf.Max(highestPulse,pulse);
            screenPeak=Mathf.Max(screenPeak,screenAura.canvasRenderer.GetAlpha());boardPeak=Mathf.Max(boardPeak,boardAura.canvasRenderer.GetAlpha());
            float ahead=game.World.companionOrb.localPosition.z-game.World.hero.transform.localPosition.z;
            closestOrb=Mathf.Min(closestOrb,ahead);farthestOrb=Mathf.Max(farthestOrb,ahead);
            emissionInSync&=Math.Abs(Shader.GetGlobalFloat("_CascadeWorldPulse")-pulse)<.0001f;
        }
        Check(game.Streak==0&&highestPulse>.08f&&lowestPulse<.02f,"world light pulses continuously before any hit with music muted (range "+lowestPulse+"–"+highestPulse+", reduced="+Read<bool>(game,"reduced")+")");
        Check(emissionInSync,"lantern emission and local light share pulse");
        Check(screenPeak>.13f&&boardPeak>.23f,"screen perimeter and broad board halo pulse before any hit");
        Check(game.RhythmGuide.Active,"visual guide works before the first hit");
        Canvas.ForceUpdateCanvases();var guideMesh=game.RhythmGuide.canvasRenderer.GetMesh();Check(guideMesh&&guideMesh.vertexCount>16,"visual guide produces visible geometry");
        Check(Read<Text>(game,"guideCaption").cachedTextGenerator.characterCountVisible>0,"rhythm instruction fits the visible caption");
        var heroFill=game.World.GetComponentsInChildren<Light>().First(l=>l.name=="Soft camera-side fill");
        uint heroMask=UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(heroFill).renderingLayers;
        Check(game.World.hero.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>(r.renderingLayerMask&heroMask)!=0)&&
            game.World.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Batch | Paver")).All(r=>(r.renderingLayerMask&heroMask)==0),"character fill illuminates runner without flattening bridge lighting");
        Check(game.World.GetComponentsInChildren<Transform>().Count(t=>t.name=="Companion music sphere")==1,"exactly one companion sphere");
        Check(closestOrb>=3.79f&&farthestOrb<=6.01f&&farthestOrb-closestOrb>.25f,"single companion visibly travels ahead of hero without leaving its lane");
        // Exercise the real screen-coordinate hit path with the original input event timestamp.
        var cell=game.GetComponentsInChildren<CascadeCell>().First(c=>c.index>=0&&c.index<36&&game.Model.cells[c.index]==0&&c.GetComponent<Collider>());
        var camera=Read<Camera>(game,"gameCamera");Physics.SyncTransforms();var point=(Vector2)camera.WorldToScreenPoint(cell.transform.position);
        int moves=game.MoveCount,emissions=game.MusicEnergy.EmissionCount,placedCharge=game.Model.NextCharge;Call(game,"AcceptPointer",point,InputState.currentTime);
        Check(game.MoveCount==moves+1,"screen tap reaches matching board collider");
        Check(game.MusicEnergy.EmissionCount==emissions+1&&game.MusicEnergy.LastCharge==placedCharge&&Vector3.Distance(game.MusicEnergy.LastOrigin,game.World.companionOrb.position)<.001f,"accepted placement emits energy from the flying companion in world space");
        Check(!game.MusicEnergy.GetComponent<Collider>()&&!game.GetComponentsInChildren<RectTransform>(true).Any(r=>r.name=="Music energy from board to hero"),"board-origin overlay removed and world stream does not block input");
        int occupied=Array.FindIndex(game.Model.cells,c=>c!=0);
        Check(!game.PlaceCell(occupied,game.SongPosition)&&game.MusicEnergy.EmissionCount==emissions+1,"occupied-cell tap creates no energy or extra move");
        yield return new WaitForEndOfFrame();Capture("08-gameplay-mi");yield return null;
        yield return new WaitForSeconds(.32f);yield return new WaitForEndOfFrame();Capture("companion-volume-flight");yield return null;
        Check(game.MusicEnergy.ActiveCount>0&&game.MusicEnergy.StreamMesh.vertexCount>500&&game.MusicEnergy.StreamMesh.bounds.size.z>.5f&&game.MusicEnergy.StreamRenderer.enabled,"music energy has real depth and visible volumetric geometry");
        game.Pause();yield return null;double energyClock=game.MusicEnergy.Clock;Vector3 orbPosition=game.World.companionOrb.localPosition;yield return new WaitForSeconds(.2f);
        Check(game.MusicEnergy.Clock==energyClock&&game.World.companionOrb.localPosition==orbPosition,"pause freezes energy in flight and the escaping sphere");
        game.Resume();
        yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();Capture("companion-volume-midflight");yield return null;
        for(int i=0;i<150&&game.MusicEnergy.Absorption<.3f;i++)yield return null;
        Check(game.MusicEnergy.Absorption>.3f,"travelling energy reaches the runner");
        var absorption=game.World.GetComponentsInChildren<Light>().First(l=>l.name=="Hero music absorption");
        Check(absorption.enabled&&absorption.intensity>0&&absorption.cullingMask==(1<<28),"absorption briefly lights only the hero");
        yield return new WaitForEndOfFrame();Capture("companion-volume-absorption");yield return null;
        yield return new WaitForSeconds(.35f);Check(game.MusicEnergy.ActiveCount==0&&!absorption.enabled,"energy and hero glow settle after arrival");
        // Actual rendered sprite identity must agree for every charge, including the
        // special note. Exercise this in the isolated attempt without changing progression.
        int originalCharge=game.Model.NextCharge;
        var nextSetter=typeof(Board).GetProperty("NextCharge").GetSetMethod(true);
        var nextView=Read<CascadeCell>(game,"preview");
        for(int charge=1;charge<=4;charge++)
        {
            nextSetter.Invoke(game.Model,new object[]{charge});Call(game,"RefreshHud");
            yield return new WaitForEndOfFrame();
            Check(game.CompanionCharge==charge&&game.CompanionSprite==nextView.raster.image.sprite&&game.CompanionSprite==cell.raster.sprites[charge-1],"world, next preview and board share charge "+charge+" artwork");
            Check(!game.World.companionOrb.GetComponentsInChildren<Renderer>().Any(r=>r.enabled),"no differently tinted 3D duplicate for charge "+charge);
            Capture("sunset-sphere-"+charge);yield return null;
        }
        nextSetter.Invoke(game.Model,new object[]{originalCharge});Call(game,"RefreshHud");
        while(game.SongPosition<5)yield return null;
        // Build an isolated genuine hit streak on past valid ticks. This exercises
        // the real multiplier → runtime light path without touching the user's save.
        var originalRhythm=Read<Rhythm>(game,"rhythm");var balance=Read<Balance>(game,"balance");
        var boosted=new Rhythm(game.TimingGrid.Map,balance);
        var recent=game.TimingGrid.Map.beats.Where(t=>t<=game.SongPosition&&!game.TimingGrid.Map.IsExcluded(t)).Reverse().Take(balance.hitsPerStep*9).Reverse();
        foreach(double tick in recent)Check(boosted.Evaluate(tick,0).kind=="hit","review hit accepted");
        Set(game,"rhythm",boosted);yield return new WaitForSeconds(.4f);
        Check(game.Multiplier==10&&game.RunPulse.Progress>.999f,"real x10 multiplier drives maximum light amplitude");
        for(int i=0;i<90&&game.RunPulse.Wave<.95f;i++)yield return null;
        Check(game.World.GetComponent<CascadeBridgeLightPool>().RhythmPulse>.44f,"maximum pulse reaches the bridge light pool");
        Check(screenAura.canvasRenderer.GetAlpha()>screenPeak*2.3f&&boardAura.canvasRenderer.GetAlpha()>boardPeak*2.8f,"both perimeter halos grow to x10 with the real streak");
        yield return new WaitForEndOfFrame();Capture("pulse-x10-peak");yield return null;
        for(int i=0;i<90&&game.RunPulse.Wave>.05f;i++)yield return null;
        yield return new WaitForEndOfFrame();Capture("pulse-x10-trough");yield return null;
        Set(game,"rhythm",originalRhythm);
        game.Pause();yield return null;double stopped=game.World.Distance,paused=game.SongPosition;float pausedPulse=game.RunPulse.Value,pausedAura=screenAura.canvasRenderer.GetAlpha();yield return new WaitForSeconds(.2f);
        Check(Math.Abs(game.World.Distance-stopped)<1e-6&&Math.Abs(game.SongPosition-paused)<1e-6,"pause freezes world and rhythm");
        Check(Math.Abs(game.RunPulse.Value-pausedPulse)<.0001f,"pause freezes light breath without drift");
        Check(Math.Abs(screenAura.canvasRenderer.GetAlpha()-pausedAura)<.0001f,"pause freezes screen halo with the shared rhythm");
        yield return new WaitForEndOfFrame();Capture("09-pause");yield return null;
        int pausedMoves=game.MoveCount;
        Click("Settings");yield return new WaitForEndOfFrame();Capture("glass-pause-settings");yield return null;
        Check(game.State==CascadeGame.Phase.Paused&&game.World.gameObject.activeInHierarchy&&!Read<RectTransform>(game.MenuUI,"background").gameObject.activeSelf,"pause settings preserve the actual world background");
        Click("Language");Click("Language 1");yield return new WaitForEndOfFrame();Capture("glass-pause-settings-en");yield return null;
        Click("Close");Check(!game.MenuUI.gameObject.activeSelf&&Read<GameObject>(game,"modal").activeSelf&&game.IsEnglish,"closing pause settings returns to the same paused attempt");
        game.Restart();yield return new WaitForEndOfFrame();Capture("glass-confirm");yield return null;Click("Cancel");
        Check(game.MoveCount==pausedMoves&&game.State==CascadeGame.Phase.Paused&&Math.Abs(game.SongPosition-paused)<1e-6&&Math.Abs(game.World.Distance-stopped)<1e-6,"cancelling restart preserves board, audio position and scenery");
        Click("Settings");Click("Language");Click("Language 0");Click("Close");
        Check(Read<GameObject>(game,"modal").transform.Find("Continue").GetComponentInChildren<Text>().text=="ПРОДОЛЖИТЬ","pause primary action follows the changed language");
        Size(750,1334);yield return new WaitForSeconds(.2f);CheckScreenAura();yield return new WaitForEndOfFrame();Capture("glass-pause-compact");yield return null;Size(828,1700);
        game.Resume();yield return new WaitForSeconds(.2f);Check(game.State==CascadeGame.Phase.Playing&&game.SongPosition>paused,"resume continues same attempt");
        Call(game,"Finish",false);yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();Capture("10-result");yield return null;
        game.ExitMenu();Check(game.State==CascadeGame.Phase.Menu,"result returns home");
        yield return null;Check(screenAura.canvasRenderer.GetAlpha()==0,"gameplay screen aura is cleared in home menu");
        game.MenuSetHero("Fa");game.StartMenuTrack(game.Catalog.Tracks[0].track_id);yield return new WaitForSeconds(3.5f);
        Check(game.World.hero.name.Contains("Fa")&&game.World.hero.avatar.isHuman,"Fa choice changes the actual runner");
        yield return new WaitForEndOfFrame();Capture("11-gameplay-fa");yield return null;
        int faCell=Array.FindIndex(game.Model.cells,c=>c==0);Check(game.PlaceCell(faCell,game.SongPosition),"Fa accepts a real placement for energy review");
        yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();Capture("companion-volume-fa-flight");yield return null;
        for(int i=0;i<150&&game.MusicEnergy.Absorption<.3f;i++)yield return null;
        Check(game.MusicEnergy.Absorption>.3f&&absorption.enabled,"energy targets the selected Fa runner too");
        yield return new WaitForEndOfFrame();Capture("companion-volume-fa-absorption");yield return null;
        Size(750,1334);yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();Capture("sunset-gameplay-compact");yield return null;Size(828,1700);
        var lightPool=game.World.GetComponent<CascadeBridgeLightPool>();bool bounded=lightPool!=null;
        for(int cycle=0;cycle<10;cycle++)
        {
            game.World.Advance(8);yield return new WaitForSeconds(.12f);
            bounded &= lightPool&&lightPool.ActiveCount>0&&lightPool.ActiveCount<=8;
            if(lightPool)bounded &= lightPool.lamps.Where(l=>l.enabled).All(l=>l.shadows==LightShadows.None&&l.transform.position.z-lightPool.viewer.position.z>=-4&&l.transform.position.z-lightPool.viewer.position.z<=27);
        }
        Check(bounded,"bridge lighting stays bounded and follows recycled scenery over 80 simulated seconds");
        game.Pause();Call(game,"ShowMenu");game.MenuSetHero("Mi");
        game.MenuUI.ShowRecords(0);
        Size(780,1688);yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();Capture("12-home-tall");yield return null;
        Size(750,1334);yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();Capture("13-home-compact");yield return null;
        Size(828,1700);yield return new WaitForSeconds(.3f);
        Check(game.MenuUI.SafeRoot.rect.size==new Vector2(414,850),"safe composition retains aspect ratio");
    }
}
