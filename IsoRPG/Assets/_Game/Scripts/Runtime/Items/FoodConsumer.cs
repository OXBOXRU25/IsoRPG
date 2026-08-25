using UnityEngine;
using IsoRPG.Localization;
using IsoRPG.Combat;
using IsoRPG.Player;

namespace IsoRPG.Items
{
    /// <summary>
    /// Еда: лечит постепенно и прерывается, если сдвинуться или получить удар.
    ///
    /// Мгновенное лечение из сумки убило бы весь бой: с двадцатью яблоками в
    /// рюкзаке здоровье перестаёт быть ресурсом, а становится кнопкой. Долгая
    /// еда, которая рвётся от первого же удара, работает иначе — она годится
    /// ПОСЛЕ боя и бесполезна во время. Ровно этого мы и хотим: перерыв между
    /// схватками, а не бессмертие внутри них.
    ///
    /// Яблоко тратится в момент начала. Прерванная еда пропадает — как в играх
    /// жанра, и это честно: иначе игрок начинал бы есть на каждом шагу,
    /// ничем не рискуя.
    /// </summary>
    public sealed class FoodConsumer : MonoBehaviour
    {
        private static readonly Color FoodColor = new Color32(0x8A, 0xC8, 0x7A, 0xFF);

        /// <summary>
        /// Порог смещения, после которого еда считается прерванной.
        ///
        /// Не ноль: персонаж на месте всё равно чуть дрожит — навигационный
        /// агент подправляет позицию, толкаются соседи. Пара сантиметров
        /// отделяет дрожь от шага.
        /// </summary>
        private const float MoveTolerance = 0.12f;

        private Health health;
        private ClickToMoveController movement;
        private CharacterAnimatorDriver animation;

        /// <summary>
        /// Свой источник, а не общий пул звуков.
        ///
        /// Жевание длится всё время еды, и его надо уметь ОБОРВАТЬ: прерванная
        /// еда, после которой персонаж ещё десять секунд чавкает стоя, выглядит
        /// сломанной. Пул выстреливает и забывает — остановить оттуда нечего.
        /// </summary>
        private AudioSource voice;

        private ItemDefinition current;
        private Vector3 startPoint;
        private float endTime;
        private float healPerSecond;
        private float healDebt;

        /// <summary>Сколько восстановлено с прошлой показанной цифры.</summary>
        private int healedSinceTick;
        private float nextTick;

        /// <summary>Когда началась еда — для полосы хода.</summary>
        private float startTime;
        private float totalTime;

        public bool IsEating => current != null;

        /// <summary>Сколько осталось есть. Ноль — не ест.</summary>
        public float Remaining => IsEating ? Mathf.Max(0f, endTime - Time.time) : 0f;

        /// <summary>Доля съеденного, от нуля до единицы. Для полосы на экране.</summary>
        public float Progress
        {
            get
            {
                if (!IsEating || totalTime <= 0f) return 0f;
                return Mathf.Clamp01((Time.time - startTime) / totalTime);
            }
        }

        /// <summary>Что едим — название нужно подписи над полосой.</summary>
        public ItemDefinition Current => current;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<ClickToMoveController>();
            animation = GetComponent<CharacterAnimatorDriver>();

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;

            // Зациклен: звук короче самой еды, и без петли последние секунды
            // персонаж жуёт молча.
            voice.loop = true;

            // Без привязки к точке в мире: это действие игрока, и оно не
            // должно затихать, когда камера отъезжает.
            voice.spatialBlend = 0f;
            voice.volume = 0.45f;
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        /// <summary>
        /// Начать есть. False — сейчас нельзя, и предмет тратить не надо.
        /// </summary>
        public bool Begin(ItemDefinition food)
        {
            if (food == null || !food.IsFood) return false;
            if (health == null || !health.IsAlive) return false;

            if (health.Current >= health.Max)
            {
                CombatLog.Add("Здоровье и так полное.", LogKind.System);
                return false;
            }

            // Мгновенная еда: лечим и не заводим отсчёт.
            if (food.healDuration <= 0.05f)
            {
                health.Heal(food.healAmount);
                CombatLog.Add(Loc.F("{0}: +{1} здоровья", Loc.T(food.displayName), food.healAmount), LogKind.System);
                IsoRPG.Audio.Sfx.Pickup(transform.position);

                IsoRPG.Combat.DamagePopup.ShowHeal(
                    transform.position + Vector3.up * 2f, food.healAmount);

                return true;
            }

            current = food;
            startPoint = transform.position;
            if (animation != null) animation.SetEating(true);
            endTime = Time.time + food.healDuration;
            healPerSecond = food.healAmount / food.healDuration;
            healDebt = 0f;

            startTime = Time.time;
            totalTime = food.healDuration;

            healedSinceTick = 0;
            nextTick = Time.time + 1f;

            CombatLog.Add(Loc.F("Ест: {0}", Loc.T(food.displayName)), LogKind.System);
            StartVoice();

            return true;
        }

        public void Interrupt(string reason)
        {
            if (!IsEating) return;

            CombatLog.Add(Loc.F("Еда прервана: {0}", Loc.T(reason)), LogKind.System);
            Stop();
        }

        private void Update()
        {
            if (!IsEating) return;

            if (health == null || !health.IsAlive)
            {
                Stop();
                return;
            }

            // Сдвинулся — конец. Проверяем по пройденному расстоянию, а не по
            // флагу «идёт»: приказ идти может прийти и без движения, а вот
            // смещение от точки начала — это ровно то, что видит игрок.
            bool moved = Vector3.Distance(transform.position, startPoint) > MoveTolerance
                         || (movement != null && movement.IsMoving);

            if (moved)
            {
                Interrupt("встал с места");
                return;
            }

            // Копим дробное лечение и отдаём целыми единицами: здоровье
            // целое, и лечить по 0.7 в кадр нечем.
            healDebt += healPerSecond * Time.deltaTime;

            int whole = Mathf.FloorToInt(healDebt);
            if (whole > 0)
            {
                health.Heal(whole);
                healDebt -= whole;
                healedSinceTick += whole;
            }

            // Цифру показываем раз в секунду, а не на каждую единицу.
            //
            // Яблоко лечит по несколько единиц в секунду, и попап на каждую
            // превратился бы в мельтешение из единиц и двоек, за которым не
            // видно ни персонажа, ни того, сколько всего восстановлено.
            if (Time.time >= nextTick && healedSinceTick > 0)
            {
                IsoRPG.Combat.DamagePopup.ShowHeal(
                    transform.position + Vector3.up * 2f, healedSinceTick);

                healedSinceTick = 0;
                nextTick = Time.time + 1f;
            }

            if (health.Current >= health.Max)
            {
                CombatLog.Add("Наелся.", LogKind.System);
                Stop();
                return;
            }

            if (Time.time >= endTime) Stop();
        }

        /// <summary>Единственный выход из еды: и поза, и звук, и состояние.</summary>
        private void Stop()
        {
            current = null;

            if (animation != null) animation.SetEating(false);
            if (voice != null && voice.isPlaying) voice.Stop();
        }

        private void StartVoice()
        {
            if (voice == null) return;

            var clip = IsoRPG.Audio.SoundBank.Pick(IsoRPG.Audio.Sfx.Bank?.chewing);
            if (clip == null) return;

            voice.clip = clip;

            // Лёгкий разброс тона: одно и то же жевание при каждом яблоке
            // ухо опознаёт как запись, а не как еду.
            voice.pitch = Random.Range(0.92f, 1.06f);
            voice.Play();
        }

        private void OnDamaged(int amount, GameObject source) => Interrupt("получен удар");
    }
}
