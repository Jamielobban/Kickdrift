using UnityEngine;

namespace ArcadeVP
{
    public class CarBailHandler : MonoBehaviour
    {
        public ArcadeVehicleController car;
        public Transform visualRoot;   // same as CarTrickController.visualRoot (for future VFX)

        [Header("Bail Behaviour")]
        public float bounceUpForce    = 5f;
        public float bounceBackForce  = 3f;
        public float speedDampFactor  = 0.3f;  // keep 30% speed after bail
        public float bailDuration     = 0.8f;  // short lockout window
        public float uprightSpeed     = 8f;    // how fast to stand upright

        bool isBailing;
        float bailTimer;

        void OnEnable()
        {
            GameSignals.OnBail += HandleBail;
        }

        void OnDisable()
        {
            GameSignals.OnBail -= HandleBail;
        }

        void HandleBail(TrickLandingInfo info)
        {
            if (car == null || car.rb == null) return;

            isBailing = true;
            bailTimer = bailDuration;

            // kill most horizontal velocity, keep a bit so they don't fully stop
            Vector3 v = car.rb.linearVelocity;
            Vector3 horizontal = new Vector3(v.x, 0f, v.z) * speedDampFactor;
            car.rb.linearVelocity = new Vector3(horizontal.x, 0f, horizontal.z);

            // bounce up and slightly backwards
            Vector3 bounce = Vector3.up * bounceUpForce
                           - car.carBody.transform.forward * bounceBackForce;

            car.rb.AddForce(bounce, ForceMode.VelocityChange);

            // ensure boost is off during bail
            car.isBoosting = false;
        }

        void Update()
        {
            if (!isBailing || car == null) return;

            bailTimer -= Time.deltaTime;
            if (bailTimer <= 0f)
            {
                isBailing = false;
                return;
            }

            // While bailing, gently stand the car upright on world up
            Quaternion targetRot = Quaternion.FromToRotation(
                car.carBody.transform.up,
                Vector3.up
            ) * car.carBody.rotation;

            car.carBody.MoveRotation(Quaternion.Slerp(
                car.carBody.rotation,
                targetRot,
                uprightSpeed * Time.deltaTime));

            // Optional: you can also slowly bleed velocity more here if it feels right
        }

        public bool IsBailing => isBailing;
    }
}
