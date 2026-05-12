using UnityEngine;

namespace JipperKeyViewer.KeyViewer
{
    public class RawRain
    {
        public Transform transform;
        public float localTime; // accumulated simulated time (ms)
        public byte color;
        public Vector2 FinalSize;
        public Vector2? sizeDelta;
        public Vector2? anchoredPosition;
        public bool removed;

        public bool UpdateLocation(float deltaMs, bool updateSize, float speedFactor, float height)
        {
            localTime += deltaMs;
            float y = localTime * speedFactor;
            if (updateSize || FinalSize == default) FinalSize = new Vector2(color switch
            {
                0 => 50,
                3 => 30,
                _ => 40
            }, localTime * speedFactor);
            if (y > height)
            {
                float sizeY = FinalSize.y - y + height;
                if (sizeY < 0) return false;
                sizeDelta = new Vector2(FinalSize.x, sizeY);
                anchoredPosition = new Vector2(0, height);
            }
            else
            {
                if (updateSize) sizeDelta = FinalSize;
                anchoredPosition = new Vector2(0, y);
            }
            return true;
        }

        public RawRain(Transform transform, byte color)
        {
            this.transform = transform;
            this.color = color;
            localTime = 0;
        }
    }
}
