using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Дрожание пламени.
    ///
    /// Ровно горящий факел читается как лампочка: глаз ищет движение и, не
    /// находя, перестаёт считать источник огнём. Достаточно мизерных
    /// колебаний яркости — эффект стоит копейки, а замечается сразу.
    ///
    /// Колебания идут по шуму, а не по случайным числам каждый кадр: случайное
    /// дёрганье выглядит помехой в проводке, а плавное — живым огнём.
    /// </summary>
    public sealed class FlickeringLight : MonoBehaviour
    {
        [Tooltip("Насколько сильно гуляет яркость, доля от исходной.")]
        [SerializeField] private float amplitude = 0.18f;

        [Tooltip("Скорость дрожания.")]
        [SerializeField] private float speed = 3.4f;

        private Light source;
        private float baseIntensity;
        private float offset;

        private void Awake()
        {
            source = GetComponent<Light>();
            if (source != null) baseIntensity = source.intensity;

            // Своя фаза у каждого огня: одинаково мигающий ряд свечей выдаёт
            // себя мгновенно — так не горит ничего живого.
            offset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (source == null) return;

            float noise = Mathf.PerlinNoise(offset + Time.time * speed, 0f);

            // Шум даёт 0..1, приводим к колебанию вокруг единицы.
            source.intensity = baseIntensity * (1f + (noise - 0.5f) * 2f * amplitude);
        }
    }
}
