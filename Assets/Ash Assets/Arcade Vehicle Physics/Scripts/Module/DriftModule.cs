 using UnityEngine;

 [System.Serializable]
    public class DriftModule
    {
        [Header("Drift Settings")]
        public AnimationCurve frictionCurve;
        public PhysicsMaterial frictionMaterial;
        public float minDriftSpeed = 15f;
        public float minSlip = 0.30f;
        public float steepCutoffY = 0.6f; // no drift on walls

        [System.NonSerialized] public ArcadeVehicleController car;

        public void Init(ArcadeVehicleController car)
        {
            this.car = car;
        }

        public void Tick(float dt, bool onGround)
        {
            float side  = Mathf.Abs(car.carVelocity.x);
            float fwd   = Mathf.Abs(car.carVelocity.z);
            float speed = car.carVelocity.magnitude;

            car.slip = side / (fwd + 0.1f);
            car.driftIntensity = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 0.65f, car.slip));

            bool steep = onGround && car.groundHit.normal.y < steepCutoffY;

            car.isDrifting = onGround && !steep && speed > minDriftSpeed && car.slip > minSlip;

            if (Mathf.Abs(car.carVelocity.x) > 0 && frictionCurve != null && frictionMaterial != null)
            {
                float t = Mathf.Abs(car.carVelocity.x / 100f);
                float f = frictionCurve.Evaluate(t);
                frictionMaterial.dynamicFriction = Mathf.Clamp(f, 0.4f, 1f);
            }
        }
    }
