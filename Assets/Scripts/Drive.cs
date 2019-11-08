using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Drive : MonoBehaviour
{
    [Header("Car Configuration")]
    public bool allwheelDrive;
    public bool rearWheelDrive = true;
    public float torque = 200f;         //Max acceleration force
    public float maxSteerAngle = 30f;   //Max steering angle of wheels
    public float maxBrakeTroque = 500f; //Max stopping force
    [Range(0.05f, 1f)]
    public float brakeBiasF_R = 0.60f;
    public float skidThreshold = 0.4f;

    [Header("Wheel Configuration")]
    [Tooltip("WheelColliders: List Front wheelColliders first!")]
    public WheelCollider[] WCs;

    [Tooltip("Wheel Meshes: List Front Whel Meshes first!")]
    public GameObject[] Wheels;

    public ParticleSystem smokePrefab;
    ParticleSystem[] skidSmoke = new ParticleSystem[4];

    //TODO: use only the brakeLight Material prefab if possible instead of
    // the meshs material
    public Renderer[] brakeLightMesh;

    public Light[] brakeLights;
    public Rigidbody rb;
    public float gearLength = 3;
    public float engingLowPitch = 1f;
    public float engineHighPitch = 6f;
    public int numbGears = 5;
    public AudioSource skidSound;
    public AudioSource highAccelerationSound;
    public float maxSpeed = 140f;
    public float shiftTime = 5f;

    private float rpm;
    private int currentGear = 1;
    private Material[] mat;
    private float currentGearPerc;

    private float currentSpeed
    {
        get
        {
            return rb.velocity.magnitude * gearLength;
        }
    }



    // Start is called before the first frame update
    void Start()
    {

        for (int i = 0; i < Wheels.Length; i++)
        {
            skidSmoke[i] = Instantiate(smokePrefab);
            skidSmoke[i].Stop();
        }

        if (brakeLightMesh.Length > 0 && brakeLights.Length > 0)
        {
            for (int j = 0; j < brakeLightMesh.Length; j++)
            {
                mat[j] = brakeLightMesh[j].material;
            }

            BrakeLightsOff();
        }

    }

    // Update is called once per frame
    void Update()
    {
        float a = Input.GetAxis("Vertical");    //Acceleration input
        float s = Input.GetAxis("Horizontal");  //Steering input
        float b = Input.GetAxis("Jump");        //Braking input
        Go(a, s, b);

        CheckForSkid();
        CaculateEngineSound();

    }

    void Go(float accel, float steer, float brake)
    {
        accel = Mathf.Clamp(accel, -1, 1);          //Normalized acceleration force
        steer = Mathf.Clamp(steer, -1, 1) * maxSteerAngle; //Normalized steering angle
        brake = Mathf.Clamp(brake, 0, 1) * maxBrakeTroque;  //Normalized brake force
        float thrustTorque = 0;                     //Force applied to wheelCollider.motorTorque
        float rearBrakeBias = brakeBiasF_R;         //Force applied to WheelColider.brakeTorque
        float frontBrakeBias = 1f - brakeBiasF_R;   //The math to figure the brake bias from rear to front

        //Limit top speed of car
        if (currentSpeed < maxSpeed)
        {
            thrustTorque = accel * torque;
        }
        for (int i = 0; i < Wheels.Length; i++)
        {
            //CAR CONFIGURATION//////
            if (allwheelDrive && !rearWheelDrive)
            {
                //Allwheel Drive
                WCs[i].motorTorque = thrustTorque;

            }
            else if (!allwheelDrive && rearWheelDrive && i > 2)
            {
                //Rear Wheel Drive
                WCs[i].motorTorque = thrustTorque * 2;

            }
            else if (!rearWheelDrive && !allwheelDrive && i < 2)
            {
                //Front Wheel Drive
                WCs[i].motorTorque = thrustTorque * 2;

            }
            else if (allwheelDrive && rearWheelDrive)
            {
                Debug.LogError("Improper car configuration, Uncheck either " +
                    "rearWheelDrive or allWheelDrive in the inspector for the car configuration");
            }

            //BRAKES////
            //Apply rear brake bias to rear wheels
            if (i > 2)
            {
                WCs[i].brakeTorque = brake * rearBrakeBias;

            }
            //Apply front brake bias to front wheels
            else if (i < 2)
            {
                WCs[i].brakeTorque = brake * frontBrakeBias;

            }

            ///Steering/////
            //front tires only
            if (i < 2)
            {
                WCs[i].steerAngle = steer;
            }

            //Allign wheel meshes with WheelColliders for mesh rotation
            Quaternion quat;
            Vector3 position;
            WCs[i].GetWorldPose(out position, out quat);
            Wheels[i].transform.position = position;
            Wheels[i].transform.rotation = quat;
        }

        if (brake > 0)
        {
            EnableBrakeLights();
        }
        else
        {
            BrakeLightsOff();
        }
    }

    void CaculateEngineSound()
    {
        float gearPercentage = (1 / (float)numbGears);
        float targetGearFactor = Mathf.InverseLerp(gearPercentage * currentGear, gearPercentage * (currentGear + 1),
            Mathf.Abs(currentSpeed / maxSpeed));
        currentGearPerc = Mathf.Lerp(currentGearPerc, targetGearFactor, Time.deltaTime * shiftTime);
        var gearNumFactor = currentGear / (float)numbGears;
        rpm = Mathf.Lerp(gearNumFactor, 1, currentGearPerc);

        float speedPercentage = Mathf.Abs(currentSpeed / maxSpeed);
        float upperGearMax = (1 / (float)numbGears) * (currentGear + 1);
        float downGearMax = (1 / (float)numbGears) * currentGear;

        if (currentGear > 0 && speedPercentage < downGearMax)
        {
            currentGear--;
        }

        if (speedPercentage > upperGearMax && (currentGear < (numbGears - 1)))
        {
            currentGear++;
        }

        float pitch = Mathf.Lerp(engingLowPitch, engineHighPitch, rpm);
        highAccelerationSound.pitch = Mathf.Min(engineHighPitch, pitch) * 0.25f;
    }

    void CheckForSkid()
    {
        int numbSkidding = 0;
        for (int i = 0; i < Wheels.Length; i++)
        {
            WheelHit wheelHit;                  //Variable for wheelCollider out
            WCs[i].GetGroundHit(out wheelHit);  //WheelCollider ground collision

            //If forward or sideways slip reaches grip threshold then induce skidding
            if (Mathf.Abs(wheelHit.forwardSlip) >= skidThreshold || Mathf.Abs(wheelHit.sidewaysSlip) >= skidThreshold)
            {
                //Play Skidding Sound
                //TODO: Work out diffrent car configurations for skidding and smoke
                numbSkidding++;
                if (!skidSound.isPlaying)
                {
                    skidSound.Play();
                }

                skidSmoke[i].transform.position = WCs[i].transform.position - WCs[i].transform.up * WCs[i].radius;
                skidSmoke[i].Emit(1);

            }


        }
        //Stop Skidding Sound
        if (numbSkidding == 0 && skidSound.isPlaying)
        {
            skidSound.Stop();
        }
    }

    void EnableBrakeLights()
    {

        for (int i = 0; i < brakeLights.Length; i++)
        {

            brakeLights[i].enabled = true;
        }

        for (int j = 0; j < brakeLightMesh.Length; j++)
        {
            mat[j].SetColor("_EmissionColor", Color.red);
        }
    }

    void BrakeLightsOff()
    {

        for (int i = 0; i < brakeLights.Length; i++)
        {

            brakeLights[i].enabled = false;
        }

        for (int j = 0; j < brakeLightMesh.Length; j++)
        {
            mat[j].SetColor("_EmissionColor", Color.black);
        }
    }

}
