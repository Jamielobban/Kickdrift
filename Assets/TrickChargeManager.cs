using UnityEngine;

namespace ArcadeVP
{
    public class TrickChargeManager : MonoBehaviour
    {
        [Header("Refs")]
        public ArcadeVehicleController car;

        [Header("Charges")]
        public int maxCharges = 3;
        public int startingCharges = 1;

        [Tooltip("XP required to gain 1 trick charge.")]
        public float xpPerCharge = 100f;

        [Header("XP Gain From Driving")]
        public float driftXpPerSecond = 25f;  // multiplied by driftIntensity
        public float airXpPerSecond   = 10f;  // passive gain while in air

        public int CurrentCharges { get; private set; }
        float currentXP;

        public float ChargesNormalized =>
            maxCharges > 0 ? (float)CurrentCharges / maxCharges : 0f;

        void Start()
        {
            CurrentCharges = Mathf.Clamp(startingCharges, 0, maxCharges);
        }

        void OnEnable()
        {
            GameSignals.OnTrickLanded += OnTrickLanded;
            GameSignals.OnBail        += OnBail;
        }

        void OnDisable()
        {
            GameSignals.OnTrickLanded -= OnTrickLanded;
            GameSignals.OnBail        -= OnBail;
        }

        void Update()
        {
            if (car == null) return;

            float dt = Time.deltaTime;

            // gain XP from drifting
            if (car.isDrifting)
            {
                float amount = car.driftIntensity * driftXpPerSecond * dt;
                GainXP(amount);
            }

            // optional: gain a bit while in air
            if (!car.grounded())
            {
                GainXP(airXpPerSecond * dt);
            }
            //Debug.Log(CurrentCharges);
        }

        void GainXP(float amount)
        {
            if (amount <= 0f) return;
            if (CurrentCharges >= maxCharges) return;

            currentXP += amount;

            while (currentXP >= xpPerCharge && CurrentCharges < maxCharges)
            {
                currentXP -= xpPerCharge;
                CurrentCharges++;
            }
        }

        public bool TryConsumeCharge()
        {
            //Debug.Log(CurrentCharges);
            if (CurrentCharges <= 0)
                return false;

            CurrentCharges--;
            return true;
        }

        void OnTrickLanded(TrickLandingInfo info)
        {
            // reward chaining tricks in one airtime
            // CarTrickController already counted stuff for this airtime
            int totalTricks = info.yawRevolutions + info.flipCount + info.rollCount;

            if (!info.clean) return;  // only reward clean landings

            if (totalTricks >= 2)
                CurrentCharges = Mathf.Min(CurrentCharges + 1, maxCharges);
            if (totalTricks >= 3)
                CurrentCharges = Mathf.Min(CurrentCharges + 1, maxCharges);
        }

        void OnBail(TrickLandingInfo info)
        {
            // optional: punish bails (for now do nothing or maybe:
            // CurrentCharges = Mathf.Max(0, CurrentCharges - 1);
        }
    }
}
