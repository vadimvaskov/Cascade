using UnityEngine;
using UnityEngine.UI;
using A=Cascade.Prototype.CascadeApprovedUI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        RectTransform modalGlass;
        void BuildGlassModal()
        {
            foreach(var text in modal.GetComponentsInChildren<Text>(true))
            {text.font=A.Font(true);text.fontStyle=FontStyle.Normal;text.color=A.C("#FFF1E0");text.horizontalOverflow=HorizontalWrapMode.Wrap;}
            var shade=modal.transform.Find("Shade").GetComponent<CascadePanelGraphic>();
            shade.color=shade.rightColor=A.C("#15102630");shade.raycastTarget=true;
            modal.transform.Find("Modal violet atmosphere").gameObject.SetActive(false);
            modal.transform.Find("Modal divider").gameObject.SetActive(false);
            modalGlass=A.Glass("Approved modal surface",modal.transform,78,460,564,572,32,false,true).rectTransform;
            modalGlass.SetSiblingIndex(1);
            foreach(var button in modal.GetComponentsInChildren<Button>(true))
            {
                foreach(var shadow in button.GetComponentsInChildren<Shadow>(true))Destroy(shadow);
                var oldRim=button.transform.Find("Fine glass rim");if(oldRim)oldRim.gameObject.SetActive(false);
                bool primary=button.name=="Continue"||button.name=="Next";
                var surface=button.GetComponent<CascadePanelGraphic>();
                surface.color=A.C(primary?"#FFF1D3E8":"#B49BCD65");surface.rightColor=A.C(primary?"#DEA34BD9":"#4629569E");
                surface.verticalGradient=true;surface.radius=22;surface.edgeFeather=.65f;surface.SetVerticesDirty();
                A.GlassHighlights((RectTransform)button.transform,primary);
                var label=button.GetComponentInChildren<Text>();label.color=A.C(primary?"#513332":"#FFF1E0");label.transform.SetAsLastSibling();
            }
            var settings=modal.transform.Find("Settings").GetComponent<Button>();settings.onClick.RemoveAllListeners();settings.onClick.AddListener(()=>menuUI.OpenPauseSettings());
        }
        static void GlassRect(Transform item,float x,float y,float width,float height)
        {
            var r=(RectTransform)item;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);
        }
        void ModalButton(string name,float x,float y,float width,float height,int font=23)
        {
            var button=modal.transform.Find(name);if(!button)return;
            GlassRect(button,x,y,width,height);
            var label=button.GetComponentInChildren<Text>(true);label.fontSize=font;
            var r=label.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=Vector2.one*.5f;r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(-24,-8);
        }
        void LayoutApprovedModal(bool confirmation=false)
        {
            if(!approvedDesign||!modalGlass)return;
            bool paused=State==Phase.Paused;
            if(confirmation)
            {
                GlassRect(modalGlass,78,510,564,410);
                GlassRect(modalTitle.transform,102,530,516,86);modalTitle.fontSize=31;
                GlassRect(modalBody.transform,106,623,508,66);modalBody.fontSize=22;
                foreach(string name in new[]{"Restart","Menu","Settings","Effects","Next"})modal.transform.Find(name).gameObject.SetActive(false);
                ModalButton("Continue",112,718,496,80,23);ModalButton("Cancel",112,814,496,76,23);
            }
            else if(paused)
            {
                GlassRect(modalGlass,78,460,564,572);
                GlassRect(modalTitle.transform,102,478,516,62);modalTitle.fontSize=34;
                GlassRect(modalBody.transform,106,544,508,52);modalBody.fontSize=22;
                modal.transform.Find("Restart").gameObject.SetActive(true);modal.transform.Find("Menu").gameObject.SetActive(true);
                ModalButton("Continue",112,612,496,80,25);
                ModalButton("Restart",112,708,240,80,21);ModalButton("Menu",368,708,240,80,22);
                ModalButton("Settings",112,804,240,80,21);ModalButton("Effects",368,804,240,80,22);
                ModalButton("Calibrate rhythm",112,920,496,76,22);
            }
            else
            {
                GlassRect(modalGlass,78,365,564,755);
                GlassRect(modalTitle.transform,102,385,516,100);modalTitle.fontSize=32;
                GlassRect(modalBody.transform,106,493,508,230);modalBody.fontSize=23;
                modal.transform.Find("Restart").gameObject.SetActive(true);modal.transform.Find("Menu").gameObject.SetActive(true);
                bool next=modal.transform.Find("Next").gameObject.activeSelf;
                ModalButton("Next",112,748,496,80,24);
                ModalButton("Restart",112,next?844:748,496,80,23);ModalButton("Menu",112,next?940:844,496,80,23);
            }
        }
        public void SetPauseSettingsVisible(bool visible)
        {
            if(State!=Phase.Paused)return;
            modal.SetActive(!visible);
            if(!visible){RestoreContinue();modalTitle.text=T("ПАУЗА","PAUSED");modalBody.text=T("Музыка и поле ждут тебя","Your music and board are waiting");SetModalButtons(true);}
        }
    }
}
