using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Существо подаёт голос: скрип костей у нежити, ворчание у живых.
    ///
    /// Это не украшение. Молчащий противник существует только когда виден;
    /// подающий голос — существует всё время, и игрок слышит, что он тут, ещё
    /// до того, как выйдет из-за стены. Половина ощущения «мир живой» держится
    /// именно на звуках, которые издаёт то, чего сейчас не видно.
    ///
    /// Звучит только рядом с игроком: сорок скелетов, скрипящих по всей карте,
    /// превращаются в шум, а не в атмосферу.
    /// </summary>
    public sealed class AmbientVoice : MonoBehaviour
    {
        [Tooltip("Клипы голоса. Пусто — молчит.")]
        [SerializeField] private AudioClip[] clips;

        [Tooltip("Минимум и максимум секунд между звуками.")]
        [SerializeField] private float minDelay = 6f;
        [SerializeField] private float maxDelay = 16f;

        [Tooltip("Дальше этого расстояния молчит: незачем шуметь на всю карту.")]
        [SerializeField] private float hearingRange = 22f;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        private Health health;
        private Transform listener;
        private float nextTime;

        public void Setup(AudioClip[] voice) => clips = voice;

        private void Awake()
        {
            health = GetComponent<Health>();
            ScheduleNext();
        }

        private void Update()
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.time < nextTime) return;

            ScheduleNext();

            // Мёртвые молчат. Проверка тут, а не в подписке на смерть: так
            // компонент работает и на существах без здоровья вообще.
            if (health != null && !health.IsAlive) return;

            if (listener == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                listener = camera.transform;
            }

            if (Vector3.Distance(transform.position, listener.position) > hearingRange) return;

            Sfx.Play(clips, transform.position, volume);
        }

        private void ScheduleNext()
        {
            // Разброс обязателен: одинаковый интервал превращает голоса в
            // метроном, и это слышно уже на третьем повторе.
            nextTime = Time.time + Random.Range(minDelay, maxDelay);
        }
    }
}
