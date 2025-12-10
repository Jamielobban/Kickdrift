using UnityEngine;
using UnityEngine.UI;
using ArcadeVP;

public class EnergyGaugeUI : MonoBehaviour
{
    public ArcadeVehicleController vehicle;
    public Slider slider;

    void Start()
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = vehicle.nos.nosMax;
        }
    }

    void Update()
    {
        if (vehicle == null || slider == null) return;

        slider.value = vehicle.nos.nosAmount;
    }
}
