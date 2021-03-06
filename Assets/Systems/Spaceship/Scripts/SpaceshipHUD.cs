using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SpaceshipHUD : MonoBehaviour
{
    [SerializeField] Spaceship LinkedSpaceship;
    [SerializeField] TextMeshProUGUI VelocityValue;
    [SerializeField] TextMeshProUGUI LocationValue;
    [SerializeField] TextMeshProUGUI HeightAboveGroundValue;
    [SerializeField] Image HealthBar;

    [SerializeField] Gradient HealthBarColour;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (LinkedSpaceship == null)
            return;

        VelocityValue.text = $"Speed: {LinkedSpaceship.CurrentVelocity:0.0} m/s";
        LocationValue.text = $"Position: X {LinkedSpaceship.transform.position.x:n0} Y {LinkedSpaceship.transform.position.y:n0} Z {LinkedSpaceship.transform.position.z:n0}";
        if (LinkedSpaceship.HeightAboveGround >= 0)
            HeightAboveGroundValue.text = $"Altitude: {LinkedSpaceship.HeightAboveGround:n0} m";
        else
            HeightAboveGroundValue.text = "Altitude: --- m";

        HealthBar.transform.localScale = new Vector3(LinkedSpaceship.HealthPercent, 1f, 1f);
        HealthBar.color = HealthBarColour.Evaluate(LinkedSpaceship.HealthPercent);
    }
}
