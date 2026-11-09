using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cascade.Prototype;
IEnumerator Reproduce()
{
 yield return null;
 var g=UnityEngine.Object.FindAnyObjectByType<CascadeGame>();var f=BindingFlags.Instance|BindingFlags.NonPublic;var s=BindingFlags.Static|BindingFlags.NonPublic;
 var pth=Path.Combine(Application.persistentDataPath,"cascade-v3-last-attempt.json");var log=File.Exists(pth)?File.ReadAllBytes(pth):null;
 int offset=(int)typeof(CascadeGame).GetField("offsetMs",f).GetValue(g);
 var old=Touchscreen.current;var t=InputSystem.AddDevice<Touchscreen>("Unguarded result reproduction");
 try
 {
  var cells=Enumerable.Repeat(1,36).ToArray();cells[26]=0;typeof(CascadeRuntimeChecks).GetMethod("Fixture",s).Invoke(null,new object[]{g,cells,1d});
  yield return null;typeof(CascadeGame).GetMethod("UpdateScreenLayout",f).Invoke(g,null);Canvas.ForceUpdateCanvases();Physics.SyncTransforms();
  var q=JsonUtility.FromJson<CascadeRasterLayout>(g.RasterLayoutAsset.text).Center(26);var pt=Screen.safeArea.center+new Vector2(q.x-470.5f,836-q.y)*Mathf.Min(Screen.safeArea.width/941,Screen.safeArea.height/1672);
  var queue=typeof(CascadeMenuChecks).GetMethod("Queue",s);typeof(CascadeRuntimeChecks).GetMethod("Clock",s).Invoke(null,new object[]{g,1d});
  queue.Invoke(null,new object[]{t,4444,pt,1,InputState.currentTime});InputSystem.Update();
  // Recreate build11's immediately interactive result panel, keeping all other UI unchanged.
  typeof(CascadeGame).GetMethod("RestoreResultActions",f).Invoke(g,null);Canvas.ForceUpdateCanvases();EventSystem.current.currentInputModule.Process();
  var down=g.State.ToString();yield return null;
  queue.Invoke(null,new object[]{t,4444,pt,3,InputState.currentTime});InputSystem.Update();EventSystem.current.currentInputModule.Process();
  File.WriteAllText("Documentation/V3/PhoneFixes/result-unguarded.txt","Native final cell26 with build11-style immediate buttons: press="+down+", release="+g.State+", resolution="+Screen.width+"x"+Screen.height);
 }
 finally
 {
  InputSystem.RemoveDevice(t);if(old!=null&&old.added)old.MakeCurrent();typeof(CascadeGame).GetField("offsetMs",f).SetValue(g,offset);typeof(CascadeGame).GetMethod("ShowMenu",f).Invoke(g,null);
  if(log==null){if(File.Exists(pth))File.Delete(pth);}else File.WriteAllBytes(pth,log);
 }
}
UnityEngine.Object.FindAnyObjectByType<CascadeGame>().StartCoroutine(Reproduce());return "Reproducing immediate-button behavior";
