using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
namespace Cascade.Prototype
{
    // Figma composites transparent artwork in sRGB. A dedicated menu target preserves those
    // colours inside the existing linear URP project; gameplay lighting stays on its own canvas.
    public sealed class CascadeMenuCompositor:MonoBehaviour
    {
        public Material CompositionMaterial{get;private set;}
        Material displayMaterial;Camera cameraUI;Canvas canvas;GameObject output;RawImage display;RenderTexture target;
        public void Initialize(Canvas source)
        {
            canvas=source;
            CompositionMaterial=new Material(Shader.Find("Cascade/Menu sRGB composition")){name="Menu composition"};
            displayMaterial=new Material(Shader.Find("Cascade/Menu sRGB display")){name="Menu display"};
            var cameraObject=new GameObject("Menu UI camera",typeof(Camera));cameraObject.transform.SetParent(transform.parent,false);
            cameraUI=cameraObject.GetComponent<Camera>();cameraUI.clearFlags=CameraClearFlags.SolidColor;cameraUI.backgroundColor=Color.black;
            cameraUI.cullingMask=1<<29;cameraUI.orthographic=true;cameraUI.orthographicSize=836;cameraUI.nearClipPlane=.1f;cameraUI.farClipPlane=20;cameraUI.allowHDR=false;cameraUI.allowMSAA=false;cameraUI.depth=20;
            var urp=cameraUI.GetUniversalAdditionalCameraData();urp.renderPostProcessing=false;urp.renderShadows=false;urp.requiresColorOption=CameraOverrideOption.Off;urp.requiresDepthOption=CameraOverrideOption.Off;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cameraUI;canvas.planeDistance=5;canvas.vertexColorAlwaysGammaSpace=true;
            output=new GameObject("Menu final display",typeof(RectTransform),typeof(Canvas));output.transform.SetParent(transform.parent,false);
            var outCanvas=output.GetComponent<Canvas>();outCanvas.renderMode=RenderMode.ScreenSpaceOverlay;outCanvas.overrideSorting=true;outCanvas.sortingOrder=11;
            var rect=new GameObject("Menu pixels",typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(output.transform,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            display=rect.gameObject.AddComponent<RawImage>();display.raycastTarget=false;display.material=displayMaterial;
        }
        void LateUpdate()
        {
            if(cameraUI==null)return;
            if(target==null||target.width!=Screen.width||target.height!=Screen.height)
            {
                if(target!=null){cameraUI.targetTexture=null;target.Release();Destroy(target);}
                target=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="Menu sRGB target",filterMode=FilterMode.Bilinear};target.Create();cameraUI.targetTexture=target;display.texture=target;
            }
        }
        void OnEnable(){if(output!=null)output.SetActive(true);if(cameraUI!=null)cameraUI.gameObject.SetActive(true);}
        void OnDisable(){if(output!=null)output.SetActive(false);if(cameraUI!=null)cameraUI.gameObject.SetActive(false);}
        void OnDestroy(){if(output!=null)Destroy(output);if(cameraUI!=null)Destroy(cameraUI.gameObject);if(target!=null){target.Release();Destroy(target);}if(CompositionMaterial!=null)Destroy(CompositionMaterial);if(displayMaterial!=null)Destroy(displayMaterial);}
    }
}
