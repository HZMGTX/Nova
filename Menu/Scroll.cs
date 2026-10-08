using UnityEngine;

namespace Nova.Menu
{
    internal sealed class Scroll
    {
        public float value;
        public float target;
        public float max;
        public float grab;
        public bool bottom;

        public void Reset()
        {
            value = 0;
            target = 0;
            grab = 0;
            bottom = false;
        }

        public void SetBounds(float height)
        {
            max = Mathf.Max(0, height);
            if (bottom) { target = max; bottom = false; }
            target = Mathf.Clamp(target, 0, max);
            value = Mathf.Clamp(value, 0, max);
        }

        public void Step(float delta, bool smooth, float speed)
        {
            target = Mathf.Clamp(target, 0, max);
            value = smooth ? Mathf.Lerp(value, target, 1 - Mathf.Exp(-Mathf.Max(0, delta) * speed)) : target;
            if (Mathf.Abs(target - value) < 0.05f) value = target;
        }
    }
}
