using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NightmareCritters
{
    internal class PoeAnimationEvents : MonoBehaviour
    {
        public PoeAI scriptReference;

        public void PlayRandomFlapSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomFlapSound();
            }
        }

        public void PlayRandomDeathSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomDeathSound();
            }
        }

        public void PlayRandomSquawkSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomSquawkSound();
            }
        }

        public void PlayRandomImpactSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomImpactSound();
            }
        }

        public void PlayRandomPatrolWarningSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomPatrolWarningSound();
            }
        }

        public void PlayRandomAttackWarningSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomAttackWarningSound();
            }
        }

        public void PlayRandomSquawkGroundFarSound()
        {
            if (scriptReference != null)
            {
                scriptReference.PlayRandomSquawkGroundFarSound();
            }
        }
    }
}
