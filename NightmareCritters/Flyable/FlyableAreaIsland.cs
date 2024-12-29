using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightmareCritters.Flyable
{
    internal class FlyableAreaIsland
    {
        public List<GameObject> flyNodes;
        public GameObject[][][] sortedFlyNodes; //Holds each node with lowest x, y, and z being 0, 0, 0
        private static readonly System.Random rng = new System.Random();
        public int xSize;
        public int ySize;
        public int zSize;
        public int cubeSize;

        //Min/Max
        public float minX;
        public float minY;
        public float minZ;
        public float maxX;
        public float maxY;
        public float maxZ;

        public FlyableAreaIsland(List<GameObject> islandNodes, int xSize, int ySize, int zSize, int cubeSize, float minX, float maxX, float minY, float maxY, float minZ, float maxZ) 
        {
            this.xSize = xSize;
            this.ySize= ySize;
            this.zSize = zSize;
            this.cubeSize = cubeSize;
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
            this.minZ = minZ;
            this.maxZ = maxZ;
            flyNodes = new List<GameObject>();
            sortedFlyNodes = new GameObject[xSize][][];
            for (int i = 0; i < xSize; i++)
            {
                sortedFlyNodes[i] = new GameObject[ySize][];
                for (int j = 0; j < ySize; j++)
                {
                    sortedFlyNodes[i][j] = new GameObject[zSize];
                }
            }
            flyNodes = islandNodes;
        }

        public void AddNode(GameObject node, int x, int y, int z)
        {
            sortedFlyNodes[x][y][z] = node;
            flyNodes.Add(node);
        }

        public GameObject GetAdjacentOffsetNode(GameObject node, int xOffset, int yOffset, int zOffset)
        {
            //My index.
            int xIndex = Mathf.Clamp(Mathf.FloorToInt((node.transform.position.x - minX) / cubeSize), 0, xSize - 1);
            int yIndex = Mathf.Clamp(Mathf.FloorToInt((node.transform.position.y - minY) / cubeSize), 0, ySize - 1);
            int zIndex = Mathf.Clamp(Mathf.FloorToInt((node.transform.position.z - minZ) / cubeSize), 0, zSize - 1);

            int targetX = xIndex + xOffset;
            int targetY = yIndex + yOffset;
            int targetZ = zIndex + zOffset;

            if (targetX < 0 || targetX >= sortedFlyNodes.Length) { return null; }
            if (targetY < 0 || targetY >= sortedFlyNodes[0].Length) { return null; }
            if (targetZ < 0 || targetZ >= sortedFlyNodes[0][0].Length) { return null; }

            return sortedFlyNodes[targetX][targetY][targetZ];
        }

        public GameObject GetAdjacentOffsetNode(int xIndex, int yIndex, int zIndex, int xOffset, int yOffset, int zOffset)
        {
            int targetX = xIndex + xOffset;
            int targetY = yIndex + yOffset;
            int targetZ = zIndex + zOffset;

            if (targetX < 0 || targetX >= sortedFlyNodes.Length) { return null; }
            if (targetY < 0 || targetY >= sortedFlyNodes[0].Length) { return null; }
            if (targetZ < 0 || targetZ >= sortedFlyNodes[0][0].Length) { return null; }

            return sortedFlyNodes[targetX][targetY][targetZ];
        }

        public GameObject[] GetNodeNeighbors(GameObject closestNode)
        {
            //My index.
            int xIndex = Mathf.Clamp(Mathf.FloorToInt((closestNode.transform.position.x - minX) / cubeSize), 0, xSize - 1);
            int yIndex = Mathf.Clamp(Mathf.FloorToInt((closestNode.transform.position.y - minY) / cubeSize), 0, ySize - 1);
            int zIndex = Mathf.Clamp(Mathf.FloorToInt((closestNode.transform.position.z - minZ) / cubeSize), 0, zSize - 1);

            List<GameObject> neighbors = new List<GameObject>();

            // Iterate through all neighboring indices within a 1-unit range in each direction
            for (int i = Mathf.Max(0, xIndex - 1); i <= Mathf.Min(xSize - 1, xIndex + 1); i++)
            {
                for (int j = Mathf.Max(0, yIndex - 1); j <= Mathf.Min(ySize - 1, yIndex + 1); j++)
                {
                    for (int k = Mathf.Max(0, zIndex - 1); k <= Mathf.Min(zSize - 1, zIndex + 1); k++)
                    {
                        // Skip the center node (the current node itself)
                        if (i == xIndex && j == yIndex && k == zIndex)
                        {
                            continue;
                        }

                        // Add the neighbor if it exists
                        GameObject neighborNode = sortedFlyNodes[i][j][k];
                        if (neighborNode != null)
                        {
                            neighbors.Add(neighborNode);
                        }
                    }
                }
            }
/*            Debug.Log($"Neighbors of {closestNode.name} found: {neighbors.Count}");*/
            return neighbors.ToArray();
        }

        //Gets the highest found node above the given transform position.
        public GameObject GetHighestNode(Transform target)
        {
            // Convert the target position to indices within sortedFlyNodes
            int xIndex = Mathf.Clamp(Mathf.FloorToInt((target.position.x - minX) / cubeSize), 0, xSize - 1);
            int yIndex = sortedFlyNodes[0].Length - 1;
            int zIndex = Mathf.Clamp(Mathf.FloorToInt((target.position.z - minZ) / cubeSize), 0, zSize - 1);

            GameObject highestNode = null;
            int attempts = 0;
            int xOffset = 0;
            int zOffset = 0;

            while (highestNode == null && attempts < 9)
            {
                for (int x = -xOffset; x <= xOffset; x++)
                {
                    for (int z = -zOffset; z <= zOffset; z++)
                    {
                        // Get node at this offset in the current y layer
                        highestNode = GetAdjacentOffsetNode(xIndex, yIndex, zIndex, x, 0, z);
                        if (highestNode != null)
                        {
                            return highestNode;
                        }
                    }
                }
                // Expand search range for next layer down
                xOffset++;
                zOffset++;
                yIndex--;  // Move down to next layer
                attempts++;
            }
            return highestNode;
        }


        public GameObject GetClosestNode(Transform target, bool known = true)
        {
            Vector3 targetPos = target.position;

            // Convert the target position to indices within sortedFlyNodes
            int xIndex = Mathf.Clamp(Mathf.FloorToInt((targetPos.x - minX) / cubeSize), 0, xSize - 1);
            int yIndex = Mathf.Clamp(Mathf.FloorToInt((targetPos.y - minY) / cubeSize), 0, ySize - 1);
            int zIndex = Mathf.Clamp(Mathf.FloorToInt((targetPos.z - minZ) / cubeSize), 0, zSize - 1);

            GameObject closestNode = null;
            float shortestDistance = Mathf.Infinity;

            // Check the node at (xIndex, yIndex, zIndex) and its immediate neighbors
            for (int i = Mathf.Max(0, xIndex - 1); i <= Mathf.Min(xSize - 1, xIndex + 1); i++)
            {
                for (int j = Mathf.Max(0, yIndex - 1); j <= Mathf.Min(ySize - 1, yIndex + 1); j++)
                {
                    for (int k = Mathf.Max(0, zIndex - 1); k <= Mathf.Min(zSize - 1, zIndex + 1); k++)
                    {
                        GameObject node = sortedFlyNodes[i][j][k];
                        if (node != null)
                        {
                            float distance = Vector3.Distance(targetPos, node.transform.position);
                            if (distance < shortestDistance)
                            {
                                shortestDistance = distance;
                                closestNode = node;
                            }
                        }
                    }
                }
            }

            return closestNode;
        }

        public List<GameObject> GetRandomPath(Transform startTransform, FlyableAreaIsland assignedIsland)
        {
            return FindShortestNodeChainDFS(startTransform, GetRandomIslandNode(assignedIsland).transform);
        }

        public GameObject GetRandomIslandNode(FlyableAreaIsland assignedIsland) 
        {
            if (assignedIsland == null || assignedIsland.flyNodes.Count == 0)
            {
                return null;
            }
            return assignedIsland.flyNodes[rng.Next(assignedIsland.flyNodes.Count)];
        }

        public List<GameObject> FindShortestNodeChainBFS(Transform startTransform, Transform endTransform)
        {
            GameObject startNode = startTransform.gameObject;
            GameObject endNode = endTransform.gameObject;
            /*Debug.Log($"Start Node: {startNode.name}, End Node: {endNode.name}");*/

            if (startNode == null || endNode == null)
            {
                Debug.Log("FindShortestNodeChain: Early return start or end is null.");
                return null;
            }

            if (startNode == endNode)
                return new List<GameObject> { startNode };

            var parents = new Dictionary<GameObject, GameObject>();
            var queue = new Queue<GameObject>();
            var visited = new HashSet<GameObject> { startNode };

            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                /*Debug.Log($"Processing node: {currentNode.name}");*/

                foreach (var neighbor in GetNodeNeighbors(currentNode))
                {
                    if (visited.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);
                    parents[neighbor] = currentNode;
                    queue.Enqueue(neighbor);

                    if (neighbor == endNode)
                    {
                        //path from start to end node, create reverse order List from finish to start, flip and return
                        var path = new List<GameObject>();
                        for (var node = endNode; node != null; node = parents.GetValueOrDefault(node))
                        {
                            path.Add(node);
                            /*Debug.Log($"Path node: {node.name}");*/
                        }
                        path.Reverse();
                        return path;
                    }
                }
            }
            Debug.Log("No path found from start to end node.");
            return null;
        }

        public List<GameObject> FindShortestNodeChainDFS(Transform startTransform, Transform endTransform)
        {
            GameObject startNode = startTransform.gameObject;
            GameObject endNode = endTransform.gameObject;
            /*Debug.Log($"Start Node: {startNode.name}, End Node: {endNode.name}");*/

            if (startNode == null || endNode == null)
            {
                Debug.Log("FindPathUsingDFS: Early return start or end is null.");
                return null;
            }

            if (startNode == endNode)
                return new List<GameObject> { startNode };

            var parents = new Dictionary<GameObject, GameObject>();
            var stack = new Stack<GameObject>();
            var visited = new HashSet<GameObject> { startNode };

            stack.Push(startNode);

            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();
                /*Debug.Log($"Processing node: {currentNode.name}");*/

                foreach (var neighbor in GetNodeNeighbors(currentNode))
                {
                    if (visited.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);
                    parents[neighbor] = currentNode;
                    stack.Push(neighbor);

                    if (neighbor == endNode)
                    {
                        // Path from start to end node, create reverse order List from finish to start, flip and return
                        var path = new List<GameObject>();
                        for (var node = endNode; node != null; node = parents.GetValueOrDefault(node))
                        {
                            path.Add(node);
                            /*Debug.Log($"Path node: {node.name}");*/
                        }
                        path.Reverse();
                        return path;
                    }
                }
            }

            Debug.Log("No path found from start to end node.");
            return null;
        }

        public GameObject GetLowestNode(Transform toThisPoint)
        {
            // Convert the target position to indices within sortedFlyNodes
            int xIndex = Mathf.Clamp(Mathf.FloorToInt((toThisPoint.position.x - minX) / cubeSize), 0, xSize - 1);
            int yIndex = Mathf.Clamp(Mathf.FloorToInt((toThisPoint.position.y - minY) / cubeSize), 0, ySize - 1);
            int zIndex = Mathf.Clamp(Mathf.FloorToInt((toThisPoint.position.z - minZ) / cubeSize), 0, zSize - 1);

            GameObject lowestNode = sortedFlyNodes[xIndex][yIndex][zIndex] ?? GetClosestNode(toThisPoint);
            if (lowestNode == null)
            {
                return null; // No valid nodes found.
            }

            int yOffset = 1;
            // Traverse downward along Y-axis until the lowest available node is found
            while (yIndex - yOffset >= 0 && sortedFlyNodes[xIndex][yIndex - yOffset][zIndex] != null)
            {
                lowestNode = sortedFlyNodes[xIndex][yIndex - yOffset][zIndex];
                yOffset++;
            }
            return lowestNode;
        }
    }
}
