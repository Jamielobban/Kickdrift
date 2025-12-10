using ArcadeVP;
using UnityEngine;

[System.Serializable]
    public class NosModule
    {
        public float nosAmount = 0f;
        public float nosMax = 100f;
        public float nosGainRate = 25f;
        public float nosUseRate = 40f;
        public float nosSpeedMultiplier = 1.35f;
        public float nosAccelMultiplier = 1.5f;
        public float minNosToBoost = 5f;

        [System.NonSerialized] public ArcadeVehicleController car;
        [System.NonSerialized] public bool boostInput;

        public void Init(ArcadeVehicleController car)
        {
            this.car = car;
        }

        public void Tick(float dt, bool onGround)
        {
            if (car.isDrifting)
                nosAmount = Mathf.Clamp(nosAmount + car.driftIntensity * nosGainRate * dt, 0, nosMax);

            if (boostInput && nosAmount > minNosToBoost && onGround)
            {
                if (!car.isBoosting)
                    GameSignals.RaiseBoostStarted();

                car.isBoosting = true;
                nosAmount -= nosUseRate * dt;
                if (nosAmount <= 0)
                {
                    nosAmount = 0;
                    car.isBoosting = false;
                    GameSignals.RaiseBoostEnded();
                }
            }
            else
            {
                if (car.isBoosting)
                    GameSignals.RaiseBoostEnded();
                car.isBoosting = false;
            }
        }
    }

