using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aircraft
{
    public class AircraftEnemyIndicator : MonoBehaviour
    {
        [Tooltip("Circle")] public Image circle;
        [Tooltip("Arrow")] public Image arrow;
        
        public void ToggleArrowActive(bool active)
        {
            arrow.gameObject.SetActive(active);
        }
    }
    
}