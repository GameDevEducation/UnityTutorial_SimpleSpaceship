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

    protected Rigidbody LinkedRB;

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
        if (_Input_ThrustPrevious != _Input_Thrust)
        {
            _Input_ThrustPrevious = _Input_Thrust;

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
            if (!Mathf.Approximately(_Input_Thrust.y, 0f))
            {
                Engine_NegY.Thrust = _Input_Thrust.y < 0 ? Mathf.Abs(_Input_Thrust.y) : 0f;
                Engine_PosY.Thrust = _Input_Thrust.y > 0 ? _Input_Thrust.y : 0f;
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
        Vector3 thrustVector = transform.right   * _Input_Thrust.x * MaxHForce +
                               transform.up      * _Input_Thrust.y * MaxVForce +
                               transform.forward * _Input_Thrust.z * MaxHForce;
        LinkedRB.AddForce(thrustVector, ForceMode.Force);
    }
}
