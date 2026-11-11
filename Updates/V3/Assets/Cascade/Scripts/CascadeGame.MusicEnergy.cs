using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        CascadeMusicEnergy3D musicEnergy;
        Light absorptionLight;
        Transform absorptionBone;
        Vector3 companionHome;
        public CascadeMusicEnergy3D MusicEnergy=>musicEnergy;

        void InitializeMusicEnergy()
        {
            var stream=new GameObject("Companion to hero | volumetric music energy");stream.layer=27;
            stream.transform.SetParent(dreamWorld.transform,false);
            musicEnergy=stream.AddComponent<CascadeMusicEnergy3D>();
            // Fixed small light, restricted to the runner's render layer.
            var glow=new GameObject("Hero music absorption",typeof(Light));glow.layer=27;glow.transform.SetParent(dreamWorld.transform,false);
            absorptionLight=glow.GetComponent<Light>();absorptionLight.type=LightType.Point;absorptionLight.range=1.8f;
            absorptionLight.shadows=LightShadows.None;absorptionLight.cullingMask=1<<28;absorptionLight.intensity=0;absorptionLight.enabled=false;
            absorptionLight.GetUniversalAdditionalLightData().renderingLayers=2u;
        }
        void ResetMusicEnergy()
        {
            companionHome=dreamWorld.companionOrb.localPosition-dreamWorld.hero.transform.localPosition;
            absorptionBone=dreamWorld.hero.GetBoneTransform(HumanBodyBones.Chest);
            if(!absorptionBone)absorptionBone=dreamWorld.hero.GetBoneTransform(HumanBodyBones.Spine);
            musicEnergy?.Clear();if(absorptionLight){absorptionLight.intensity=0;absorptionLight.enabled=false;}
        }
        void AnimateCompanionFlight()
        {
            dreamWorld.companionOrb.localPosition=dreamWorld.hero.transform.localPosition+companionHome+
                CascadeCompanionFlight.Offset(dreamWorld.SceneTime,reduced);
        }
        void EmitMusicEnergy(Turn turn,bool onBeat)
        {
            if(!approvedDesign||!musicEnergy||turn==null)return;
            // The board action is only the trigger. All energy originates in the world companion.
            musicEnergy.Emit(dreamWorld.companionOrb.position,turn.placedCharge,onBeat,effectClock);
        }
        void UpdateMusicEnergy()
        {
            if(!musicEnergy)return;
            bool visible=State==Phase.Playing||State==Phase.Reaction||State==Phase.Paused;
            if(!visible){musicEnergy.Clear();absorptionLight.enabled=false;return;}
            var aim=absorptionBone?absorptionBone.position:dreamWorld.hero.transform.position+Vector3.up*1.1f;
            musicEnergy.Present(effectClock,dreamWorld.companionOrb.position,aim,reduced);
            absorptionLight.transform.position=aim-dreamWorld.hero.transform.forward*.75f;
            absorptionLight.color=Color.Lerp(musicEnergy.AbsorptionColor,new Color(1,.9f,.7f),.65f);
            absorptionLight.intensity=musicEnergy.Absorption*(reduced?.35f:1f);
            absorptionLight.enabled=absorptionLight.intensity>.005f;
        }
    }
}
