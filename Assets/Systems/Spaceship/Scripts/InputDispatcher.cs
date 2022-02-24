using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

[RequireComponent(typeof(PlayerInput))]
public class InputDispatcher : MonoBehaviour
{
    public enum ETarget
    {
        Unknown,

        Player,
        Spaceship
    }

    [SerializeField] CharacterMotor PlayerController;
    [SerializeField] GameObject PlayerHUD;
    [SerializeField] GameObject PlayerGO;
    [SerializeField] CinemachineVirtualCamera PlayerCamera;

    [SerializeField] Spaceship SpaceshipController;
    [SerializeField] GameObject SpaceshipHUD;
    [SerializeField] GameObject SpaceshipGO;
    [SerializeField] CinemachineVirtualCamera SpaceshipCamera;

    [SerializeField] ETarget InitialTarget = ETarget.Player;
    [SerializeField] ETarget RequestedTarget;

    protected PlayerInput LinkedInput;
    protected ETarget CurrentTarget;

    private void Awake()
    {
        LinkedInput = GetComponent<PlayerInput>();

        RequestedTarget = CurrentTarget = InitialTarget;

        TargetChanged();
    }

    private void Update()
    {
        if (RequestedTarget != CurrentTarget)
        {
            CurrentTarget = RequestedTarget;
            TargetChanged();
        }
    }

    void TargetChanged()
    {
        PlayerHUD.SetActive(CurrentTarget == ETarget.Player);
        PlayerCamera.enabled = CurrentTarget == ETarget.Player;
        SpaceshipHUD.SetActive(CurrentTarget == ETarget.Spaceship);
        SpaceshipCamera.enabled = CurrentTarget == ETarget.Spaceship;
        LinkedInput.SwitchCurrentActionMap(CurrentTarget.ToString());
    }

    protected void OnMove(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnMove", value);
    }

    protected void OnLook(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnLook", value);
    }

    protected void OnJump(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnJump", value);
    }

    protected void OnRun(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnRun", value);
    }

    protected void OnCrouch(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnCrouch", value);
    }

    protected void OnPrimaryAction(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnPrimaryAction", value);
    }

    protected void OnSecondaryAction(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
            PlayerGO.SendMessage("OnSecondaryAction", value);
    }

    protected void OnHorizontalThrust(InputValue value)
    {
        if (CurrentTarget == ETarget.Spaceship)
            SpaceshipGO.SendMessage("OnHorizontalThrust", value);
    }

    protected void OnVerticalThrust(InputValue value)
    {
        if (CurrentTarget == ETarget.Spaceship)
            SpaceshipGO.SendMessage("OnVerticalThrust", value);
    }

    protected void OnToggleAutoLand(InputValue value)
    {
        if (CurrentTarget == ETarget.Spaceship)
            SpaceshipGO.SendMessage("OnToggleAutoLand", value);
    }

    protected void OnEnterVehicle(InputValue value)
    {
        if (CurrentTarget == ETarget.Player)
        {
            if (SpaceshipController.AttemptToEnterVehicle(PlayerController))
            {
                RequestedTarget = ETarget.Spaceship;
                TargetChanged();
            }
        }
    }

    protected void OnExitVehicle(InputValue value)
    {
        if (CurrentTarget == ETarget.Spaceship)
        {
            if (SpaceshipController.AttemptToExitVehicle(PlayerController))
            {
                RequestedTarget = ETarget.Player;
                TargetChanged();
            }
        }
    }
}