using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(SplineContainer))]
public class GrindRail : MonoBehaviour
{
    public SplineContainer container;

    [Header("Rail Behavior")]
    [Tooltip("If false, reaching the end bails you off.")]
    public bool loop = false;

    [Tooltip("Allow grinding backwards if you hit the rail the wrong way.")]
    public bool allowReverse = true;

    [Tooltip("How strongly the car is pulled to the spline centerline.")]
    public float snapStrength = 40f;

    [Tooltip("Max lateral offset (meters) allowed before bailing.")]
    public float maxLateralError = 0.45f;

    void Reset()
    {
        container = GetComponent<SplineContainer>();
    }
}
