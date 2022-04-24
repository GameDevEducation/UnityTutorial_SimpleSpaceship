using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GravityTracker))]
public class Spaceship : MonoBehaviour
{
    [Header("Health and Damage")]
    [SerializeField] int MaxHealth = 100;
    [SerializeField] int InitialHealth = 100;
    [SerializeField] float MinImpactSpeedToTakeDamage = 10f;
    [SerializeField] float ImpactSpeedForCriticalDamage = 45f;
    [SerializeField] int MinDamage = 1;
    [SerializeField] AnimationCurve ImpactDamageCurve;
    [SerializeField] List<string> TagsToIgnore;
    [SerializeField] float RepairRate = 10f;

    [Header("Engines")]
    [SerializeField] RocketEngine Engine_NegX;
    [SerializeField] RocketEngine Engine_PosX;
    [SerializeField] RocketEngine Engine_NegY;
    [SerializeField] RocketEngine Engine_PosY;
    [SerializeField] RocketEngine Engine_NegZ;
    [SerializeField] RocketEngine Engine_PosZ;

    [Header("Physics")]
    [SerializeField] AnimationCurve InputTranslationCurve;
    [SerializeField] float _MaxVForce = 15000;
    [SerializeField] float _MaxHForce = 5000f;
    [SerializeField] AnimationCurve ThrustImpactVsDamage;

    [Header("Haptics")]
    [SerializeField] bool EnableHaptics = true;
    [SerializeField] AnimationCurve LowFrequencyMotorCurve;
    [SerializeField] [Range(0f, 1f)] float MaxLowFrequencyMotor = 0.75f;
    [SerializeField] AnimationCurve HighFrequencyMotorCurve;
    [SerializeField] [Range(0f, 1f)] float MaxHighFrequencyMotor = 0.5f;

    [Header("Above Ground Check")]
    [SerializeField] float AGC_MaximumRange = 2000f;
    [SerializeField] float AGC_VerticalOffset = -1f;
    [SerializeField] LayerMask AGC_LayerMask = ~0;

    [Header("Thrust Induced Role")]
    [SerializeField] float MaxThrustInducedTorque = 5f;
    [SerializeField] AnimationCurve InducedTorqueVsThrustCurve;

    [Header("Auto Leveling")]
    [SerializeField] bool AutoLevel_Enabled = false;
    [SerializeField] float AutoLevel_PitchErrorInfluence = 10f;
    [SerializeField] float AutoLevel_PitchVelocityInfluence = 0f;
    [SerializeField] float AutoLevel_RollErrorInfluence = 10f;
    [SerializeField] float AutoLevel_RollVelocityInfluence = 0f;
    [SerializeField] float MinGravityToAutoLevel = 1f;

    [Header("Auto Braking")]
    [SerializeField] bool AutoBraking_Enabled = false;
    [SerializeField] float AutoBraking_LinearVelocityScale = -10000f;
    [SerializeField] float AutoBraking_AngularVelocityScale = -100f;

    [Header("Auto Landing")]
    [SerializeField] bool AutoLand_Enabled = true;
    [SerializeField] float AutoLand_MinHeight = 0.25f;
    [SerializeField] float AutoLand_MaxHeight = 100f;
    [SerializeField] float AutoLand_DistanceInfluence = 0f;
    [SerializeField] float AutoLand_SpeedInfluence = 0f;
    [SerializeField] AnimationCurve AutoLand_TargetSpeedVsDistance;

    [Header("Entry and Exit")]
    [SerializeField] float MaxPermittedEntryHeight = 1f;
    [SerializeField] float MaxPermittedEntryDistance = 10f;
    [SerializeField] float MaxPermittedExitHeight = 1f;
    [SerializeField] float ExitMarkerRaycastRange = 2f;
    [SerializeField] List<Transform> ExitMarkers;

    [Header("Landing Camera")]
    [SerializeField] GameObject LandingCameraDisplay;
    [SerializeField] float MaxHeightToUseLandingCamera = 100f;
    [SerializeField] Camera LandingCamera;
    [SerializeField] float LandingCameraFPS = 30f;

    [SerializeField] UnityEvent OnSpaceshipDestroyed = new UnityEvent();

    protected float TimeUntilNextLandingCameraRefresh;

    protected bool PerformAutoLand = false;
    protected bool PerformAutoBraking = false;

