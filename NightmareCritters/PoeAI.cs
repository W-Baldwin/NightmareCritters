using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using UnityEngine;
using Unity.Netcode;

namespace NightmareCritters
{
    internal class PoeAI : EnemyAI
    {

        internal enum poeStates { Patrol, Divebomb, Observe, Ascend, Descend, Walk, Idle};
        internal enum HeadRotationMode { Ground, Ascend, Air };

        public GameObject topLevelMapContainer = null;
        public static GameObject[][] flyableAreaMap;
        public static GameObject[][] flyableAreaIslands;
        public static Vector3 flyableAreaMapOrigin;
        public static bool mapCreatedOrInProgress = false;
        public static bool destructionInProgress = false;

        public HeadRotationMode headRotationMode = HeadRotationMode.Ground;

        public Transform animationContainer;

        public AISearchRoutine poeSearch;

        public AudioClip[] flapSounds;

        //previous frame rotation y
        public int targetTiltX = 25;
        float frameCountYRot = 0;
        float yRotFiveFramesAgo = 0;
        float yRotTenFramesAgo = 0;

        //Head rotations
        public float frameRotationOffset = 0;
        public Transform headBone;
        public float maxLookAngle = 55f;
        public float lookSpeed = 3.0f;
        public Quaternion targetLookAngle;
        public Quaternion currentLookAngle;
        public Quaternion noTargetLookAngle = Quaternion.identity;

        //Fly map stuff
        public static int debugFramesNodesNull = 0;

        public override void Start()
        {
            base.Start();
            if (!mapCreatedOrInProgress)
            {
                mapCreatedOrInProgress = true;
                StartCoroutine(ConstructFlyableAreaMap());
            }
        }


        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (StartOfRound.Instance.allPlayersDead || isEnemyDead)
            {
                return;
            }
            if (!poeSearch.inProgress)
            {
                StartSearch(transform.position, poeSearch);
            }
            Invoke("FooClientRpc", 1);
        }

        public override void Update()
        {
            base.Update();
            if (isEnemyDead)
            {
                return;
            }
            UpdateRotationCalculations();
        }

        [ClientRpc]
        private void FooClientRpc()
        {
            Debug.Log("Bar");
        }

        public void LateUpdate()
        {
            UpdateHeadRotation();
        }

        public IEnumerator ConstructFlyableAreaMap()
        {
            while (allAINodes == null)
            {
                yield return null;
            }
            const float cubeSize = 10.0f;

            // Calculate average center and lowest Y for origin
            Vector3 averagePosition = Vector3.zero;
            float lowestY = Mathf.Infinity;
            float minX = Mathf.Infinity, maxX = -Mathf.Infinity;
            float minZ = Mathf.Infinity, maxZ = -Mathf.Infinity;

            foreach (var AInode in allAINodes)
            {
                Vector3 pos = AInode.transform.position;
                averagePosition += pos;

                // Track the lowest y position
                if (pos.y < lowestY)
                    lowestY = pos.y;

                // Track x and z boundaries to determine grid size
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;
                yield return null;
            }

            averagePosition /= allAINodes.Length;
            flyableAreaMapOrigin = new Vector3(averagePosition.x, lowestY, averagePosition.z);
            GameObject flyableGridContainer = new GameObject("FlyableGrid");
            topLevelMapContainer = flyableGridContainer;
            GameObject node = new GameObject($"FlyGridNode{flyableAreaMapOrigin.x},{flyableAreaMapOrigin.y},{flyableAreaMapOrigin.z}");
            node.transform.position = flyableAreaMapOrigin;
            node.transform.parent = flyableGridContainer.transform;

            Debug.Log($"Average Position: {averagePosition}");
            Debug.Log($"Lowest Y: {lowestY}");
            Debug.Log($"Min X: {minX}, Max X: {maxX}");
            Debug.Log($"Min Z: {minZ}, Max Z: {maxZ}");
            Debug.Log($"Flyable Area Map Origin: {flyableAreaMapOrigin}");

            // Determine grid dimensions
            int gridSizeX = Mathf.CeilToInt((maxX - minX) / cubeSize) + 1;
            int gridSizeZ = Mathf.CeilToInt((maxZ - minZ) / cubeSize) + 1;
            int gridLayersY = 7; // Arbitrary number of Y layers; adjust as needed

            // Initialize a 1D list to store nodes
            List<GameObject> flyableAreaMap = new List<GameObject>();

            // Populate the grid with nodes
            for (int yLayer = 0; yLayer < gridLayersY; yLayer++)
            {
                for (int xIndex = 0; xIndex < gridSizeX; xIndex++)
                {
                    for (int zIndex = 0; zIndex < gridSizeZ; zIndex++)
                    {
                        // Calculate the position for each node
                        Vector3 position = new Vector3(
                            minX + (xIndex * cubeSize),
                            lowestY + (yLayer * cubeSize),
                            minZ + (zIndex * cubeSize)
                        );

                        // Check for collisions
                        Collider[] colliders = Physics.OverlapBox(position, Vector3.one * (cubeSize / 2), Quaternion.identity);
                        bool hasCollision = false;

                        foreach (Collider collider in colliders)
                        {
                            if (!collider.isTrigger) // Ignore trigger colliders
                            {
                                hasCollision = true;
                                break;
                            }
                        }

                        // If no collision, create and add the node
                        if (!hasCollision)
                        {
                            GameObject AInode = new GameObject($"FlyGridNode{xIndex},{yLayer},{zIndex}");
                            AInode.transform.position = position;
                            AInode.transform.parent = flyableGridContainer.transform;
                            BoxCollider collider = AInode.AddComponent<BoxCollider>();
                            collider.size = Vector3.one * cubeSize;
                            collider.isTrigger = true;
                            AInode.AddComponent<MeshRenderer>();

                            // Add node to the list
                            flyableAreaMap.Add(AInode);
                        }

                        // Yield to avoid lag
                    }
                    yield return null;
                }
            }
            Debug.Log($"Total nodes created in flyableAreaMap: {flyableAreaMap.Count}");
            ConstructIslands(flyableAreaMap, cubeSize);


            if (flyableAreaIslands != null && flyableAreaIslands.Length > 1)
            {
                GameObject parentObject = GameObject.Find("FlyableGrid");
                int minIsland = 0;
                float minYNode = 999999f;
                for (int i = 0; i < flyableAreaIslands.Length; i++)
                {
                    if (flyableAreaIslands[i] == null)
                    {
                        continue;
                    }
                    else if (flyableAreaIslands[i].Length == 0)
                    {
                        if (parentObject != null)
                        {
                            Transform islandToDestroy = parentObject.transform.Find("FlyableAreaIsland" + (i + 1));
                            if (islandToDestroy != null)
                            {
                                Destroy(islandToDestroy.gameObject);
                            }
                        }
                        continue;
                    }

                    for (int j = 0; j < flyableAreaIslands[i].Length; j++)
                    {
                        if (flyableAreaIslands[i][j] == null) { continue; }
                            
                        if (flyableAreaIslands[i][j].transform.position.y < minYNode)
                        {
                            minYNode = flyableAreaIslands[i][j].transform.position.y;
                            minIsland = i;
                        }
                    }
                }
                if (flyableAreaIslands[minIsland].Length > 0)
                {
                    Destroy(flyableAreaIslands[minIsland][0].transform.parent.gameObject);
                    flyableAreaIslands[minIsland] = null;
                }

                if (parentObject != null)
                {
                    foreach (Transform child in parentObject.transform)
                    {
                        if (child.childCount == 0)
                        {
                            Destroy(child.gameObject);
                        }
                    }
                }
            }
        }

