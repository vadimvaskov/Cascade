// Unity MCP eval script. Controlled Play Mode preview, never saved into a scene or progress.
var g=UnityEngine.Object.FindAnyObjectByType<Cascade.Prototype.CascadeGame>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
if(g.enabled)
{
    UnityEditor.SessionState.SetInt("CascadeBonusPreviewOffset",(int)typeof(Cascade.Prototype.CascadeGame).GetField("offsetMs",flags).GetValue(g));
    UnityEditor.SessionState.SetBool("CascadeBonusPreviewMute",((UnityEngine.AudioSource)typeof(Cascade.Prototype.CascadeGame).GetField("music",flags).GetValue(g)).mute);
}
int hits=UnityEditor.SessionState.GetInt("CascadeBonusPreviewHits",28);
var frame=new UnityEngine.GameObject("Bonus preview").AddComponent<CascadeEditorFrame>();
frame.action=()=>
{
    var balance=UnityEngine.JsonUtility.FromJson<Cascade.Prototype.Balance>(UnityEngine.Resources.Load<UnityEngine.TextAsset>("V3/balance").text);
    var cells=new Cascade.Prototype.Board(balance,125).cells;
    typeof(CascadeRuntimeChecks).GetMethod("Fixture",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{g,cells,hits+.2d});
    var rhythm=(Cascade.Prototype.Rhythm)typeof(Cascade.Prototype.CascadeGame).GetField("rhythm",flags).GetValue(g);
    for(int i=1;i<=hits;i++)g.Bonus.Evaluate(rhythm.Evaluate(i,0),i,0);
    typeof(Cascade.Prototype.CascadeGame).GetField("<State>k__BackingField",flags).SetValue(g,Cascade.Prototype.CascadeGame.Phase.Paused);
    typeof(Cascade.Prototype.CascadeGame).GetField("pausedPosition",flags).SetValue(g,hits+.2d);
    typeof(Cascade.Prototype.CascadeGame).GetMethod("RefreshHud",flags).Invoke(g,null);
    g.enabled=false;
};
return "Preview queued";
