var g=UnityEngine.Object.FindAnyObjectByType<Cascade.Prototype.CascadeGame>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
typeof(Cascade.Prototype.CascadeGame).GetField("offsetMs",flags).SetValue(g,UnityEditor.SessionState.GetInt("CascadeBonusPreviewOffset",0));
((UnityEngine.AudioSource)typeof(Cascade.Prototype.CascadeGame).GetField("music",flags).GetValue(g)).mute=UnityEditor.SessionState.GetBool("CascadeBonusPreviewMute",false);
typeof(Cascade.Prototype.CascadeGame).GetMethod("ShowMenu",flags).Invoke(g,null);
g.enabled=true;
UnityEngine.Application.runInBackground=false;
return "Menu restored";
