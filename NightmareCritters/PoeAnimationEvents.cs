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
    }
}
