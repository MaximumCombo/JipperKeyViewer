using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JipperKeyViewer.KeyViewer
{
    public class Key : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public Image background;
        public Image outline;
        public TextMeshProUGUI value;
        public GameObject rain;
        public byte color;
        public List<RawRain> rainList = new List<RawRain>();
        public Queue<RawRain> rawRainQueue = new Queue<RawRain>();
        public bool isPressed;

        public void ProcessRainQueue()
        {
            while (rawRainQueue.Count > 0)
            {
                RawRain rawRain = rawRainQueue.Dequeue();
                Rain rainComponent = KeyViewer.instance.GetRainFromPool(rain.transform);
                rainComponent.rawRain = rawRain;
                rainComponent.image.color = color switch
                {
                    0 => KeyViewer.Settings.RainColor,
                    3 => KeyViewer.Settings.RainColor3,
                    _ => KeyViewer.Settings.RainColor2
                };
                rainComponent.transform.SetSiblingIndex(color - 1);
            }
        }

        private void OnDestroy()
        {
            while (rawRainQueue.Count > 0)
                rawRainQueue.Dequeue();
            foreach (RawRain rawRain in rainList)
            {
                rawRain.removed = true;
            }
            rainList.Clear();
        }
    }
}
