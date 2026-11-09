using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    public sealed partial class CascadeGame
    {
        // The last board press can reveal a button under that same finger. Keep the
        // shade blocking input until release, and absorb the player's immediate next tap.
        const double ResultReadDelay = .35;
        CanvasGroup resultActions;
        double resultShownAt;
        int resultShownFrame;
        bool resultActionsReady = true;

        void PrepareResultActions()
        {
            if (resultActions == null) resultActions = modal.GetComponent<CanvasGroup>();
            if (resultActions == null) resultActions = modal.AddComponent<CanvasGroup>();
            resultActionsReady = false;
            resultShownAt = Time.unscaledTimeAsDouble;
            resultShownFrame = Time.frameCount;
            resultActions.interactable = false;
            resultActions.blocksRaycasts = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void RestoreResultActions()
        {
            resultActionsReady = true;
            if (resultActions != null) resultActions.interactable = true;
        }

        void UpdateResultActions()
        {
            if (State != Phase.Result || resultActionsReady || Time.frameCount <= resultShownFrame ||
                Time.unscaledTimeAsDouble - resultShownAt < ResultReadDelay) return;
            foreach (var device in InputSystem.devices)
            {
                if (device is Touchscreen touch)
                    foreach (var finger in touch.touches) if (finger.press.isPressed) return;
                if (device is Mouse mouse && mouse.leftButton.isPressed) return;
            }
            // LateUpdate runs after the UI has processed release, so that release cannot click.
            RestoreResultActions();
        }
    }
}
