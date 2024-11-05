using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightmareCritters.Flyable
{
    [HarmonyPatch(typeof(RoundManager))]
    [HarmonyPatch("UnloadSceneObjectsEarly")]
    internal class EndOfRoundCleanup
    {
        [HarmonyPostfix]
        private static void UnloadSceneObjectsEarly()
        {
            GameObject topContainer = GameObject.Find("FlyableGrid");
            UnityEngine.Object.Destroy(topContainer);
            FlyableGrid.mapCreatedOrInProgress = false;
            FlyableGrid.created = false;
            FlyableGrid.flyableAreaIslands.Clear();
        }
    }
}