    public float CurrentVelocity { get; private set; } = 0f;
    public float HeightAboveGround { get; private set; } = 0f;

    protected Rigidbody LinkedRB;
    protected GravityTracker LocalGravity;
    Vector3 NormalisedThrustVector;
    Vector3 PreviousNormalisedThrustVector;

    public int CurrentHealth { get; private set; } = int.MinValue;
    public float HealthPercent => CurrentHealth / (float)MaxHealth;
    public bool CanBeRepaired => CurrentHealth < MaxHealth;

    protected float ThrustImpactFromDamage => ThrustImpactVsDamage.Evaluate(1f - HealthPercent);
    protected float MaxVForce => _MaxVForce * (1f - ThrustImpactFromDamage);
    protected float MaxHForce => _MaxHForce * (1f - ThrustImpactFromDamage);

    Vector3 _Input_ThrustPrevious;
    Vector3 _Input_Thrust;
    protected void OnHorizontalThrust(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        _Input_Thrust.x = InputTranslationCurve.Evaluate(Mathf.Abs(input.x)) * Mathf.Sign(input.x);
        _Input_Thrust.z = InputTranslationCurve.Evaluate(Mathf.Abs(input.y)) * Mathf.Sign(input.y);
    }
    protected void OnVerticalThrust(InputValue value)
    {
        float input = value.Get<float>();
        _Input_Thrust.y = InputTranslationCurve.Evaluate(Mathf.Abs(input)) * Mathf.Sign(input);
    }
    protected void OnToggleAutoLand(InputValue value)
    {
        if (value.isPressed && AutoLand_Enabled)
            PerformAutoLand = !PerformAutoLand;
    }
    protected void OnToggleAutoBraking(InputValue value)
    {
        if (value.isPressed && AutoBraking_Enabled)
            PerformAutoBraking = !PerformAutoBraking;
    }

    void Awake()
    {
        LinkedRB = GetComponent<Rigidbody>();
        LocalGravity = GetComponent<GravityTracker>();
        CurrentHealth = InitialHealth;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // thrust input changed?
        if (NormalisedThrustVector != PreviousNormalisedThrustVector)
        {
            PreviousNormalisedThrustVector = NormalisedThrustVector;

            // update X axis thrusters
            if (!Mathf.Approximately(NormalisedThrustVector.x, 0f))
            {
                Engine_NegX.Thrust = NormalisedThrustVector.x < 0 ? Mathf.Abs(NormalisedThrustVector.x) : 0f;
                Engine_PosX.Thrust = NormalisedThrustVector.x > 0 ? NormalisedThrustVector.x : 0f;
            }
            else
            {
                Engine_NegX.Thrust = 0f;
                Engine_PosX.Thrust = 0f;
            }

            // update Y axis thrusters
            if (!Mathf.Approximately(NormalisedThrustVector.y, 0f))
            {
                Engine_NegY.Thrust = NormalisedThrustVector.y < 0 ? Mathf.Abs(NormalisedThrustVector.y) : 0f;
                Engine_PosY.Thrust = NormalisedThrustVector.y > 0 ? NormalisedThrustVector.y : 0f;
            }
            else
            {
                Engine_NegY.Thrust = 0f;
                Engine_PosY.Thrust = 0f;
            }

            // update Z axis thrusters
            if (!Mathf.Approximately(NormalisedThrustVector.z, 0f))
            {
                Engine_NegZ.Thrust = NormalisedThrustVector.z < 0 ? Mathf.Abs(NormalisedThrustVector.z) : 0f;
                Engine_PosZ.Thrust = NormalisedThrustVector.z > 0 ? NormalisedThrustVector.z : 0f;
            }
            else
            {
                Engine_NegZ.Thrust = 0f;
                Engine_PosZ.Thrust = 0f;
            }

            if (EnableHaptics && Gamepad.current != null && Gamepad.current.enabled)
            {
                float heavyEngine = Mathf.Abs(NormalisedThrustVector.y);
                float lightEngine = Mathf.Clamp01(Mathf.Abs(NormalisedThrustVector.x) + Mathf.Abs(NormalisedThrustVector.z));
                Gamepad.current.SetMotorSpeeds(LowFrequencyMotorCurve.Evaluate(heavyEngine) * MaxLowFrequencyMotor,
                                               HighFrequencyMotorCurve.Evaluate(lightEngine) * MaxHighFrequencyMotor);
            }
        }

        // can we use the landing camera
        if (HeightAboveGround < MaxHeightToUseLandingCamera)
        {
            LandingCameraDisplay.SetActive(true);

            // ready to render the next frame?
            TimeUntilNextLandingCameraRefresh -= Time.deltaTime;
            if (TimeUntilNextLandingCameraRefresh <= 0)
            {
                LandingCamera.Render();
                TimeUntilNextLandingCameraRefresh = 1f / LandingCameraFPS;
            }
        }
        else
        {
            TimeUntilNextLandingCameraRefresh = 0f;
            LandingCameraDisplay.SetActive(false);
        }
    }

