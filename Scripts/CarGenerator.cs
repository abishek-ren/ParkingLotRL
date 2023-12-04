using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarGenerator : MonoBehaviour
{
    public float TravelRange = 20f;
    public float BaseVelocity = 1.5f; // Base speed in meters per second
    public float VelocityVariation = 1.5f; // Speed variation in meters per second
    public float GenerationInterval = 2f;
    public float IntervalVariation = 3f;

    private float countdown = 0f;
    
    public GameObject FirstVehicle;
    private List<GameObject> GeneratedVehicles = new List<GameObject>();

    private Vector3 GenerationPoint;
    private Quaternion GenerationDirection;

    // Called before the first frame update
    void Start()
    {
        GenerationPoint = gameObject.transform.position;
        GenerationDirection = gameObject.transform.rotation;

        GenerateVehicle();
    }

    // Called once per frame
    void Update()
    {
        countdown += Time.deltaTime;

        if (countdown >= GenerationInterval + Random.Range(0, IntervalVariation))
        {
            countdown = 0f;
            GenerateVehicle();
        }

        foreach (GameObject vehicle in GeneratedVehicles)
        {
            float TravelledDistance = Mathf.Abs(GenerationPoint.x - vehicle.transform.position.x);
            
            if (TravelledDistance > TravelRange)
            {
                RemoveVehicle(vehicle);
                break;
            }
            else
            {
                float currentSpeed = BaseVelocity + Random.Range(-VelocityVariation, VelocityVariation);
                vehicle.transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
            }   
        }
    }

    private void GenerateVehicle()
    {
        GameObject newVehicle = Instantiate(FirstVehicle, GenerationPoint, GenerationDirection);
        GeneratedVehicles.Add(newVehicle);
    }

    private void RemoveVehicle(GameObject vehicle)
    {
        GeneratedVehicles.Remove(vehicle);
        Destroy(vehicle);
    }
}
