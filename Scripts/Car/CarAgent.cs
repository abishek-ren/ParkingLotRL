using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

namespace UnityStandardAssets.Vehicles.Car
{
    public class CarAgent : Agent
    {
        public float areaWidth = 20f;
        public float areaDepth = 20f;
        public float boundaryX = 3f;
        public float boundaryZ = 10f;
        public float targetProximityMultiplier = 1.5f;
        public GameObject goal;
        public SensorCompressionType compressionType = SensorCompressionType.PNG;

        private CarController autoController;
        EnvironmentParameters envParameters;
        private Rigidbody rigidBodyComponent;
        private int stepCounter = 0;

        private bool isWithinTarget = false;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 previousPosition;

        public bool seekParking = true;
        private bool aligning;
        private RayPerceptionSensorComponent3D raySensorComponent;
        private Vector3 parkingSpotLocation;
        private float estimatedSpotSize = 0f;

        void FixedUpdate()
        {
            RequestDecision();
            if (Mathf.Abs(transform.position.x - goal.transform.position.x) > boundaryX || Mathf.Abs(transform.position.z - goal.transform.position.z) > boundaryZ)
            {
                AddReward(-100f);
                EndEpisode();
            }
        }

        private void ResetAgent()
        {
            float randomX = Random.Range(initialPosition.x - areaWidth, initialPosition.x + areaWidth);
            float randomZ = Random.Range(initialPosition.z - areaDepth, initialPosition.z + areaDepth);
            Vector3 spawnPoint = new Vector3(randomX, initialPosition.y, randomZ);

            rigidBodyComponent.transform.position = spawnPoint;
            rigidBodyComponent.transform.rotation = initialRotation;
            rigidBodyComponent.velocity = Vector3.zero;
            rigidBodyComponent.angularVelocity = Vector3.zero;

            stepCounter = 0;
        }

        public override void Initialize()
        {
            autoController = GetComponent<CarController>();
            rigidBodyComponent = GetComponent<Rigidbody>();
            raySensorComponent = GetComponent<RayPerceptionSensorComponent3D>();
            envParameters = Academy.Instance.EnvironmentParameters;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            previousPosition = initialPosition;

            ResetAgent();
        }

        private void AdjustSpeed(float desiredSpeed)
        {
            if (autoController.CurrentSpeed < desiredSpeed)
            {
                autoController.Move(0, 0.5f, 0f, 0f);
            }
            else if (autoController.CurrentSpeed > desiredSpeed)
            {
                autoController.Move(0, -0.5f, 0f, 0f);
            }
        }

        private void AlignVehicle(float offset)
        {
            float distanceX = Mathf.Abs(transform.position.x - parkingSpotLocation.x);
            float absoluteOffset = Mathf.Abs(offset);

            if (distanceX < absoluteOffset && offset < 0)
            {
                autoController.Move(0f, -.3f, 0f, 0f);
            }
            else if (distanceX < absoluteOffset && offset > 0)
            {
                autoController.Move(0f, .1f, 0f, 0f);
            }
            else
            {
                aligning = false;
            }
        }

        private float ComputeReward()
        {
            float reward = 0f;
            float totalDirectionReward = 0f;
            float totalAngleReward = 0f;
            float totalDistanceReward = 0f;

            if (previousPosition != Vector3.zero)
            {
                float distanceX = Mathf.Abs(transform.position.x - goal.transform.position.x);
                float distanceZ = Mathf.Abs(transform.position.z - goal.transform.position.z);
                float previousDistanceX = Mathf.Abs(previousPosition.x - goal.transform.position.x);
                float previousDistanceZ = Mathf.Abs(previousPosition.z - goal.transform.position.z);
                float directionDeltaX = previousDistanceX - distanceX;
                float directionDeltaZ = previousDistanceZ - distanceZ;

                totalDirectionReward = (directionDeltaX + directionDeltaZ) * 10f;
                totalDirectionReward = Mathf.Clamp(totalDirectionReward, -0.5f, 0.5f);

                float distanceRewardX = (1f - distanceX / boundaryX);
                float distanceRewardZ = (1f - distanceZ / boundaryZ);

                totalDistanceReward = (distanceRewardX + distanceRewardZ) / 20f;

                reward += totalDirectionReward + totalDistanceReward;
            }

            if (isWithinTarget)
            {
                float angleToGoal = Vector3.Angle(transform.forward, goal.transform.forward);
                if (angleToGoal > 90f)
                {
                    angleToGoal = 180f - angleToGoal;
                }

                angleToGoal = Mathf.Clamp(angleToGoal, 0f, 90f);
                float angleReward = (-(1f / 45f) * angleToGoal) + 1f;

                totalAngleReward = angleReward + 1f;
                reward += totalAngleReward;

                float distanceToGoal = Vector3.Distance(transform.position, goal.transform.position);
                if (angleToGoal < 2.5f && distanceToGoal < 1f && Mathf.Abs(autoController.CurrentSpeed) < 2f)
                {
                    Debug.Log("Car parked!");
                    reward += 100f;
                    EndEpisode();
                }
            }

            previousPosition = transform.position;
            return reward;
        }

        public override void OnEpisodeBegin()
        {
            ResetAgent();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(autoController.CurrentSpeed);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            float steering = actions.ContinuousActions[0];
            float accel = actions.ContinuousActions[1];
            float reverse = actions.ContinuousActions[2];

            accel = (accel + 1) / 2;
            reverse = (reverse + 1) / 2;

            accel = accel - reverse;
            if (!aligning)
            {
                autoController.Move(steering, accel, 0f, 0f);
            }

            stepCounter++;
            float reward = ComputeReward();
            AddReward(reward);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;

            float steeringInput = Input.GetAxis("Horizontal");
            float accelInput = Input.GetAxis("Accelerate");
            float reverseInput = Input.GetAxis("Reverse");

            accelInput = accelInput * 2 - 1;
            reverseInput = reverseInput * 2 - 1;

            continuousActionsOut[0] = steeringInput;
            continuousActionsOut[1] = accelInput;
            continuousActionsOut[2] = reverseInput;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.tag == "Finish")
            {
                isWithinTarget = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.gameObject.tag == "Finish")
            {
                isWithinTarget = false;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.tag == "Wall")
            {
                AddReward(-10f);
                EndEpisode();
            }
        }

        void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.tag == "Kerb")
            {
                AddReward(-2f);
            }
            else if (collision.gameObject.tag == "Car")
            {
                float collisionPenalty = -Mathf.Abs(autoController.CurrentSpeed) * 70f - 5f;
                AddReward(collisionPenalty);
                EndEpisode();
            }
        }
    }
}