    void FixedUpdate()
    {
        // check for the ground
        RaycastHit hitInfo;
        HeightAboveGround = -1f;
        if (Physics.Raycast(LinkedRB.position + LocalGravity.Up * AGC_VerticalOffset, LocalGravity.Down, out hitInfo,
                            AGC_MaximumRange, AGC_LayerMask, QueryTriggerInteraction.Ignore))
        {
            HeightAboveGround = hitInfo.distance;
        }

        CurrentVelocity = LinkedRB.velocity.magnitude;

        // apply translational thrust
        Vector3 thrustVector = transform.right   * _Input_Thrust.x * MaxHForce +
                               transform.up      * _Input_Thrust.y * MaxVForce +
                               transform.forward * _Input_Thrust.z * MaxHForce;

        // performing auto braking?
        if (PerformAutoBraking)
        {
            float compRight     = AutoBraking_LinearVelocityScale * Vector3.Dot(LinkedRB.velocity, transform.right);
            float compUp        = AutoBraking_LinearVelocityScale * Vector3.Dot(LinkedRB.velocity, transform.up);
            float compForward   = AutoBraking_LinearVelocityScale * Vector3.Dot(LinkedRB.velocity, transform.forward);

            thrustVector  = transform.right     * Mathf.Clamp(compRight,   -MaxHForce, MaxHForce);
            thrustVector += transform.forward   * Mathf.Clamp(compForward, -MaxHForce, MaxHForce);
            thrustVector += transform.up        * Mathf.Clamp(compUp,      -MaxVForce, MaxVForce);

            LinkedRB.AddTorque(LinkedRB.angularVelocity.x * AutoBraking_AngularVelocityScale,
                               LinkedRB.angularVelocity.y * AutoBraking_AngularVelocityScale,
                               LinkedRB.angularVelocity.z * AutoBraking_AngularVelocityScale);
        }

        bool autoLanding = !PerformAutoBraking && PerformAutoLand && 
                            HeightAboveGround >= AutoLand_MinHeight && HeightAboveGround <= AutoLand_MaxHeight;

        // can perform autoland?
        if (autoLanding)
        {
            float targetVelocity = -AutoLand_TargetSpeedVsDistance.Evaluate(HeightAboveGround / AutoLand_MaxHeight);
            float currentVelocity = Vector3.Dot(LinkedRB.velocity, LocalGravity.Up);

            float autoLandThrust = HeightAboveGround * AutoLand_DistanceInfluence +
                                   (targetVelocity - currentVelocity) * AutoLand_SpeedInfluence;
            autoLandThrust = Mathf.Clamp(autoLandThrust, -MaxVForce, MaxVForce);

            // remove the vertical thrust
            thrustVector = Vector3.Dot(thrustVector, transform.right) * transform.right +
                           Vector3.Dot(thrustVector, transform.forward) * transform.forward;

            thrustVector += transform.up * autoLandThrust;
        }
        else
        {
            if (HeightAboveGround <= AutoLand_MinHeight)
                PerformAutoLand = false;
        }

        // update the normalised thrust vector
        NormalisedThrustVector.x = thrustVector.x / MaxHForce;
        NormalisedThrustVector.y = thrustVector.y / MaxHForce;
        NormalisedThrustVector.z = thrustVector.z / MaxHForce;

        LinkedRB.AddForce(thrustVector, ForceMode.Force);

        // apply thrust induced torque
        float inducedRoll  = InducedTorqueVsThrustCurve.Evaluate(Mathf.Abs(_Input_Thrust.x)) * Mathf.Sign(_Input_Thrust.x);
        float inducedPitch = InducedTorqueVsThrustCurve.Evaluate(Mathf.Abs(_Input_Thrust.z)) * Mathf.Sign(_Input_Thrust.z);
        LinkedRB.AddTorque(inducedPitch * MaxThrustInducedTorque, 0f, inducedRoll * MaxThrustInducedTorque);

        // can perform auto level
        if (!PerformAutoBraking && (AutoLevel_Enabled || autoLanding) && 
            LocalGravity.GravityVector.magnitude > MinGravityToAutoLevel)
        {
            Vector3 levelingVector = Quaternion.FromToRotation(transform.up, LocalGravity.Up).eulerAngles;

            if (levelingVector.x > 180f) levelingVector.x -= 360f;
            if (levelingVector.y > 180f) levelingVector.y -= 360f;
            if (levelingVector.z > 180f) levelingVector.z -= 360f;
            if (levelingVector.x < -180f) levelingVector.x += 360f;
            if (levelingVector.y < -180f) levelingVector.y += 360f;
            if (levelingVector.z < -180f) levelingVector.z += 360f;

            float autoLevelPitchComponent = levelingVector.x * AutoLevel_PitchErrorInfluence +
                                            LinkedRB.angularVelocity.x * AutoLevel_PitchVelocityInfluence;
            float autoLevelRollComponent  = levelingVector.z * AutoLevel_RollErrorInfluence +
                                            LinkedRB.angularVelocity.z * AutoLevel_RollVelocityInfluence;

            LinkedRB.AddTorque(autoLevelPitchComponent, 0f, autoLevelRollComponent);
        }
    }

