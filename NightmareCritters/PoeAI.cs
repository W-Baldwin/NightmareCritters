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
using UnityEngine.AI;

namespace NightmareCritters
{
    internal class PoeAI : EnemyAI
    {

        internal enum poeStates { GroundWander, Ascend, Flying, asd, Descend, Walk, Idle};
        internal enum HeadRotationMode { Ground, Ascend, Air };

        internal enum poeFlyingPatterns { FlyToHighest, Circle, Swoop, Glide, None };
        internal Queue<poeFlyingPatterns> flyingPatternQueue = new Queue<poeFlyingPatterns>();

        public HeadRotationMode headRotationMode = HeadRotationMode.Ground;

        public Transform animationContainer;

        public AISearchRoutine poeSearch;

        //Audio Clips
        public AudioClip[] flapSounds;
        public AudioClip[] deathSounds;
        public AudioClip[] squawkSounds;
        public AudioClip[] impactSounds;
        public AudioClip[] patrolWarningSounds;
        public AudioClip[] attackWarningSounds;
        public AudioClip[] squawkFlySounds;
        public AudioClip[] squawkGroundFarSounds;


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
        public GameObject lastDestinationNode = null;
        public Vector3 targetFlightNodePosition;
        [SerializeField]
        public Queue<GameObject> flyingRouteLocations = new Queue<GameObject>();
        public poeFlyingPatterns flyingPattern = poeFlyingPatterns.None;

        //Wandering
        public readonly float maxWanderTime = 5f;
        public float wanderTime = 0;

        //Ascending/Descending
        public bool ascensionTargetValid = false;
        public GameObject ascensionTarget;
        public Vector3 descensionTarget;
        public bool gravityApplied = false;

        //Flight Engine
        float flightExtraVelocity = 0;
        float fleightVelocity = 5.0f;
        float maxSpeed = 22f;
        float flightBaseSpeed = 5.0f; // Adjust as needed for desired ascension speed
        float heightSpeed = 2.0f; // Separate speed factor for height adjustment
        FlyableGrid instanceFlyableGrid = null;
        public Vector3 flightDirection;
        public bool targetNodeAbove = false;

        //Flight state
        


        public override void Start()
        {
            base.Start();
            if (!FlyableGrid.mapCreatedOrInProgress && isOutside && (IsHost || IsServer))
            {
                FlyableGrid.mapCreatedOrInProgress = true;
                StartCoroutine(CreateFlyableArea());
            }
            else if (IsHost || IsServer)
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
            instanceFlyableGrid = flyableGridObject.AddComponent<FlyableGrid>(); ;
            yield return StartCoroutine(instanceFlyableGrid.ConstructFlyableAreaMap(flyableGridObject, allAINodes));
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
                    this.flyableArea = island;
                    Debug.Log($"Poe AssignFlyableArea: Island assigned! Found:{flyableArea != null}");
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
                        if (Vector3.Distance(transform.position, destinationNode.transform.position) < 3)
                        {
                            lastDestinationNode = destinationNode;
                            destinationNode = null;
                        }
                    }
                    else //Destination node == null;
                    {
                        if (flyingRouteLocations.Count == 0)
                        {
                            List<GameObject> potentialPath = null;
                            int attempts = 0;
                            while (potentialPath == null && attempts < 15)
                            {
                                potentialPath = flyableArea.GetRandomPath(transform, flyableArea);
                                attempts++;
                            }       
                            if (potentialPath != null && potentialPath.Count > 0)
                            {
                                foreach (GameObject node in potentialPath)
                                {
                                    if (node != null)
                                    {
                                        flyingRouteLocations.Enqueue(node);
                                    }
                                }
                            }
                            else { Debug.LogWarning("Failed to find flight path after 20 attempts."); }
                        }
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
            if (IsHost || IsServer)
            {
                UpdateRotationCalculations();
                UpdateStateDependent();
                UpdateFlightEngine();
            }
        }

        public void LateUpdate()
        {
            if (IsHost || IsServer)
            {
                UpdateHeadRotation();
            }
        }


