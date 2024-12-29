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
            poeAIScript.eye = FindDeepChild(poe.enemyPrefab.transform, "Eye");
            poeAIScript.agent = poe.enemyPrefab.GetComponent<NavMeshAgent>();
            poeAIScript.creatureAnimator = poe.enemyPrefab.transform.Find("PoeAnimationContainer").GetComponent<Animator>();
            poeAIScript.enemyHP = 2;

            PoeAnimationEvents poeEvents = poeAIScript.creatureAnimator.gameObject.AddComponent<PoeAnimationEvents>();
            poeEvents.scriptReference = poeAIScript;

            //Collision and transform
            poeAIScript.animationContainer = poe.enemyPrefab.transform.Find("PoeAnimationContainer").transform;
            poeAIScript.animationContainer.Find("PoeCollision").GetComponent<EnemyAICollisionDetect>().mainScript = poeAIScript;
            poeAIScript.headBone = FindDeepChild(poe.enemyPrefab.transform, "Bone.008");

            poeAIScript.flapSounds = new AudioClip[4];
            poeAIScript.flapSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeFlap1");
            poeAIScript.flapSounds[1] = assetBundle.LoadAsset<AudioClip>("PoeFlap2");
            poeAIScript.flapSounds[2] = assetBundle.LoadAsset<AudioClip>("PoeFlap3");
            poeAIScript.flapSounds[3] = assetBundle.LoadAsset<AudioClip>("PoeFlap4");

            //Death
            poeAIScript.deathSounds = new AudioClip[1];
            poeAIScript.deathSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeDeath");

            //Regular Loud Squawks
            poeAIScript.squawkSounds = new AudioClip[2];
            poeAIScript.squawkSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeSquawk1");
            poeAIScript.squawkSounds[1] = assetBundle.LoadAsset<AudioClip>("PoeSquawk2");

            //Impact Sounds
            poeAIScript.impactSounds = new AudioClip[3];
            poeAIScript.impactSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeImpact1");
            poeAIScript.impactSounds[1] = assetBundle.LoadAsset<AudioClip>("PoeImpact2");
            poeAIScript.impactSounds[2] = assetBundle.LoadAsset<AudioClip>("PoeImpact3");

            //Patrol Warning Sounds
            poeAIScript.patrolWarningSounds = new AudioClip[2];
            poeAIScript.patrolWarningSounds[0] = assetBundle.LoadAsset<AudioClip>("PoePatrolWarning1");
            poeAIScript.patrolWarningSounds[1] = assetBundle.LoadAsset<AudioClip>("PoePatrolWarning2");

            //Attack Warning Sounds
            poeAIScript.attackWarningSounds = new AudioClip[1];
            poeAIScript.attackWarningSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeAttackWarning1");

            //Squawk fly sounds
            poeAIScript.squawkFlySounds = new AudioClip[2];
            poeAIScript.squawkFlySounds[0] = assetBundle.LoadAsset<AudioClip>("PoeSquawkFly1");
            poeAIScript.squawkFlySounds[1] = assetBundle.LoadAsset<AudioClip>("PoeSquawkFly2");

            //Squawk Ground Far Sounds
            poeAIScript.squawkGroundFarSounds = new AudioClip[3];
            poeAIScript.squawkGroundFarSounds[0] = assetBundle.LoadAsset<AudioClip>("PoeSquawkGoundFar1");
            poeAIScript.squawkGroundFarSounds[1] = assetBundle.LoadAsset<AudioClip>("PoeSquawkGoundFar2");
            poeAIScript.squawkGroundFarSounds[2] = assetBundle.LoadAsset<AudioClip>("PoeSquawkGoundFar3");

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
