using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Cascade.Prototype;
public sealed class CascadeBuildValidation:IPreprocessBuildWithReport
{
 public int callbackOrder=>0;
 [Serializable] class Manifest{public Track[] tracks;}
 [Serializable] class Track{public string trackId,resourceName,audioSha256;public double duration;}
 public void OnPreprocessBuild(BuildReport report){Run();}
 [MenuItem("Cascade/Verify audio and maps")]
 public static string Run()
 {
  var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Cascade/Resources/Data/audio-manifest.json"));
  foreach(var item in manifest.tracks)
  {
   string path="Assets/Cascade/Resources/Audio/"+item.resourceName+".wav";string hash;
   using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
   var map=JsonUtility.FromJson<BeatMap>(File.ReadAllText("Assets/Cascade/Resources/Data/"+item.resourceName+".json"));var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
   if(hash!=item.audioSha256||map.trackId!=item.trackId||clip==null||!map.Valid(clip.length,hash,true))throw new BuildFailedException("Audio/map mismatch: "+item.resourceName);
  }
  return "PASS: 3 audio SHA-256 signatures, imported clip durations and beat-map structures";
 }
}
