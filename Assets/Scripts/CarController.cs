using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheels")]
    [SerializeField] private Wheel[] wheels;

    [Header("Car Settings")]
    public float motorForce = 1500f;       
    public float brakeForce = 3000f;          

    [SerializeField] private AnimationCurve steeringCurve;
    public string key = "0";
    [SerializeField] private GearRatio[] gearRatios = 
    {
        new GearRatio { key = "0", ratio = 3f},
        new GearRatio { key = "1", ratio = 2.5f},
        new GearRatio { key = "2", ratio = 1.5f},
        new GearRatio { key = "3", ratio = 1f},
        new GearRatio { key = "4", ratio = 0.8f},
        new GearRatio { key = "5", ratio = 0.6f, maxSpeed = 40f},
        new GearRatio { key = "R", ratio = 2.8f}
    };

    public float horizontalInput;
    public float verticalInput;
    private float brakeInput;
    
    public float currentSpeed;

    private float gearRatio;
    private int maxGear;
    public float speedup;

    private Rigidbody rb;

    void Start()
    {
        maxGear = gearRatios.Length - 2;
        rb = GetComponent<Rigidbody>();
        SpeedCalculation();
    }

    public void SetInput(float valueForward, float valueTurnAround)
    {
        verticalInput = valueForward;
        horizontalInput = valueTurnAround;
    }

    void FixedUpdate()
    {
        GetGear();
        CheckInput();
        HandleMotor();
        Brake();
        HandleSteering();
    }
    private void CheckInput()
    {

        float movingDirectional = Vector3.Dot(transform.forward, rb.velocity);
        brakeInput = (movingDirectional < -0.5f && verticalInput > 0) || (movingDirectional > 0.5f && verticalInput < 0) ? Mathf.Abs(verticalInput) : 0;
        if (verticalInput == 0)
            brakeInput = Mathf.Abs(movingDirectional) * 0.001f;
    }
    private void GetGear()
    {
        currentSpeed = rb.velocity.magnitude;
        if (verticalInput >= 0 && IsMovingForward())
        {
            for (int i = 1; i <= maxGear; i++)
            {
                if (currentSpeed < gearRatios[i].maxSpeed) 
                {
                    key = i.ToString();
                    break;
                }
            }
        }
        else if (verticalInput < 0f)
        {
            key = "R";
        }
    }
    private void HandleMotor()
    {
        currentSpeed = rb.velocity.magnitude;
        gearRatio = GetGearRatio();
        //Debug.Log(gearRatio);
        foreach (Wheel wheel in wheels)
        {
            if ((key == "R") && (currentSpeed < gearRatios[maxGear + 1].maxSpeed)) wheel.WheelCollider.motorTorque = gearRatio * motorForce * verticalInput * 0.6f - currentSpeed;
            else if ((key != "R") && (currentSpeed > 0) && (currentSpeed < gearRatios[maxGear].maxSpeed)) wheel.WheelCollider.motorTorque = gearRatio * motorForce * verticalInput * 0.6f - currentSpeed;
            else wheel.WheelCollider.motorTorque = 0;
            wheel.UpdateMeshPosition();
        }
        speedup = wheels[0].WheelCollider.motorTorque;
    }

    public bool IsMovingForward()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        return localVelocity.z > 0.1f;
    }

    private void HandleSteering()
    {
        float steeringAngle = horizontalInput * steeringCurve.Evaluate(currentSpeed);
        float slipAngle = Vector3.Angle(transform.forward, rb.velocity - transform.forward);

        if (slipAngle < 120)
        {
            steeringAngle += Vector3.SignedAngle(transform.forward, rb.velocity, Vector3.up);
        }

        steeringAngle = Mathf.Clamp(steeringAngle, -30, 30);

        foreach (Wheel wheel in wheels)
        {
            if (wheel.IsForwardWheel)
            {
                wheel.WheelCollider.steerAngle = steeringAngle;
            }
        }
    }
    private void Brake()
    {
        foreach (Wheel wheel in wheels)
        {
            wheel.WheelCollider.brakeTorque = brakeInput * brakeForce * (wheel.IsForwardWheel ? 0.7f : 0.7f);
        }
    }
    private void SpeedCalculation()
    {
        for(int i = 1; i < maxGear; i++) gearRatios[i].maxSpeed = (gearRatios[maxGear].maxSpeed * gearRatios[maxGear].ratio * i) / (gearRatios[i].ratio * maxGear);
        gearRatios[maxGear + 1].maxSpeed = (gearRatios[maxGear].maxSpeed * gearRatios[maxGear].ratio) * 3/ (gearRatios[maxGear + 1].ratio * maxGear);
    }
    float GetGearRatio()
    {
        //Debug.Log($"Передача {key}");
        foreach (var gear in gearRatios)
        {
            if (gear.key == key)
                return gear.ratio;
        }
        Debug.LogError($"Передача {key} не найдена!");
        return 0f;
    }
    public float CurrentSpeed()
    {
        return currentSpeed;
    }
}
[System.Serializable]
public struct Wheel
{
    public Transform WheelMesh;
    public WheelCollider WheelCollider;
    public bool IsForwardWheel;

    /// <summary>
    /// Обновляет позицию и поворот визуальной модели колеса в соответствии с физическим колесом.
    /// </summary>
    public void UpdateMeshPosition()
    {
        Vector3 position;
        Quaternion rotation;

        WheelCollider.GetWorldPose(out position, out rotation);
        WheelMesh.position = position;
        WheelMesh.rotation = rotation;
    }
}

[System.Serializable]
public struct GearRatio
{
    public string key;
    public float ratio;
    public float maxSpeed;
}