using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightmareCritters.Flyable
{
    internal class FlyableGrid : MonoBehaviour
    {
        public static GameObject topLevelMapContainer = null;
        public static List<FlyableAreaIsland> flyableAreaIslands;
        public static Vector3 flyableAreaMapOrigin;
        public static bool mapCreatedOrInProgress = false;
        public static float nodeSize;
        public static bool created = false;

        public IEnumerator ConstructFlyableAreaMap(GameObject flyableGridObject, GameObject[] allAINodes)
        {
            // Calculate average center and lowest Y for origin
            Vector3 averagePosition = Vector3.zero;
            float lowestY = Mathf.Infinity;
            float minX = Mathf.Infinity, maxX = -Mathf.Infinity;
            float minZ = Mathf.Infinity, maxZ = -Mathf.Infinity;

            const int aiNodesPerFrame = 10;
            int currentAINodeFrame = 0;
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
                currentAINodeFrame++;
                if (currentAINodeFrame >= aiNodesPerFrame)
                {
                    currentAINodeFrame = 0;
                    yield return null;
                }
            }

            //Dynamic node size.
            float totalGridSizeX = maxX - minX;
            float totalGridSizeZ = maxZ - minZ;
            float averageGridSize = (totalGridSizeX + totalGridSizeZ) / 2f;
            float cubeSize = Mathf.Clamp(10.0f + (int)(averageGridSize / 25), 5f, 25f);
            nodeSize = cubeSize;

            //Compute average position of all AI nodes on the map.
            averagePosition /= allAINodes.Length;
            flyableAreaMapOrigin = new Vector3(averagePosition.x, lowestY, averagePosition.z);
            GameObject flyableGridContainer = flyableGridObject;
            topLevelMapContainer = flyableGridContainer;
            GameObject node = new GameObject($"FlyGridWorldCenter{flyableAreaMapOrigin.x},{flyableAreaMapOrigin.y},{flyableAreaMapOrigin.z}");
            node.transform.position = flyableAreaMapOrigin;
            node.transform.parent = flyableGridContainer.transform;

            Debug.Log($"Average Position: {averagePosition}");
            Debug.Log($"Lowest Y: {lowestY}");
            Debug.Log($"Min X: {minX}, Max X: {maxX}");
            Debug.Log($"Min Z: {minZ}, Max Z: {maxZ}");
            Debug.Log($"Flyable Area Map Origin: {flyableAreaMapOrigin}");

            //Determine grid dimensions
            int gridSizeX = Mathf.CeilToInt((maxX - minX) / cubeSize) + 1;
            int gridSizeZ = Mathf.CeilToInt((maxZ - minZ) / cubeSize) + 1;
            int gridLayersY = 8;

            // Initialize a 1D list to store nodes
            List<GameObject> allFlyNodes = new List<GameObject>();

            // Populate the grid with nodes
            for (int yLayer = 0; yLayer < gridLayersY; yLayer++)
            {
                for (int xIndex = 0; xIndex < gridSizeX; xIndex++)
                {
                    for (int zIndex = 0; zIndex < gridSizeZ; zIndex++)
                    {
                        //Calculate the position for each node
                        Vector3 position = new Vector3(
                            minX + (xIndex * cubeSize),
                            lowestY + (yLayer * cubeSize),
                            minZ + (zIndex * cubeSize)
                        );

                        //Check for collisions
                        Collider[] colliders = Physics.OverlapBox(position, Vector3.one * (cubeSize / 2), Quaternion.identity);
                        bool hasCollision = false;

                        foreach (Collider collider in colliders)
                        {
                            if (!collider.isTrigger) //Ignore trigger colliders
                            {
                                hasCollision = true;
                                break;
                            }
                        }

                        //If no collision, create and add the node
                        if (!hasCollision)
                        {
                            GameObject AInode = new GameObject($"FlyGridNode{xIndex},{yLayer},{zIndex}");
                            AInode.transform.position = position;
                            AInode.transform.parent = flyableGridContainer.transform;
                            BoxCollider collider = AInode.AddComponent<BoxCollider>();
                            collider.size = Vector3.one * cubeSize;
                            collider.isTrigger = true;

                            // Add node to the list
                            allFlyNodes.Add(AInode);
                        }
                    }
                    yield return null;
                }
            }
            Debug.Log($"Total nodes created in flyableAreaMap: {allFlyNodes.Count}");
            yield return StartCoroutine(ConstructIslands(allFlyNodes, cubeSize));

            // Post map creation pruning and processing.
            if (flyableAreaIslands != null && flyableAreaIslands.Count > 1)
            {
                GameObject parentObject = GameObject.Find("FlyableGrid");

                // Step 1: Delete islands with fewer than 100 nodes
                foreach (Transform child in parentObject.transform)
                {
                    if (child.childCount < 100)
                    {
                        Destroy(child.gameObject);
                    }
                }

                foreach (Transform child in parentObject.transform)
                {
                    if (child.childCount < 100)
                    {
                        Destroy(child.gameObject);
                    }
                }

            }

            created = true;
        }

        private IEnumerator ConstructIslands(List<GameObject> allFlyNodes, float cubeSize)
        {
            flyableAreaIslands = new List<FlyableAreaIsland>();
            HashSet<GameObject> visitedNodes = new HashSet<GameObject>();

            foreach (GameObject node in allFlyNodes)
            {
                if (!visitedNodes.Contains(node))
                {
                    yield return StartCoroutine(ExploreIsland(node, visitedNodes, allFlyNodes, cubeSize));
                }
            }

            for (int i = 0; i < flyableAreaIslands.Count; i++)
            {
                // Create a parent GameObject for each island
                GameObject islandParent = new GameObject($"FlyableAreaIsland{i + 1}");
                islandParent.transform.parent = topLevelMapContainer.transform;

                // Set each node in the island as a child of the parent GameObject
                foreach (GameObject node in flyableAreaIslands[i].flyNodes)
                {
                    node.transform.parent = islandParent.transform;
                }
            }

            int largestIslandSize = 0;
            foreach (var island in flyableAreaIslands)
            {
                if (island.flyNodes.Count > largestIslandSize)
                    largestIslandSize = island.flyNodes.Count;
            }

            Debug.Log($"Total islands found: {flyableAreaIslands.Count}");
            Debug.Log($"Largest island size: {largestIslandSize}");
            for (int i = 0; i < flyableAreaIslands.Count; i++)
            {
                Debug.Log("Island " + (i + 1) + " size: " + flyableAreaIslands[i].flyNodes.Count);
            }
        }

        private static List<GameObject> GetAdjacentNodes(GameObject node, HashSet<GameObject> visitedNodes, List<GameObject> flyableAreaMap, float cubeSize)
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

        private static IEnumerator ExploreIsland(GameObject startNode, HashSet<GameObject> visitedNodes, List<GameObject> flyableAreaMap, float cubeSize)
        {
            List<GameObject> islandNodes = new List<GameObject>();
            Queue<GameObject> queue = new Queue<GameObject>();
            queue.Enqueue(startNode);
            visitedNodes.Add(startNode);
            int nodeProcessedFrame = 0;

            float minX = Mathf.Infinity, minY = Mathf.Infinity, minZ = Mathf.Infinity;
            float maxX = -Mathf.Infinity, maxY = -Mathf.Infinity, maxZ = -Mathf.Infinity;

            // Traverse all connected nodes to form the island
            while (queue.Count > 0)
            {
                GameObject node = queue.Dequeue();
                islandNodes.Add(node);

                // Update island bounds
                Vector3 pos = node.transform.position;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;

                foreach (GameObject neighbor in GetAdjacentNodes(node, visitedNodes, flyableAreaMap, cubeSize))
                {
                    if (!visitedNodes.Contains(neighbor))
                    {
                        visitedNodes.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                nodeProcessedFrame++;
                if (nodeProcessedFrame > 10)
                {
                    nodeProcessedFrame = 0;
                    yield return null; // Yield control to avoid frame lag
                }
            }

            if (islandNodes.Count < 200)
            {
                yield break;
            }

            // Calculate x, y, z sizes based on bounds and cubeSize
            int xSize = Mathf.CeilToInt((maxX - minX) / cubeSize) + 1;
            int ySize = Mathf.CeilToInt((maxY - minY) / cubeSize) + 1;
            int zSize = Mathf.CeilToInt((maxZ - minZ) / cubeSize) + 1;

            // Create FlyableAreaIsland
            FlyableAreaIsland island = new FlyableAreaIsland(islandNodes, xSize, ySize, zSize, (int)cubeSize, minX, maxX, minY, maxY, minZ, maxZ);

            // Add nodes to the island based on relative positions
            int currentNodeCalc = 0;
            List<GameObject> islandNodesCopy = new List<GameObject>(islandNodes); // Make a copy of islandNodes

            foreach (GameObject node in islandNodesCopy)
            {
                Vector3 pos = node.transform.position;
                int xIndex = Mathf.FloorToInt((pos.x - minX) / cubeSize);
                int yIndex = Mathf.FloorToInt((pos.y - minY) / cubeSize);
                int zIndex = Mathf.FloorToInt((pos.z - minZ) / cubeSize);

                island.AddNode(node, xIndex, yIndex, zIndex);
                currentNodeCalc++;
                if (currentNodeCalc > 10)
                {
                    currentNodeCalc = 0;
                    yield return null;
                }
            }

            flyableAreaIslands.Add(island);
        }

    }
}
