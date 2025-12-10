using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

namespace ArcadeVP
{
    public class BoostFXController : MonoBehaviour
    {
        [Header("Post FX Material (BoostRadialFX)")]
        public Material boostMat;

        [Header("Boost Intensity")]
        public float boostIntensityValue   = 1f;  // target when boosting
        public float normalIntensityValue  = 0f;  // at rest

        [Header("Cinemachine FOV")]
        public CinemachineCamera vcam;
        public float normalFOV = 60f;
        public float boostFOV  = 80f;

        [Header("Boost-IN Tween")]
        public float boostInDuration = 0.2f;
        public AnimationCurve boostInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Boost-OUT Tween")]
        public float boostOutDuration = 0.5f;
        public AnimationCurve boostOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Tween intensityTween;
        private Tween fovTween;

        void OnEnable()
        {
            GameSignals.OnBoostStarted += HandleBoostStarted;
            GameSignals.OnBoostEnded   += HandleBoostEnded;
        }

        void OnDisable()
        {
            GameSignals.OnBoostStarted -= HandleBoostStarted;
            GameSignals.OnBoostEnded   -= HandleBoostEnded;
        }

        void HandleBoostStarted()
        {
            TweenIntensity(boostIntensityValue, boostInDuration, boostInCurve);
            TweenFOV(boostFOV, boostInDuration, boostInCurve);
        }

        void HandleBoostEnded()
        {
            TweenIntensity(normalIntensityValue, boostOutDuration, boostOutCurve);
            TweenFOV(normalFOV, boostOutDuration, boostOutCurve);
        }

        void TweenIntensity(float target, float duration, AnimationCurve curve)
        {
            if (boostMat == null) return;

            intensityTween?.Kill();

            float current = boostMat.GetFloat("_BoostIntensity");

            intensityTween = DOTween.To(() => current, x =>
            {
                current = x;
                boostMat.SetFloat("_BoostIntensity", current);
            },
            target, duration)
            .SetEase(curve != null ? curve : AnimationCurve.EaseInOut(0, 0, 1, 1));
        }

        void TweenFOV(float targetFOV, float duration, AnimationCurve curve)
        {
            if (vcam == null) return;

            fovTween?.Kill();

            float current = vcam.Lens.FieldOfView;

            fovTween = DOTween.To(() => current, x =>
            {
                current = x;
                var lens = vcam.Lens;
                lens.FieldOfView = current;
                vcam.Lens = lens;
            },
            targetFOV, duration)
            .SetEase(curve != null ? curve : AnimationCurve.EaseInOut(0, 0, 1, 1));
        }
    }
}
