using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cascade.Dream
{
    // Local build queue; excluded from the exported package.
    [InitializeOnLoad] public static class DreamLocalSession
    {
        static double next;
        static DreamLocalSession(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            next=EditorApplication.timeSinceStartup+1;
            const string request="Temp/CascadeDream.request";
            if(!File.Exists(request))return;
            string command=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                switch(command)
                {
                    case "import": DreamSceneBuilder.ImportHero(); break;
                    case "build": DreamSceneBuilder.Build(); break;
                    case "preview": DreamSceneTools.Preview(); break;
                    case "verify": DreamSceneTools.Verify(); break;
                    case "playcheck": DreamSceneTools.PlayCheck(); break;
                    case "record": DreamSceneTools.Record(); break;
                    case "export": DreamSceneTools.Export(); break;
                    case "demo": DreamSceneBuilder.Open(); EditorApplication.EnterPlaymode(); break;
                    default: throw new Exception("Unknown Dream scene command");
                }
                File.WriteAllText("Temp/CascadeDream.result",command+": OK");
            }
            catch(Exception e){File.WriteAllText("Temp/CascadeDream.result",command+": "+e);Debug.LogException(e);}
        }
    }
}
