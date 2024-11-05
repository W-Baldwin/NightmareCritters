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

namespace NightmareCritters
{
    internal class PoeAI : EnemyAI
    {

        internal enum poeStates { GroundWander, Ascend, AirPatrol, asd, Descend, Walk, Idle};
        internal enum HeadRotationMode { Ground, Ascend, Air };

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
        public FlyableAreaIsland flyableArea = null;
        public GameObject closestNode;
        public Queue<Vector3> flyingRouteLocations = new Queue<Vector3>();

        //Wandering
        private readonly float maxWanderTime = 5f;
        private float wanderTime = 0;


        public override void Start()
        {
            base.Start();
            if (!FlyableGrid.mapCreatedOrInProgress && isOutside && IsHost)
            {
                FlyableGrid.mapCreatedOrInProgress = true;
                StartCoroutine(CreateFlyableArea());
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
            //Wait a little for extra safety.
            yield return new WaitForSeconds(1.0f);

            if (FlyableGrid.flyableAreaIslands == null)
            {
                yield break;
            }

            FlyableAreaIsland flyableArea = null;
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
                    this.flyableArea = flyableArea;
                }
            }
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
                    closestNode = flyableArea.GetClosestNode(transform);
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
        }

        public void LateUpdate()
        {
            UpdateHeadRotation();
        }


        private void UpdateRotationCalculations()
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
            if (poeSearch.inProgress)
            {
                StopSearch(poeSearch, true);
            }
            SwitchToBehaviourState((int)poeStates.Ascend);
            creatureAnimator.SetBool("Flying", true);
            closestNode = null;
            agent.enabled = false;
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
