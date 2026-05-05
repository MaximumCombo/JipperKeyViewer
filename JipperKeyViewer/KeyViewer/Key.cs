using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        public ConcurrentQueue<RawRain> rawRainQueue = new ConcurrentQueue<RawRain>();
        public bool isPressed; // 保留现有的按键状态

        private void Update()
        {
            while (rawRainQueue.TryDequeue(out RawRain rawRain))
            {
                Rain rainComponent = CreateRain(rawRain.transform);
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
            while (rawRainQueue.TryDequeue(out _)) { }
            foreach (RawRain rawRain in rainList)
            {
                rawRain.removed = true;
            }
            rainList.Clear();
        }

        private Rain CreateRain(Transform parent)
        {
            GameObject rainPrefab = new GameObject("Rain");
            RectTransform transform = rainPrefab.AddComponent<RectTransform>();
            transform.SetParent(parent);
            transform.anchorMin = transform.anchorMax = transform.pivot = new Vector2(0.5f, 1);
            transform.anchoredPosition = transform.sizeDelta = Vector2.zero;
            transform.localScale = Vector3.one;
            return rainPrefab.AddComponent<Rain>();
        }
    }
}
