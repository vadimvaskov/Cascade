using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cascade.UI
{
    /// <summary>Простые анимации без корутин: функция получает прогресс от 0 до 1.</summary>
    public sealed class Anim : MonoBehaviour
    {
        private sealed class Tween
        {
            public UnityEngine.Object Owner;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public bool Unscaled;
            public Action<float> Step;
            public Action Done;
        }

        private static Anim instance;
        private readonly List<Tween> tweens = new List<Tween>(64);
        private readonly List<Tween> incoming = new List<Tween>(16);

        private static Anim Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("CascadeAnim");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Anim>();
                }
                return instance;
            }
        }

        /// <summary>Запуск анимации. Если владелец уничтожен, анимация тихо прекращается.</summary>
        public static void Run(UnityEngine.Object owner, float delay, float duration, Action<float> step, Action done, bool unscaled)
        {
            Tween tween = new Tween();
            tween.Owner = owner;
            tween.Delay = delay;
            tween.Duration = duration;
            tween.Step = step;
            tween.Done = done;
            tween.Unscaled = unscaled;
            Instance.incoming.Add(tween);
        }

        public static void Run(UnityEngine.Object owner, float duration, Action<float> step)
        {
            Run(owner, 0f, duration, step, null, false);
        }

        public static void Delay(UnityEngine.Object owner, float delay, Action done)
        {
            Run(owner, delay, 0f, null, done, false);
        }

        public static void DelayUnscaled(UnityEngine.Object owner, float delay, Action done)
        {
            Run(owner, delay, 0f, null, done, true);
        }

        public static float EaseOut(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private void Update()
        {
            if (incoming.Count > 0)
            {
                tweens.AddRange(incoming);
                incoming.Clear();
            }
            float scaled = Time.deltaTime;
            float unscaled = Time.unscaledDeltaTime;
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                Tween tween = tweens[i];
                if (tween.Owner == null)
                {
                    tweens.RemoveAt(i);
                    continue;
                }
                float dt = tween.Unscaled ? unscaled : scaled;
                if (tween.Delay > 0f)
                {
                    tween.Delay -= dt;
                    if (tween.Delay > 0f)
                    {
                        continue;
                    }
                    dt = -tween.Delay;
                }
                tween.Elapsed += dt;
                float t = tween.Duration <= 0f ? 1f : Mathf.Clamp01(tween.Elapsed / tween.Duration);
                try
                {
                    if (tween.Step != null)
                    {
                        tween.Step(t);
                    }
                    if (t >= 1f)
                    {
                        tweens.RemoveAt(i);
                        if (tween.Done != null)
                        {
                            tween.Done();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    if (i < tweens.Count && tweens[i] == tween)
                    {
                        tweens.RemoveAt(i);
                    }
                }
            }
        }
    }
}
