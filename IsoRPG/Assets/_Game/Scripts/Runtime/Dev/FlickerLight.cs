using UnityEngine;

namespace IsoRPG.Dev
{
    /// <summary>
    /// Живой огонь: лёгкое мерцание источника света.
    ///
    /// Ровно горящий факел читается как лампа под потолком — и это одна из
    /// причин, по которым подземелье выглядит музеем, а не местом, где
    /// кто-то живёт. Настоящее пламя дышит, и глазу достаточно очень
    /// немногого: пятнадцати процентов по яркости и лёгкого гуляния радиуса.
    ///
    /// Фаза у каждого факела своя. Без этого весь зал мигает в такт, и
    /// получается не огонь, а неисправная проводка — ошибка заметная сразу и
    /// именно та, из-за которой мерцание часто отвергают как приём.
    ///
    /// Шум, а не синус: чистая синусоида даёт ритм, который считывается за
    /// пару секунд. Перлин звучит случайно, оставаясь плавным.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [Tooltip("Насколько сильно гуляет яркость, доля от исходной.")]
        [Range(0f, 0.5f)]
        public float Amount = 0.15f;

        [Tooltip("Скорость мерцания. Больше — суетливее.")]
        public float Speed = 1.6f;

        private Light target;
        private float baseIntensity;
        private float baseRange;
        private float phase;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            baseRange = target.range;

            // Своя точка на шуме у каждого факела — иначе зал мигает в такт.
            phase = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (target == null) return;

            float noise = Mathf.PerlinNoise(phase + Time.time * Speed, 0f);

            // Шум даёт 0…1 со средним около 0.5 — приводим к отклонению
            // вокруг единицы, иначе факел в среднем тусклее задуманного.
            float factor = 1f + (noise - 0.5f) * 2f * Amount;

            target.intensity = baseIntensity * factor;
            target.range = baseRange * (1f + (noise - 0.5f) * Amount);
        }
    }
}
