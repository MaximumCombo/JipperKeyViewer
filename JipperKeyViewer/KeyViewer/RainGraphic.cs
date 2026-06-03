using UnityEngine;
using UnityEngine.UI;

namespace JipperKeyViewer.KeyViewer
{
    public class RainGraphic : MaskableGraphic
    {
        private float dNear;
        private float dFar;
        private float trackHeight;
        private float fadePx;
        private bool reverseFade;

        public void SetFadeParams(float dNear, float dFar, float trackHeight, float fadePx, bool reverse)
        {
            bool changed =
                this.dNear != dNear ||
                this.dFar != dFar ||
                this.trackHeight != trackHeight ||
                this.fadePx != fadePx ||
                this.reverseFade != reverse;
            this.dNear = dNear;
            this.dFar = dFar;
            this.trackHeight = trackHeight;
            this.fadePx = fadePx;
            this.reverseFade = reverse;
            if (changed) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;

            float xL = r.xMin;
            float xR = r.xMax;
            float yB = r.yMin;
            float yT = r.yMax;
            float h = r.height;

            Color baseCol = color;
            float fade = fadePx;
            float trackH = trackHeight;
            float span = dFar - dNear;

            if (fade <= 0.5f || trackH <= 0.5f || span <= 0.0001f)
            {
                AddQuad(vh, xL, yB, xR, yT, baseCol, baseCol);
                return;
            }

            float fadeStartD = trackH - fade;
            float aNear = AlphaAtD(dNear, fadeStartD, trackH, fade);
            float aFar = AlphaAtD(dFar, fadeStartD, trackH, fade);
            Color colNear = baseCol; colNear.a = baseCol.a * aNear;
            Color colFar = baseCol; colFar.a = baseCol.a * aFar;

            bool crosses = dNear < fadeStartD && dFar > fadeStartD;

            if (!crosses)
            {
                if (reverseFade)
                    AddQuad(vh, xL, yB, xR, yT, colFar, colNear);
                else
                    AddQuad(vh, xL, yB, xR, yT, colNear, colFar);
                return;
            }

            float t = (fadeStartD - dNear) / span;
            if (reverseFade)
            {
                float yMid = yT - t * h;
                AddQuad(vh, xL, yMid, xR, yT, baseCol, colNear);
                AddQuad(vh, xL, yB, xR, yMid, colFar, baseCol);
            }
            else
            {
                float yMid = yB + t * h;
                AddQuad(vh, xL, yB, xR, yMid, colNear, baseCol);
                AddQuad(vh, xL, yMid, xR, yT, baseCol, colFar);
            }
        }

        private static float AlphaAtD(float d, float fadeStartD, float trackH, float fade)
        {
            if (d <= fadeStartD) return 1f;
            if (d >= trackH) return 0f;
            return (trackH - d) / fade;
        }

        private static void AddQuad(VertexHelper vh, float xL, float yB, float xR, float yT, Color bot, Color top)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.position = new Vector3(xL, yB, 0f); v.color = bot; vh.AddVert(v);
            v.position = new Vector3(xR, yB, 0f); v.color = bot; vh.AddVert(v);
            v.position = new Vector3(xR, yT, 0f); v.color = top; vh.AddVert(v);
            v.position = new Vector3(xL, yT, 0f); v.color = top; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
