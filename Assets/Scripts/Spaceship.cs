using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Spaceship : MonoBehaviour
{
    [Header("Engines")]
    [SerializeField] RocketEngine Engine_NegX;
    [SerializeField] RocketEngine Engine_PosX;
    [SerializeField] RocketEngine Engine_NegY;
    [SerializeField] RocketEngine Engine_PosY;
    [SerializeField] RocketEngine Engine_NegZ;
    [SerializeField] RocketEngine Engine_PosZ;

    [Header("Physics")]
    [SerializeField] AnimationCurve InputTranslationCurve;
    [SerializeField] float MaxVForce = 15000;
    [SerializeField] float MaxHForce = 5000f;

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
    [SerializeField] float AutoLevel_UpVectorInfluence = 0f;
    [SerializeField] float AutoLevel_AngularVelocityInfluence = 0f;

    [Header("Auto Landing")]
    [SerializeField] bool AutoLand_Enabled = true;
    [SerializeField] float AutoLand_MinHeight = 0.25f;
    [SerializeField] float AutoLand_MaxHeight = 100f;
    [SerializeField] float AutoLand_DistanceInfluence = 0f;
    [SerializeField] float AutoLand_SpeedInfluence = 0f;
    [SerializeField] AnimationCurve AutoLand_TargetSpeedVsDistance;
    protected bool PerformAutoLand = false;

    public float CurrentVelocity { get; private set; } = 0f;
    public float HeightAboveGround { get; private set; } = 0f;

    protected Rigidbody LinkedRB;
    protected float CurrentAutoLandNormalisedThrust = 0f;
    protected float PreviousWorkingYThrust = 0f;

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

    void Awake()
    {
        LinkedRB = GetComponent<Rigidbody>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // thrust input changed?
        float workingYThrust = _Input_Thrust.y + CurrentAutoLandNormalisedThrust;
        if (_Input_ThrustPrevious != _Input_Thrust || PreviousWorkingYThrust != workingYThrust)
        {
            _Input_ThrustPrevious = _Input_Thrust;
            PreviousWorkingYThrust = workingYThrust;

            // update X axis thrusters
            if (!Mathf.Approximately(_Input_Thrust.x, 0f))
            {
                Engine_NegX.Thrust = _Input_Thrust.x < 0 ? Mathf.Abs(_Input_Thrust.x) : 0f;
                Engine_PosX.Thrust = _Input_Thrust.x > 0 ? _Input_Thrust.x : 0f;
            }
            else
            {
                Engine_NegX.Thrust = 0f;
                Engine_PosX.Thrust = 0f;
            }

            // update Y axis thrusters
            if (!Mathf.Approximately(workingYThrust, 0f))
            {
                Engine_NegY.Thrust = workingYThrust < 0 ? Mathf.Abs(workingYThrust) : 0f;
                Engine_PosY.Thrust = workingYThrust > 0 ? workingYThrust : 0f;
            }
            else
            {
                Engine_NegY.Thrust = 0f;
                Engine_PosY.Thrust = 0f;
            }

            // update Z axis thrusters
            if (!Mathf.Approximately(_Input_Thrust.z, 0f))
            {
                Engine_NegZ.Thrust = _Input_Thrust.z < 0 ? Mathf.Abs(_Input_Thrust.z) : 0f;
                Engine_PosZ.Thrust = _Input_Thrust.z > 0 ? _Input_Thrust.z : 0f;
            }
            else
            {
                Engine_NegZ.Thrust = 0f;
                Engine_PosZ.Thrust = 0f;
            }

            if (EnableHaptics && Gamepad.current != null && Gamepad.current.enabled)
            {
                float heavyEngine = Mathf.Abs(_Input_Thrust.y);
                float lightEngine = Mathf.Clamp01(Mathf.Abs(_Input_Thrust.x) + Mathf.Abs(_Input_Thrust.z));
                Gamepad.current.SetMotorSpeeds(LowFrequencyMotorCurve.Evaluate(heavyEngine) * MaxLowFrequencyMotor,
                                               HighFrequencyMotorCurve.Evaluate(lightEngine) * MaxHighFrequencyMotor);
            }
        }

    }

    void FixedUpdate()
    {
        // check for the ground
        RaycastHit hitInfo;
        HeightAboveGround = -1f;
        if (Physics.Raycast(LinkedRB.position + Vector3.up * AGC_VerticalOffset, Vector3.down, out hitInfo,
                            AGC_MaximumRange, AGC_LayerMask, QueryTriggerInteraction.Ignore))
        {
            HeightAboveGround = hitInfo.distance;
        }

        CurrentVelocity = LinkedRB.velocity.magnitude;

        // apply translational thrust
        Vector3 thrustVector = transform.right   * _Input_Thrust.x * MaxHForce +
                               transform.up      * _Input_Thrust.y * MaxVForce +
                               transform.forward * _Input_Thrust.z * MaxHForce;
        LinkedRB.AddForce(thrustVector, ForceMode.Force);

        // apply thrust induced torque
        float inducedRoll  = InducedTorqueVsThrustCurve.Evaluate(Mathf.Abs(_Input_Thrust.x)) * Mathf.Sign(_Input_Thrust.x);
        float inducedPitch = InducedTorqueVsThrustCurve.Evaluate(Mathf.Abs(_Input_Thrust.z)) * Mathf.Sign(_Input_Thrust.z);
        LinkedRB.AddTorque(inducedPitch * MaxThrustInducedTorque, 0f, inducedRoll * MaxThrustInducedTorque);

        bool autoLanding = PerformAutoLand && HeightAboveGround >= AutoLand_MinHeight && 
                                              HeightAboveGround <= AutoLand_MaxHeight;

        // can perform auto level
        if (AutoLevel_Enabled || autoLanding)
        {
            Vector3 levelingVector = new Vector3(-transform.up.x, 0f, -transform.up.z);

            float autoLevelRollComponent  = levelingVector.x * AutoLevel_UpVectorInfluence +
                                            LinkedRB.angularVelocity.z * AutoLevel_AngularVelocityInfluence;
            float autoLevelPitchComponent = levelingVector.z * AutoLevel_UpVectorInfluence +
                                            -LinkedRB.angularVelocity.x * AutoLevel_AngularVelocityInfluence;

            LinkedRB.AddTorque(autoLevelPitchComponent, 0f, -autoLevelRollComponent);
        }

        // can perform autoland?
        if (autoLanding)
        {
            float targetVelocity = -AutoLand_TargetSpeedVsDistance.Evaluate(HeightAboveGround / AutoLand_MaxHeight);

            float autoLandThrust = HeightAboveGround * AutoLand_DistanceInfluence +
                                   (targetVelocity - LinkedRB.velocity.y) * AutoLand_SpeedInfluence;
            autoLandThrust = Mathf.Clamp(autoLandThrust, -MaxVForce, MaxVForce);

            CurrentAutoLandNormalisedThrust = autoLandThrust / MaxVForce;

            LinkedRB.AddForce(transform.up * autoLandThrust, ForceMode.Force);
        }
        else
        {
            CurrentAutoLandNormalisedThrust = 0f;

            if (HeightAboveGround <= AutoLand_MinHeight)
                PerformAutoLand = false;
        }
    }
}
