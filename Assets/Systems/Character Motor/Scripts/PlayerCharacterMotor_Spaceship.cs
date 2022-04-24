using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCharacterMotor_Spaceship : PlayerCharacterMotor
{
    [SerializeField] protected TextMeshProUGUI RepairHUD;

    protected Spaceship SpaceshipToBeRepaired = null;

    #region Input System Handling

    protected override void OnPrimaryAction(InputValue value)
    {
        base.OnPrimaryAction(value);

        if (_Input_PrimaryAction)
        {
            Ray cameraRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            RaycastHit hitInfo;
            if (Physics.Raycast(cameraRay, out hitInfo, Config.MaxInteractionDistance))
            {
                // are we looking at the spaceship
                Spaceship spaceshipController = hitInfo.collider.GetComponentInParent<Spaceship>();
                if (spaceshipController != null && spaceshipController.CanBeRepaired)
                {
                    // spaceship changed
                    if (spaceshipController != SpaceshipToBeRepaired && SpaceshipToBeRepaired != null)
                    {
                        RepairHUD.gameObject.SetActive(false);
                        SpaceshipToBeRepaired.StopRepair();
                    }

                    SpaceshipToBeRepaired = spaceshipController;

                    SpaceshipToBeRepaired.StartRepair();
                    RepairHUD.gameObject.SetActive(true);
                    RepairHUD.text = string.Empty;
                }
            }
        }
        else
        {
            if (SpaceshipToBeRepaired != null)
            {
                RepairHUD.gameObject.SetActive(false);
                SpaceshipToBeRepaired.StopRepair();
            }
            SpaceshipToBeRepaired = null;
        }
    }
    #endregion

    protected override void Update()
    {
        base.Update();

        if (SpaceshipToBeRepaired != null && _Input_PrimaryAction)
        {
            bool tickRepairs = false;

            // are we still looking at the spaceship?
            Ray cameraRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            RaycastHit hitInfo;
            if (Physics.Raycast(cameraRay, out hitInfo, Config.MaxInteractionDistance))
            {
                // are we looking at the spaceship
                Spaceship spaceshipController = hitInfo.collider.GetComponentInParent<Spaceship>();
                if (spaceshipController == SpaceshipToBeRepaired)
                    tickRepairs = true;
            }

            // perform repairs
            if (tickRepairs)
            {
                SpaceshipToBeRepaired.TickRepair();
                RepairHUD.text = $"Repairing: {Mathf.RoundToInt(SpaceshipToBeRepaired.HealthPercent * 100)}%";
            }

            // clear the spaceship if repairs complete or if could not repair
            if (!tickRepairs || !SpaceshipToBeRepaired.CanBeRepaired)
            {
                RepairHUD.gameObject.SetActive(false);
                SpaceshipToBeRepaired.StopRepair();
                SpaceshipToBeRepaired = null;
            }
        }
    }
}
