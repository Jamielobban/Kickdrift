using UnityEngine;
using MoreMountains.Feedbacks;

namespace ArcadeVP
{
    /// <summary>
    /// Listens to GameSignals.OnTrickStarted and plays different
    /// More Mountains feedbacks for each trick type.
    /// </summary>
    public class TrickFeedbackManager : MonoBehaviour
    {
        [Header("Position")]
        [Tooltip("Where to spawn VFX / position-based feedbacks (usually the car body).")]
        public Transform vfxOrigin;

        [Header("Yaw Spin Feedbacks")]
        public MMF_Player yawSpinFeedback;

        [Header("Flip Feedbacks")]
        public MMF_Player flipFeedback;

        [Header("Barrel Roll Feedbacks")]
        public MMF_Player rollFeedback;

        void OnEnable()
        {
            GameSignals.OnTrickStarted += HandleTrickStarted;
        }

        void OnDisable()
        {
            GameSignals.OnTrickStarted -= HandleTrickStarted;
        }

        void HandleTrickStarted(TrickStartInfo info)
        {
            Vector3 pos = vfxOrigin != null ? vfxOrigin.position : transform.position;

            switch (info.type)
            {
                case TrickType.YawSpin:
                    if (yawSpinFeedback != null)
                    {
                        Debug.Log("Yaw spin");
                        yawSpinFeedback.PlayFeedbacks(pos);
                    }
                    break;

                case TrickType.Flip:
                    if (flipFeedback != null)
                    {
                        Debug.Log("Flip spin");
                        flipFeedback.PlayFeedbacks(pos);
                    }
                    break;

                case TrickType.BarrelRoll:
                    if (rollFeedback != null)
                    {
                        Debug.Log("Roll spin");
                        rollFeedback.PlayFeedbacks(pos);
                    }
                    break;
            }
        }
    }
}
