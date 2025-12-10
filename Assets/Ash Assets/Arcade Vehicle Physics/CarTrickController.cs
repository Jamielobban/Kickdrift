using UnityEngine;
using DG.Tweening;

namespace ArcadeVP
{
    public class CarTrickController : MonoBehaviour
    {
        [Header("Refs")]
        public ArcadeVehicleController car;
        public Transform visualRoot;  // usually BodyMesh.parent
        public TrickChargeManager trickChargeManager;

        [Header("Yaw Spin (Left/Right)")]
        public float yawDuration = 0.5f;
        public float yawAngle = 360f;
        public AnimationCurve yawEase = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Flips (Forward/Back)")]
        public float flipDuration = 0.6f;
        public float flipAngle = 360f;
        public AnimationCurve flipEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Barrel Rolls")]
        public float rollDuration = 0.6f;
        public float rollAngle = 360f;
        public AnimationCurve rollEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Landing Quality")]
        public float landingTiltThreshold = 15f; // degrees from upright

        [Header("Floor Safety")]
        [Tooltip("Minimum distance from ground required to START a trick.")]
        public float minTrickHeight = 0.8f;
        [Tooltip("Max distance to raycast down when checking trick height.")]
        public float groundRayLength = 5f;

        Tween currentTween;
        bool tweenActive;
        bool awaitingLanding;
        TrickType lastTrick;

        // combo tracking for this airtime
        int yawSpinCount;
        int flipCount;
        int rollCount;
        float yawAngleAccum;

        // simple queue for one “next trick”
        bool queuedTrick;
        TrickType queuedType;
        float queuedDir;

        void OnDisable()
        {
            if (currentTween != null && currentTween.IsActive())
            {
                currentTween.Kill();
                currentTween = null;
            }

            if (car != null)
                car.SetAirControlLocked(false);
        }

        /// <summary>
        /// Public entry: request a trick. If one is already playing, we queue this
        /// so it fires instantly when the current tween ends (good for 720s, etc.).
        /// direction: +1 or -1 (right/left, front/back depending on trick type)
        /// </summary>
        public void TryStartTrick(TrickType type, float direction = 1f)
        {
            if (car == null || visualRoot == null) return;
            if (car.grounded()) return;   // only in air

            // normalize direction so 0 becomes +1 by default
            if (Mathf.Approximately(direction, 0f))
                direction = 1f;

            // if a tween is currently active, queue the next trick instead of dropping it
            if (tweenActive)
            {
                queuedTrick = true;
                queuedType = type;
                queuedDir = direction;
                return;
            }

            StartTrickInternal(type, direction);
        }

        /// <summary>
        /// Actually start a trick tween, update combo counts, lock air control, etc.
        /// </summary>
    void StartTrickInternal(TrickType type, float direction)
    {
        if (car == null || visualRoot == null) return;
        if (car.grounded()) return;

        // normalize direction
        if (Mathf.Approximately(direction, 0f))
            direction = 1f;
        float sign = Mathf.Sign(direction);

        // --- height guard: don't allow NEW tricks right on the floor ---
        if (!HasEnoughHeight())
            return;

        // --- CHARGE CONSUMPTION: spend 1 charge RIGHT NOW ---
        if (trickChargeManager != null)
        {
            //Debug.Log(trickChargeManager);
            if (!trickChargeManager.TryConsumeCharge())
            {
                Debug.Log(trickChargeManager.TryConsumeCharge());
                // no charges → trick doesn't start
                return;
            }
        }

        // if this is the FIRST trick of this airtime, reset combo + reset visual rotation
        if (!awaitingLanding)
        {
            yawSpinCount  = 0;
            flipCount     = 0;
            rollCount     = 0;
            yawAngleAccum = 0f;
            visualRoot.localRotation = Quaternion.identity;
        }

        awaitingLanding = true;
        lastTrick = type;

        if (currentTween != null && currentTween.IsActive())
            currentTween.Kill();

        Quaternion startRot = visualRoot.localRotation;
        Vector3 axis = Vector3.up;
        float angle = 0f;
        float duration = 0.5f;
        AnimationCurve ease = AnimationCurve.Linear(0, 0, 1, 1);

        switch (type)
        {
            case TrickType.YawSpin:
                axis = Vector3.up;
                angle = yawAngle * sign;
                duration = yawDuration;
                ease = yawEase;
                yawSpinCount++;
                yawAngleAccum += Mathf.Abs(angle);
                break;

            case TrickType.Flip:
                axis = Vector3.right;
                angle = flipAngle * sign;
                duration = flipDuration;
                ease = flipEase;
                flipCount++;
                break;

            case TrickType.BarrelRoll:
                axis = Vector3.forward;
                angle = rollAngle * sign;
                duration = rollDuration;
                ease = rollEase;
                rollCount++;
                break;
        }

        // 🔔 Now it’s safe to notify about trick start (for boosts, VFX, etc.)
        var startInfo = new TrickStartInfo
        {
            type = type,
            direction = sign
        };
        GameSignals.RaiseTrickStarted(startInfo);

        tweenActive = true;
        if (car != null)
            car.SetAirControlLocked(true);

        float t = 0f;
        currentTween = DOTween.To(
                () => t,
                x =>
                {
                    t = x;
                    float a = Mathf.Lerp(0f, angle, t);
                    visualRoot.localRotation = Quaternion.AngleAxis(a, axis) * startRot;
                },
                1f,
                duration
            )
            .SetEase(ease)
            .OnComplete(() =>
            {
                tweenActive = false;

                if (car != null)
                    car.SetAirControlLocked(false);

                int yawRevs = Mathf.RoundToInt(yawAngleAccum / 360f);
                var progress = new TrickProgressInfo
                {
                    yawRevolutions = yawRevs,
                    flipCount      = flipCount,
                    rollCount      = rollCount,
                    lastType       = lastTrick
                };
                GameSignals.RaiseTrickProgress(progress);

                if (queuedTrick && !car.grounded())
                {
                    var qt = queuedType;
                    var qd = queuedDir;
                    queuedTrick = false;
                    StartTrickInternal(qt, qd);
                }
            });
    }


        // Check if we are far enough from the ground to safely START a new trick
        bool HasEnoughHeight()
        {
            if (car == null || car.rb == null) return true;

            RaycastHit hit;
            Vector3 origin = car.rb.position;
            Vector3 dir = Vector3.down;

            if (Physics.Raycast(origin, dir, out hit, groundRayLength, car.movement.drivableSurface))
            {
                // hit distance is how far we are from the drivable surface
                return hit.distance >= minTrickHeight;
            }

            // no ground detected within ray length = we're high enough
            return true;
        }

        void Update()
        {
            if (!awaitingLanding) return;
            if (!car.grounded())  return;

            // we just touched ground after at least one trick this airtime
            bool wasMidSpin = tweenActive;   // if true, tween is still finishing (visual spin continues)

            awaitingLanding = false;
            queuedTrick = false;

            float tilt = Vector3.Angle(car.carBody.transform.up, Vector3.up);
            bool cleanOrientation = tilt <= landingTiltThreshold;

            int yawRevs = Mathf.RoundToInt(yawAngleAccum / 360f);

            var info = new TrickLandingInfo
            {
                clean          = cleanOrientation,
                fullyCompleted = !wasMidSpin,   // “timing” flag
                yawRevolutions = yawRevs,
                flipCount      = flipCount,
                rollCount      = rollCount,
                lastType       = lastTrick
            };

            GameSignals.RaiseTrickLanded(info);

            // IMPORTANT: we don't reset visualRoot rotation here
            // so the spin can visually settle on the floor if needed.
        }
    }
}
