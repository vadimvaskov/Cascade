using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cascade.Dream
{
    [Serializable] public class DreamLayer
    {
        public Transform[] sections;
        public float spacing = 36;
        public float speedFactor = 1;
        public float recycleBehind = -40;
        [NonSerialized] public Vector3[] origins;
    }

    /// <summary>Stationary runner with pooled scenery. No runtime mesh generation or spawning.</summary>
    public sealed class DreamRunWorld : MonoBehaviour
    {
        public DreamLayer[] layers;
        public Animator hero;
        public Transform companionOrb;
        public Transform orbHalo;
        public Camera sceneCamera;
        [Range(0,12)] public float targetSpeed = 4.6f;
        [Min(.01f)] public float accelerationTime = 1.5f;
        public float strideSpeed = 4.6f;
        public float musicBpm = 112;
        public bool paused;
        public bool reducedMotion = true;
        public bool enableDemoKeys = true;
        public double Distance { get; private set; }
        public double SceneTime { get; private set; }
        public float CurrentSpeed { get; private set; }
        Vector3 cameraOrigin, orbOffset, heroOrigin;
        float velocity;
        bool initialized;
        public event Action<bool> PauseChanged;
        public event Action<float> SpeedChanged;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return;
            foreach (var layer in layers)
            {
                layer.origins = new Vector3[layer.sections.Length];
                for (int i=0;i<layer.sections.Length;i++) layer.origins[i]=layer.sections[i].localPosition;
            }
            if (sceneCamera) cameraOrigin=sceneCamera.transform.localPosition;
            if (hero) { heroOrigin=hero.transform.localPosition; hero.applyRootMotion=false; }
            if (companionOrb) orbOffset=companionOrb.localPosition-heroOrigin;
            initialized=true;
        }
        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (enableDemoKeys && Keyboard.current!=null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!paused);
                if (Keyboard.current.rKey.wasPressedThisFrame) ResetRun();
            }
#endif
            Advance(Mathf.Min(Time.deltaTime,.1f));
        }
        public void SetSpeed(float metresPerSecond) { targetSpeed=Mathf.Max(0,metresPerSecond); SpeedChanged?.Invoke(targetSpeed); }
        public void SetPaused(bool value) { paused=value; if(hero)hero.speed=value?0:CurrentSpeed/strideSpeed; PauseChanged?.Invoke(value); }
        public void ResetRun()
        {
            Initialize(); Distance=0; SceneTime=0; CurrentSpeed=0; velocity=0;
            foreach(var layer in layers)for(int i=0;i<layer.sections.Length;i++)layer.sections[i].localPosition=layer.origins[i];
            if(hero){hero.transform.localPosition=heroOrigin;hero.Play("Run",0,0);}
            if(companionOrb)companionOrb.localPosition=heroOrigin+orbOffset;
            SetPaused(false);
        }
        public void Advance(float dt)
        {
            Initialize(); if(paused || dt<=0)return;
            CurrentSpeed=Mathf.SmoothDamp(CurrentSpeed,targetSpeed,ref velocity,accelerationTime,Mathf.Infinity,dt);
            Distance+=(double)CurrentSpeed*dt; SceneTime+=dt;
            foreach(var layer in layers)
            {
                double span=layer.spacing*layer.sections.Length;
                for(int i=0;i<layer.sections.Length;i++)
                {
                    var p=layer.origins[i];
                    double z=p.z-Distance*layer.speedFactor;
                    z=layer.recycleBehind+((z-layer.recycleBehind)%span+span)%span;
                    p.z=(float)z; layer.sections[i].localPosition=p;
                }
            }
            if(hero)hero.speed=CurrentSpeed/Mathf.Max(.01f,strideSpeed);
            float t=(float)(SceneTime%3600);
            Shader.SetGlobalFloat("_DreamTime",t);
            if(companionOrb)
            {
                var p=hero?hero.transform.localPosition:heroOrigin;
                p+=orbOffset;p.y+=Mathf.Sin(t*2.1f)*(reducedMotion?.035f:.075f);
                companionOrb.localPosition=p;
                float pulse=1+.018f*Mathf.Sin(t*musicBpm/60*Mathf.PI*2);
                companionOrb.localScale=Vector3.one*pulse;
                if(orbHalo)orbHalo.localRotation=Quaternion.Euler(74,0,t*14);
            }
            if(sceneCamera)sceneCamera.transform.localPosition=cameraOrigin+(reducedMotion?Vector3.zero:Vector3.up*Mathf.Sin(t*1.7f)*.009f);
        }
    }
}
