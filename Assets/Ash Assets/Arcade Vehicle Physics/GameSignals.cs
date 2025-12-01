using System;

namespace ArcadeVP
{
    public enum TrickType
    {
        YawSpin,    // dir: +1 = right, -1 = left
        Flip,       // dir: +1 = frontflip, -1 = backflip
        BarrelRoll  // dir: +1 = roll right, -1 = roll left
    }

    public struct TrickLandingInfo
    {
        public bool clean;          // orientation OK (tilt within threshold)
        public bool fullyCompleted; // true if we finished the tween in air (not mid-spin)
        public int  yawRevolutions;
        public int  flipCount;
        public int  rollCount;
        public TrickType lastType;
    }

    public struct TrickProgressInfo
    {
        public int yawRevolutions;  // how many 360s so far this jump
        public int flipCount;       // flips so far
        public int rollCount;       // rolls so far
        public TrickType lastType;  // last trick performed
    }

    public struct TrickStartInfo
    {
        public TrickType type;
        public float direction;  // +1/-1 (right/left, front/back)
    }

    public static class GameSignals
    {
        public static event Action<TrickLandingInfo> OnTrickLanded;
        public static event Action<TrickLandingInfo> OnBail;  // NEW
        public static event Action OnBoostStarted;
        public static event Action OnBoostEnded;
        public static event Action<TrickProgressInfo> OnTrickProgress;

        public static event Action<TrickStartInfo> OnTrickStarted;   // NEW

        public static void RaiseTrickLanded(TrickLandingInfo info)
        {
            OnTrickLanded?.Invoke(info);
        }
        public static void RaiseBail(TrickLandingInfo info)   // NEW
        => OnBail?.Invoke(info);

        public static void RaiseTrickProgress(TrickProgressInfo info)
        {
            OnTrickProgress?.Invoke(info);
        }

        public static void RaiseTrickStarted(TrickStartInfo info)    // NEW
        => OnTrickStarted?.Invoke(info);

        public static void RaiseBoostStarted() => OnBoostStarted?.Invoke();
        public static void RaiseBoostEnded()   => OnBoostEnded?.Invoke();
    }
}
