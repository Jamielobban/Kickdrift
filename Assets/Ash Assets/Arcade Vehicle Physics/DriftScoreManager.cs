using UnityEngine;
using TMPro;
using MoreMountains.Feedbacks;

namespace ArcadeVP
{
    public class DriftScoreManager : MonoBehaviour
    {
        [Header("Refs")]
        public ArcadeVehicleController car;

        [Header("UI (optional)")]
        public TMP_Text scoreText;
        public TMP_Text comboText;
        public TMP_Text popupText;

        [Header("Speed Influence")]
        // x = normalized speed (0..1), y = speed factor
        // Suggested: keys (0,0), (0.5,1), (1,0.8)
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Base Scoring")]
        public float basePointsPerSecond = 100f;
        public float maxCombo = 10f;

        [Header("Combo Behaviour")]
        public float comboGainSpeed = 1.5f;    // how fast combo builds while drifting
        public float comboDecaySpeed = 3f;     // how fast combo shrinks when not drifting
        public float comboGraceTime = 0.6f;    // time after drift ends before combo decays

        [Header("Bonuses")]
        public float entryBonus = 150f;
        public float transitionBonus = 200f;

        [Header("Near Miss")]
        public LayerMask nearMissLayers;
        public float nearMissRadius = 2.0f;
        public float nearMissBonusPerSec = 75f;
        public float wallScrapeDistance = 1.0f;
        public float wallScrapeTime = 0.4f;
        public float wallScrapeBonus = 250f;

        [Header("Trick Bonuses")]
        public float spinLandingBonus = 300f;   // 360 / yaw spin
        public float frontflipBonus = 400f;
        public float backflipBonus = 400f;
        public float barrelRollBonus = 450f;    // if you use barrel rolls later
        public bool onlyCleanLandings = true;   // require clean to get bonus

        [Header("Trick Combo Scoring")]
        public float pointsPer360Spin = 200f;   // 1 rev
        public float pointsPerFlip = 300f;      // each flip
        public float pointsPerRoll = 350f;      // each barrel roll
        public float sloppyLandingMultiplier = 0.4f;  // if you still want some points
        public bool zeroPointsOnSloppy = false;       // if true, no points when not clean

        [Header("On Fire Mode")]
        public float fireMinIntensity = 0.7f;   // driftIntensity
        public float fireMinSpeedNorm = 0.7f;   // normalized speed
        public float fireDuration = 2f;
        public float fireScoreMultiplier = 2f;

        [Header("Floating Text (More Mountains)")]
        public MMF_Player bonusFloatingTextPlayer;   // MMF_Player that has MMF_FloatingText

        float score;
        float combo = 1f;
        float graceTimer;
        bool wasDrifting;

        float lastSteerSign;
        float scrapeTimer;
        bool onFire;
        float fireTimer;

        MMF_FloatingText _bonusFloatingText;   // cached feedback

        void Awake()
        {
            if (bonusFloatingTextPlayer != null)
            {
                _bonusFloatingText = bonusFloatingTextPlayer.GetFeedbackOfType<MMF_FloatingText>();
            }
        }

        void OnEnable()
        {
            GameSignals.OnTrickLanded += HandleTrickLanded;
            GameSignals.OnTrickProgress += HandleTrickProgress;
        }

        void OnDisable()
        {
            GameSignals.OnTrickLanded -= HandleTrickLanded;
            GameSignals.OnTrickProgress -= HandleTrickProgress;
        }

        void Update()
        {
            if (car == null) return;

            float dt = Time.deltaTime;

            bool drifting = car.isDrifting;
            float intensity = Mathf.Clamp01(car.driftIntensity);
            float fwdSpeed = Mathf.Abs(car.carVelocity.z);
            float speedNorm = Mathf.Clamp01(fwdSpeed / Mathf.Max(1f, car.movement.MaxSpeed));
            float speedFactor = speedCurve.Evaluate(speedNorm);

            // ---------- NEAR MISS / WALL SCRAPE ----------
            bool nearWall, scraping;
            NearMissCheck(out nearWall, out scraping);

            if (nearWall && drifting)
            {
                score += nearMissBonusPerSec * intensity * dt;
            }

            if (scraping && drifting)
            {
                scrapeTimer += dt;
                if (scrapeTimer >= wallScrapeTime)
                {
                    AddBonus(wallScrapeBonus, "SCRAPE");
                    scrapeTimer = 0f;
                }
            }
            else
            {
                scrapeTimer = 0f;
            }

            // ---------- ON FIRE STATE ----------
            if (!onFire && drifting && intensity > fireMinIntensity && speedNorm > fireMinSpeedNorm && nearWall)
            {
                onFire = true;
                fireTimer = fireDuration;
                // you can trigger extra VFX/SFX here
            }

            if (onFire)
            {
                fireTimer -= dt;
                if (fireTimer <= 0f) onFire = false;
            }

            // ---------- MAIN DRIFT SCORING ----------
            if (drifting)
            {
                // grow combo based on angle/intensity
                combo += comboGainSpeed * intensity * dt;
                combo = Mathf.Clamp(combo, 1f, maxCombo);

                float fireMult = onFire ? fireScoreMultiplier : 1f;

                float gain = basePointsPerSecond * intensity * speedFactor * combo * fireMult * dt;
                score += gain;

                graceTimer = comboGraceTime;
            }
            else
            {
                if (graceTimer > 0f)
                {
                    graceTimer -= dt;
                }
                else if (combo > 1f)
                {
                    combo -= comboDecaySpeed * dt;
                    if (combo < 1f) combo = 1f;
                }
            }

            // ---------- ENTRY BONUS ----------
            if (!wasDrifting && drifting && speedNorm > 0.4f)
            {
                //AddBonus(entryBonus, "ENTRY");
            }

            // ---------- TRANSITION BONUS (switch drift direction) ----------
            float steerSign = Mathf.Sign(car.steeringInputPublic);

            if (drifting && Mathf.Abs(steerSign) > 0.2f && Mathf.Abs(lastSteerSign) > 0.2f)
            {
                //if (Mathf.Sign(steerSign) != Mathf.Sign(lastSteerSign))
                //{
                //    AddBonus(transitionBonus, "SWITCH");
                //}
            }
            lastSteerSign = steerSign;
            wasDrifting = drifting;

            // ---------- UI ----------
            if (scoreText) scoreText.text = Mathf.FloorToInt(score).ToString();

            if (comboText)
            {
                comboText.text = combo > 1.01f ? "x" + combo.ToString("0.0") : "";
            }

            if (popupText)
            {
                // simple fade-out for popup
                var c = popupText.color;
                c.a = Mathf.MoveTowards(c.a, 0f, dt * 2f);
                popupText.color = c;
            }
        }

