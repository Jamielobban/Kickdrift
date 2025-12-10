using UnityEngine;

namespace ArcadeVP
{
    public class TrickLandingBoostReward : MonoBehaviour
    {
        public ArcadeVehicleController car;
        public float nosReward = 30f;

        void OnEnable()
        {
            GameSignals.OnTrickLanded += OnTrickLanded;
        }

        void OnDisable()
        {
            GameSignals.OnTrickLanded -= OnTrickLanded;
        }

        void OnTrickLanded(TrickLandingInfo info)
        {
            if (!info.clean) return;

            // reward NOS
            car.nos.nosAmount = Mathf.Clamp(car.nos.nosAmount + nosReward, 0f, car.nos.nosMax);
            //Debug.Log(";anded");
            // later: trigger small auto-boost, SFX, particles, camera shake, etc.
        }
    }
}