        private void UpdateRotationCalculations()
        {
            Quaternion currentRotation = animationContainer.transform.rotation; ;
            if (!flying && currentBehaviourStateIndex == (int)poeStates.GroundWander)
            {
                float ratioForward = agent.velocity.magnitude / agent.speed; 
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
            else if (currentBehaviourStateIndex == (int)poeStates.Flying)//flying, we want to make z go up to +25 or -25 smoothly (this is in update) based on whether we are turning left or right. I can declare more variables above to achieve this.  It likely loooks like x rotations above
            {
                // Calculate Z tilt for flying based on turn direction
                float turnDirection = Vector3.Dot(transform.right, agent.velocity.normalized);
                float targetRotationZ = Mathf.Clamp(targetTiltX * turnDirection, -25f, 25f); // Adjust max tilt angle as needed

                Quaternion targetRotation = Quaternion.Euler(currentRotation.eulerAngles.x, currentRotation.eulerAngles.y, targetRotationZ);

                // Smoothly apply the Z tilt
                animationContainer.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, 2f * Time.deltaTime); // Adjust speed as desired
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
                case (int)poeStates.Descend:
                    agent.updatePosition = false;
                    agent.updateRotation = false;
                    agent.updateUpAxis = false;
                    if (descensionTarget == null)
                    {
                        Debug.Log("Flight Engine: descensionTarget is null.");
                        return;
                    }
                    targetFlightNodePosition = descensionTarget;
                    transform.position = Vector3.Lerp(transform.position, targetFlightNodePosition, Time.deltaTime);
                    Vector3 directionToTarget = new Vector3(targetFlightNodePosition.x - transform.position.x, 0, targetFlightNodePosition.z - transform.position.z).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 1.0f);
                    break;
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
                        if (Mathf.Abs(targetFlightNodePosition.y - headBone.position.y) > 3)
                        {
                            flightExtraVelocity += (targetFlightNodePosition.y < headBone.position.y) ? Time.deltaTime * heightSpeed : -Time.deltaTime * heightSpeed * 1f;
                        }
                        else { flightExtraVelocity += -Time.deltaTime * heightSpeed *1.2f; }
                        flightExtraVelocity = Mathf.Clamp(flightExtraVelocity, 0, (maxSpeed - flightBaseSpeed));
                        fleightVelocity = Mathf.Clamp(flightBaseSpeed + flightExtraVelocity, flightBaseSpeed, maxSpeed);
                        transform.position += flightDirection * fleightVelocity * Time.deltaTime;
                        /*Vector3 directionToTarget2 = new Vector3(targetFlightNodePosition.x - transform.position.x, 0, targetFlightNodePosition.z - transform.position.z);*/
                        Quaternion targetRotation2 = Quaternion.LookRotation(targetDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation2, Time.deltaTime * 2.0f);
                        creatureAnimator.SetBool("Altitude", targetFlightNodePosition.y > transform.position.y + 3);
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
            if (!IsHost) { return; }
            SwitchToBehaviourState((int)poeStates.Ascend);
            creatureAnimator.speed = 2.3f;
            closestNode = null;
            gravityApplied = true;
            agent.enabled = false;
        }

