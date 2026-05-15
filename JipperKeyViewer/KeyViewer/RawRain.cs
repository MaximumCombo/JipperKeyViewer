using UnityEngine;

namespace JipperKeyViewer.KeyViewer
{
    public class RawRain
    {
        public Transform transform;
        public float elapsedMs;
        public byte color;
        public Vector2 FinalSize;
        public Vector2? sizeDelta;
        public Vector2? anchoredPosition;
        public bool removed;
        public Rain rainComponent;
        public float alpha = 1f;
        public float fadeElapsed = -1f;

        public bool UpdateLocation(bool updateSize, float speedFactor, float height, bool enableFade, float deltaMs)
        {
            elapsedMs += deltaMs;
            float y = elapsedMs * speedFactor;
            if (updateSize || FinalSize == default)
                FinalSize = new Vector2(color switch
                {
                    0 => 50,
                    3 => 30,
                    _ => 40
                }, y);
            if (y > height)
            {
                float sizeY = FinalSize.y - y + height;
                if (sizeY < 0) return false;
                if (!updateSize && enableFade)
                {
                    if (fadeElapsed < 0) fadeElapsed = 0f;
                    fadeElapsed += deltaMs;
                    float sizeAlpha = sizeY / height;
                    float timeAlpha = 1f - Mathf.Clamp01(fadeElapsed / 150f);
                    alpha = Mathf.Max(sizeAlpha, timeAlpha);
                }
                sizeDelta = new Vector2(FinalSize.x, sizeY);
                Vector2 pos = new Vector2(0, height);
                if (anchoredPosition != pos) anchoredPosition = pos;
            }
            else
            {
                fadeElapsed = -1f;
                if (updateSize) sizeDelta = FinalSize;
                anchoredPosition = new Vector2(0, y);
            }
            return true;
        }

        public RawRain(Transform transform, byte color)
        {
            this.transform = transform;
            this.color = color;
            alpha = 1f;
            fadeElapsed = -1f;
            elapsedMs = 0f;
        }
    }
}
