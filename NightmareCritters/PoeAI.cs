using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using NightmareCritters.Flyable;
using LethalConfig.ConfigItems;
using System.Net;
using static Unity.Audio.Handle;
using TMPro;
using System.Linq;

namespace NightmareCritters
{
    internal class PoeAI : EnemyAI
    {

        internal enum poeStates { GroundWander, Ascend, Flying, asd, Descend, Walk, Idle};
        internal enum HeadRotationMode { Ground, Ascend, Air };

        internal enum poeFlyingPatterns { FlyToHighest, Circle, Swoop, Glide, None };

        public HeadRotationMode headRotationMode = HeadRotationMode.Ground;

        public Transform animationContainer;

        public AISearchRoutine poeSearch;

        public AudioClip[] flapSounds;

        //previous frame rotation y
        public int targetTiltX = 25;
        public float currentFrameYRotation = 0;
        public float previousFrameYRotation = 0;

        //Head rotations
        public float frameRotationOffset = 0;
        public Transform headBone;
        public float maxLookAngle = 55f;
        public float lookSpeed = 3.0f;
        public Quaternion targetLookAngle;
        public Quaternion currentLookAngle;
        public Quaternion noTargetLookAngle = Quaternion.identity;


        //Flying Location Nodes
        public bool flying = false;
        public bool validFlightPattern = false;
        public FlyableAreaIsland flyableArea = null;
        public GameObject closestNode = null;
        public GameObject destinationNode = null;
        public Vector3 targetFlightNodePosition;
        [SerializeField]
        public Queue<GameObject> flyingRouteLocations = new Queue<GameObject>();
        public poeFlyingPatterns flyingPattern = poeFlyingPatterns.None;

        //Wandering
        private readonly float maxWanderTime = 5f;
        private float wanderTime = 0;

        //Ascending
        public bool ascensionTargetValid = false;
        public GameObject ascensionTarget;
        public bool gravityApplied = false;

        //Flight Engine
        public Vector3 velocity;
        float flightSpeed = 5.0f; // Adjust as needed for desired ascension speed
        float heightSpeed = 2.0f; // Separate speed factor for height adjustment
        public Vector3 flightDirection;


        public override void Start()
        {
            base.Start();
            if (!FlyableGrid.mapCreatedOrInProgress && isOutside && IsHost)
            {
                FlyableGrid.mapCreatedOrInProgress = true;
                StartCoroutine(CreateFlyableArea());
            }
            else
            {
                StartCoroutine(AssignFlyableArea());
            }
        }

        private IEnumerator CreateFlyableArea()
        {
            while (allAINodes == null)
            {
                yield return null;
            }
            GameObject flyableGridObject = new GameObject("FlyableGrid");
            FlyableGrid flyableGridComponent = flyableGridObject.AddComponent<FlyableGrid>(); ;
            yield return StartCoroutine(flyableGridComponent.ConstructFlyableAreaMap(flyableGridObject, allAINodes));
            StartCoroutine(AssignFlyableArea());
        }

        private IEnumerator AssignFlyableArea()
        {
            float timeWaited = 0f;
            //Wait a little for extra safety.
            while (!FlyableGrid.created && timeWaited < 15.0f)
            {
                timeWaited += Time.deltaTime;
                yield return null;
            }

            if (FlyableGrid.flyableAreaIslands == null)
            {
                Debug.LogWarning("Poe AssignFlyableArea: Couldn't find flying islands!");
                yield break;
            }

            Debug.Log("Poe AssignFlyableArea: Trying to set island..");
            GameObject closestNode = null;
            float closestDistance = Mathf.Infinity;
            foreach (FlyableAreaIsland flyingIsland in FlyableGrid.flyableAreaIslands)
            {
                float nodeCount = 0;
                if (flyingIsland == null) { continue; }
                foreach (GameObject node in flyingIsland.flyNodes)
                {
                    if (node == null) {  continue; }
                    float distance = (Vector3.Distance(headBone.transform.position, node.transform.position));
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestNode = node;
                    }
                    nodeCount++;
                    if (nodeCount > 50)
                    {
                        nodeCount = 0;
                        yield return null;
                    }
                }
            }

