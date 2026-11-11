using UnityEngine;
using UnityEngine.InputSystem;

namespace Cascade.Sunset
{
    public sealed class SunsetRunWorld : MonoBehaviour
    {
        public Transform[] roadSections;
        public float sectionLength = 24f;
        [Range(0,12)] public float speed = 5.2f;
        public Animator hero;
        public Camera sceneCamera;
        public Material[] pulseMaterials;
        public bool paused;
        public bool reducedMotion;
        public float musicBpm = 112;
        [HideInInspector] public double travelled;
        float elapsed;
        Vector3 cameraOrigin;
        bool initialized;
        public float SceneTime => elapsed;
        void Awake(){ Initialize(); Application.targetFrameRate=60; }
        public void Initialize(){ if(initialized)return; if(sceneCamera)cameraOrigin=sceneCamera.transform.localPosition;initialized=true; }
        void Update()
        {
            if(Keyboard.current!=null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)) SetPaused(!paused);
            if(Keyboard.current!=null && Keyboard.current.rKey.wasPressedThisFrame) ResetRun();
            if(!paused) Advance(Time.deltaTime);
        }
        public void SetPaused(bool value){ paused=value;if(hero)hero.speed=value?0:1; }
        public void ResetRun(){elapsed=0;travelled=0;for(int i=0;i<roadSections.Length;i++)roadSections[i].localPosition=new Vector3(0,0,(i-1)*sectionLength);if(hero)hero.Play("Run",0,0);SetPaused(false);}
        public void Advance(float dt)
        {
            Initialize();
            if(paused || dt<=0)return;
            elapsed+=dt;travelled+=speed*dt;
            float span=sectionLength*roadSections.Length;
            foreach(var t in roadSections){var p=t.localPosition;p.z-=speed*dt;while(p.z< -2*sectionLength)p.z+=span;t.localPosition=p;}
            Shader.SetGlobalFloat("_SunsetTime",elapsed);
            float beat=Mathf.Pow(.5f+.5f*Mathf.Cos(elapsed*musicBpm/60f*Mathf.PI*2),8);
            if(pulseMaterials!=null)foreach(var mat in pulseMaterials)if(mat)mat.SetColor("_EmissionColor",new Color(1,.43f,.13f)*(1.7f+(reducedMotion?.12f:.5f)*beat));
            if(sceneCamera){var p=cameraOrigin;p.y+=reducedMotion?0:Mathf.Sin(elapsed*1.7f)*.018f;sceneCamera.transform.localPosition=p;}
        }
    }
}
