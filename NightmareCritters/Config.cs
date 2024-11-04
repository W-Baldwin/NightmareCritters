using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace NightmareCritters
{
    public class NightmareConfig
    {

        internal enum RarityAddTypes { All, Modded, Vanilla, List };
        private static LethalLib.Modules.Levels.LevelTypes chosenRegistrationMethod = LethalLib.Modules.Levels.LevelTypes.All;

        private static int poeRarity = 100;

        public static void ConfigureAndRegisterAssets(AssetBundle assetBundle, ManualLogSource Logger)
        {
            ConfigureAndRegisterPoe(assetBundle, Logger);
        }

        internal static void ConfigureAndRegisterPoe(AssetBundle assetBundle, ManualLogSource Logger)
        {
            EnemyType poe = assetBundle.LoadAsset<EnemyType>("Nightmare Poe");

            PoeAI poeAIScript = poe.enemyPrefab.AddComponent<PoeAI>();
            poeAIScript.creatureVoice = poe.enemyPrefab.GetComponent<AudioSource>();
            poeAIScript.creatureSFX = poe.enemyPrefab.GetComponent<AudioSource>();
            poeAIScript.enemyBehaviourStates = new EnemyBehaviourState[7];
            poeAIScript.AIIntervalTime = 0.1f;
            poeAIScript.syncMovementSpeed = 0f;
            poeAIScript.updatePositionThreshold = 99999999f;
            poeAIScript.exitVentAnimationTime = 1;
            poeAIScript.enemyType = poe;
            poeAIScript.eye = poe.enemyPrefab.transform.Find("Eye");
            poeAIScript.agent = poe.enemyPrefab.GetComponent<NavMeshAgent>();
            poeAIScript.creatureAnimator = poe.enemyPrefab.transform.Find("PoeAnimationContainer").GetComponent<Animator>();
            poeAIScript.enemyHP = 2;

            PoeAnimationEvents poeEvents = poeAIScript.creatureAnimator.gameObject.AddComponent<PoeAnimationEvents>();
            poeEvents.scriptReference = poeAIScript;

            //Collision and transform
            poe.enemyPrefab.transform.Find("PoeCollision").GetComponent<EnemyAICollisionDetect>().mainScript = poeAIScript;
            poeAIScript.animationContainer = poe.enemyPrefab.transform.Find("PoeAnimationContainer").transform;
            poeAIScript.headBone = FindDeepChild(poe.enemyPrefab.transform, "Bone.008");

            poeAIScript.flapSounds = new AudioClip[4];
            poeAIScript.flapSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeFlap1");
            poeAIScript.flapSounds[1] = assetBundle.LoadAsset<AudioClip>("PoeFlap2");
            poeAIScript.flapSounds[2] = assetBundle.LoadAsset<AudioClip>("PoeFlap3");
            poeAIScript.flapSounds[3] = assetBundle.LoadAsset<AudioClip>("PoeFlap4");

            TerminalNode poeTerminalNode = assetBundle.LoadAsset<TerminalNode>("Nightmare Poe Terminal Node");
            TerminalKeyword poeTerminalKeyword = assetBundle.LoadAsset<TerminalKeyword>("Nightmare Poe Terminal Keyword");
            LethalLib.Modules.Utilities.FixMixerGroups(poe.enemyPrefab);
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(poe.enemyPrefab);
            LethalLib.Modules.Enemies.RegisterEnemy(poe, poeRarity, chosenRegistrationMethod, poeTerminalNode, poeTerminalKeyword);
            Logger.LogMessage("Registered poe with a rarity of: " + poeRarity + ".");
        }

        public static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;
                Transform result = FindDeepChild(child, childName);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