            foreach (FlyableAreaIsland island in FlyableGrid.flyableAreaIslands)
            {
                if (island.flyNodes.Contains(closestNode))
                {
                    Debug.Log($"Poe AssignFlyableArea: Island assigned! Found:{ flyableArea != null }");
                    this.flyableArea = island;
                    yield break;
                }
            }
            Debug.Log("Poe AssignFlyableArea: Searched through islands and didn't find any!");
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (StartOfRound.Instance.allPlayersDead || isEnemyDead)
            {
                return;
            }
            switch(currentBehaviourStateIndex)
            {
                case (int)poeStates.GroundWander:
                    if (!poeSearch.inProgress)
                    {
                        StartSearch(transform.position, poeSearch);
                    }
                    break;
                case (int)poeStates.Ascend:
                    if (poeSearch.inProgress)
                    {
                        StopSearch(poeSearch, true);
                    }
                    if (flyableArea != null)
                    {
                        if (closestNode != null)
                        {
                            if (ascensionTarget == null)
                            {
                                //Check for a valid place to go up.
                                if (!Physics.Linecast(eye.position, closestNode.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                                {
                                    ascensionTarget = closestNode;
                                }
                                ascensionTarget = closestNode;
                            }
                            else //ascensionTarget = valid
                            {
                                if (Physics.Linecast(eye.position, ascensionTarget.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                                {
                                    ascensionTarget = null;
                                }
                                ascensionTarget = closestNode;
                            }
                        }
                        else //no closest node????
                        {
                            Debug.Log("PoeAIUpdate: No closest node.");
                        }
                        closestNode = flyableArea.GetClosestNode(transform);
                    }
                    else { Debug.Log("Poe: Flyable Island null!"); }

                    if (ascensionTarget != null)
                    {
                        float distanceToAscensionPoint = Vector3.Distance(transform.position, ascensionTarget.transform.position);
                        if (distanceToAscensionPoint < 3)
                        {
                            SwitchToFlying();
                        }
                    }
                    break;
                case (int)poeStates.Flying:
                    closestNode = flyableArea.GetClosestNode(transform);
                    if (destinationNode != null)
                    {
                        if (Vector3.Distance(transform.position, destinationNode.transform.position) < 2)
                        {
                            destinationNode = null;
                        }
                    }
                    else //Destination node == null;
                    {
                        GameObject result;
                        if (flyingRouteLocations.TryPeek(out result))
                        {
                            destinationNode = flyingRouteLocations.Dequeue();
                            targetFlightNodePosition = destinationNode.transform.position;
                        }
                    }
                    break;
            }

        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead)
            {
                return;
            }
            UpdateRotationCalculations();
            UpdateStateDependent();
            UpdateFlightEngine();
        }

        public void LateUpdate()
        {
            UpdateHeadRotation();
        }


        private void UpdateRotationCalculations()
        {
            if (!flying && currentBehaviourStateIndex == (int)poeStates.GroundWander)
            {
                float ratioForward = agent.velocity.magnitude / agent.speed;
                Quaternion currentRotation = animationContainer.transform.rotation;
                //We want the x rotation to be up to 25 based on the ratioForward
                float targetRotationX = targetTiltX * ratioForward;
                Quaternion targetRotation = Quaternion.Euler(targetRotationX, currentRotation.eulerAngles.y, currentRotation.eulerAngles.z);
                animationContainer.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, 4f * Time.deltaTime);
                frameRotationOffset = -targetRotation.eulerAngles.x;

                //Head stuff
                Vector3 currentHeadEulerAngles = Vector3.zero;
                currentHeadEulerAngles.x = frameRotationOffset;
                noTargetLookAngle = Quaternion.Euler(currentHeadEulerAngles);
            }
            else //flying
            {

            }
        }

        private void UpdateStateDependent()
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)poeStates.GroundWander:
                    wanderTime += Time.deltaTime;
                    if (wanderTime > maxWanderTime)
                    {
                        wanderTime = 0;
                        SwitchToAscend();
                    }
                    break;
            }
        }

        private void UpdateFlightEngine()
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)poeStates.Ascend:
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    agent.updateUpAxis = false;
                    if (ascensionTarget == null)
                    {
                        Debug.Log("Flight Engine: ascensionTarget is null.");
                        return;
                    }
                    targetFlightNodePosition = ascensionTarget.transform.position;
                    transform.position = Vector3.Lerp(transform.position, targetFlightNodePosition, Time.deltaTime);
                    Vector3 directionToTarget = new Vector3(targetFlightNodePosition.x - transform.position.x, 0, targetFlightNodePosition.z - transform.position.z).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 1.0f);
                    break;
                case (int)poeStates.Flying:
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    agent.updateUpAxis = false;
                    //Check if we have valid flight pattern, if none set a new one.
                    switch (flyingPattern)
                    {
                        case poeFlyingPatterns.None:
                            flyingPattern = poeFlyingPatterns.FlyToHighest;
                            break;
                        case poeFlyingPatterns.FlyToHighest:

                            break;
                    }
                    if (destinationNode != null)
                    {
                        targetFlightNodePosition = destinationNode.transform.position;
                        Vector3 targetDirection = (targetFlightNodePosition - transform.position).normalized;
                        Vector3 currentDirection = transform.forward.normalized;
                        flightDirection = Vector3.Slerp(currentDirection, targetDirection, Time.deltaTime);
                        transform.position += flightDirection * flightSpeed * Time.deltaTime;
                        Vector3 directionToTarget2 = new Vector3(targetFlightNodePosition.x - transform.position.x, 0, targetFlightNodePosition.z - transform.position.z);
                        Quaternion targetRotation2 = Quaternion.LookRotation(targetDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation2, Time.deltaTime * 2.0f);
                    }
                    
                    //Implement the pattern by filling queue of nodes

                    //If within distance of next location, set next location.
                    break;
            }
        }

        private void UpdateHeadRotation()
        {
            PlayerControllerB closestPlayer = GetClosestPlayerFixed(transform.position);

            if (closestPlayer == null || Vector3.Distance(closestPlayer.transform.position, transform.position) > 12)
            {
                targetLookAngle = noTargetLookAngle;
            }
            else
            {
                switch (headRotationMode)
                {
                    case HeadRotationMode.Ground:
                        //alculate direction to the player
                        Vector3 playerEyePosition = closestPlayer.playerEye.position;
                        Vector3 directionToPlayer = playerEyePosition - headBone.position;

                        //generate a rotation towards the player in world space
                        Quaternion worldRotationTowardsPlayer = Quaternion.LookRotation(directionToPlayer);

                        //convert the target rotation to the local space of the creature’s body
                        Quaternion localRotationTowardsPlayer = Quaternion.Inverse(headBone.parent.rotation) * worldRotationTowardsPlayer;

                        //clamp the local rotation within max look angles
                        Vector3 targetEulerAngles = localRotationTowardsPlayer.eulerAngles;

                        //Clamp
                        targetEulerAngles.x = Mathf.Clamp(targetEulerAngles.x > 180 ? targetEulerAngles.x - 360 : targetEulerAngles.x, -maxLookAngle, maxLookAngle);
                        targetEulerAngles.y = Mathf.Clamp(targetEulerAngles.y > 180 ? targetEulerAngles.y - 360 : targetEulerAngles.y, -maxLookAngle, maxLookAngle);

                        //Update targetLookAngle based on the clamped angles
                        targetLookAngle = Quaternion.Euler(targetEulerAngles);
                        break;
                }
            }
            //interpolate
            currentLookAngle = Quaternion.Slerp(currentLookAngle, targetLookAngle, lookSpeed * Time.deltaTime);
            headBone.localRotation = currentLookAngle;
        }

        private void SwitchToAscend()
        {
            SwitchToBehaviourState((int)poeStates.Ascend);
            creatureAnimator.speed = 2.3f;
            closestNode = null;
            gravityApplied = true;
            agent.enabled = false;
        }

        private void SwitchToFlying()
        {
            SwitchToBehaviourState((int)poeStates.Flying);
            creatureAnimator.SetBool("Flying", true);
            creatureAnimator.speed = 1.0f;
            flying = true;
            GameObject temp = flyableArea.GetHighestNode(closestNode.transform);
            List<GameObject> tempList = flyableArea.FindShortestNodeChain(closestNode.transform, temp.transform); 
            if (temp != null)
            {
                if (tempList != null)
                {
                    foreach (GameObject node in tempList)
                    {
                        flyingRouteLocations.Enqueue(node);
                    }
                    return;
                }
                Debug.Log("Couldn't find chain, using single node.");
                flyingRouteLocations.Enqueue(temp);
            }
        }


        private PlayerControllerB GetClosestPlayerFixed(Vector3 toThisPosition)
        {
            PlayerControllerB closestPlayer = null;
            float distanceOfClosestPlayerSoFar = 10000f;
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (StartOfRound.Instance.allPlayerScripts[i].isPlayerDead || !(!StartOfRound.Instance.allPlayerScripts[i].isInsideFactory == this.isOutside) || StartOfRound.Instance.allPlayerScripts[i].inSpecialInteractAnimation)
                {
                    continue;
                }
                float playerDistanceToPosition = Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].transform.position, toThisPosition);
                if (playerDistanceToPosition < distanceOfClosestPlayerSoFar)
                {
                    closestPlayer = StartOfRound.Instance.allPlayerScripts[i];
                    distanceOfClosestPlayerSoFar = playerDistanceToPosition;
                }
            }
            return closestPlayer;
        }

        public void PlayRandomFlapSound()
        {
            if (flapSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, flapSounds, true, 1);
            }
        }
    }
}
