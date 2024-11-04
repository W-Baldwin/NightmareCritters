using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightmareCritters
{
    [HarmonyPatch(typeof(RoundManager))]
    [HarmonyPatch("UnloadSceneObjectsEarly")]
    internal class EndOfRoundCleanup
    {
        [HarmonyPostfix]
        private static void UnloadSceneObjectsEarly()
        {
            GameObject topContainer = GameObject.Find("FlyableGrid");
            GameObject.Destroy(topContainer);
            PoeAI.mapCreatedOrInProgress = false;
        }
    }
}