        void NearMissCheck(out bool nearWall, out bool scraping)
        {
            nearWall = false;
            scraping = false;

            Vector3 origin = car.carBody.transform.position;
            Vector3 right = car.carBody.transform.right;

            RaycastHit hit;

            // right side
            if (Physics.Raycast(origin, right, out hit, nearMissRadius, nearMissLayers, QueryTriggerInteraction.Ignore))
            {
                nearWall = true;
                if (hit.distance < wallScrapeDistance) scraping = true;
            }

            // left side
            if (Physics.Raycast(origin, -right, out hit, nearMissRadius, nearMissLayers, QueryTriggerInteraction.Ignore))
            {
                nearWall = true;
                if (hit.distance < wallScrapeDistance) scraping = true;
            }
        }

        // ---------- A) AddBonus hooked to More Mountains floating text ----------
        void AddBonus(float amount, string label)
        {
            score += amount;

            // Old popupText HUD (optional, still works)
            if (popupText)
            {
                popupText.text = label + "  +" + Mathf.RoundToInt(amount);
                var c = popupText.color;
                c.a = 1f;
                popupText.color = c;
            }

            // More Mountains floating text
            if (bonusFloatingTextPlayer != null && _bonusFloatingText != null)
            {
                _bonusFloatingText.Value = $"{label}  +{Mathf.RoundToInt(amount)}";

                Vector3 spawnPos = car != null
                    ? car.carBody.transform.position
                    : transform.position;

                bonusFloatingTextPlayer.PlayFeedbacks(spawnPos);
            }
        }

        void HandleTrickLanded(TrickLandingInfo info)
        {
            // ---- Bail rule: any flip + bad timing or bad orientation ----
            bool didFlip = info.flipCount > 0;
            bool badTilt = !info.clean;
            bool badTiming = !info.fullyCompleted;
            bool bailThisFlip = didFlip && (badTilt || badTiming);

            if (bailThisFlip)
            {
                GameSignals.RaiseBail(info);   // tell the bail system
                return;                        // no trick score
            }

            // ---- Normal trick scoring if not bailed ----
            float basePoints = 0f;
            basePoints += info.yawRevolutions * pointsPer360Spin;
            basePoints += info.flipCount * pointsPerFlip;
            basePoints += info.rollCount * pointsPerRoll;

            if (basePoints <= 0f) return;

            float final = basePoints;

            // optional light penalty if just not clean (but still not a bail)
            if (!info.clean)
                final *= sloppyLandingMultiplier;  // e.g. 0.7

            string label = BuildTrickLabel(info);  // "360", "720", "FLIP", etc
            AddBonus(final, label);
        }

        void HandleTrickProgress(TrickProgressInfo info)
        {
            if (popupText == null) return;

            string label = BuildProgressLabel(info);
            if (string.IsNullOrEmpty(label)) return;

            popupText.text = label;

            // make sure it’s fully visible again
            var c = popupText.color;
            c.a = 1f;
            popupText.color = c;
        }

        string BuildTrickLabel(TrickLandingInfo info)
        {
            // If you only care about spins:
            if (info.yawRevolutions > 0)
            {
                int angle = info.yawRevolutions * 360;
                if (info.flipCount > 0 || info.rollCount > 0)
                    return angle + " + COMBO";
                return angle.ToString();   // "360", "720", "1080"...
            }

            // No spins, but flips / rolls:
            if (info.flipCount > 0 && info.rollCount > 0)
                return "TRICK COMBO";
            if (info.flipCount > 0)
                return info.flipCount == 1 ? "FLIP" : info.flipCount + "x FLIP";
            if (info.rollCount > 0)
                return info.rollCount == 1 ? "ROLL" : info.rollCount + "x ROLL";

            return "TRICK";
        }

        string BuildProgressLabel(TrickProgressInfo info)
        {
            // spins first: 360 / 720 / 1080...
            if (info.yawRevolutions > 0)
            {
                int angle = info.yawRevolutions * 360;
                return angle.ToString();  // "360", "720", "1080"
            }

            // no spins yet, but flips/rolls
            if (info.flipCount > 0)
                return info.flipCount == 1 ? "FLIP" : info.flipCount + "x FLIP";

            if (info.rollCount > 0)
                return info.rollCount == 1 ? "ROLL" : info.rollCount + "x ROLL";

            return "";
        }

        public float CurrentScore => score;
        public float CurrentCombo => combo;
    }
}
