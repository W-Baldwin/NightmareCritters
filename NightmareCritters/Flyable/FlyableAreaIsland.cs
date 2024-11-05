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
    }
}
