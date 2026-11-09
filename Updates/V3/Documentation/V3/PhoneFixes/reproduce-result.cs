using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Cascade.Prototype;
var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();
var flags=BindingFlags.Instance|BindingFlags.NonPublic;
var path=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");
var prior=File.Exists(path)?File.ReadAllBytes(path):null;
var old=Touchscreen.current;Touchscreen probe=null;
try {
 probe=InputSystem.AddDevice<Touchscreen>("Result transition repro");
 int[] cells=Enumerable.Repeat(1,36).ToArray();cells[26]=0;
 typeof(CascadeRuntimeChecks).GetMethod("Fixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{g,cells,1d});
 typeof(CascadeGame).GetMethod("UpdateScreenLayout",flags).Invoke(g,null);Canvas.ForceUpdateCanvases();Physics.SyncTransforms();
 var layout=JsonUtility.FromJson<CascadeRasterLayout>(g.RasterLayoutAsset.text);
 var p=Screen.safeArea.center+(layout.Center(26)-new Vector2(470.5f,836))*new Vector2(1,-1)*Mathf.Min(Screen.safeArea.width/941,Screen.safeArea.height/1672);
 var ev=typeof(CascadeMenuChecks).GetMethod("UIEvent",BindingFlags.Static|BindingFlags.NonPublic);
 ev.Invoke(null,new object[]{probe,p,1});var afterDown=g.State.ToString();
 ev.Invoke(null,new object[]{probe,p,3});var afterUp=g.State.ToString();
 var result=$"Cell26 same native touch: down={afterDown}, release={afterUp}, screen={Screen.width}x{Screen.height}";
 File.WriteAllText("Documentation/V3/PhoneFixes/result-repro-before.txt",result);return result;
} finally {
 if(probe!=null)InputSystem.RemoveDevice(probe);if(old!=null&&old.added)old.MakeCurrent();
 typeof(CascadeGame).GetMethod("ShowMenu",flags).Invoke(g,null);
 if(prior==null){if(File.Exists(path))File.Delete(path);}else File.WriteAllBytes(path,prior);
}