        private void SwitchToFlying()
        {
            if (!IsHost) { return; }
            SwitchToBehaviourState((int)poeStates.Flying);
            creatureAnimator.SetBool("Flying", true);
            creatureAnimator.speed = 1.0f;
            flying = true;
            GameObject temp = flyableArea.GetHighestNode(closestNode.transform);
            List<GameObject> tempList = flyableArea.FindShortestNodeChainDFS(closestNode.transform, temp.transform); 
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

        private void SwitchToDescend()
        {
            if (!IsHost) { return; }
            SwitchToBehaviourState((int)poeStates.Descend);
            creatureAnimator.SetBool("Flying", false);
            creatureAnimator.speed = 1.5f;
            AcquireDescensionTarget();
        }

        private void SwitchToGroundWander()
        {
            SwitchToBehaviourState((int)poeStates.GroundWander);
            creatureAnimator.SetBool("Flying", false);
            wanderTime = 0.0f;
            creatureAnimator.speed = 1.0f;
        }

        private bool AcquireDescensionTarget()
        {
            bool success = false;
            descensionTarget = Vector3.zero;
            int attempts = 10;
            float arcAngle = 45f; // Starting angle for downward arc
            float rayDistance = 5f; // Distance for each raycast
            float downwardStep = 3f; // Vertical distance per step

            for (int i = 0; i < attempts; i++)
            {
                // Calculate a point in an arc in front of and below the creature
                float angle = Mathf.Deg2Rad * arcAngle;
                Vector3 forwardDownPoint = transform.position + transform.forward * rayDistance + Vector3.down * (i * downwardStep);

                // Check for NavMesh hit at the calculated point
                if (NavMesh.SamplePosition(forwardDownPoint, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
                {
                    descensionTarget = hit.position;
                    success = true;
                    Debug.Log("Descension target found on NavMesh.");
                    break;
                }

                // Incrementally increase the arc angle for a wider search
                arcAngle += 5f;
            }

            if (!success)
            {
                Debug.LogWarning("AcquireDescensionTarget: No NavMesh target found within the arc search.");
            }

            return success;
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

        //In case current methods stop working well.
        public IEnumerator FindShortestNodeChainBFS(Transform startTransform, Transform endTransform, Action<List<GameObject>> callback, FlyableAreaIsland flyingArea)
        {
            GameObject startNode = startTransform.gameObject;
            GameObject endNode = endTransform.gameObject;
            Debug.Log($"Start Node: {startNode.name}, End Node: {endNode.name}");

            if (startNode == null || endNode == null)
            {
                Debug.Log("FindShortestNodeChain: Early return start or end is null.");
                callback(null);
                yield break;
            }

            if (startNode == endNode)
            {
                callback(new List<GameObject> { startNode });
                yield break;
            }

            var parents = new Dictionary<GameObject, GameObject>();
            var queue = new Queue<GameObject>();
            var visited = new HashSet<GameObject> { startNode };

            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                Debug.Log($"Processing node: {currentNode.name}");

                foreach (var neighbor in flyingArea.GetNodeNeighbors(currentNode))
                {
                    if (visited.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);
                    parents[neighbor] = currentNode;
                    queue.Enqueue(neighbor);

                    if (neighbor == endNode)
                    {
                        // Path from start to end node
                        var path = new List<GameObject>();
                        for (var node = endNode; node != null; node = parents.GetValueOrDefault(node))
                        {
                            path.Add(node);
                            Debug.Log($"Path node: {node.name}");
                        }
                        path.Reverse();

                        // Invoke the callback with the path and exit the coroutine
                        callback(path);
                        yield break;
                    }
                }

                yield return null; // Yield control to avoid frame lag if necessary
            }

            Debug.Log("No path found from start to end node.");
            callback(null); // Callback with null if no path found
        }

        public void PlayRandomFlapSound()
        {
            if (flapSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, flapSounds, true, 1);
            }
        }

        public void PlayRandomDeathSound()
        {
            if (deathSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, deathSounds, true, 1);
            }
        }

        public void PlayRandomSquawkSound()
        {
            if (squawkSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, squawkSounds, true, 1);
            }
        }

        public void PlayRandomImpactSound()
        {
            if (impactSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, impactSounds, true, 1);
            }
        }

        public void PlayRandomPatrolWarningSound()
        {
            if (patrolWarningSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, patrolWarningSounds, true, 1);
            }
        }

        public void PlayRandomAttackWarningSound()
        {
            if (attackWarningSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, attackWarningSounds, true, 1);
            }
        }

        public void PlayRandomSquawkGroundFarSound()
        {
            if (squawkGroundFarSounds != null)
            {
                RoundManager.PlayRandomClip(creatureVoice, squawkGroundFarSounds, true, 1);
            }
        }
    }
}
