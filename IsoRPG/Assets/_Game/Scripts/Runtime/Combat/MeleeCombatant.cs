using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Player;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Ближний бой: подойти к выбранной цели и бить её автоатакой.
    ///
    /// Работает по вовской схеме: цель выбрана — персонаж сам держит
    /// дистанцию и бьёт по кулдауну, не требуя клика на каждый удар.
    /// Игрок в это время думает о способностях, а не о кликанье.
    /// </summary>
    [RequireComponent(typeof(TargetSelector))]
    public sealed class MeleeCombatant : MonoBehaviour, ICombatant
    {
        [Header("Удар")]
        [Tooltip("Дальность удара сверх радиусов тел. Кинжал — короткая.")]
        [SerializeField] private float reach = 0.9f;

        [Tooltip("Секунд между ударами, если оружия нет. У игрока берётся из WeaponStats — там это характеристика предмета.")]
        [SerializeField] private float attackInterval = 2f;

        [Tooltip("Задержка урона от начала замаха — момент касания в анимации. Без неё урон срабатывает раньше, чем клинок дошёл, и это видно.")]
        [SerializeField] private float impactDelay = 0.5f;

        [Tooltip("Урон, если оружия нет. У игрока урон берётся из WeaponStats, у монстров пока отсюда.")]
        [SerializeField] private int fallbackDamage = 10;

        [Header("Исход удара")]
        [Range(0f, 1f)]
        /// <summary>Доля урона за единицу силы.</summary>
        private const float DamagePerStrength = 0.01f;

        /// <summary>Доля крита за единицу ловкости.</summary>
        private const float CritPerAgility = 0.003f;

        [SerializeField] private float critChance = CombatMath.DefaultCritChance;
        [SerializeField] private float critMultiplier = CombatMath.DefaultCritMultiplier;

        [Tooltip("Шанс частично отражённого удара — урон вдвое меньше.")]
        [Range(0f, 1f)]
        [SerializeField] private float missChance = CombatMath.DefaultMissChance;
        [SerializeField] private float missMultiplier = CombatMath.DefaultMissMultiplier;

        [Header("Преследование")]
        [Tooltip("Догонять ли цель, если она отошла.")]
        [SerializeField] private bool chaseTarget = true;

        [Tooltip("Насколько цель должна отойти, чтобы мы тронулись за ней. Без запаса персонаж дёргается на месте.")]
        [SerializeField] private float chaseTolerance = 0.35f;

        [Header("Разворот")]
        [SerializeField] private float turnSpeed = 720f;

        private TargetSelector targets;
        private NavMeshAgent agent;
        private ClickToMoveController movement;
        private CharacterAnimatorDriver animDriver;
        private Targetable self;
        private WeaponStats weapon;

        private float nextAttackTime;
        private float pendingImpactTime = -1f;
        private Targetable pendingVictim;

        // Игрок приказал идти. Пока приказ в силе, за целью не бежим:
        // ручное управление всегда главнее автоматики, иначе персонаж
        // возвращается к врагу, хотя его явно уводят.
        private bool manualMoveOverride;

        /// <summary>Отменить преследование. Зовётся, когда игрок кликнул по земле.</summary>
        public void CancelChase()
        {
            manualMoveOverride = true;
        }

        /// <summary>Взяться за цель. Зовётся при клике по врагу — снимает запрет на погоню.</summary>
        public void EngageTarget()
        {
            manualMoveOverride = false;
        }

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            agent = GetComponent<NavMeshAgent>();
            movement = GetComponent<ClickToMoveController>();
            animDriver = GetComponent<CharacterAnimatorDriver>();
            self = GetComponent<Targetable>();
            weapon = GetComponent<WeaponStats>();
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.Changed += ApplyWeaponRhythm;
            ApplyWeaponRhythm();
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.Changed -= ApplyWeaponRhythm;
        }

        /// <summary>Ритм боя задаёт оружие: и частоту ударов, и длительность анимации.</summary>
        private void ApplyWeaponRhythm()
        {
            float interval = CurrentInterval;

            // Анимацию поджимаем чуть сильнее интервала: замах должен
            // закончиться до следующего, а не впритык к нему.
            if (animDriver != null) animDriver.SetActionDuration(interval * 0.9f);
        }

        /// <summary>
        /// Пауза между ударами. Скорость от талантов делит интервал, а не
        /// вычитается из него: +10% скорости должны значить одно и то же
        /// и для кинжала, и для двуручного топора.
        /// </summary>
        private float CurrentInterval
        {
            get
            {
                float baseInterval = weapon != null ? weapon.AttackInterval : attackInterval;
                float speed = 1f + TalentBonus(IsoRPG.Progression.TalentEffect.AttackSpeed);

                return baseInterval / Mathf.Max(0.2f, speed);
            }
        }

        private void Update()
        {
            ResolvePendingImpact();

            if (!targets.HasHostileTarget) return;

            var target = targets.Current;

            // Меряем по земле, без высоты.
            //
            // Герой может стоять на обломке ограды или на ступени, и тогда
            // прямое расстояние до монстра растёт на разницу высот. Монстр
            // при этом стоит вплотную, но считает, что не достаёт, — со
            // стороны это выглядит как «стоит рядом и не бьёт».
            Vector3 flat = target.transform.position - transform.position;
            flat.y = 0f;
            float distance = flat.magnitude;
            float attackDistance = AttackDistanceTo(target);

            if (distance > attackDistance + chaseTolerance)
            {
                // Пока игрок ведёт персонажа сам — не вмешиваемся. Приказ
                // считается выполненным, когда персонаж остановился.
                if (manualMoveOverride)
                {
                    if (movement != null && !movement.IsMoving) manualMoveOverride = false;
                    return;
                }

                if (chaseTarget) ChaseTo(target, attackDistance);
                return;
            }

            // Дошли до цели сами — ручной приказ больше не действует.
            manualMoveOverride = false;

            StopMoving();
            FaceTarget(target);

            if (Time.time >= nextAttackTime) Attack(target);
        }

        /// <summary>
        /// Записать удар в боевой лог.
        ///
        /// Лог ведётся с точки зрения игрока, поэтому драки монстров между
        /// собой в него не попадают — иначе он забьётся чужими сообщениями,
        /// и своё в них будет не найти.
        /// </summary>
        private void ReportToLog(Targetable victim, int amount, HitResult result)
        {
            bool weArePlayer = self != null && self.Faction == Faction.Player;
            bool victimIsPlayer = victim.Faction == Faction.Player;

            if (weArePlayer) CombatLog.DamageDealt(victim.DisplayName, amount, result);
            else if (victimIsPlayer)
            {
                CombatLog.DamageTaken(self != null ? self.DisplayName : "Противник",
                                      amount, result);
            }
        }

        private float AttackDistanceTo(Targetable target)
        {
            float ownRadius = self != null ? self.BodyRadius : 0.5f;
            return reach + ownRadius + target.BodyRadius;
        }

        private void ChaseTo(Targetable target, float attackDistance)
        {
            // Идём не в саму цель, а в точку перед ней: иначе агент упирается
            // в её тело, толкается и не может доехать до «места назначения».
            Vector3 toSelf = (transform.position - target.transform.position);
            toSelf.y = 0f;

            Vector3 direction = toSelf.sqrMagnitude > 0.001f
                ? toSelf.normalized
                : transform.forward;

            Vector3 standPoint = target.transform.position + direction * (attackDistance * 0.8f);

            // У игрока движением заведует контроллер кликов — он умеет
            // притягивать точку к навигационной сетке и показывать отметку.
            // У монстра такого контроллера нет, и раньше погоня здесь молча
            // заканчивалась ничем: монстры стояли столбом без единой ошибки
            // в консоли. Поэтому запасной путь — напрямую через агента.
            if (movement != null)
            {
                movement.MoveTo(standPoint);
            }
            else if (agent != null && agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(standPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                else
                    agent.SetDestination(target.transform.position);
            }
        }

        private void StopMoving()
        {
            if (agent != null && agent.isOnNavMesh && agent.hasPath)
                agent.ResetPath();
        }

        private void FaceTarget(Targetable target)
        {
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, wanted, turnSpeed * Time.deltaTime);
        }

        private void Attack(Targetable target)
        {
            nextAttackTime = Time.time + CurrentInterval;

            if (animDriver != null) animDriver.PlayAttack();

            // Звук замаха — сразу, вместе с анимацией. Попадание прозвучит
            // отдельно и позже: между ними и живёт ощущение удара.
            IsoRPG.Audio.Sfx.Swing(transform.position);

            // Урон отложен: он должен совпасть с моментом, когда клинок
            // достаёт цель, а не с началом замаха.
            pendingVictim = target;
            pendingImpactTime = Time.time + impactDelay;
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

            // Цель могла умереть или уйти, пока летел удар — это нормально,
            // просто промахиваемся.
            if (pendingVictim == null || !pendingVictim.IsAlive) return;

            float distance = Vector3.Distance(transform.position, pendingVictim.transform.position);
            if (distance > AttackDistanceTo(pendingVictim) + 1f) return;

            if (pendingVictim.Health != null)
            {
                // Автоатака бьёт ровно уроном оружия, без прибавок и без
                // комбо-очков. Очки даёт только способность — это отличает
                // «просто бью» от «делаю приём».
                int baseDamage = weapon != null ? weapon.WeaponDamage : fallbackDamage;

                // Таланты спрашиваем в момент удара, а не храним прибавку у
                // себя: сброшенный талант должен переставать работать сразу,
                // а не после перезахода в игру.
                float bonusDamage = TalentBonus(IsoRPG.Progression.TalentEffect.Damage);
                float bonusCrit = TalentBonus(IsoRPG.Progression.TalentEffect.CritChance);

                // Характеристики. Сила добавляет урон, ловкость — крит.
                //
                // Проценты, а не плоские прибавки: плоские ломаются на
                // обоих концах — на кулаках десять силы удваивают урон, а с
                // хорошим кинжалом не значат ничего.
                var stats = GetComponent<IsoRPG.Progression.TalentStats>();

                if (stats != null)
                {
                    var total = stats.TotalStats;

                    bonusDamage += total.Strength * DamagePerStrength;
                    bonusCrit += total.Agility * CritPerAgility;
                }
                float bonusCritMult = TalentBonus(IsoRPG.Progression.TalentEffect.CritMultiplier);

                baseDamage = Mathf.RoundToInt(baseDamage * (1f + bonusDamage));

                int dealt = CombatMath.Roll(baseDamage,
                                            Mathf.Clamp01(critChance + bonusCrit),
                                            critMultiplier + bonusCritMult,
                                            missChance, missMultiplier, out HitResult result);

                // Показываем то, что дошло после брони, а не то, чем замахивались.
                int actual = pendingVictim.Health.TakeDamage(dealt, gameObject);

                // Своё и чужое рисуем по-разному: числа летят с обеих сторон,
                // и в свалке из шести монстров по одному только положению не
                // понять, чьё здоровье уходит.
                if (pendingVictim.Faction == Faction.Player)
                    DamagePopup.ShowTaken(pendingVictim.OverheadPoint, actual, result);
                else
                    DamagePopup.Show(pendingVictim.OverheadPoint, actual, result);

                // Звук в момент попадания, а не замаха: ухо связывает удар с
                // тем, что увидело, и рассинхрон в полсекунды слышен сразу.
                if (self != null && self.Faction == Faction.Player)
                    IsoRPG.Audio.Sfx.BladeHit(pendingVictim.transform.position);
                else
                    IsoRPG.Audio.Sfx.HeavyHit(pendingVictim.transform.position);
                ReportToLog(pendingVictim, actual, result);
            }

            pendingVictim = null;
        }
    }
}

