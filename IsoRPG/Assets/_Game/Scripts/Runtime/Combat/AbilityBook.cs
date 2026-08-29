using System;
using System.Collections.Generic;
using IsoRPG.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using IsoRPG.Player;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Набор способностей персонажа и их применение.
    ///
    /// Сам ничего не знает про конкретные удары: берёт описание из данных,
    /// проверяет условия и исполняет. Добавить способность — положить ещё
    /// один ассет в список, кода это не касается.
    /// </summary>
    public sealed class AbilityBook : MonoBehaviour
    {
        [Tooltip("Способности по порядку. Первая на клавише 1, вторая на 2 и так далее.")]
        [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

        [Tooltip("Панель скрытности. Подменяет обычную, пока персонаж в тени.")]
        [SerializeField] private List<AbilityDefinition> stealthAbilities = new List<AbilityDefinition>();

        [Tooltip("Глобальный откат, если оружия нет. У игрока берётся от скорости оружия: следующий приём начинается, когда закончился замах предыдущего.")]
        [SerializeField] private float globalCooldown = 1.3f;

        private TargetSelector targets;
        private ResourcePool energy;
        private ComboPoints combo;
        private Targetable self;
        private CharacterAnimatorDriver animDriver;
        private WeaponStats weapon;
        private StealthState stealth;

        private readonly Dictionary<AbilityDefinition, float> readyTime = new Dictionary<AbilityDefinition, float>();
        private float globalReadyTime;

        private AbilityDefinition pendingAbility;
        private Targetable pendingVictim;
        private int pendingCombo;
        private float pendingImpactTime = -1f;

        /// <summary>
        /// Панель, действующая прямо сейчас.
        ///
        /// Скрытность — не просто состояние, а отдельный режим со своими
        /// приёмами: в тени доступно другое, чем в открытом бою. Поэтому
        /// подменяется вся панель целиком, а не добавляются кнопки сбоку.
        /// Так у игрока не может быть выбора, которого у него нет.
        /// </summary>
        public IReadOnlyList<AbilityDefinition> Abilities => ActiveBar;

        private List<AbilityDefinition> ActiveBar =>
            InStealth && stealthBar.Count > 0 ? stealthBar : abilities;

        private bool InStealth => stealth != null && stealth.IsStealthed;

        /// <summary>Панель сменилась — интерфейсу пора перерисоваться.</summary>
        public event Action BarChanged;
        /// <summary>
        /// Глобальный откат. Равен ритму оружия: следующий приём начинается
        /// тогда, когда закончился замах предыдущего — без пауз стояния.
        /// </summary>
        public float GlobalCooldown => weapon != null ? weapon.AttackInterval : globalCooldown;

        /// <summary>Способность применена — интерфейсу нужно подсветить кнопку и запустить откат.</summary>
        public event Action<AbilityDefinition> Used;

        /// <summary>Применить не вышло: способность и причина. Пригодится для подсказок игроку.</summary>
        public event Action<AbilityDefinition, string> Failed;

        /// <summary>
        /// Готовая панель скрытности: приёмы из тени плюс сама скрытность,
        /// чтобы из режима можно было выйти той же кнопкой, которой вошёл.
        /// </summary>
        private readonly List<AbilityDefinition> stealthBar = new List<AbilityDefinition>();

        private bool wasStealthed;

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            energy = GetComponent<ResourcePool>();
            combo = GetComponent<ComboPoints>();
            self = GetComponent<Targetable>();
            animDriver = GetComponent<CharacterAnimatorDriver>();
            weapon = GetComponent<WeaponStats>();
            stealth = GetComponent<StealthState>();

            // Панель скрытности собираем заново при каждом запуске: в файле
            // сцены лежит только список приёмов, а не готовая панель.
            BuildStealthBar();
        }

        private void Update()
        {
            ResolvePendingImpact();
            WatchStealth();
            ReadHotkeys();
        }

        /// <summary>
        /// Замечает вход и выход из скрытности и объявляет смену панели.
        ///
        /// Опрос вместо подписки на событие сознательно: скрытность
        /// сбрасывается из нескольких мест — приёмом, уроном, движением, —
        /// и каждое из них пришлось бы помнить о панели. Одна проверка в
        /// кадре стоит дешевле, чем правило, которое легко забыть.
        /// </summary>
        private void WatchStealth()
        {
            bool now = InStealth;
            if (now == wasStealthed) return;

            wasStealthed = now;
            BarChanged?.Invoke();
        }

        private void ReadHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Клавиша берётся из самой способности, а не из её места в списке.
            // Пока панель была одна, разницы не было; с подменяемой панелью
            // порядок перестал совпадать с раскладкой, и привязка по индексу
            // начала бы вызывать не то, что нарисовано на кнопке.
            var bar = ActiveBar;

            for (int i = 0; i < bar.Count; i++)
            {
                var ability = bar[i];
                if (ability == null) continue;

                var key = KeyFor(ability.hotkeyLabel);
                if (key == Key.None) continue;

                if (keyboard[key].wasPressedThisFrame) TryUse(ability);
            }
        }

        private static Key KeyFor(string label)
        {
            switch (label)
            {
                case "1": return Key.Digit1;
                case "2": return Key.Digit2;
                case "3": return Key.Digit3;
                case "4": return Key.Digit4;
                case "5": return Key.Digit5;
                case "0": return Key.Digit0;
                default:  return Key.None;
            }
        }

        /// <summary>Готова ли способность прямо сейчас — нужно интерфейсу для затемнения кнопок.</summary>
        public bool IsReady(AbilityDefinition ability)
        {
            if (ability == null) return false;
            if (Time.time < globalReadyTime) return false;
            if (readyTime.TryGetValue(ability, out float ready) && Time.time < ready) return false;
            return energy == null || energy.Has(ability.energyCost);
        }

        /// <summary>Сколько секунд осталось до готовности. Ноль — готова.</summary>
        public float CooldownLeft(AbilityDefinition ability)
        {
            if (ability == null) return 0f;

            float left = 0f;
            if (readyTime.TryGetValue(ability, out float ready)) left = ready - Time.time;

            float global = globalReadyTime - Time.time;
            return Mathf.Max(0f, Mathf.Max(left, global));
        }

        public bool TryUse(AbilityDefinition ability)
        {
            if (ability == null) return false;

            if (Time.time < globalReadyTime) return Fail(ability, "ещё не готов");

            if (readyTime.TryGetValue(ability, out float ready) && Time.time < ready)
                return Fail(ability, "откат");

            // Скрытность — способность без цели, поэтому её проверяем первой
            // и отдельно: обычные условия про дистанцию к ней не относятся.
            if (ability.togglesStealth) return ToggleStealth(ability);

            var target = targets != null ? targets.Current : null;

            if (ability.requiresTarget)
            {
                if (target == null || !target.IsAlive || !target.IsHostileTo(targets.OwnFaction))
                    return Fail(ability, "нет цели");

                float distance = Vector3.Distance(transform.position, target.transform.position);
                float allowed = ability.reach
                                + (self != null ? self.BodyRadius : 0.5f)
                                + target.BodyRadius;

                if (distance > allowed + 0.35f) return Fail(ability, "слишком далеко");

                if (ability.requiresBehindTarget && !IsBehind(target, ability.behindAngle))
                    return Fail(ability, "нужно зайти со спины");
            }

            if (ability.requiresStealth && (stealth == null || !stealth.IsStealthed))
                return Fail(ability, "только из скрытности");

            // Финишер без очков применять бессмысленно — он потратит откат
            // и энергию впустую. Лучше сказать честно.
            int comboAvailable = combo != null ? combo.PointsOn(target) : 0;
            if (ability.comboRole == ComboRole.Finisher && comboAvailable <= 0)
                return Fail(ability, "нет комбо-очков");

            if (energy != null && !energy.Spend(ability.energyCost))
                return Fail(ability, "не хватает энергии");

            // --- Способность пошла ---
            globalReadyTime = Time.time + GlobalCooldown;
            if (ability.cooldown > 0f) readyTime[ability] = Time.time + ability.cooldown;

            int comboSpent = 0;
            if (combo != null)
            {
                if (ability.comboRole == ComboRole.Generator) combo.Add(target, ability.comboGain);
                else if (ability.comboRole == ComboRole.Finisher) comboSpent = combo.Consume(target);
            }

            // Анимация приёма занимает столько же, сколько глобальный откат:
            // тогда замах ровно заполняет паузу до следующей возможности ударить.
            if (animDriver != null) animDriver.SetActionDuration(GlobalCooldown * 0.9f);

            PlayAnimation(ability);

            pendingAbility = ability;
            pendingVictim = target;
            pendingCombo = comboSpent;
            pendingImpactTime = Time.time + ability.impactDelay;

            // Ударил — вышел из тени. Сама скрытность сюда не доходит,
            // она обрабатывается выше отдельной веткой.
            if (ability.breaksStealth && stealth != null) stealth.Exit();

            Used?.Invoke(ability);
            return true;
        }

        private bool Fail(AbilityDefinition ability, string reason)
        {
            Failed?.Invoke(ability, reason);
            return false;
        }

        /// <summary>
        /// Включить или выключить скрытность.
        ///
        /// Откат ведёт сама скрытность, а не книга способностей: он должен
        /// сохраняться, даже если персонаж вышел из тени сам, получив урон.
        /// </summary>
        private bool ToggleStealth(AbilityDefinition ability)
        {
            if (stealth == null) return Fail(ability, "скрытность недоступна");

            if (stealth.IsStealthed)
            {
                stealth.Exit();
                Used?.Invoke(ability);
                return true;
            }

            string refusal = stealth.TryEnter();
            if (!string.IsNullOrEmpty(refusal)) return Fail(ability, refusal);

            globalReadyTime = Time.time + GlobalCooldown;
            Used?.Invoke(ability);
            return true;
        }

        private bool IsBehind(Targetable target, float angle)
        {
            Vector3 toAttacker = transform.position - target.transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return false;

            // Угол между взглядом цели и направлением на нас. Больше половины
            // заданного сектора — значит мы у неё за спиной.
            float between = Vector3.Angle(-target.transform.forward, toAttacker.normalized);
            return between <= angle * 0.5f;
        }

        private void PlayAnimation(AbilityDefinition ability)
        {
            if (animDriver == null) return;

            if (string.IsNullOrEmpty(ability.animationTrigger) || ability.animationTrigger == "Attack")
                animDriver.PlayAttack();
            else if (ability.animationTrigger == "StealthKill")
                animDriver.PlayStealthKill();
            else
                animDriver.PlayAttack();
        }

        /// <summary>
        /// Прибавка от талантов. Книга ищется лениво и один раз: у монстров
        /// её нет вовсе, и дёргать GetComponent на каждый удар ради
        /// гарантированного нуля незачем.
        /// </summary>
        private float TalentBonus(IsoRPG.Progression.TalentEffect effect)
        {
            if (!talentsChecked)
            {
                talents = GetComponent<IsoRPG.Progression.TalentBook>();
                talentsChecked = true;
            }

            return talents != null ? talents.Bonus(effect) : 0f;
        }

        private IsoRPG.Progression.TalentBook talents;
        private bool talentsChecked;
        private void ResolvePendingImpact()
        {
            if (pendingImpactTime < 0f || Time.time < pendingImpactTime) return;

            pendingImpactTime = -1f;

            if (pendingAbility == null || pendingVictim == null || !pendingVictim.IsAlive) return;

            if (pendingAbility.dealsDamage && pendingVictim.Health != null)
            {
                int weaponDamage = weapon != null ? weapon.WeaponDamage : 10;

                // Урон приёма собирает три прибавки: общую, для приёмов и
                // отдельную для тех, что бьют из скрытности. Последняя и есть
                // смысл ветки скрытности: она платит за подход, а не за бой.
                float multiplier = 1f
                    + TalentBonus(IsoRPG.Progression.TalentEffect.Damage)
                    + TalentBonus(IsoRPG.Progression.TalentEffect.AbilityDamage);

                if (pendingAbility.requiresStealth)
                    multiplier += TalentBonus(IsoRPG.Progression.TalentEffect.StealthDamage);

                int damage = pendingAbility.RollDamage(
                    weaponDamage, pendingCombo,
                    TalentBonus(IsoRPG.Progression.TalentEffect.CritChance),
                    TalentBonus(IsoRPG.Progression.TalentEffect.CritMultiplier),
                    multiplier, out HitResult result);

                // Показываем то, что дошло после брони, а не то, чем замахивались.
                int actual = pendingVictim.Health.TakeDamage(damage, gameObject);
                DamagePopup.Show(pendingVictim.OverheadPoint, actual, result);

                // В логе пишем название приёма — иначе не отличить, чем
                // именно ударили, а это половина смысла лога.
                CombatLog.Add(
                    Loc.F("{0} по {1}: {2}{3}",
                          Loc.T(pendingAbility.displayName),
                          Loc.T(pendingVictim.DisplayName), actual,
                          result == HitResult.Crit ? " " + Loc.T("(крит)")
                        : result == HitResult.Miss ? " " + Loc.T("(вскользь)") : ""),
                    result == HitResult.Crit ? LogKind.Crit : LogKind.DamageDealt);
            }

            // Оглушение накладываем после урона: если удар добил цель,
            // оглушать уже некого, и StunReceiver на трупе только мешал бы
            // обработчику смерти.
            float stun = pendingAbility.ComputeStun(pendingCombo);
            if (stun > 0f && pendingVictim.IsAlive)
            {
                var receiver = pendingVictim.GetComponent<StunReceiver>();
                if (receiver != null)
                {
                    receiver.Apply(stun);
                    CombatLog.Stunned(pendingVictim.DisplayName, stun);
                }
            }

            pendingAbility = null;
            pendingVictim = null;
            pendingCombo = 0;
        }

        /// <summary>Задать набор способностей из кода. Нужно сборщику сцены.</summary>
        public void Setup(IEnumerable<AbilityDefinition> list)
        {
            Setup(list, null);
        }

        /// <summary>
        /// Задаёт обе панели: обычную и ту, что действует в скрытности.
        /// </summary>
        public void Setup(IEnumerable<AbilityDefinition> list,
                          IEnumerable<AbilityDefinition> stealthList)
        {
            abilities.Clear();
            abilities.AddRange(list);

            stealthAbilities.Clear();
            if (stealthList != null) stealthAbilities.AddRange(stealthList);

            BuildStealthBar();
        }

        /// <summary>
        /// Собирает готовую панель скрытности из сохранённого списка.
        ///
        /// Вызывается и при сборке сцены, и при запуске игры — потому что
        /// между ними список проходит через файл сцены, а туда попадают
        /// только сериализуемые поля. Готовая панель к ним не относится: она
        /// вычисляется из списка, а не хранится.
        /// </summary>
        private void BuildStealthBar()
        {
            stealthBar.Clear();
            stealthBar.AddRange(stealthAbilities);

            // Саму скрытность дописываем в конец: выходить из режима надо той
            // же кнопкой, которой вошёл, иначе игрок оказывается запертым в
            // тени до первого удара.
            if (stealthBar.Count == 0) return;

            foreach (var ability in abilities)
            {
                if (ability != null && ability.togglesStealth && !stealthBar.Contains(ability))
                {
                    stealthBar.Add(ability);
                    break;
                }
            }
        }
    }
}