        void ConstructIslands(List<GameObject> flyableAreaMap, float cubeSize)
        {
            List<List<GameObject>> islands = new List<List<GameObject>>();
            HashSet<GameObject> visitedNodes = new HashSet<GameObject>();

            foreach (GameObject node in flyableAreaMap)
            {
                if (!visitedNodes.Contains(node))
                {
                    List<GameObject> newIsland = ExploreIsland(node, visitedNodes, flyableAreaMap, cubeSize);
                    islands.Add(newIsland);
                }
            }

            flyableAreaIslands = new GameObject[islands.Count][];
            for (int i = 0; i < islands.Count; i++)
            {
                flyableAreaIslands[i] = islands[i].ToArray();

                // Create a parent GameObject for each island
                GameObject islandParent = new GameObject($"FlyableAreaIsland{i + 1}");
                islandParent.transform.parent = topLevelMapContainer.transform;

                // Set each node in the island as a child of the parent GameObject
                foreach (GameObject node in islands[i])
                {
                    node.transform.parent = islandParent.transform;
                }
            }

            int largestIslandSize = 0;
            foreach (var island in islands)
            {
                if (island.Count > largestIslandSize)
                    largestIslandSize = island.Count;
            }

            for (int i = 0; i < islands.Count; i++)
            {
                if (islands[i].Count < 100 && islands[i].Count < largestIslandSize)
                {
                    foreach (GameObject node in islands[i])
                    {
                        Destroy(node);
                    }
                }
            }

            Debug.Log($"Total islands found: {islands.Count}");
            Debug.Log($"Largest island size: {largestIslandSize}");
            for (int i = 0; i < flyableAreaIslands.Length; i++)
            {
                Debug.Log("Island " + (i + 1) + " size: " + flyableAreaIslands[i].Length);
            }
        }

        List<GameObject> GetAdjacentNodes(GameObject node, HashSet<GameObject> visitedNodes, List<GameObject> flyableAreaMap, float cubeSize)
        {
            Vector3 pos = node.transform.position;
            List<GameObject> adjacentNodes = new List<GameObject>();

            foreach (GameObject otherNode in flyableAreaMap)
            {
                if (!visitedNodes.Contains(otherNode))
                {
                    Vector3 otherPos = otherNode.transform.position;
                    float dx = Mathf.Abs(otherPos.x - pos.x);
                    float dy = Mathf.Abs(otherPos.y - pos.y);
                    float dz = Mathf.Abs(otherPos.z - pos.z);

                    if ((Mathf.Abs(dx - cubeSize) < 0.01f && dy < 0.01f && dz < 0.01f) ||
                        (Mathf.Abs(dy - cubeSize) < 0.01f && dx < 0.01f && dz < 0.01f) ||
                        (Mathf.Abs(dz - cubeSize) < 0.01f && dx < 0.01f && dy < 0.01f))
                    {
                        adjacentNodes.Add(otherNode);
                    }
                }
            }
            return adjacentNodes;
        }

        List<GameObject> ExploreIsland(GameObject startNode, HashSet<GameObject> visitedNodes, List<GameObject> flyableAreaMap, float cubeSize)
        {
            List<GameObject> island = new List<GameObject>();
            Queue<GameObject> queue = new Queue<GameObject>();
            queue.Enqueue(startNode);
            visitedNodes.Add(startNode);

            while (queue.Count > 0)
            {
                GameObject node = queue.Dequeue();
                island.Add(node);

                foreach (GameObject neighbor in GetAdjacentNodes(node, visitedNodes, flyableAreaMap, cubeSize))
                {
                    if (!visitedNodes.Contains(neighbor))
                    {
                        visitedNodes.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            return island;
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
