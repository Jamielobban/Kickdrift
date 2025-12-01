using UnityEngine;

namespace ArcadeVP
{
    public class TrickEffects : MonoBehaviour
    {
        public ArcadeVehicleController car;
        public Rigidbody rb;

        [Header("Spin (Yaw) Effects")]
        public float spinUpwardImpulse = 2f;   // little hop
        public float spinForwardBoost  = 5f;   // small push forward

        [Header("Flip Effects")]
        public float flipForwardBoost  = 10f;  // speed gain

        [Header("Barrel Roll Effects")]
        public float rollSideImpulse   = 6f;   // move car sideways

        void OnEnable()
        {
            GameSignals.OnTrickStarted += OnTrickStarted;
        }

        void OnDisable()
        {
            GameSignals.OnTrickStarted -= OnTrickStarted;
        }

        void OnTrickStarted(TrickStartInfo info)
        {
            if (car == null || rb == null) return;

            switch (info.type)
            {
                case TrickType.YawSpin:
                    ApplySpinStartEffect(info);
                    break;

                case TrickType.Flip:
                    ApplyFlipStartEffect(info);
                    break;

                case TrickType.BarrelRoll:
                    ApplyRollStartEffect(info);
                    break;
            }
        }

        void ApplySpinStartEffect(TrickStartInfo info)
        {
            // little pop + small forward
            rb.AddForce(Vector3.up * spinUpwardImpulse, ForceMode.VelocityChange);
            rb.AddForce(car.carBody.transform.forward * spinForwardBoost, ForceMode.Acceleration);
        }

        void ApplyFlipStartEffect(TrickStartInfo info)
        {
            // pure forward speed gain, direction doesn't matter here
            rb.AddForce(car.carBody.transform.forward * flipForwardBoost, ForceMode.Acceleration);
        }

        void ApplyRollStartEffect(TrickStartInfo info)
        {
            // move sideways according to direction:
            // +1 = roll right (push right), -1 = roll left (push left)
            Vector3 side = -car.carBody.transform.right * info.direction;
            rb.AddForce(side * rollSideImpulse, ForceMode.VelocityChange);
        }
    }
}
