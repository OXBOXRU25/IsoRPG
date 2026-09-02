using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Голос существа на урон и на смерть.
    ///
    /// В карте звуков Павлона (`Войс/sound-list.md`) на каждого зверя
    /// заложено пять состояний: угроза, атака, урон, смерть и холостой звук.
    /// Три из них уже были — угрозу подаёт <see cref="Combat.MonsterBrain"/>
    /// при захвате цели, холостой звук ведёт <see cref="AmbientVoice"/>, замах
    /// звучит из боевой системы. Урон и смерть не звучали ни у кого: зверь
    /// молча получал кинжалом и молча падал.
    ///
    /// Компонент общий и без пород внутри: какие клипы подставить, решает
    /// задание `voice-kit`. Пустой набор — молчание, и это законно: звук может
    /// быть ещё не сгенерирован, а игра обязана работать.
    /// </summary>
    public sealed class CreatureVoice : MonoBehaviour
    {
        [Tooltip("Крик боли. Играет не на каждый удар — см. паузу ниже.")]
        [SerializeField] private AudioClip[] hurt;

        [Tooltip("Предсмертный звук. Играет один раз.")]
        [SerializeField] private AudioClip[] death;

        [Tooltip("Пауза между криками боли. Без неё серия ударов даёт кашу из наложенных воплей.")]
        [SerializeField] private float hurtGap = 1.1f;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;

        private Health health;
        private float nextHurt;

        /// <summary>Настройка из задания сборки.</summary>
        public void Setup(AudioClip[] hurtSet, AudioClip[] deathSet)
        {
            hurt = hurtSet;
            death = deathSet;
        }

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable()
        {
            if (health == null) return;

            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health == null) return;

            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDamaged(int amount, GameObject source)
        {
            // Смертельный удар озвучивает смерть, а не боль: два голоса
            // одного зверя в один кадр слышны как заедание.
            if (health != null && !health.IsAlive) return;

            if (Time.time < nextHurt) return;
            nextHurt = Time.time + hurtGap;

            Sfx.Play(hurt, transform.position, volume, 0.09f);
        }

        private void OnDied(GameObject killer)
        {
            Sfx.Play(death, transform.position, volume, 0.05f);
        }
    }
}
