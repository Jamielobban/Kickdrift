using UnityEngine;
using UnityEngine.UI;

namespace ArcadeVP
{
    public class TrickChargesUI : MonoBehaviour
    {
        public TrickChargeManager chargeManager;

        [Header("Charge Icons (Left to Right)")]
        public Image icon1;
        public Image icon2;
        public Image icon3;

        [Header("Sprites")]
        public Sprite fullSprite;
        public Sprite emptySprite;

        void Update()
        {
            if (chargeManager == null) return;

            int charges = chargeManager.CurrentCharges;

            // Icon 1
            if (icon1 != null)
                icon1.sprite = charges >= 1 ? fullSprite : emptySprite;

            // Icon 2
            if (icon2 != null)
                icon2.sprite = charges >= 2 ? fullSprite : emptySprite;

            // Icon 3
            if (icon3 != null)
                icon3.sprite = charges >= 3 ? fullSprite : emptySprite;
        }
    }
}