    public bool AttemptToEnterVehicle(CharacterMotor player)
    {
        // too high to safely enter?
        if (HeightAboveGround > MaxPermittedEntryHeight)
            return false;

        // too far to enter?
        if (Vector3.Distance(player.transform.position, transform.position) > MaxPermittedEntryDistance)
            return false;

        // are we looking at the spaceship
        Ray cameraRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hitInfo;
        if (Physics.Raycast(cameraRay, out hitInfo, MaxPermittedEntryDistance))
        {
            // are we looking at the spaceship
            if (hitInfo.collider.GetComponentInParent<Spaceship>() == this)
            {
                return true;
            }
        }

        return false;
    }

    public bool AttemptToExitVehicle(CharacterMotor player)
    {
        // too high to safely exit
        if (HeightAboveGround > MaxPermittedExitHeight)
            return false;

        // attempt to find a valid marker
        foreach(var marker in ExitMarkers)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(marker.position, LocalGravity.Down, out hitInfo, ExitMarkerRaycastRange))
            {
                player.transform.position = hitInfo.point;
                return true;
            }
        }

        return false; 
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ignore this tag
        if (TagsToIgnore.Contains(collision.gameObject.tag))
            return;

        float impactVelocity = collision.relativeVelocity.magnitude;

        // impact too low to take damage
        if (impactVelocity < MinImpactSpeedToTakeDamage)
            return;

        // taken critical damage?
        if (impactVelocity >= ImpactSpeedForCriticalDamage)
        {
            DestroySpaceship();
            return;
        }

        // calculate the damage
        float impactFactor = Mathf.InverseLerp(MinImpactSpeedToTakeDamage, ImpactSpeedForCriticalDamage, impactVelocity);
        impactFactor = ImpactDamageCurve.Evaluate(impactFactor);
        int damageTaken = Mathf.RoundToInt(Mathf.Lerp(MinDamage, MaxHealth, impactFactor));

        // apply the damage
        CurrentHealth = Mathf.Max(CurrentHealth - damageTaken, 0);

        // have we been destroyed?
        if (CurrentHealth <= 0)
            DestroySpaceship();
    }

    void DestroySpaceship()
    {
        OnSpaceshipDestroyed.Invoke();
        Destroy(gameObject);
    }

    public void StartRepair()
    {
        _CachedRepairAmount = 0f;
    }

    float _CachedRepairAmount = 0f;
    public bool TickRepair()
    {
        _CachedRepairAmount += RepairRate * Time.deltaTime;

        if (_CachedRepairAmount >= 1f)
        {
            int healthToAdd = Mathf.FloorToInt(_CachedRepairAmount);
            CurrentHealth = Mathf.Min(CurrentHealth + healthToAdd, MaxHealth);

            _CachedRepairAmount -= healthToAdd;
        }

        return CurrentHealth == MaxHealth;
    }

    public void StopRepair()
    {
    }
}
 