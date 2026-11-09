#if UNITY_EDITOR
using System;
using UnityEngine;

// Editor validation hook. This type is omitted from every player build.
public sealed class CascadeEditorFrame:MonoBehaviour
{
    public Action action;
    void Update()
    {
        enabled=false;
        try{action?.Invoke();}finally{Destroy(gameObject);}
    }
}
#endif
